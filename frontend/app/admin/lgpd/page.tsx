'use client'
// =============================================================================
// admin/lgpd/page.tsx — Painel admin de Compliance LGPD
// Aba 1: Requisições LGPD | Aba 2: Audit Log
// =============================================================================

import { useState, useEffect, useCallback } from 'react'
import { api } from '@/lib/api'
import { Shield, FileText, Clock, CheckCircle, XCircle, AlertTriangle, ChevronLeft, ChevronRight } from 'lucide-react'

// ── Tipos ─────────────────────────────────────────────────────────────────────

interface LgpdRequest {
  id: string
  requesterName: string
  requesterEmail: string
  requesterCpf: string
  requestType: string
  description: string | null
  status: string
  adminResponse: string | null
  createdAt: string
  deadline: string
  respondedAt: string | null
  isOverdue: boolean
  isUrgent: boolean
}

interface AuditLog {
  id: string
  actorUserId: string | null
  actorUserName: string | null
  action: string
  entityType: string
  entityId: string | null
  details: string | null
  createdAt: string
}

interface AuditPagedResponse {
  items: AuditLog[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const STATUS_STYLES: Record<string, { label: string; className: string; icon: React.ReactNode }> = {
  Recebido:  {
    label:     'Recebido',
    className: 'bg-yellow-100 text-yellow-800 border border-yellow-200',
    icon:      <Clock className="w-3 h-3" />,
  },
  EmAnalise: {
    label:     'Em Análise',
    className: 'bg-blue-100 text-blue-800 border border-blue-200',
    icon:      <FileText className="w-3 h-3" />,
  },
  Concluido: {
    label:     'Concluído',
    className: 'bg-green-100 text-green-800 border border-green-200',
    icon:      <CheckCircle className="w-3 h-3" />,
  },
  Negado: {
    label:     'Negado',
    className: 'bg-red-100 text-red-800 border border-red-200',
    icon:      <XCircle className="w-3 h-3" />,
  },
}

function StatusBadge({ status }: { status: string }) {
  const s = STATUS_STYLES[status] ?? { label: status, className: 'bg-gray-100 text-gray-700', icon: null }
  return (
    <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full ${s.className}`}>
      {s.icon}
      {s.label}
    </span>
  )
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// ── Modal de resposta ─────────────────────────────────────────────────────────

function RespondModal({
  request,
  onClose,
  onSaved,
}: {
  request: LgpdRequest
  onClose: () => void
  onSaved:  () => void
}) {
  const [status,   setStatus]   = useState('EmAnalise')
  const [response, setResponse] = useState(request.adminResponse ?? '')
  const [loading,  setLoading]  = useState(false)
  const [error,    setError]    = useState<string | null>(null)

  async function handleSave() {
    if (!response.trim()) { setError('A resposta é obrigatória.'); return }
    setError(null)
    setLoading(true)
    try {
      await api.put(`/api/lgpd/requests/${request.id}/respond`, { status, adminResponse: response })
      onSaved()
    } catch {
      setError('Erro ao salvar resposta. Tente novamente.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4">
      <div className="bg-surface-800 rounded-2xl shadow-2xl border border-surface-500 w-full max-w-lg p-6">
        <h3 className="text-lg font-bold text-white mb-1">Responder Solicitação</h3>
        <p className="text-xs text-gray-400 mb-4 font-mono"># {request.id}</p>

        <div className="grid grid-cols-2 gap-3 text-sm mb-4">
          <div>
            <p className="text-gray-400 text-xs">Solicitante</p>
            <p className="text-white font-medium">{request.requesterName}</p>
          </div>
          <div>
            <p className="text-gray-400 text-xs">Tipo</p>
            <p className="text-white font-medium">{request.requestType}</p>
          </div>
        </div>

        {request.description && (
          <div className="bg-surface-700 rounded-lg p-3 mb-4 text-sm text-gray-300 max-h-24 overflow-y-auto">
            <p className="text-xs text-gray-500 mb-1">Descrição do solicitante:</p>
            {request.description}
          </div>
        )}

        <div className="mb-3">
          <label className="block text-xs text-gray-400 mb-1">Novo status</label>
          <select
            value={status}
            onChange={e => setStatus(e.target.value)}
            className="w-full bg-surface-700 border border-surface-500 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="EmAnalise">Em Análise</option>
            <option value="Concluido">Concluído</option>
            <option value="Negado">Negado</option>
          </select>
        </div>

        <div className="mb-4">
          <label className="block text-xs text-gray-400 mb-1">
            Resposta formal <span className="text-red-400">*</span>
          </label>
          <textarea
            value={response}
            onChange={e => setResponse(e.target.value)}
            rows={5}
            maxLength={4000}
            className="w-full bg-surface-700 border border-surface-500 rounded-lg px-3 py-2 text-sm text-white resize-none focus:outline-none focus:ring-2 focus:ring-brand-500 placeholder:text-gray-500"
            placeholder="Descreva a resposta formal ao titular dos dados..."
          />
          <p className="text-xs text-gray-500 mt-1">{response.length}/4000 caracteres</p>
        </div>

        {error && (
          <p className="text-sm text-red-400 mb-3">{error}</p>
        )}

        <div className="flex gap-2 justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-400 hover:text-white transition-colors"
          >
            Cancelar
          </button>
          <button
            onClick={handleSave}
            disabled={loading}
            className="px-5 py-2 bg-brand-500 text-white text-sm font-medium rounded-lg hover:bg-brand-600 disabled:opacity-50 transition-colors"
          >
            {loading ? 'Salvando...' : 'Salvar resposta'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Componente principal ──────────────────────────────────────────────────────

export default function LgpdAdminPage() {
  const [tab, setTab] = useState<'requests' | 'audit'>('requests')

  // ── Requisições LGPD ────────────────────────────────────────────────────
  const [requests,      setRequests]      = useState<LgpdRequest[]>([])
  const [reqLoading,    setReqLoading]    = useState(true)
  const [reqError,      setReqError]      = useState<string | null>(null)
  const [statusFilter,  setStatusFilter]  = useState('')
  const [responding,    setResponding]    = useState<LgpdRequest | null>(null)

  const loadRequests = useCallback(async () => {
    setReqLoading(true)
    setReqError(null)
    try {
      const res = await api.get<LgpdRequest[]>('/api/lgpd/requests', {
        params: statusFilter ? { status: statusFilter } : undefined,
      })
      setRequests(res.data)
    } catch {
      setReqError('Erro ao carregar solicitações LGPD.')
    } finally {
      setReqLoading(false)
    }
  }, [statusFilter])

  useEffect(() => {
    if (tab === 'requests') loadRequests()
  }, [tab, loadRequests])

  // ── Audit Log ───────────────────────────────────────────────────────────
  const [auditData,    setAuditData]    = useState<AuditPagedResponse | null>(null)
  const [auditLoading, setAuditLoading] = useState(false)
  const [auditError,   setAuditError]   = useState<string | null>(null)
  const [auditPage,    setAuditPage]    = useState(1)

  const loadAudit = useCallback(async (page = 1) => {
    setAuditLoading(true)
    setAuditError(null)
    try {
      const res = await api.get<AuditPagedResponse>('/api/audit', {
        params: { page, pageSize: 50 },
      })
      setAuditData(res.data)
      setAuditPage(page)
    } catch {
      setAuditError('Erro ao carregar audit log.')
    } finally {
      setAuditLoading(false)
    }
  }, [])

  useEffect(() => {
    if (tab === 'audit') loadAudit(1)
  }, [tab, loadAudit])

  return (
    <div className="p-4 sm:p-6">
      {/* Título */}
      <div className="flex items-center gap-3 mb-6">
        <div className="w-10 h-10 rounded-xl bg-brand-500/20 border border-brand-500/30 flex items-center justify-center">
          <Shield className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-white">LGPD &amp; Privacidade</h1>
          <p className="text-xs text-gray-400">Gestão de solicitações e trilha de auditoria</p>
        </div>
      </div>

      {/* Abas */}
      <div className="flex gap-1 bg-surface-700 rounded-xl p-1 w-fit mb-6">
        {(['requests', 'audit'] as const).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium rounded-lg transition-colors ${
              tab === t
                ? 'bg-brand-500 text-white'
                : 'text-gray-400 hover:text-white'
            }`}
          >
            {t === 'requests' ? 'Requisições LGPD' : 'Audit Log'}
          </button>
        ))}
      </div>

      {/* ── Aba: Requisições ───────────────────────────────────────────────── */}
      {tab === 'requests' && (
        <div>
          {/* Filtro */}
          <div className="flex gap-2 mb-4">
            {['', 'Recebido', 'EmAnalise', 'Concluido', 'Negado'].map(s => (
              <button
                key={s}
                onClick={() => setStatusFilter(s)}
                className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
                  statusFilter === s
                    ? 'bg-brand-500 text-white'
                    : 'bg-surface-700 text-gray-400 hover:text-white'
                }`}
              >
                {s === '' ? 'Todos' : STATUS_STYLES[s]?.label ?? s}
              </button>
            ))}
          </div>

          {reqLoading && (
            <p className="text-gray-400 text-sm">Carregando...</p>
          )}
          {reqError && (
            <p className="text-red-400 text-sm">{reqError}</p>
          )}

          {!reqLoading && !reqError && (
            <div className="bg-surface-800 rounded-2xl border border-surface-500 overflow-hidden">
              {requests.length === 0 ? (
                <div className="p-8 text-center text-gray-400 text-sm">
                  Nenhuma solicitação encontrada.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-surface-500 text-xs text-gray-400">
                        <th className="text-left px-4 py-3 font-medium">Protocolo</th>
                        <th className="text-left px-4 py-3 font-medium">Solicitante</th>
                        <th className="text-left px-4 py-3 font-medium">Tipo</th>
                        <th className="text-left px-4 py-3 font-medium">Status</th>
                        <th className="text-left px-4 py-3 font-medium">Prazo</th>
                        <th className="text-left px-4 py-3 font-medium">Ação</th>
                      </tr>
                    </thead>
                    <tbody>
                      {requests.map(req => (
                        <tr
                          key={req.id}
                          className={`border-b border-surface-500/50 hover:bg-surface-700/50 transition-colors ${
                            req.isOverdue ? 'bg-red-900/10' : req.isUrgent ? 'bg-yellow-900/10' : ''
                          }`}
                        >
                          <td className="px-4 py-3">
                            <div className="flex items-center gap-1.5">
                              {req.isOverdue && (
                                <AlertTriangle className="w-3.5 h-3.5 text-red-400 shrink-0" aria-label="Prazo vencido" />
                              )}
                              {!req.isOverdue && req.isUrgent && (
                                <AlertTriangle className="w-3.5 h-3.5 text-yellow-400 shrink-0" aria-label="Prazo urgente" />
                              )}
                              <span className="font-mono text-xs text-gray-400 max-w-[100px] truncate">
                                {req.id}
                              </span>
                            </div>
                            <p className="text-xs text-gray-500 mt-0.5">{fmtDate(req.createdAt)}</p>
                          </td>
                          <td className="px-4 py-3">
                            <p className="text-white font-medium">{req.requesterName}</p>
                            <p className="text-xs text-gray-400">{req.requesterEmail}</p>
                          </td>
                          <td className="px-4 py-3 text-gray-300">{req.requestType}</td>
                          <td className="px-4 py-3">
                            <StatusBadge status={req.status} />
                          </td>
                          <td className="px-4 py-3">
                            <span className={`text-xs font-medium ${req.isOverdue ? 'text-red-400' : req.isUrgent ? 'text-yellow-400' : 'text-gray-400'}`}>
                              {fmtDate(req.deadline)}
                            </span>
                          </td>
                          <td className="px-4 py-3">
                            <button
                              onClick={() => setResponding(req)}
                              className="text-xs text-brand-400 hover:text-brand-300 font-medium transition-colors"
                            >
                              {req.status === 'Concluido' || req.status === 'Negado'
                                ? 'Ver resposta'
                                : 'Responder'}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* ── Aba: Audit Log ─────────────────────────────────────────────────── */}
      {tab === 'audit' && (
        <div>
          {auditLoading && <p className="text-gray-400 text-sm">Carregando...</p>}
          {auditError  && <p className="text-red-400 text-sm">{auditError}</p>}

          {!auditLoading && !auditError && auditData && (
            <>
              <div className="bg-surface-800 rounded-2xl border border-surface-500 overflow-hidden mb-4">
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-surface-500 text-xs text-gray-400">
                        <th className="text-left px-4 py-3 font-medium">Data</th>
                        <th className="text-left px-4 py-3 font-medium">Ator</th>
                        <th className="text-left px-4 py-3 font-medium">Ação</th>
                        <th className="text-left px-4 py-3 font-medium">Entidade</th>
                        <th className="text-left px-4 py-3 font-medium">Detalhes</th>
                      </tr>
                    </thead>
                    <tbody>
                      {auditData.items.map(log => (
                        <tr key={log.id} className="border-b border-surface-500/50 hover:bg-surface-700/50 transition-colors">
                          <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                            {fmtDateTime(log.createdAt)}
                          </td>
                          <td className="px-4 py-3">
                            <p className="text-white text-xs">{log.actorUserName ?? 'Sistema'}</p>
                            {log.actorUserId && (
                              <p className="text-gray-500 text-xs font-mono">{log.actorUserId.substring(0, 8)}...</p>
                            )}
                          </td>
                          <td className="px-4 py-3">
                            <span className="text-brand-400 font-medium text-xs">{log.action}</span>
                          </td>
                          <td className="px-4 py-3">
                            <p className="text-gray-300 text-xs">{log.entityType}</p>
                            {log.entityId && (
                              <p className="text-gray-500 text-xs font-mono">{log.entityId.substring(0, 12)}...</p>
                            )}
                          </td>
                          <td className="px-4 py-3 text-xs text-gray-400 max-w-[200px] truncate" title={log.details ?? ''}>
                            {log.details ?? '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Paginação */}
              <div className="flex items-center justify-between text-sm text-gray-400">
                <p>
                  {auditData.totalCount} registros — Página {auditData.page} de {auditData.totalPages}
                </p>
                <div className="flex gap-2">
                  <button
                    onClick={() => loadAudit(auditPage - 1)}
                    disabled={auditPage === 1}
                    className="flex items-center gap-1 px-3 py-1.5 bg-surface-700 rounded-lg hover:bg-surface-600 disabled:opacity-40 transition-colors"
                  >
                    <ChevronLeft className="w-4 h-4" />
                    Anterior
                  </button>
                  <button
                    onClick={() => loadAudit(auditPage + 1)}
                    disabled={auditPage >= auditData.totalPages}
                    className="flex items-center gap-1 px-3 py-1.5 bg-surface-700 rounded-lg hover:bg-surface-600 disabled:opacity-40 transition-colors"
                  >
                    Próximo
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      )}

      {/* Modal de resposta */}
      {responding && (
        <RespondModal
          request={responding}
          onClose={() => setResponding(null)}
          onSaved={() => {
            setResponding(null)
            loadRequests()
          }}
        />
      )}
    </div>
  )
}
