'use client'
import { useEffect, useState, useCallback } from 'react'
import { platformApi, PlatformAuditLogDto, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { History, Loader2, RefreshCw, Eye } from 'lucide-react'
import { summarizeAuditDetails } from '@/lib/auditFormat'
import SeverityBadge from '@/components/admin/SeverityBadge'
import { AuditLogDetailModal } from '@/components/admin/AuditLogDetailModal'

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function PlataformaLogsPage() {
  const [logs, setLogs] = useState<PlatformAuditLogDto[]>([])
  const [loading, setLoading] = useState(true)
  const [tenantFilter, setTenantFilter] = useState('')
  const [viewingLog, setViewingLog] = useState<PlatformAuditLogDto | null>(null)

  const fetchLogs = useCallback(() => {
    setLoading(true)
    platformApi.getAggregatedAuditLogs()
      .then(r => setLogs(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar logs')))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { fetchLogs() }, [fetchLogs])

  const tenantSlugs = Array.from(new Set(logs.map(l => l.tenantSlug))).sort()
  const filtered = tenantFilter ? logs.filter(l => l.tenantSlug === tenantFilter) : logs

  return (
    <div className="space-y-5">
      <PageHeader
        icon={History}
        title="Logs"
        description="Feed agregado de auditoria (Create/Update/Delete) — até 100 registros mais recentes entre todas as lojas ativas"
        actions={
          <div className="flex items-center gap-2">
            <select className="input text-sm py-1.5" value={tenantFilter} onChange={e => setTenantFilter(e.target.value)}>
              <option value="">Todas as lojas</option>
              {tenantSlugs.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
            <button onClick={fetchLogs} className="btn-secondary text-sm py-1.5"><RefreshCw className="w-4 h-4" /></button>
          </div>
        }
      />

      <div className="card overflow-x-auto">
        {loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : filtered.length === 0 ? (
          <p className="text-gray-400 text-center py-16">Nenhum registro de auditoria ainda.</p>
        ) : (
          <table className="w-full min-w-[820px] text-sm">
            <thead>
              <tr className="text-left text-gray-500 border-b border-surface-600">
                <th className="py-2 font-medium">Quando</th>
                <th className="py-2 font-medium">Loja</th>
                <th className="py-2 font-medium">Ator</th>
                <th className="py-2 font-medium">Ação</th>
                <th className="py-2 font-medium">Entidade</th>
                <th className="py-2 font-medium">Resumo</th>
                <th className="py-2 font-medium">Severidade</th>
                <th className="w-8"></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(l => (
                <tr
                  key={`${l.tenantSlug}-${l.id}`}
                  onClick={() => setViewingLog(l)}
                  className="border-b border-surface-700 last:border-0 hover:bg-surface-700/40 transition-colors cursor-pointer"
                >
                  <td className="py-2.5 text-gray-400 whitespace-nowrap">{fmtDateTime(l.createdAt)}</td>
                  <td className="py-2.5 text-brand-300 font-medium">{l.tenantSlug}</td>
                  <td className="py-2.5 text-white">{l.actorUserName ?? 'Sistema'}</td>
                  <td className="py-2.5 text-gray-400">{l.action}</td>
                  <td className="py-2.5 text-gray-400">{l.entityType}{l.entityId ? ` #${l.entityId.slice(0, 8)}` : ''}</td>
                  <td className="py-2.5 text-gray-400 max-w-[220px] truncate">{summarizeAuditDetails(l.details)}</td>
                  <td className="py-2.5"><SeverityBadge severity={l.severity} /></td>
                  <td className="py-2.5 text-gray-500"><Eye className="w-3.5 h-3.5" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {viewingLog && (
        <AuditLogDetailModal log={viewingLog} onClose={() => setViewingLog(null)} />
      )}
    </div>
  )
}
