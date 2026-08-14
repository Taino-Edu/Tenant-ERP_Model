'use client'
import { useEffect, useState, useCallback } from 'react'
import { platformApi, PlatformAuditLogDto, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { History, Loader2, RefreshCw, Eye } from 'lucide-react'
import { summarizeAuditDetails } from '@/lib/auditFormat'
import SeverityBadge from '@/components/admin/SeverityBadge'
import DataTable from '@/components/admin/ui/DataTable'
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

      {/* Oito colunas de auditoria não cabem em 375px de jeito nenhum, e rolar
          820px de lado pra ler CADA linha não é leitura, é garimpo. O DataTable
          mantém a tabela no desktop e reorganiza as mesmas colunas em cards no
          celular, a partir de uma definição só — os papéis `title`/`trailing`/
          `meta` dizem o que vira o quê. */}
      <>
        {loading ? (
          <div className="card flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : (
          <DataTable
            rows={filtered}
            rowKey={l => `${l.tenantSlug}-${l.id}`}
            onRowClick={setViewingLog}
            minWidth="820px"
            // O `.card` que existia aqui em volta só emoldurava a tabela — no
            // celular virava card dentro de card. Agora ele vale só no desktop.
            desktopCard
            empty={<p className="text-gray-400 text-center py-16">Nenhum registro de auditoria ainda.</p>}
            columns={[
              { key: 'quando',    header: 'Quando',     mobile: 'meta',     className: 'text-gray-400 whitespace-nowrap', cell: l => fmtDateTime(l.createdAt) },
              { key: 'loja',      header: 'Loja',       mobile: 'meta',     className: 'text-brand-300 font-medium',      cell: l => <span className="text-brand-300 font-medium">{l.tenantSlug}</span> },
              { key: 'acao',      header: 'Ação',       mobile: 'title',    className: 'text-gray-400',                   cell: l => l.action },
              { key: 'severidade', header: 'Severidade', mobile: 'trailing', cell: l => <SeverityBadge severity={l.severity} /> },
              { key: 'ator',      header: 'Ator',       mobile: 'field',    className: 'text-white',                      cell: l => l.actorUserName ?? 'Sistema' },
              { key: 'entidade',  header: 'Entidade',   mobile: 'field',    className: 'text-gray-400',                   cell: l => `${l.entityType}${l.entityId ? ` #${l.entityId.slice(0, 8)}` : ''}` },
              { key: 'resumo',    header: 'Resumo',     mobile: 'field',    className: 'text-gray-400 max-w-[220px] truncate', cell: l => summarizeAuditDetails(l.details) },
              // Só a tabela precisa do ícone de "abrir": no card o chevron da
              // linha inteira clicável já cumpre esse papel.
              { key: 'abrir',     header: '',           mobile: 'hidden',   className: 'text-gray-500', headerClassName: 'w-8', cell: () => <Eye className="w-3.5 h-3.5" /> },
            ]}
          />
        )}
      </>

      {viewingLog && (
        <AuditLogDetailModal log={viewingLog} onClose={() => setViewingLog(null)} />
      )}
    </div>
  )
}
