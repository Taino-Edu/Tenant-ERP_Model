// =============================================================================
// lib/api.ts — Cliente HTTP centralizado (axios + interceptors JWT)
// =============================================================================
import axios from 'axios'
import Cookies from 'js-cookie'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

export const api = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

// Injeta o token JWT em todas as requisições
api.interceptors.request.use((config) => {
  const token = Cookies.get('accessToken')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// Tenta renovar o token se receber 401
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      try {
        const refresh = Cookies.get('refreshToken')
        if (!refresh) throw new Error('Sem refresh token')
        const { data } = await axios.post(`${BASE_URL}/api/auth/refresh`, { refreshToken: refresh })
        Cookies.set('accessToken',  data.accessToken,  { expires: 1 })
        Cookies.set('refreshToken', data.refreshToken, { expires: 30 })
        original.headers.Authorization = `Bearer ${data.accessToken}`
        return api(original)
      } catch {
        Cookies.remove('accessToken')
        Cookies.remove('refreshToken')
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

// ── Tipagens ──────────────────────────────────────────────────────────────────

export interface AuthResponse {
  accessToken: string; refreshToken: string; expiresAt: string
  role: string; userName: string; userId: string
  comandaId?: string  // Preenchido apenas no quick-login (cliente via QR Code)
}

export interface ComandaDto {
  id: string; userName: string; userId: string
  tableIdentifier: string | null; status: string
  totalInReais: number; pointsApplied: number
  openedAt: string; closedAt?: string
  items: ComandaItemDto[]
}

export interface ComandaItemDto {
  id: string; itemNameSnapshot: string; quantity: number
  unitPriceInReais: number; subtotalInReais: number; addedAt: string
}

export interface Product {
  id: string; name: string; description: string | null; category: string
  priceInCents: number; stockQuantity: number; minimumStock: number
  isActive: boolean; isFeatured: boolean; imageUrl: string | null
  isLowStock: boolean; priceInReais: number
}

export interface AnnouncementDto {
  id: string; title: string; body: string | null
  imageUrl: string | null; linkUrl: string | null
  type: string; isActive: boolean
  expiresAt: string | null; createdAt: string
}

export const ANNOUNCEMENT_TYPES = ['Banner', 'Aviso', 'Destaque'] as const

export interface CardCache {
  tcgCardId: string; name: string; game: string; setName: string | null
  rarity: string | null; type: string | null; imageUrlSmall: string | null
  imageUrlLarge: string | null; marketPrices: { market: number | null; mid: number | null } | null
  cachedAt: string
}

export interface Championship {
  id: string; name: string; game: string; status: string
  startDate: string; entryFeeInCents: number; maxParticipants: number | null
  participants?: ChampionshipParticipant[]
}

export interface ChampionshipParticipant {
  id: string; userId: string; playerNumber: number
  user: { name: string; email: string | null }
  deckName: string | null; placement: number | null
}

export interface UserSummary {
  id: string; name: string; email: string | null
  cpf: string | null; whatsApp: string | null; role: string
  pointsBalance: number; pointsExpiresAt: string | null
  pointsExpired: boolean; isActive: boolean; createdAt: string
}

export interface UserProfile {
  id: string; name: string; email: string | null
  cpf: string | null; whatsApp: string | null; role: string
  pointsBalance: number; pointsExpiresAt: string | null
  pointsExpired: boolean; createdAt: string
}

// ── Funções de API ────────────────────────────────────────────────────────────

export const authApi = {
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/login', { email, password }),
  quickLogin: (name: string, cpf: string, whatsApp: string, tableIdentifier?: string) =>
    api.post<AuthResponse>('/api/auth/quick-login', { name, cpf, whatsApp, tableIdentifier }),
  logout:          () => api.post('/api/auth/logout'),
  forgotPassword:  (email: string) =>
    api.post('/api/auth/forgot-password', { email }),
  resetPassword:   (token: string, newPassword: string) =>
    api.post('/api/auth/reset-password', { token, newPassword }),
}

export const announcementApi = {
  visible: () => api.get<AnnouncementDto[]>('/api/announcements'),
  all:     () => api.get<AnnouncementDto[]>('/api/announcements/all'),
  create:  (data: Omit<AnnouncementDto, 'id' | 'createdAt'>) =>
    api.post<AnnouncementDto>('/api/announcements', data),
  update:  (id: string, data: Partial<Omit<AnnouncementDto, 'id' | 'createdAt'>>) =>
    api.put<AnnouncementDto>(`/api/announcements/${id}`, data),
  delete:  (id: string) => api.delete(`/api/announcements/${id}`),
}

export interface VendaAvulsaDto {
  id: string
  clientName: string | null
  paymentMethod: string
  totalInReais: number
  discountPercent: number
  discountInReais: number
  soldAt: string
  soldByAdminName: string
  items: {
    productName: string
    productCategory: string | null
    quantity: number
    unitPriceInReais: number
    subtotalInReais: number
  }[]
}

export const PAYMENT_METHODS = [
  { value: 'Pix',           label: 'Pix' },
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
] as const

export const comandaApi = {
  dashboard:    () => api.get<ComandaDto[]>('/api/comanda/dashboard'),
  history:      () => api.get<ComandaDto[]>('/api/comanda/history'),
  myComanda:    () => api.get<ComandaDto>('/api/comanda/my'),
  addItem:      (id: string, item: { productId?: string; cardCacheId?: string; itemName: string; unitPriceInCents: number; quantity: number }) =>
    api.post<ComandaDto>(`/api/comanda/${id}/items`, item),
  removeItem:   (id: string, itemId: string) => api.delete<ComandaDto>(`/api/comanda/${id}/items/${itemId}`),
  close:        (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/close`),
  cancel:       (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/cancel`),
  applyPoints:  (id: string, points: number) =>
    api.post<ComandaDto>(`/api/comanda/${id}/apply-points`, { points }),
}

export const vendaAvulsaApi = {
  register: (clientName: string | null, paymentMethod: string, items: { productId: string; quantity: number }[], discountPercent = 0) =>
    api.post<VendaAvulsaDto>('/api/venda-avulsa', { clientName, paymentMethod, items, discountPercent }),
  recent: (limit = 50) =>
    api.get<VendaAvulsaDto[]>('/api/venda-avulsa/recent', { params: { limit } }),
}

export const productApi = {
  list:        (category?: string) => api.get<Product[]>('/api/product', { params: { category } }),
  get:         (id: string)         => api.get<Product>(`/api/product/${id}`),
  create:      (p: Partial<Product>) => api.post<Product>('/api/product', p),
  update:      (id: string, p: Partial<Product>) => api.put<Product>(`/api/product/${id}`, p),
  deactivate:  (id: string)         => api.delete(`/api/product/${id}`),
  lowStock:    ()                   => api.get<Product[]>('/api/product/low-stock'),
  adjustStock: (id: string, delta: number) => api.patch(`/api/product/${id}/stock`, { delta }),
}

export const userApi = {
  list:      (search?: string) => api.get<UserSummary[]>('/api/user', { params: { search } }),
  getById:   (id: string)      => api.get<UserSummary>(`/api/user/${id}`),
  me:        ()                => api.get<UserProfile>('/api/user/me'),
  addPoints: (id: string, points: number, reason?: string) =>
    api.post<UserSummary>(`/api/user/${id}/points`, { points, reason }),
}

export const tcgApi = {
  search: (name: string, game?: string, page = 1, pageSize = 20) =>
    api.get<{ items: CardCache[]; totalCount: number; totalPages: number }>('/api/tcg/search',
      { params: { name, game, page, pageSize } }),
  getCard: (id: string) => api.get<CardCache>(`/api/tcg/cards/${id}`),
  sets:    (game: string) => api.get('/api/tcg/sets', { params: { game } }),
}

export const championshipApi = {
  list:       () => api.get<Championship[]>('/api/championship'),
  get:        (id: string) => api.get<Championship>(`/api/championship/${id}`),
  create:     (c: Partial<Championship>) => api.post<Championship>('/api/championship', c),
  register:   (id: string, userId: string, deckName?: string) =>
    api.post(`/api/championship/${id}/register`, { userId, deckName }),
  setStatus:  (id: string, status: string) =>
    api.put(`/api/championship/${id}/status`, { status }),
}
