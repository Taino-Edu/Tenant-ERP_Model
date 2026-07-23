// =============================================================================
// lib/api.ts — Cliente HTTP centralizado (axios + interceptors JWT)
//
// Segurança: os tokens JWT são armazenados como cookies HttpOnly pelo backend.
// O browser envia esses cookies automaticamente — não há manipulação manual
// de tokens no frontend, evitando exposição via JavaScript (proteção XSS).
// =============================================================================
import axios from 'axios'
import { clearAuth } from './auth'

// baseURL vazio: todas as chamadas abaixo já incluem o prefixo /api/... —
// o nginx (produção) e o rewrite do next.config.js (dev sem nginx) roteiam
// esse prefixo pro backend no mesmo host que serviu o frontend, não importa
// se o acesso é por IP, domínio ou subdomínio de tenant.
export const api = axios.create({
  baseURL: '',
  headers: { 'Content-Type': 'application/json' },
  // withCredentials garante que o browser envie os cookies HttpOnly
  // (accessToken, refreshToken) em todas as requisições cross-origin.
  withCredentials: true,
})

/**
 * Extrai a mensagem de erro real de uma falha de requisição — o backend sempre
 * devolve `{ message }` (erro de negócio tratado pelo controller, ou o
 * middleware global de exceção não tratada, que também inclui `traceId`).
 * Cai no `fallback` só quando não tem body nenhum pra ler (rede caiu, CORS,
 * timeout) — nesses casos não existe mensagem real pra mostrar de qualquer jeito.
 */
export function getErrorMessage(err: unknown, fallback: string): string {
  const data = (err as { response?: { data?: { message?: string; traceId?: string } } })?.response?.data
  if (!data?.message) return fallback
  return data.traceId ? `${data.message} (ref: ${data.traceId})` : data.message
}

// Mutex de refresh: evita múltiplas requisições simultâneas disparando vários refreshes
// quando o token expira com várias chamadas em paralelo na mesma página.
let refreshPromise: Promise<void> | null = null

