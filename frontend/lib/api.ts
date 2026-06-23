// =============================================================================
// lib/api.ts — Cliente HTTP centralizado (axios + interceptors JWT)
//
// Segurança: os tokens JWT são armazenados como cookies HttpOnly pelo backend.
// O browser envia esses cookies automaticamente — não há manipulação manual
// de tokens no frontend, evitando exposição via JavaScript (proteção XSS).
// =============================================================================
import axios from 'axios'
import { clearAuth } from './auth'

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
        // Refresh falhou — redireciona de acordo com o tipo de página:
        //   /admin/*  → /login   (painel de gestão)
        //   /cliente/* → /entrar (área do cliente)
        //   demais    → limpa cookies e fica na página (QR code, campeonatos, etc.)
        if (typeof window !== 'undefined' && !window.location.pathname.includes('/login')) {
          const path = window.location.pathname
          clearAuth()
          if (path.startsWith('/admin')) {
            window.location.href = '/login'
          } else if (path.startsWith('/cliente')) {
            window.location.href = '/entrar'
          }
          // Páginas públicas (/mesa, /campeonato, /produtos…): só limpa os cookies,
          // o usuário continua na mesma página sem redirecionamento.
        }
      }
    }
    return Promise.reject(error)
  }
)

// ── Tipagens ──────────────────────────────────────────────────────────────────

export interface AuthResponse {
  accessToken?: string; refreshToken?: string; expiresAt: string
  role: string; userName: string; userId: string
  comandaId?: string
  permissions?: string[]
}

export interface ComandaDto {
  id: string; userName: string; userId: string
  tableIdentifier: string | null; status: string
  totalInReais: number; pointsApplied: number
  openedAt: string; closedAt?: string
  paymentMethod: string | null
  secondPaymentMethod: string | null
  secondPaymentAmountInCents: number
  items: ComandaItemDto[]
  /** Saldo de pontos do cliente (para exibir na modal de fechamento). */
  userPointsBalance: number
  /** Saldo de cashback/crédito do cliente em centavos. */
  userBalanceInCents: number
  profileImageUrl?: string | null
}

export interface ComandaItemDto {
  id: string; productId?: string | null; itemNameSnapshot: string; quantity: number
  unitPriceInCents: number; unitPriceInReais: number; subtotalInReais: number; addedAt: string
}

export interface EditarItemRequest {
  comandaItemId?: string
  remover?: boolean
  productId?: string
  itemName: string
  unitPriceInCents: number
  quantity: number
}

export interface EditarComandaRequest {
  paymentMethod?: string
  secondPaymentMethod?: string
  secondPaymentAmountInCents?: number
  novoClienteId?: string
  descontoEmCentavos?: number
  notes?: string
  itens?: EditarItemRequest[]
}

export interface Product {
  id: string; name: string; description: string | null; category: string
  barcode: string | null
  priceInCents: number; costPriceInCents: number; stockQuantity: number; minimumStock: number
  discountPriceInCents: number | null; discountPriceInReais: number | null; isOnPromo: boolean
  isActive: boolean; isFeatured: boolean; showOnSite: boolean; showOnMarketplace: boolean; isPreVenda: boolean; imageUrl: string | null
  imageUrls: string[]; fullDescription: string | null
  isLowStock: boolean; priceInReais: number; costPriceInReais: number
  marginInReais: number; marginPercent: number
  updatedAt: string; createdAt: string
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
  preInscricaoCount?: number; listaEsperaCount?: number
  registrationDeadline?: string | null; endDate?: string | null
  imageUrl?: string | null; podioJson?: string | null
  participants?: ChampionshipParticipant[]
}

export interface ChampionshipPreInscricao {
  id: string; nome: string; whatsApp: string; isListaEspera?: boolean; createdAt: string
}

export interface PodioItem { lugar: number; nome: string }

export interface ChampionshipParticipant {
  id: string; userId: string; userName: string; playerNumber: number
  deckName?: string | null; placement?: number | null; registeredAt: string
}

