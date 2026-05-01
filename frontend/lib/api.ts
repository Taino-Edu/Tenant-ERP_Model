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
}

export interface ComandaDto {
  id: string; userName: string; userId: string
  tableIdentifier: string | null; status: string
  totalInReais: number; openedAt: string
  items: ComandaItemDto[]
}

export interface ComandaItemDto {
  id: string; itemNameSnapshot: string; quantity: number
  unitPriceInReais: number; subtotalInReais: number; addedAt: string
}

export interface Product {
  id: string; name: string; description: string | null; category: string
  priceInCents: number; stockQuantity: number; minimumStock: number
  isActive: boolean; imageUrl: string | null; isLowStock: boolean
  priceInReais: number
}

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

// ── Funções de API ────────────────────────────────────────────────────────────

export const authApi = {
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/login', { email, password }),
  quickLogin: (name: string, cpf: string, whatsApp: string, tableIdentifier?: string) =>
    api.post<{ auth: AuthResponse; comanda: ComandaDto }>('/api/auth/quick-login',
      { name, cpf, whatsApp, tableIdentifier }),
  logout: () => api.post('/api/auth/logout'),
}

export const comandaApi = {
  dashboard:  () => api.get<ComandaDto[]>('/api/comanda/dashboard'),
  myComanda:  () => api.get<ComandaDto>('/api/comanda/my'),
  addItem:    (id: string, item: { productId?: string; cardCacheId?: string; itemName: string; unitPriceInCents: number; quantity: number }) =>
    api.post<ComandaDto>(`/api/comanda/${id}/items`, item),
  removeItem: (id: string, itemId: string) => api.delete<ComandaDto>(`/api/comanda/${id}/items/${itemId}`),
  close:      (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/close`),
  cancel:     (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/cancel`),
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