async function doRefresh(): Promise<void> {
  // O refreshToken é enviado automaticamente via cookie HttpOnly (withCredentials).
  // O backend lê o cookie e retorna novos cookies — sem manipulação manual de tokens.
  await axios.post('/api/auth/refresh', {}, { withCredentials: true })
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

/// Um acerto de senha encontrado em outra loja/Contador/Dono da Plataforma —
/// ver POST /api/auth/locate-account. tenantSlug só vem quando targetKind é "Tenant".
export interface LocateAccountMatch {
  label: string; targetKind: 'Tenant' | 'PlatformOwner' | 'Contador'; tenantSlug: string | null; ticket: string
}

export interface ComandaDto {
  id: string; userName: string; userId: string
  tableIdentifier: string | null; status: string
  totalInReais: number; pointsApplied: number; discountInCents: number
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
  /** Preenchidos só quando o fechamento pediu emissão de NFC-e (emitirNotaFiscal=true). */
  notaFiscalId?: string | null
  notaFiscalStatus?: string | null
  notaFiscalMotivoRejeicao?: string | null
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
  hasVariants: boolean
  /** NCM (Nomenclatura Comum do Mercosul) — obrigatório para emitir NFC-e deste produto. */
  ncm: string | null
  /** CEST com 7 digitos; obrigatorio quando a natureza usa ICMS-ST. */
  cest: string | null
  percentualTributosFederais: number | null
  percentualTributosEstaduais: number | null
  percentualTributosMunicipais: number | null
  /** Fonte/versao aprovada pelo contador, por exemplo IBPT 26.1.A. */
  fonteTributos: string | null
  tributosPreenchidosAutomaticamente: boolean
  tributosAtualizadosEm: string | null
  tributosVigenciaInicio: string | null
  tributosVigenciaFim: string | null
  ibptVersao: string | null
  ibptChave: string | null
  /** Natureza de operação (CFOP/CSOSN) usada na emissão fiscal. Null = usa a marcada como padrão. */
  naturezaOperacaoId: string | null
  updatedAt: string; createdAt: string
}

export interface ProductVariant {
  id: string; productId: string
  size: string | null; color: string | null
  stockQuantity: number; priceInCents: number | null
  sku: string | null; label: string; createdAt: string
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

export interface TimerDto {
  id: string; name: string; durationSeconds: number; pausedRemaining: number | null
  state: 'stopped' | 'running' | 'paused' | 'finished'
  startedAt: string | null; soundPreset: string; warnAtSeconds: number
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
  preVenda:      boolean
  avisosContador: boolean
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
  clientes: true, produtos: true, lgpd: true, preVenda: true, avisosContador: true,
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
  register: (name: string, email: string, password: string, whatsApp?: string, cpf?: string) =>
    api.post<AuthResponse>('/api/auth/register', { name, email, password, whatsApp, cpf }),
  cpfLookup: (cpf: string) =>
    api.post<CpfLookupResponse>('/api/auth/cpf-lookup', { cpf }),
  setupAccount: (cpf: string, email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/setup-account', { cpf, email, password }),
  quickLogin: (name: string, cpf: string | null, whatsApp: string, tableIdentifier?: string) =>
    api.post<AuthResponse>('/api/auth/quick-login', { name, cpf: cpf || null, whatsApp, tableIdentifier }),
  logout:         () => api.post('/api/auth/logout'),
  forgotPassword: (email: string) =>
    api.post('/api/auth/forgot-password', { email }),
  resetPassword:  (token: string, newPassword: string) =>
    api.post('/api/auth/reset-password', { token, newPassword }),
  registerContador: (name: string, email: string, password: string, tenantSlug: string) =>
    api.post<AuthResponse>('/api/auth/contador/register', { name, email, password, tenantSlug }),
  locateAccount: (email: string, password: string) =>
    api.post<LocateAccountMatch[]>('/api/auth/locate-account', { email, password }),
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
  /** Preenchidos só quando o registro pediu emissão de NFC-e (emitirNotaFiscal=true). */
  notaFiscalId?: string | null
  notaFiscalStatus?: string | null
  notaFiscalMotivoRejeicao?: string | null
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
  addItem:      (id: string, item: { productId?: string; cardCacheId?: string; variantId?: string; itemName: string; unitPriceInCents: number; quantity: number }) =>
    api.post<ComandaDto>(`/api/comanda/${id}/items`, item),
  removeItem:   (id: string, itemId: string) => api.delete<ComandaDto>(`/api/comanda/${id}/items/${itemId}`),
  updateItem:   (id: string, itemId: string, quantity: number) =>
    api.patch<ComandaDto>(`/api/comanda/${id}/items/${itemId}`, { quantity }),
  close:        (id: string, paymentMethod = 'Dinheiro', observacao?: string, secondPaymentMethod?: string, secondPaymentAmountInCents = 0, crediarioExistenteId?: string, discountInCents = 0, emitirNotaFiscal = false) =>
    api.put<ComandaDto>(`/api/comanda/${id}/close`, { paymentMethod, observacao, secondPaymentMethod, secondPaymentAmountInCents, crediarioExistenteId, discountInCents, emitirNotaFiscal }),
  cancel:       (id: string) => api.put<ComandaDto>(`/api/comanda/${id}/cancel`),
  editar:       (id: string, request: EditarComandaRequest) => api.put<ComandaDto>(`/api/comanda/${id}/editar`, request),
  adminOpen:    (userId: string, tableIdentifier?: string) =>
    api.post<ComandaDto>('/api/comanda/admin-open', { userId, tableIdentifier }),
  applyPoints:  (id: string, points: number) =>
    api.post<ComandaDto>(`/api/comanda/${id}/apply-points`, { points }),
  removePoints: (id: string) =>
    api.delete<ComandaDto>(`/api/comanda/${id}/apply-points`),
  gerarPix: (id: string) =>
    api.post<PixCobrancaDto>(`/api/comanda/${id}/pix`),
  statusPix: (id: string, txid: string) =>
    api.get<{ txId: string; status: string; pagoEm: string | null; comanda: ComandaDto | null }>(`/api/comanda/${id}/pix/${txid}/status`),
  // Lado do cliente: cobrança ativa da própria comanda + verificação de pagamento
  meuPix: () =>
    api.get<PixCobrancaDto>('/api/comanda/my/pix'),
  verificarMeuPix: () =>
    api.post<{ txId: string; status: string; pagoEm: string | null; comanda: ComandaDto | null }>('/api/comanda/my/pix/verificar'),
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

export interface PixCobrancaDto {
  txId: string
  status: string // 'ATIVA' | 'CONCLUIDA' | 'REMOVIDA_PELO_USUARIO_RECEBEDOR' | 'REMOVIDA_PELO_PSP'
  pixCopiaCola: string | null
  imagemQrCode: string | null // data URI base64, pronta pra <img src=...>
  expiraEm: string | null
  valorEmReais: number
}

export const crediarioApi = {
  list:        (status?: string) =>
    api.get<CrediariosDto[]>('/api/crediarios', { params: { status } }),
  byUser:      (userId: string) =>
    api.get<CrediariosDto[]>(`/api/crediarios/usuario/${userId}`),
  meu:         () => api.get<CrediariosDto>('/api/crediarios/meu'),
  marcarPago:  (id: string, observacao?: string) =>
    api.put<CrediariosDto>(`/api/crediarios/${id}/pagar`, { observacao }),
  registrarPagamento: (id: string, req: { valorEmCentavos: number; formaPagamento: string; secondFormaPagamento?: string; secondValorEmCentavos?: number; observacao?: string; idempotencyKey?: string }) =>
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
  gerarPix: (id: string) =>
    api.post<PixCobrancaDto>(`/api/crediarios/${id}/pix`),
  statusPix: (id: string, txid: string) =>
    api.get<{ txId: string; status: string; pagoEm: string | null }>(`/api/crediarios/${id}/pix/${txid}/status`),
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
    items: { productId: string; quantity: number; variantId?: string }[],
    discountPercent = 0,
    userId?: string,
    secondPaymentMethod?: string | null,
    secondPaymentAmountInCents = 0,
    discountInCents?: number,
    emitirNotaFiscal = false,
  ) =>
    api.post<VendaAvulsaDto>('/api/venda-avulsa', {
      clientName, paymentMethod, items, discountPercent, discountInCents, userId,
      secondPaymentMethod: secondPaymentMethod || null,
      secondPaymentAmountInCents,
      emitirNotaFiscal,
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

export const variantApi = {
  list:   (productId: string) =>
            api.get<ProductVariant[]>(`/api/products/${productId}/variants`),
  update: (productId: string, variantId: string, v: Partial<ProductVariant>) =>
            api.put<ProductVariant>(`/api/products/${productId}/variants/${variantId}`, v),
  remove: (productId: string, variantId: string) =>
            api.delete(`/api/products/${productId}/variants/${variantId}`),
  bulk:   (productId: string, sizes: string[], colors: string[], stockQty: number) =>
            api.post<ProductVariant[]>(`/api/products/${productId}/variants/bulk`, { sizes, colors, baseStockQuantity: stockQty }),
}

export interface WaitListEntry { id: string; productId: string; userId?: string; name: string; whatsApp: string; position: number; createdAt: string; notifiedAt?: string }

export const waitListApi = {
  myPosition: (productId: string)  => api.get<{ inList: boolean; position?: number; entryId?: string }>(`/api/products/${productId}/waitlist/my`),
  join:       (productId: string)  => api.post<WaitListEntry>(`/api/products/${productId}/waitlist`),
  leave:      (productId: string)  => api.delete(`/api/products/${productId}/waitlist`),
  adminList:  (productId: string)  => api.get<{ productId: string; productName: string; total: number; entries: WaitListEntry[] }>(`/api/products/${productId}/waitlist`),
  adminRemove:(productId: string, entryId: string) => api.delete(`/api/products/${productId}/waitlist/${entryId}`),
  preVendaPendentesCount: () => api.get<{ count: number }>('/api/products/waitlist/pre-venda/pendentes'),
  mine: () => api.get<MyWaitListEntry[]>('/api/products/waitlist/mine'),
}

export interface MyWaitListEntry {
  id: string; productId: string; productName: string; productImageUrl?: string
  position: number; createdAt: string; notifiedAt?: string | null
}

export interface MyReservation {
  id: string; productId: string; productName?: string; productImageUrl?: string
  variantId?: string; variantLabel?: string; quantity: number; status: string
  notes?: string; reservedAt: string; expiresAt: string
  fulfilledAt?: string; cancelledAt?: string; isExpired: boolean
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
export interface ClienteHistoricoDto {
  userId: string; userName: string
  totalVisitas: number; totalGasto: number
  primeiraVisita: string | null; ultimaVisita: string | null
  comandas: ClienteHistoricoComanda[]
  vendasAvulsas: ClienteHistoricoVendaAvulsa[]
  crediarios: ClienteHistoricoCrediario[]
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

// ── Timers ────────────────────────────────────────────────────────────────────

export const timerApi = {
  list:   () => api.get<TimerDto[]>('/api/timers'),
  create: (t: { name: string; durationSeconds: number; soundPreset: string; warnAtSeconds: number }) =>
    api.post<TimerDto>('/api/timers', t),
  update: (id: string, req: { action: string; name?: string; durationSeconds?: number; soundPreset?: string; warnAtSeconds?: number; fromRemaining?: number }) =>
    api.put<TimerDto>(`/api/timers/${id}`, req),
  remove: (id: string) => api.delete(`/api/timers/${id}`),
}

// ── Assistente IA ─────────────────────────────────────────────────────────────

export interface AiChatResponse {
  reply:   string
  success: boolean
  error?:  string
  action?: { type: 'navigate' | 'openWizard'; route?: string }
}

export const aiApi = {
  chat: (message: string) =>
    api.post<AiChatResponse>('/api/ai/chat', { message }),
}

// ── Painel do dono da plataforma (gestão de tenants) ──────────────────────────

export type TenantStatus = 'Active' | 'Suspended'
export type TenantPaymentStatus = 'Pago' | 'Atrasado' | 'Isento'

export interface TenantSummary {
  id: string; slug: string; schemaName: string
  status: TenantStatus; createdAt: string
  planName: string; paymentStatus: TenantPaymentStatus; enabledModules: string[]
  customDomain: string | null
  maxUsers: number | null
}

export interface CreateTenantRequest {
  slug: string; adminEmail: string; adminPassword: string; enabledModules?: string[]
  planName?: string; maxUsers?: number | null
}

/** Catálogo de módulos pagos — mesma lista que o backend aceita
 * (TenantProvisioningService.KnownModules / RequireModuleAttribute). */
export const TENANT_MODULES = [
  { value: 'fiscal',   label: 'Fiscal',              description: 'Emissão de NFC-e' },
  { value: 'estoque',  label: 'Estoque',              description: 'Variantes, reservas e lista de espera (pré-venda)' },
  { value: 'pontos',   label: 'Fidelidade (Pontos)',  description: 'Programa de pontos/cashback dos clientes' },
  { value: 'contador', label: 'Portal do Contador',   description: 'Acesso cross-tenant do contador da loja' },
  { value: 'ia',       label: 'Assistente de IA',     description: 'Chat com IA (Gemini) sobre estoque e devedores' },
  { value: 'eventos',  label: 'Gestão de Eventos',    description: 'Cadastro de eventos e cobrança de entrada' },
] as const

/** Presets de plano pro painel de criação de tenant — só pré-marcam os
 * módulos e o limite de acesso; o dono da plataforma ainda pode ajustar
 * manualmente antes de criar (módulos personalizados continuam possíveis). */
export const TENANT_PLAN_PRESETS = [
  {
    name: 'Mar',
    description: 'Plano completo — todos os módulos',
    modules: TENANT_MODULES.map(m => m.value) as string[],
    maxUsers: null as number | null,
  },
  {
    name: 'Lagoa',
    description: 'Plano base — sem IA, eventos, contador ou estoque avançado',
    modules: ['fiscal', 'pontos'] as string[],
    maxUsers: 4 as number | null,
  },
] as const

export interface UpdateTenantBillingRequest {
  planName: string; paymentStatus: TenantPaymentStatus; enabledModules: string[]
  maxUsers?: number | null
}

export interface TenantActivity {
  tenantId: string; receitaMesAtualCents: number; lastActivityAt: string | null
}

export interface PlatformOverviewDto {
  activeTenants: number; suspendedTenants: number
  receitaMesAtualCents: number
  paymentStatusCounts: Record<string, number>
  moduleAdoptionCounts: Record<string, number>
  tenants: TenantActivity[]
}

export interface TenantUsagePathDto {
  path: string; horas: number; visitas: number
}

export interface TenantUsageDto {
  totalHoras: number; usuariosAtivos: number; topPaths: TenantUsagePathDto[]
}

export const platformApi = {
  listTenants: () =>
    api.get<TenantSummary[]>('/api/platform/tenants'),
  createTenant: (req: CreateTenantRequest) =>
    api.post<TenantSummary>('/api/platform/tenants', req),
  updateTenantStatus: (id: string, status: TenantStatus) =>
    api.patch<TenantSummary>(`/api/platform/tenants/${id}/status`, { status }),
  updateTenantBilling: (id: string, req: UpdateTenantBillingRequest) =>
    api.patch<TenantSummary>(`/api/platform/tenants/${id}/billing`, req),
  updateTenantDomain: (id: string, customDomain: string | null) =>
    api.patch<TenantSummary>(`/api/platform/tenants/${id}/domain`, { customDomain }),
  getOverview: () =>
    api.get<PlatformOverviewDto>('/api/platform/overview'),
  impersonate: (id: string) =>
    api.post<{ ticket: string }>(`/api/platform/tenants/${id}/impersonate`, {}),
  listLeads: (status?: LeadStatus) =>
    api.get<LeadDto[]>('/api/platform/leads', { params: status ? { status } : undefined }),
  updateLead: (id: string, req: UpdateLeadRequest) =>
    api.patch<LeadDto>(`/api/platform/leads/${id}`, req),
  getTenantStaff: (id: string) =>
    api.get<TenantStaffDto[]>(`/api/platform/tenants/${id}/staff`),
  getTenantCustomers: (id: string, page = 1, pageSize = 50) =>
    api.get<PagedResult<TenantCustomerDto>>(`/api/platform/tenants/${id}/customers`, { params: { page, pageSize } }),
  getTenantAuditLogs: (id: string, page = 1, pageSize = 50) =>
    api.get<PagedResult<AuditLogDto>>(`/api/platform/tenants/${id}/audit-logs`, { params: { page, pageSize } }),
  getTenantUsage: (id: string, de?: string, ate?: string) =>
    api.get<TenantUsageDto>(`/api/platform/tenants/${id}/usage`, { params: { de, ate } }),
  getAggregatedAuditLogs: () =>
    api.get<PlatformAuditLogDto[]>('/api/platform/audit-logs'),
  listSupportTickets: (params?: { status?: SupportTicketStatus; tenantId?: string }) =>
    api.get<SupportTicketDto[]>('/api/platform/support-tickets', { params }),
  getSupportTicket: (id: string) =>
    api.get<SupportTicketDetailDto>(`/api/platform/support-tickets/${id}`),
  replySupportTicket: (id: string, body: string, imageUrl?: string) =>
    api.post<void>(`/api/platform/support-tickets/${id}/messages`, { body, imageUrl }),
  updateSupportTicketStatus: (id: string, status: SupportTicketStatus) =>
    api.patch<{ id: string; status: SupportTicketStatus }>(`/api/platform/support-tickets/${id}/status`, { status }),
}

// ── Paginação genérica ──────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[]; totalCount: number; page: number; pageSize: number; totalPages: number
  hasNext: boolean; hasPrev: boolean
}

// ── Funcionários & clientes de um tenant (visão do dono da plataforma) ─────────

export interface TenantStaffDto {
  id: string; name: string; email: string | null; role: string; perfilNome: string | null
  isActive: boolean; lastLoginAt: string | null; createdAt: string
}

export interface TenantCustomerDto {
  id: string; name: string; email: string | null; whatsApp: string | null
  isActive: boolean; lastLoginAt: string | null; createdAt: string
}

// ── Audit log cross-tenant (fase 1 de logs) ─────────────────────────────────────

export type AuditSeverity = 'Info' | 'Warning' | 'Critical'

export interface AuditLogDto {
  id: string; actorUserId: string | null; actorUserName: string | null
  action: string; entityType: string; entityId: string | null; details: string | null
  targetUserId: string | null; channel: string | null; severity: AuditSeverity; traceId: string | null
  createdAt: string
}

export interface AuditLogPagedResponse {
  items: AuditLogDto[]; totalCount: number; page: number; pageSize: number; totalPages: number
}

export interface PlatformAuditLogDto extends AuditLogDto {
  tenantSlug: string
}

export interface ImportRowErrorDto {
  linha: number; motivo: string
}

export interface ImportResultDto {
  totalLinhas: number; importados: number; erros: ImportRowErrorDto[]
}

// ── Leads (captação pública, CTA da landing) ──────────────────────────────────

export type LeadStatus = 'Novo' | 'Contatado' | 'Convertido' | 'Perdido'
export type LeadDigitalPresence = 'SemSite' | 'SiteLegado' | 'ECommerce'

export interface LeadDto {
  id: string; nome: string; telefone: string; email: string | null; mensagem: string | null
  origem: string; status: LeadStatus; notas: string | null
  digitalPresence: LeadDigitalPresence | null; opportunityScore: number | null; placeId: string | null
  createdAt: string; updatedAt: string; convertedTenantId: string | null
}

export interface CreateLeadRequest {
  nome: string; telefone: string; email?: string; mensagem?: string
}

export interface UpdateLeadRequest {
  status: LeadStatus; notas?: string | null; convertedTenantId?: string | null
  digitalPresence?: LeadDigitalPresence | null; opportunityScore?: number | null; placeId?: string | null
}

export const leadsApi = {
  create: (req: CreateLeadRequest) =>
    api.post<{ message: string }>('/api/leads', req),
}

// ── Suporte (chamados entre lojista e plataforma) ──────────────────────────────

export type SupportTicketStatus = 'Aberto' | 'EmAndamento' | 'Resolvido' | 'Fechado'
export type SupportTicketAuthorRole = 'Tenant' | 'Platform'

export interface SupportTicketMessageDto {
  id: string; authorRole: SupportTicketAuthorRole; authorName: string; body: string
  imageUrl: string | null; createdAt: string
}

export interface SupportTicketDto {
  id: string; tenantId: string; tenantSlug: string | null
  subject: string; status: SupportTicketStatus; createdByUserName: string
  createdAt: string; updatedAt: string; messageCount: number
}

export interface SupportTicketDetailDto extends SupportTicketDto {
  messages: SupportTicketMessageDto[]
}

/** Lado do lojista — chamados da própria loja. */
export const supportApi = {
  listTickets: () =>
    api.get<SupportTicketDto[]>('/api/support/tickets'),
  createTicket: (subject: string, body: string, imageUrl?: string) =>
    api.post<SupportTicketDto>('/api/support/tickets', { subject, body, imageUrl }),
  getTicket: (id: string) =>
    api.get<SupportTicketDetailDto>(`/api/support/tickets/${id}`),
  addMessage: (id: string, body: string, imageUrl?: string) =>
    api.post<void>(`/api/support/tickets/${id}/messages`, { body, imageUrl }),
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
  listAudit: (page = 1, pageSize = 50, entityType?: string) =>
    api.get<AuditLogPagedResponse>('/api/audit', { params: { page, pageSize, entityType } }),
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
  /** Só vem preenchido quando o período consultado é "1º do mês até hoje". */
  projecao?: ProjecaoDto | null
}

export interface ProjecaoDto {
  valorProjetado: number
  metodo: 'ponderado' | 'flat'
  detalhePorDiaSemana?: { diaSemana: string; mediaHistorica: number; ocorrencias: number }[]
}

export interface FechamentoPeriodoDto {
  id: string
  tipo: 'Dia' | 'Semana' | 'Mes'
  dataInicio: string // yyyy-MM-dd
  dataFim: string    // yyyy-MM-dd
  receitaComandas: number
  receitaAvulsa: number
  receita: number
  custoComandas: number
  custoAvulsa: number
  custo: number
  margem: number
  createdAt: string
}

export const analyticsApi = {
  dashboard: () => api.get('/api/analytics/dashboard'),
  clientes:  (apenasInativos = false) =>
    api.get<ClienteInsightDto[]>('/api/analytics/clientes', { params: { apenasInativos } }),
  financeiro: (inicio?: string, fim?: string, filterPaymentMethod?: string) =>
    api.get<FinanceiroDto>('/api/analytics/financeiro', {
      params: { inicio, fim, filterPaymentMethod: filterPaymentMethod || undefined },
    }),
  // Snapshot de um período já fechado (dia/semana/mês) — 404 se ainda não foi fechado.
  getFechamento: (tipo: 'Dia' | 'Semana' | 'Mes', inicio: string, fim: string) =>
    api.get<FechamentoPeriodoDto>('/api/analytics/fechamentos', { params: { tipo, inicio, fim } }),
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

// ── Perfil público ────────────────────────────────────────────────────────────

export interface PublicProfileDto {
  id: string; name: string; profileImageUrl: string | null; memberSince: string
  totalCompras: number
  pointsBalance: number
}

export const publicProfileApi = {
  get: (userId: string) => api.get<PublicProfileDto>(`/api/profile/${userId}`),
}

// ── Reservas (pré-venda) ──────────────────────────────────────────────────────
export const reservationApi = {
  list:      (params?: { status?: string; page?: number; pageSize?: number }) =>
               api.get('/api/reservations', { params }),
  mine:      ()                                    => api.get<MyReservation[]>('/api/reservations/mine'),
  create:    (body: { productId: string; variantId?: string; quantity?: number; notes?: string }) =>
               api.post('/api/reservations', body),
  cancel:    (id: string)                          => api.delete(`/api/reservations/${id}`),
  extend:    (id: string)                          => api.put(`/api/reservations/${id}/extend`),
  homologar: (id: string, body: { mode: 'pdv' | 'comanda'; paymentMethod?: string; comandaId?: string }) =>
               api.post(`/api/reservations/${id}/homologar`, body),
  updateStatus: (id: string, status: string)       => api.put(`/api/reservations/${id}/status`, { status }),
}

// ── Contas a Receber / Pagar ──────────────────────────────────────────────────
export const contasReceberApi = {
  list:      (params?: { type?: string; status?: string; source?: string; search?: string; page?: number }) =>
               api.get('/api/contas-receber', { params }),
  summary:   ()                                    => api.get('/api/contas-receber/summary'),
  create:    (body: { type: string; amount: number; description: string; dueDate?: string; category?: string; supplier?: string; notes?: string }) =>
               api.post('/api/contas-receber', body),
  update:    (id: string, body: Partial<{ description: string; amount: number; dueDate: string; status: string; category: string; supplier: string; notes: string }>) =>
               api.put(`/api/contas-receber/${id}`, body),
  remove:    (id: string)                          => api.delete(`/api/contas-receber/${id}`),
  importOfx: (file: File)                          => {
               const form = new FormData(); form.append('file', file)
               return api.post('/api/contas-receber/import-ofx', form, { headers: { 'Content-Type': 'multipart/form-data' } })
             },
  integracoes:   ()                                => api.get('/api/contas-receber/integracoes'),
  saveIntegracao:(source: string, body: { clientId?: string; clientSecret?: string; cnpj?: string; isActive?: boolean }) =>
                  api.put(`/api/contas-receber/integracoes/${source}`, body),
  sefazStatus:  ()                                 => api.get('/api/contas-receber/sefaz-status'),
}

// ── Fiscal (NFC-e, certificado A1, naturezas de operação) ─────────────────────

export interface FiscalConfigDto {
  cnpj?: string; razaoSocial?: string; inscricaoEstadual?: string
  logradouro?: string; numero?: string; complemento?: string; bairro?: string
  codigoMunicipioIbge?: string; municipio?: string; uf?: string; cep?: string
  cscId?: string; cscConfigurado: boolean
  regimeTributario: string; ambiente: string
  serieNfce: number; proximoNumeroNfce: number
  emailContador?: string
  certificadoConfigurado: boolean
  certificadoValidade?: string
  diasParaVencer?: number
  formasPagamentoAutoEmissao: string[]
  ibptConfigurado: boolean
  ibptAutoSyncEnabled: boolean
  ibptUltimaSincronizacao?: string
  ibptUltimaVersao?: string
  ibptVigenciaInicio?: string
  ibptVigenciaFim?: string
  ibptUltimoErro?: string
}

export interface FiscalSaudeDto {
  status: 'Pronto' | 'RequerAtencao' | 'Bloqueado'
  ambiente: 'Homologacao' | 'Producao'
  checklist: { etapa: string; concluido: boolean }[]
  notas: {
    autorizadas24h: number; rejeitadas24h: number
    pendentesTotal: number; pendenteMaisAntigaDesde?: string
  }
  certificado: { configurado: boolean; certificadoValidade?: string; diasParaVencer?: number; vencido: boolean }
  produtos: { produtosAtivos: number; semNcm: number; produtosPendentes: number; produtosVencidos: number }
  pendencias: { categoria: string; mensagem: string; bloqueia: boolean }[]
  proximaAcao: string
}

export interface IbptStatusDto {
  configurado: boolean; autoSyncAtivo: boolean
  ultimaSincronizacao?: string; ultimaVersao?: string
  vigenciaInicio?: string; vigenciaFim?: string; ultimoErro?: string
  produtosAtivos: number; produtosAutomaticos: number; produtosPendentes: number; produtosVencidos: number
}

export interface IbptSyncResult {
  total: number; atualizados: number; ignoradosManuais: number; falhas: number; erros: string[]
}

export interface NaturezaOperacaoDto {
  id: string; descricao: string; cfop: string; csosn?: string
  percentualCreditoIcmsSn?: number
  origemMercadoria: number
  modalidadeBcSt?: number
  percentualMvaSt?: number
  percentualReducaoBcSt?: number
  aliquotaIcmsSt?: number
  aliquotaIcmsProprio?: number
  aliquotaFcpSt?: number
  baseStFixaEmCentavos?: number
  ibsCbsCst: string
  ibsCbsClassTrib: string
  isPadrao: boolean; isActive: boolean
}

export interface SaveNaturezaFiscalBody {
  descricao: string; cfop: string; csosn?: string; percentualCreditoSn?: number
  origemMercadoria: number; modalidadeBcSt?: number; percentualMvaSt?: number
  percentualReducaoBcSt?: number; aliquotaIcmsSt?: number; aliquotaIcmsProprio?: number
  aliquotaFcpSt?: number; baseStFixaEmCentavos?: number
  ibsCbsCst: string; ibsCbsClassTrib: string; isPadrao: boolean
}

export interface NotaFiscalDto {
  id: string; origem: string
  comandaId?: string; vendaAvulsaId?: string
  status: string
  valorTotalEmCentavos: number
  serie?: number; numero?: number
  chaveAcesso?: string; protocolo?: string; motivoRejeicao?: string
  emitidoEm?: string; canceladoEm?: string; inutilizadoEm?: string
  erpEstornadoEm?: string; erpEstornoErro?: string
  tentativasReprocessamento: number
  createdAt: string
}

export interface CupomItemDto {
  nome: string; quantidade: number; precoUnitarioCentavos: number; subtotalCentavos: number
  tributosAproximadosCentavos: number
}

export interface CupomDto {
  razaoSocial: string; cnpj: string; endereco: string
  chaveAcesso?: string; protocolo?: string; emitidoEm?: string
  serie: number; numero: number; status: string
  itens: CupomItemDto[]; descontoTotalCentavos: number; valorTotalCentavos: number; formaPagamento: string
  tributosFederaisCentavos: number; tributosEstaduaisCentavos: number; tributosMunicipaisCentavos: number
  fontesTributos?: string
  qrCodeUrl?: string
}

export const fiscalApi = {
  getSaude:   () => api.get<FiscalSaudeDto>('/api/fiscal/saude'),
  getConfig:  ()                                    => api.get<FiscalConfigDto>('/api/fiscal/config'),
  saveConfig: (body: Partial<{
    cnpj: string; razaoSocial: string; inscricaoEstadual: string
    logradouro: string; numero: string; complemento: string; bairro: string
    codigoMunicipioIbge: string; municipio: string; uf: string; cep: string
    cscId: string; cscToken: string
    regimeTributario: string; ambiente: string; serieNfce: number; emailContador: string
    formasPagamentoAutoEmissao: string[]
    ibptToken: string; ibptAutoSyncEnabled: boolean; removerIbptToken: boolean
  }>) => api.put<FiscalConfigDto>('/api/fiscal/config', body),

  getIbptStatus: () => api.get<IbptStatusDto>('/api/fiscal/ibpt/status'),
  sincronizarIbpt: () => api.post<IbptSyncResult>('/api/fiscal/ibpt/sincronizar'),

  uploadCertificado: (file: File, senha: string) => {
    const form = new FormData()
    form.append('file', file)
    form.append('senha', senha)
    return api.post<{ message: string; validade: string; diasRestantes: number }>(
      '/api/fiscal/certificado', form, { headers: { 'Content-Type': 'multipart/form-data' } })
  },

  listNaturezas:  ()                                => api.get<NaturezaOperacaoDto[]>('/api/fiscal/naturezas-operacao'),
  createNatureza: (body: SaveNaturezaFiscalBody) =>
                   api.post<NaturezaOperacaoDto>('/api/fiscal/naturezas-operacao', body),
  updateNatureza: (id: string, body: SaveNaturezaFiscalBody) =>
                   api.put<NaturezaOperacaoDto>(`/api/fiscal/naturezas-operacao/${id}`, body),
  removeNatureza: (id: string)                      => api.delete(`/api/fiscal/naturezas-operacao/${id}`),

  exportarXmls: (inicio: string, fim: string) =>
    api.get('/api/fiscal/exportar-xmls', { params: { inicio, fim }, responseType: 'blob' }),

  listNotas: (params?: { status?: string; page?: number; pageSize?: number }) =>
    api.get<{ items: NotaFiscalDto[]; total: number; totalPages: number; pendentesCount: number; pendenteMaisAntiga?: string }>('/api/fiscal/notas', { params }),
  reprocessarNota: (id: string) =>
    api.post<{ id: string; status: string; motivoRejeicao?: string }>(`/api/fiscal/notas/${id}/reprocessar`),
  cancelarNota: (id: string, justificativa: string) =>
    api.post<{ id: string; status: string; erpEstornadoEm?: string; erpEstornoErro?: string }>(`/api/fiscal/notas/${id}/cancelar`, { justificativa }),
  inutilizarFaixa: (body: {
    ano: number; serie: number; numeroInicial: number; numeroFinal: number; justificativa: string
  }) => api.post<{
    id: string; ano: number; serie: number; numeroInicial: number; numeroFinal: number
    protocolo: string; inutilizadoEm: string
  }>('/api/fiscal/inutilizacoes', body),
  reprocessarEstornoErp: (id: string) =>
    api.post<{ id: string; erpEstornadoEm?: string; erpEstornoErro?: string }>(`/api/fiscal/notas/${id}/reprocessar-estorno-erp`),
  obterCupom: (id: string) => api.get<CupomDto>(`/api/fiscal/notas/${id}/cupom`),

  emitirNotaComanda: (comandaId: string) =>
    api.post<{ id: string; status: string; motivoRejeicao?: string }>(`/api/fiscal/emitir/comanda/${comandaId}`),
  emitirNotaVendaAvulsa: (vendaId: string) =>
    api.post<{ id: string; status: string; motivoRejeicao?: string }>(`/api/fiscal/emitir/venda-avulsa/${vendaId}`),

  convidarContador: (email: string) =>
    api.post<{ message: string }>('/api/fiscal/contador/convidar', { email }),
  listSolicitacoesContador: () =>
    api.get<SolicitacaoContadorDto[]>('/api/fiscal/contador/solicitacoes'),
  aprovarSolicitacaoContador: (linkId: string) =>
    api.post<{ message: string }>(`/api/fiscal/contador/solicitacoes/${linkId}/aprovar`),
  recusarSolicitacaoContador: (linkId: string) =>
    api.post<{ message: string }>(`/api/fiscal/contador/solicitacoes/${linkId}/recusar`),
  listAvisosContador: () =>
    api.get<AvisoContadorDto[]>('/api/fiscal/contador/avisos'),
  postAvisoContador: (mensagem: string, linkId?: string) =>
    api.post<{ message: string }>('/api/fiscal/contador/avisos', { mensagem, linkId }),
}

export interface MinhaNotaDto {
  id: string; status: string; valorTotalEmCentavos: number
  emitidoEm?: string; createdAt: string
}

export const minhasNotasApi = {
  list: () => api.get<MinhaNotaDto[]>('/api/minhas-notas'),
  obterCupom: (id: string) => api.get<CupomDto>(`/api/minhas-notas/${id}/cupom`),
}

// ── Portal do contador (cross-tenant — uma conta, vários clientes) ────────────

export interface ContadorClienteDto {
  tenantId: string; slug: string; status: 'Pending' | 'Approved'
  certificadoValidade?: string; ultimaNotaEm?: string
}

export interface AvisoContadorDto {
  id: string; autor: 'Contador' | 'Lojista'; mensagem: string; createdAt: string
}

export interface ContadorNotaDto {
  id: string; origem: string; status: string
  valorTotalEmCentavos: number
  serie?: number; numero?: number; chaveAcesso?: string
  emitidoEm?: string; canceladoEm?: string; createdAt: string
}

export interface ContadorConfigDto {
  cnpj?: string; razaoSocial?: string; inscricaoEstadual?: string
  logradouro?: string; numero?: string; complemento?: string; bairro?: string
  municipio?: string; uf?: string; cep?: string
  regimeTributario: string
}

export interface SolicitacaoContadorDto {
  linkId: string; name: string; email: string
  status: 'Pending' | 'Approved'; createdAt: string
}

export const contadorApi = {
  listClientes: () => api.get<ContadorClienteDto[]>('/api/contador-portal/clientes'),
  solicitarAcesso: (tenantSlug: string) =>
    api.post<{ message: string }>('/api/contador-portal/solicitar-acesso', { tenantSlug }),
  listNotas: (tenantId: string, params?: { inicio?: string; fim?: string; status?: string; page?: number; pageSize?: number }) =>
    api.get<{ items: ContadorNotaDto[]; total: number; totalPages: number }>(`/api/contador-portal/clientes/${tenantId}/notas`, { params }),
  exportarXmls: (tenantId: string, inicio: string, fim: string) =>
    api.get(`/api/contador-portal/clientes/${tenantId}/exportar-xmls`, { params: { inicio, fim }, responseType: 'blob' }),
  getConfig: (tenantId: string) => api.get<ContadorConfigDto>(`/api/contador-portal/clientes/${tenantId}/config`),
  listAvisos: (tenantId: string) => api.get<AvisoContadorDto[]>(`/api/contador-portal/clientes/${tenantId}/avisos`),
  postAviso: (tenantId: string, mensagem: string) =>
    api.post<{ message: string }>(`/api/contador-portal/clientes/${tenantId}/avisos`, { mensagem }),
}

// ── Personalização do site (nome, textos, cores da landing) ───────────────────

export interface SiteConfigDto {
  siteName: string
  heroSubtitle: string
  addressLine: string
  contactPersonName: string
  whatsappNumber: string
  contactEmail: string
  logoUrl?: string | null
  faviconUrl?: string | null
  pwaIconUrl?: string | null
  adminIconUrl?: string | null
  navTorneiosLabel: string
  navProdutosLabel: string
  navMercadoLabel: string
  navPontosLabel: string
  ctaVerEventosLabel: string
  ctaVerTorneiosLabel: string
  ctaVerProdutosLabel: string
  torneiosEyebrow: string
  torneiosTitle: string
  produtosEyebrow: string
  produtosTitle: string
  pontosEyebrow: string
  pontosTitle: string
  pontosParagraph: string
  pontosFidelidadeAtivo: boolean
  colorPrimary: string
  colorAccent: string
  colorNavy: string
  colorBackground: string
  colorCard: string
  /** Módulos pagos habilitados pro tenant atual (ex: "fiscal") — "carona" no fetch
   * de SiteConfig pra evitar round-trip novo, não é config de site de verdade. */
  enabledModules: string[]
}

export const siteConfigApi = {
  get:  () => api.get<SiteConfigDto>('/api/site-config'),
  save: (body: Partial<SiteConfigDto>) => api.put<SiteConfigDto>('/api/site-config', body),
}

// ── SMTP próprio do tenant (opcional, cai pro global se não configurado) ──────

export interface EmailConfigDto {
  smtpHost?: string | null
  smtpPort?: number | null
  smtpUsername?: string | null
  fromName?: string | null
  isActive: boolean
  hasPassword: boolean
}

export interface SaveEmailConfigRequest {
  smtpHost?: string
  smtpPort?: number
  smtpUsername?: string
  smtpPassword?: string
  fromName?: string
  isActive?: boolean
}

export const emailConfigApi = {
  get:  () => api.get<EmailConfigDto>('/api/email-config'),
  save: (body: SaveEmailConfigRequest) => api.put<EmailConfigDto>('/api/email-config', body),
}

// ── Diretório público de lojas (site institucional) ──────────────────────────

export interface PublicTenantDto {
  slug: string
  displayName: string
  logoUrl: string | null
}

export const publicDirectoryApi = {
  listTenants: () => api.get<PublicTenantDto[]>('/api/public/tenants'),
}

// ── Notificações in-app ───────────────────────────────────────────────────────

export interface AppNotification {
  id: string; title: string; body: string; link?: string; imageUrl?: string
  createdAt: string; readAt?: string; isRead: boolean
}

export const notificationsApi = {
  list:       () => api.get<AppNotification[]>('/api/notifications'),
  unreadCount:() => api.get<{ count: number }>('/api/notifications/unread-count'),
  markRead:   (id: string) => api.patch(`/api/notifications/${id}/read`),
  markAllRead:() => api.patch('/api/notifications/read-all'),
  remove:     (id: string) => api.delete(`/api/notifications/${id}`),
}

// ── Mensageria (admin) ────────────────────────────────────────────────────────

export interface MensageriaClient {
  id: string; name: string; email?: string; whatsApp?: string; pointsBalance: number
}

export interface MensageriaSegment { id: string; label: string }

export const mensageriaApi = {
  clients:  () => api.get<MensageriaClient[]>('/api/admin/mensageria/clients'),
  segments: () => api.get<MensageriaSegment[]>('/api/admin/mensageria/segments'),
  send:     (body: {
    title: string; body: string; link?: string; imageUrl?: string
    channel: 'inapp' | 'email' | 'both'
    segment?: string; userIds?: string[]
  }) => api.post<{ message: string; inApp: number; emails: number; total: number }>('/api/admin/mensageria/send', body),
}

// ── Push notifications (browser) ──────────────────────────────────────────────

export const pushApi = {
  publicKey:   () => api.get<{ publicKey: string }>('/api/push/vapid-public-key'),
  subscribe:   (sub: { endpoint: string; p256dh: string; auth: string }) =>
                 api.post('/api/push/subscribe', sub),
  unsubscribe: (endpoint: string) =>
                 api.delete('/api/push/subscribe', { data: { endpoint } }),
}

// ── Eventos (gestão de eventos + cobrança de entrada) ─────────────────────────

export type EventoStatus = 'Planejado' | 'EmAndamento' | 'Concluido' | 'Cancelado'

export interface EventoDto {
  id: string; nome: string; descricao: string | null; dataEvento: string
  precoEntradaInCents: number; capacidadeMaxima: number | null; status: EventoStatus
  entradasVendidas: number; entradasCheckIn: number; faturamentoInCents: number
  createdAt: string
}

export interface EventoEntradaDto {
  id: string; nomeCliente: string; userId: string | null
  formaPagamento: string; valorPagoInCents: number
  checkInEm: string | null; canceladaEm: string | null
  vendidaPorAdminNome: string; createdAt: string
}

export interface SaveEventoRequest {
  nome: string; descricao?: string; dataEvento: string
  precoEntradaInCents: number; capacidadeMaxima?: number | null
}

export const eventosApi = {
  list: (status?: EventoStatus) =>
    api.get<EventoDto[]>('/api/eventos', { params: status ? { status } : undefined }),
  create: (body: SaveEventoRequest) =>
    api.post<EventoDto>('/api/eventos', body),
  update: (id: string, body: SaveEventoRequest & { status: EventoStatus }) =>
    api.put<EventoDto>(`/api/eventos/${id}`, body),
  cancel: (id: string) =>
    api.delete(`/api/eventos/${id}`),
  listEntradas: (eventoId: string) =>
    api.get<EventoEntradaDto[]>(`/api/eventos/${eventoId}/entradas`),
  venderEntrada: (eventoId: string, body: { nomeCliente: string; formaPagamento: string; userId?: string; valorPagoInCents?: number }) =>
    api.post<EventoEntradaDto>(`/api/eventos/${eventoId}/entradas`, body),
  checkIn: (eventoId: string, entradaId: string) =>
    api.post<EventoEntradaDto>(`/api/eventos/${eventoId}/entradas/${entradaId}/checkin`),
  cancelarEntrada: (eventoId: string, entradaId: string) =>
    api.delete(`/api/eventos/${eventoId}/entradas/${entradaId}`),
}