export interface UserSummary {
  id: string; name: string; email: string | null
  cpf: string | null; whatsApp: string | null; role: string; profileImageUrl: string | null
  perfilId: string | null; perfilNome: string | null
  pointsBalance: number; pointsExpiresAt: string | null
  pointsExpired: boolean; balanceInCents: number; isActive: boolean; createdAt: string
}

export interface UserProfile {
  id: string; name: string; email: string | null
  cpf: string | null; whatsApp: string | null; role: string; profileImageUrl: string | null
  pointsBalance: number; pointsExpiresAt: string | null
  pointsExpired: boolean; balanceInCents: number; createdAt: string
}

type PrefCorner = 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left'

export type DashChartScheme = 'default' | 'blue' | 'neon'
export type DashRefreshInterval = 15 | 30 | 60 | 0

export interface DashboardPanels {
  finHoje:       boolean
  grafico:       boolean
  previsao:      boolean
  patrimonio:    boolean
  clientes:      boolean
  produtos:      boolean
  lgpd:          boolean
  preInscricoes: boolean
}

export interface UserPreferences {
  aiButton:      { mode: 'draggable' | 'fixed'; corner: PrefCorner; enabled: boolean }
  vlibras:       { enabled: boolean; corner: PrefCorner }
  notifications: { soundEnabled: boolean; browserEnabled: boolean }
  pdv:           { defaultDiscount: 0 | 5 | 10 | 15 | 20 }
  dashboard:     { refreshInterval: DashRefreshInterval; chartScheme: DashChartScheme; panels: DashboardPanels }
}

export const DEFAULT_DASHBOARD_PANELS: DashboardPanels = {
  finHoje: true, grafico: true, previsao: true, patrimonio: true,
  clientes: true, produtos: true, lgpd: true, preInscricoes: true,
}

