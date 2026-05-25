// =============================================================================
// lib/api.ts — Cliente HTTP centralizado (axios + interceptors JWT)
//
// Segurança: os tokens JWT são armazenados como cookies HttpOnly pelo backend.
// O browser envia esses cookies automaticamente — não há manipulação manual
// de tokens no frontend, evitando exposição via JavaScript (proteção XSS).
// =============================================================================
import axios from 'axios'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

export const api = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  // withCredentials garante que o browser envie os cookies HttpOnly
  // (accessToken, refreshToken) em todas as requisições cross-origin.
  withCredentials: true,
})

// Mutex de refresh: evita múltiplas requisições simultâneas disparando vários refreshes
// quando o token expira com várias chamadas em paralelo na mesma página.
let refreshPromise: Promise<void> | null = null

async function doRefresh(): Promise<void> {
  // O refreshToken é enviado automaticamente via cookie HttpOnly (withCredentials).
  // O backend lê o cookie e retorna novos cookies — sem manipulação manual de tokens.
  await axios.post(`${BASE_URL}/api/auth/refresh`, {}, { withCredentials: true })
}

// Tenta renovar o token se receber 401
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      try {
        // Reutiliza o mesmo promise se já há um refresh em andamento
        if (!refreshPromise) {
          refreshPromise = doRefresh().finally(() => { refreshPromise = null })
        }
        await refreshPromise
        // Re-tenta a requisição original — o novo accessToken já está no cookie
        return api(original)
      } catch {
        // Refresh falhou — redireciona para login (guarda contra loop de redirect)
        if (typeof window !== 'undefined' && !window.location.pathname.includes('/login')) {
          window.location.href = '/login'
        }
      }
    }
    return Promise.reject(error)
  }
)

// ── Tipagens ──────────────────────────────────────────────────────────────────

export interface AuthResponse {
  // accessToken e refreshToken são opcionais pois o backend os envia como
  // cookies HttpOnly. O JSON de resposta os inclui para compatibilidade, mas
  // o frontend não deve armazená-los — o browser gerencia os cookies.
  accessToken?: string; refreshToken?: string; expiresAt: string
  role: string; userName: string; userId: string
  comandaId?: string  // Preenchido apenas no quick-login (cliente via QR Code)
}

export interface ComandaDto {
  id: string; userName: string; userId: string
  tableIdentifier: string | null; status: string
  totalInReais: number; pointsApplied: number
  openedAt: string; closedAt?: string
  paymentMethod: string | null
  items: ComandaItemDto[]
}

export interface ComandaItemDto {
  id: string; itemNameSnapshot: string; quantity: number
  unitPriceInReais: number; subtotalInReais: number; addedAt: string
}

export interface Product {
  id: string; name: string; description: string | null; category: string
  barcode: string | null
  priceInCents: number; costPriceInCents: number; stockQuantity: number; minimumStock: number
  isActive: boolean; isFeatured: boolean; imageUrl: string | null
  isLowStock: boolean; priceInReais: number; costPriceInReais: number
  marginInReais: number; marginPercent: number
}

export interface ProductCategory {
  id: string; name: string; emoji: string | null
  displayOrder: number; isActive: boolean; createdAt: string
}

export const categoryApi = {
  list:   ()                          => api.get<ProductCategory[]>('/api/category'),
  create: (c: Partial<ProductCategory>) => api.post<ProductCategory>('/api/category', c),
  update: (id: string, c: Partial<ProductCategory>) => api.put<ProductCategory>(`/api/category/${id}`, c),
  delete: (id: string)                => api.delete(`/api/category/${id}`),
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
  number?: string | null; rarity: string | null; type: string | null; imageUrlSmall: string | null
  imageUrlLarge: string | null; marketPrices: { market: number | null; mid: number | null } | null
  cachedAt: string
}

export interface Championship {
  id: string; name: string; game: string; status: string
  startDate: string; entryFeeInCents: number; entryFeeInReais: number; maxParticipants: number | null
  description?: string | null; participantCount?: number
  registrationDeadline?: string | null; endDate?: string | null
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
  pointsExpired: boolean; balanceInCents: number; isActive: boolean; createdAt: string
}

export interface UserProfile {
  id: string; name: string; email: string | null
  cpf: string | null; whatsApp: string | null; role: string
  pointsBalance: number; pointsExpiresAt: string | null
  pointsExpired: boolean; balanceInCents: number; createdAt: string
}

// ── Funções de API ────────────────────────────────────────────────────────────