export const DEFAULT_PREFERENCES: UserPreferences = {
  aiButton:      { mode: 'draggable', corner: 'bottom-right', enabled: true },
  vlibras:       { enabled: true, corner: 'bottom-right' },
  notifications: { soundEnabled: true, browserEnabled: true },
  pdv:           { defaultDiscount: 0 },
  dashboard:     { refreshInterval: 30, chartScheme: 'default', panels: DEFAULT_DASHBOARD_PANELS },
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
  uploadProfileImage: (file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    return api.post<{ url: string }>('/api/upload/profile-image', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
  }
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
  secondPaymentMethod: string | null
  secondPaymentAmountInCents: number
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
  { value: 'Crediario',     label: 'Crediário' },
  { value: 'Pontos',        label: 'Pontos' },
  { value: 'Cashback',      label: 'Cashback' },
] as const

// Métodos que requerem cliente cadastrado selecionado
export const PAYMENT_NEEDS_USER = ['Crediario', 'Pontos', 'Cashback'] as const

// Métodos disponíveis como segundo pagamento (complemento via carteira do cliente)
export const SECOND_PAYMENT_METHODS = [
  { value: 'Cashback', label: 'Cashback (Saldo)' },
  { value: 'Pontos',   label: 'Pontos de Fidelidade' },
] as const

export const comandaApi = {
  dashboard:    () => api.get<ComandaDto[]>('/api/comanda/dashboard'),
  history:      (data?: string) => api.get<ComandaDto[]>('/api/comanda/history', { params: data ? { data } : undefined }),
  myComanda:    () => api.get<ComandaDto>('/api/comanda/my'),
  myHistory:    () => api.get<ComandaDto[]>('/api/comanda/my-history'),
  addItem:      (id: string, item: { productId?: string; cardCacheId?: string; itemName: string; unitPriceInCents: number; quantity: number }) =>
    api.post<ComandaDto>(`/api/comanda/${id}/items`, item),
  removeItem:   (id: string, itemId: string) => api.delete<ComandaDto>(`/api/comanda/${id}/items/${itemId}`),
  updateItem:   (id: string, itemId: string, quantity: number) =>
    api.patch<ComandaDto>(`/api/comanda/${id}/items/${itemId}`, { quantity }),
  close:        (id: string, paymentMethod = 'Dinheiro', observacao?: string, secondPaymentMethod?: string, secondPaymentAmountInCents = 0, crediarioExistenteId?: string) =>
    api.put<ComandaDto>(`/api/comanda/${id}/close`, { paymentMethod, observacao, secondPaymentMethod, secondPaymentAmountInCents, crediarioExistenteId }),
  cancel:       (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/cancel`),
  editar:       (id: string, request: EditarComandaRequest) => api.put<ComandaDto>(`/api/comanda/${id}/editar`, request),
  adminOpen:    (userId: string, tableIdentifier?: string) =>
    api.post<ComandaDto>('/api/comanda/admin-open', { userId, tableIdentifier }),
  applyPoints:  (id: string, points: number) =>
    api.post<ComandaDto>(`/api/comanda/${id}/apply-points`, { points }),
  removePoints: (id: string) =>
    api.delete<ComandaDto>(`/api/comanda/${id}/apply-points`),
}

// ── Crediário ─────────────────────────────────────────────────────────────────

export interface PagamentoCrediarioDto {
  id: string
  valorEmReais: number
  formaPagamento: string
  observacao: string | null
  createdAt: string
}

export interface ItemCrediarioDto {
  itemName: string
  quantity: number
  unitPriceInReais: number
  subtotalInReais: number
}

export interface CrediariosDto {
  id: string
  userId: string
  userName: string
  userEmail: string | null
  comandaId: string | null
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
  itensComanda: ItemCrediarioDto[]
}

export const FORMAS_PAGAMENTO_CREDIARIO = [
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'Pix',           label: 'Pix' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
] as const

export interface CriarCrediarioManualRequest {
  userId: string
  valorEmCentavos: number
  observacao?: string
  dataAbertura?: string   // ISO string, opcional — data real da dívida
  dataVencimento?: string // ISO string, opcional — se null usa dataAbertura + 30 dias
  itens?: ItemCrediarioDto[]
}

export interface CrediariosClienteDto {
  userId: string
  userName: string
  userEmail: string | null
  userWhatsApp: string | null
  saldoTotal: number
  totalDividas: number
  temVencido: boolean
  proximoVencimento: string
  dividas: CrediariosDto[]
}

export const crediarioApi = {
  list:        (status?: string) =>
    api.get<CrediariosDto[]>('/api/crediarios', { params: { status } }),
  byUser:      (userId: string) =>
    api.get<CrediariosDto[]>(`/api/crediarios/usuario/${userId}`),
  meu:         () => api.get<CrediariosDto>('/api/crediarios/meu'),
  marcarPago:  (id: string, observacao?: string) =>
    api.put<CrediariosDto>(`/api/crediarios/${id}/pagar`, { observacao }),
  registrarPagamento: (id: string, req: { valorEmCentavos: number; formaPagamento: string; secondFormaPagamento?: string; secondValorEmCentavos?: number; observacao?: string }) =>
    api.post<CrediariosDto>(`/api/crediarios/${id}/pagamento`, req),
  criarManual: (req: CriarCrediarioManualRequest) =>
    api.post<CrediariosDto>('/api/crediarios', req),
  editar: (id: string, req: { valorEmCentavos?: number; observacao?: string; dataVencimento?: string; limparItens?: boolean; itens?: ItemCrediarioDto[] }) =>
    api.patch<CrediariosDto>(`/api/crediarios/${id}`, req),
  deletar: (id: string) =>
    api.delete(`/api/crediarios/${id}`),
  meuHistorico: () =>
    api.get<CrediariosDto[]>('/api/crediarios/historico'),
  porCliente: () =>
    api.get<CrediariosClienteDto[]>('/api/crediarios/por-cliente'),
}

export const COMANDA_PAYMENT_METHODS = [
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'Pix',           label: 'Pix' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
  { value: 'Crediario',     label: 'Crediário (30 dias)' },
  { value: 'Pontos',        label: 'Pontos de Fidelidade' },
  { value: 'Cashback',      label: 'Cashback (Saldo)' },
] as const

export interface EditarPagamentoVendaAvulsaRequest {
  paymentMethod: string
  secondPaymentMethod?: string
  secondPaymentAmountInCents?: number
  clientName?: string
  clearClientName?: boolean
  discountInCents?: number
}

export const vendaAvulsaApi = {
  register: (
    clientName: string | null,
    paymentMethod: string,
    items: { productId: string; quantity: number }[],
    discountPercent = 0,
    userId?: string,
    secondPaymentMethod?: string | null,
    secondPaymentAmountInCents = 0,
  ) =>
    api.post<VendaAvulsaDto>('/api/venda-avulsa', {
      clientName, paymentMethod, items, discountPercent, userId,
      secondPaymentMethod: secondPaymentMethod || null,
      secondPaymentAmountInCents,
    }),
  recent: (limit = 50) =>
    api.get<VendaAvulsaDto[]>('/api/venda-avulsa/recent', { params: { limit } }),
  byDate: (date: string) =>
    api.get<VendaAvulsaDto[]>('/api/venda-avulsa/by-date', { params: { date } }),
  backfillCosts: () =>
    api.post<{ itensAtualizados: number; mensagem: string }>('/api/venda-avulsa/backfill-costs'),
  editarPagamento: (id: string, request: EditarPagamentoVendaAvulsaRequest) =>
    api.patch<VendaAvulsaDto>(`/api/venda-avulsa/${id}/pagamento`, request),
}

export const productApi = {
  list:        (category?: string) => api.get<Product[]>('/api/product', { params: { category } }),
  listAdmin:   ()                  => api.get<Product[]>('/api/product/admin'),
  listStore:   ()                  => api.get<Product[]>('/api/product/store'),
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
  role?: string
  perfilId?: string
}

export interface PerfilDto {
  id: string
  nome: string
  permissoes: string[]
  criadoEm: string
  atualizadoEm: string
  totalUsuarios: number
}

export const perfisApi = {
  list:       ()                                    => api.get<PerfilDto[]>('/api/perfis'),
  permissoes: ()                                    => api.get<{ key: string; label: string }[]>('/api/perfis/permissoes'),
  create:     (nome: string, permissoes: string[])  => api.post<PerfilDto>('/api/perfis', { nome, permissoes }),
  update:     (id: string, data: { nome?: string; permissoes?: string[] }) =>
    api.put<PerfilDto>(`/api/perfis/${id}`, data),
  delete:     (id: string)                          => api.delete(`/api/perfis/${id}`),
}

// ── Histórico de cliente ──────────────────────────────────────────────────────

export interface ClienteHistoricoComandaItem {
  itemName: string; quantity: number; unitPriceInReais: number; subtotalInReais: number
}
export interface ClienteHistoricoComanda {
  id: string; status: string; totalInReais: number
  paymentMethod: string | null; secondPaymentMethod: string | null
  openedAt: string; closedAt: string | null; tableIdentifier: string | null
  items: ClienteHistoricoComandaItem[]
}
export interface ClienteHistoricoVendaAvulsaItem {
  productName: string; quantity: number; unitPriceInReais: number; subtotalInReais: number
}
export interface ClienteHistoricoVendaAvulsa {
  id: string; totalInReais: number; paymentMethod: string; soldAt: string
  items: ClienteHistoricoVendaAvulsaItem[]
}
export interface ClienteHistoricoCrediario {
  id: string; valorEmReais: number; saldoRestante: number
  status: string; vencido: boolean
  dataAbertura: string; dataVencimento: string; dataPagamento: string | null
  observacao: string | null
}
export interface ClienteHistoricoCampeonato {
  championshipId: string; championshipName: string; game: string
  status: string; startDate: string; playerNumber: number
  deckName: string | null; placement: number | null; registeredAt: string
}
export interface ClienteHistoricoDto {
  userId: string; userName: string
  totalVisitas: number; totalGasto: number
  primeiraVisita: string | null; ultimaVisita: string | null
  comandas: ClienteHistoricoComanda[]
  vendasAvulsas: ClienteHistoricoVendaAvulsa[]
  crediarios: ClienteHistoricoCrediario[]
  campeonatos: ClienteHistoricoCampeonato[]
}

export const userApi = {
  list:      (search?: string, role?: string) => api.get<UserSummary[]>('/api/user', { params: { search, role } }),
  getById:   (id: string)      => api.get<UserSummary>(`/api/user/${id}`),
  me:        ()                => api.get<UserProfile>('/api/user/me'),
  historico: (id: string)      => api.get<ClienteHistoricoDto>(`/api/user/${id}/historico`),
  addPoints: (id: string, points: number, reason?: string) =>
    api.post<UserSummary>(`/api/user/${id}/points`, { points, reason }),
  adjustBalance: (id: string, amountInCents: number, reason?: string) =>
    api.post<UserSummary>(`/api/user/${id}/balance`, { amountInCents, reason }),
  // Admin: criar conta e redefinir senha
  adminCreate: (data: AdminCreateUserRequest) =>
    api.post<UserSummary>('/api/user', data),
  adminResetPassword: (id: string, newPassword: string) =>
    api.put(`/api/user/${id}/reset-password`, { newPassword }),
  adminUpdatePerfil: (id: string, perfilId: string | null) =>
    api.put<UserSummary>(`/api/user/${id}/perfil`, { perfilId }),
  adminDelete: (id: string) =>
    api.delete(`/api/user/${id}`),
  // LGPD — Direitos do titular
  updateMe:  (data: UpdateMeRequest) => api.put<UserProfile>('/api/user/me', data),
  deleteMe:  ()                      => api.delete('/api/user/me'),
  // Preferências pessoais
  getPreferences:    ()                        => api.get<UserPreferences>('/api/user/me/preferences'),
  updatePreferences: (data: UserPreferences)   => api.put<UserPreferences>('/api/user/me/preferences', data),
}

export const tcgApi = {
  search: (name: string, game?: string, page = 1, pageSize = 20) =>
    api.get<{ items: CardCache[]; totalCount: number; totalPages: number }>('/api/tcg/search',
      { params: { name, game, page, pageSize } }),
  getCard: (id: string) => api.get<CardCache>(`/api/tcg/cards/${id}`),
  sets:    (game: string) => api.get('/api/tcg/sets', { params: { game } }),
}


export interface MyParticipation {
  participationId: string; championshipId: string
  championshipName: string; game: string; startDate: string; status: string
  entryFeeInReais: number; playerNumber: number; deckName: string | null
  placement: number | null; registeredAt: string
}

export const championshipApi = {
  list:             () => api.get<Championship[]>('/api/championship'),
  update:           (id: string, c: Partial<Championship>) => api.put<Championship>(`/api/championship/${id}`, c),
  myParticipations: () => api.get<MyParticipation[]>('/api/championship/my-participations'),
  listAll:          (search?: string) => api.get<Championship[]>('/api/championship/admin/all', { params: search ? { search } : {} }),
  get:              (id: string) => api.get<Championship>(`/api/championship/${id}`),
  create:           (c: Partial<Championship>) => api.post<Championship>('/api/championship', c),
  delete:           (id: string) => api.delete(`/api/championship/${id}`),
  register:         (id: string, userId: string, deckName?: string) =>
    api.post(`/api/championship/${id}/register`, { userId, deckName }),
  adminRegister:    (id: string, userId: string, deckName?: string) =>
    api.post<ChampionshipParticipant>(`/api/championship/${id}/admin-register`, { userId, deckName }),
  participants:     (id: string) =>
    api.get<ChampionshipParticipant[]>(`/api/championship/${id}/participants`),
  removeParticipant:(id: string, participantId: string) =>
    api.delete(`/api/championship/${id}/participants/${participantId}`),
  setStatus:        (id: string, status: string) =>
    api.put(`/api/championship/${id}/status`, { status }),
  setPlacement:     (id: string, participantId: string, placement: number) =>
    api.put(`/api/championship/${id}/participants/${participantId}/placement`, { placement }),
  setImage:         (id: string, imageUrl: string | null) =>
    api.put<Championship>(`/api/championship/${id}/image`, { imageUrl }),
  addPreInscricao:  (id: string, nome: string, whatsApp: string) =>
    api.post<ChampionshipPreInscricao>(`/api/championship/${id}/preinscricoes`, { nome, whatsApp }),
  getPreInscricoes: (id: string) =>
    api.get<ChampionshipPreInscricao[]>(`/api/championship/${id}/preinscricoes`),
  deletePreInscricao: (championshipId: string, preInscricaoId: string) =>
    api.delete(`/api/championship/${championshipId}/preinscricoes/${preInscricaoId}`),
  setPodio:         (id: string, podioJson: string) =>
    api.patch(`/api/championship/${id}/podio`, { podioJson }),
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

export interface ClienteInsightDto {
  userId: string
  nome: string
  email: string | null
  whatsApp: string | null
  gastoTotal: number
  ticketMedio: number
  numVisitas: number
  ultimaVisita: string | null
  inativo30: boolean
  pontos: number
  /** Dias até os pontos vencerem. Negativo = já venceu. Null = sem data de vencimento. */
  pontosVencemEm: number | null
}

export interface DiaFinanceiroDto {
  dia: string
  receita: number
  custo: number
}

export interface TopProductFinDto {
  nome: string
  categoria: string
  qtd: number
  qtdComandas: number
  qtdAvulsa: number
  receita: number
  receitaComandas: number
  receitaAvulsa: number
  custo: number
  margem: number
}

export interface TransacaoFinDto {
  origem: string       // 'Comanda' | 'VendaAvulsa'
  cliente: string | null
  valor: number
  data: string
  nota?: string | null   // ex.: "+ Cashback R$ 19,00" para split payment
}

export interface PagamentoCrediarioPeriodoDto {
  clienteNome: string
  clienteWhatsApp: string | null
  valorEmReais: number
  formaPagamento: string
  observacao: string | null
  createdAt: string
}

export interface FormaPagamentoTotalDto {
  forma: string
  total: number
  quantidade: number
  transacoes: TransacaoFinDto[]
}

export interface FinanceiroDto {
  receita: number
  receitaComandas: number
  receitaAvulsa: number
  custo: number
  margem: number
  margemPercent: number
  crediarios: number
  recebidoCrediario: number
  diaDia: DiaFinanceiroDto[]
  topProdutos: TopProductFinDto[]
  pagamentosPorForma: FormaPagamentoTotalDto[]
  pagamentosCrediarioPeriodo: PagamentoCrediarioPeriodoDto[]
}

export const analyticsApi = {
  dashboard: () => api.get('/api/analytics/dashboard'),
  clientes:  (apenasInativos = false) =>
    api.get<ClienteInsightDto[]>('/api/analytics/clientes', { params: { apenasInativos } }),
  financeiro: (inicio?: string, fim?: string, filterPaymentMethod?: string) =>
    api.get<FinanceiroDto>('/api/analytics/financeiro', {
      params: { inicio, fim, filterPaymentMethod: filterPaymentMethod || undefined },
    }),
}

// ── Relatórios de vendas por categoria ───────────────────────────────────────

export interface RelatorioProduto {
  nome: string
  quantidadeVendida: number
  totalEmReais: number
}

export interface RelatorioCategoria {
  categoria: string
  emoji: string
  quantidadeVendida: number
  totalEmReais: number
  produtos: RelatorioProduto[]
}

export interface RelatorioVendasDto {
  mes: number
  ano: number
  totalGeralEmReais: number
  totalItensVendidos: number
  porCategoria: RelatorioCategoria[]
}

export interface DevedorDto {
  userId: string
  nome: string
  email: string | null
  whatsApp: string | null
  saldoEmReais: number
  vencido: boolean
  diasAtraso: number
  dataVencimento: string
}

export interface PagamentoMesDto {
  clienteNome: string
  valorEmReais: number
  formaPagamento: string
  observacao: string | null
  createdAt: string
}

export interface RelatorioCrediarioDto {
  mes: number
  ano: number
  totalEmAbertoEmReais: number
  totalVencidoEmReais: number
  qtdAbertos: number
  qtdVencidos: number
  recebidoNoMesEmReais: number
  qtdPagamentosNoMes: number
  devedores: DevedorDto[]
  pagamentosNoMes: PagamentoMesDto[]
}

export const relatorioApi = {
  vendas: (mes: number, ano: number) =>
    api.get<RelatorioVendasDto>('/api/relatorios/vendas', { params: { mes, ano } }),
  crediario: (mes: number, ano: number) =>
    api.get<RelatorioCrediarioDto>('/api/relatorios/crediario', { params: { mes, ano } }),
}