export interface CpfLookupResponse { name: string; hasPassword: boolean }

export const authApi = {
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/login', { email, password }),
  clientLogin: (email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/client-login', { email, password }),
  cpfLookup: (cpf: string) =>
    api.post<CpfLookupResponse>('/api/auth/cpf-lookup', { cpf }),
  setupAccount: (cpf: string, email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/setup-account', { cpf, email, password }),
  quickLogin: (name: string, cpf: string, whatsApp: string, tableIdentifier?: string) =>
    api.post<AuthResponse>('/api/auth/quick-login', { name, cpf, whatsApp, tableIdentifier }),
  logout:         () => api.post('/api/auth/logout'),
  forgotPassword: (email: string) =>
    api.post('/api/auth/forgot-password', { email }),
  resetPassword:  (token: string, newPassword: string) =>
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
  close:        (id: string, paymentMethod = 'Dinheiro', observacao?: string) =>
    api.put<ComandaDto>(`/api/comanda/${id}/close`, { paymentMethod, observacao }),
  cancel:       (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/cancel`),
  applyPoints:  (id: string, points: number) =>
    api.post<ComandaDto>(`/api/comanda/${id}/apply-points`, { points }),
}

// ── Crediário ─────────────────────────────────────────────────────────────────

export interface PagamentoCrediarioDto {
  id: string
  valorEmReais: number
  formaPagamento: string
  observacao: string | null
  createdAt: string
}

export interface CrediariosDto {
  id: string
  userId: string
  userName: string
  userEmail: string | null
  comandaId: string
  valorEmReais: number
  valorPagoEmReais: number
  saldoRestanteEmReais: number
  dataAbertura: string
  dataVencimento: string
  dataPagamento: string | null
  status: string        // 'Aberto' | 'Pago' | 'Vencido'
  observacao: string | null
  vencido: boolean
  diasRestantes: number
  pagamentos: PagamentoCrediarioDto[]
}

export const FORMAS_PAGAMENTO_CREDIARIO = [
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'Pix',           label: 'Pix' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
] as const

export const crediarioApi = {
  list:        (status?: string) =>
    api.get<CrediariosDto[]>('/api/crediarios', { params: { status } }),
  byUser:      (userId: string) =>
    api.get<CrediariosDto[]>(`/api/crediarios/usuario/${userId}`),
  meu:         () => api.get<CrediariosDto>('/api/crediarios/meu'),
  marcarPago:  (id: string, observacao?: string) =>
    api.put<CrediariosDto>(`/api/crediarios/${id}/pagar`, { observacao }),
  registrarPagamento: (id: string, valorEmCentavos: number, formaPagamento: string, observacao?: string) =>
    api.post<CrediariosDto>(`/api/crediarios/${id}/pagamento`, { valorEmCentavos, formaPagamento, observacao }),
}

export const COMANDA_PAYMENT_METHODS = [
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'Pix',           label: 'Pix' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
  { value: 'Crediario',     label: 'Crediário (30 dias)' },
] as const

export const vendaAvulsaApi = {
  register: (clientName: string | null, paymentMethod: string, items: { productId: string; quantity: number }[], discountPercent = 0) =>
    api.post<VendaAvulsaDto>('/api/venda-avulsa', { clientName, paymentMethod, items, discountPercent }),
  recent: (limit = 50) =>
    api.get<VendaAvulsaDto[]>('/api/venda-avulsa/recent', { params: { limit } }),
}

export const productApi = {
  list:        (category?: string) => api.get<Product[]>('/api/product', { params: { category } }),
  get:         (id: string)         => api.get<Product>(`/api/product/${id}`),
  getByBarcode:(barcode: string)    => api.get<Product>(`/api/product/barcode/${encodeURIComponent(barcode)}`),
  create:      (p: Partial<Product>) => api.post<Product>('/api/product', p),
  update:      (id: string, p: Partial<Product>) => api.put<Product>(`/api/product/${id}`, p),
  deactivate:  (id: string)         => api.delete(`/api/product/${id}`),
  lowStock:    ()                   => api.get<Product[]>('/api/product/low-stock'),
  adjustStock: (id: string, delta: number) => api.patch(`/api/product/${id}/stock`, { delta }),
}

export interface UpdateMeRequest {
  name?: string
  email?: string
  whatsApp?: string
}

export interface AdminCreateUserRequest {
  name: string
  cpf?: string
  whatsApp?: string
  email?: string
  password?: string
}

export const userApi = {
  list:      (search?: string) => api.get<UserSummary[]>('/api/user', { params: { search } }),
  getById:   (id: string)      => api.get<UserSummary>(`/api/user/${id}`),
  me:        ()                => api.get<UserProfile>('/api/user/me'),
  addPoints: (id: string, points: number, reason?: string) =>
    api.post<UserSummary>(`/api/user/${id}/points`, { points, reason }),
  adjustBalance: (id: string, amountInCents: number, reason?: string) =>
    api.post<UserSummary>(`/api/user/${id}/balance`, { amountInCents, reason }),
  // Admin: criar conta e redefinir senha
  adminCreate: (data: AdminCreateUserRequest) =>
    api.post<UserSummary>('/api/user', data),
  adminResetPassword: (id: string, newPassword: string) =>
    api.put(`/api/user/${id}/reset-password`, { newPassword }),
  // LGPD — Direitos do titular
  updateMe:  (data: UpdateMeRequest) => api.put<UserProfile>('/api/user/me', data),
  deleteMe:  ()                      => api.delete('/api/user/me'),
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

// ── Assistente IA ─────────────────────────────────────────────────────────────

export interface AiChatResponse {
  reply:   string
  success: boolean
  error?:  string
}

export const aiApi = {
  chat: (message: string) =>
    api.post<AiChatResponse>('/api/ai/chat', { message }),
}

// ── Upload de imagem ──────────────────────────────────────────────────────────

export const uploadApi = {
  /** Envia um arquivo de imagem e retorna a URL pública gerada pelo servidor. */
  image: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.post<{ url: string }>('/api/upload/image', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  },
}

// ── LGPD — Público ────────────────────────────────────────────────────────────

export interface LgpdRequestCreate {
  requesterName:  string
  requesterEmail: string
  requesterCpf:   string
  requestType:    string
  description?:   string
}

export interface LgpdRequestDto {
  id:            string
  requesterName: string
  requesterEmail:string
  requesterCpf:  string
  requestType:   string
  description:   string | null
  status:        string
  adminResponse: string | null
  createdAt:     string
  deadline:      string
  respondedAt:   string | null
  isOverdue:     boolean
  isUrgent:      boolean
}

export const lgpdApi = {
  /** Abre uma solicitação de exercício de direitos LGPD (público, sem auth). */
  submitRequest: (data: LgpdRequestCreate) =>
    api.post<{ protocol: string; deadline: string; message: string }>('/api/lgpd/request', data),

  /** Consulta o status de uma solicitação pelo número de protocolo. */
  getRequest: (id: string) =>
    api.get<{
      id: string; requestType: string; status: string
      adminResponse: string | null; createdAt: string
      deadline: string; respondedAt: string | null
    }>(`/api/lgpd/request/${id}`),

  /** Registra consentimento ou recusa de cookies. */
  recordConsent: (accepted: boolean) =>
    api.post('/api/lgpd/consent', { accepted }),
}

// ── LGPD — Admin ──────────────────────────────────────────────────────────────

export const lgpdAdminApi = {
  /** Lista todas as solicitações LGPD, com filtro opcional por status. */
  listRequests: (status?: string) =>
    api.get<LgpdRequestDto[]>('/api/lgpd/requests', { params: status ? { status } : undefined }),

  /** Responde formalmente a uma solicitação LGPD. */
  respond: (id: string, data: { status: string; adminResponse: string }) =>
    api.put<LgpdRequestDto>(`/api/lgpd/requests/${id}/respond`, data),

  /** Lista audit logs paginados. */
  listAudit: (page = 1, pageSize = 50) =>
    api.get('/api/audit', { params: { page, pageSize } }),
}

// ── Analytics ─────────────────────────────────────────────────────────────────

export interface DiaFinanceiroDto {
  dia: string
  receita: number
  custo: number
}

export interface TopProductFinDto {
  nome: string
  qtd: number
  receita: number
  custo: number
  margem: number
}

export interface FinanceiroDto {
  receita: number
  custo: number
  margem: number
  margemPercent: number
  crediarios: number
  diaDia: DiaFinanceiroDto[]
  topProdutos: TopProductFinDto[]
}

export const analyticsApi = {
  dashboard: () => api.get('/api/analytics/dashboard'),
  clientes:  (apenasInativos = false) =>
    api.get('/api/analytics/clientes', { params: { apenasInativos } }),
  financeiro: (inicio?: string, fim?: string) =>
    api.get<FinanceiroDto>('/api/analytics/financeiro', { params: { inicio, fim } }),
}
