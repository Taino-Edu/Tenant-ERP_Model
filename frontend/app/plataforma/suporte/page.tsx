'use client'
import { useEffect, useState, useCallback } from 'react'
import Link from 'next/link'
import { platformApi, SupportTicketDto, SupportTicketStatus, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { LifeBuoy, Loader2 } from 'lucide-react'
import clsx from 'clsx'

const STATUS_OPTIONS: SupportTicketStatus[] = ['Aberto', 'EmAndamento', 'Resolvido', 'Fechado']

const STATUS_STYLES: Record<SupportTicketStatus, string> = {
  Aberto:      'bg-red-500/10 text-red-400 border-red-500/30',
  EmAndamento: 'bg-amber-500/10 text-amber-400 border-amber-500/30',
  Resolvido:   'bg-accent-green/10 text-accent-green border-accent-green/30',
  Fechado:     'bg-gray-500/10 text-gray-400 border-gray-500/30',
}

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function PlataformaSuportePage() {
  const [tickets, setTickets] = useState<SupportTicketDto[]>([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState<SupportTicketStatus | ''>('')

  const fetchTickets = useCallback(() => {
    setLoading(true)
    platformApi.listSupportTickets(statusFilter ? { status: statusFilter } : undefined)
      .then(r => setTickets(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar chamados')))
      .finally(() => setLoading(false))
  }, [statusFilter])

  useEffect(() => { fetchTickets() }, [fetchTickets])

  return (
    <div className="space-y-5">
      <PageHeader
        icon={LifeBuoy}
        title="Suporte"
        description="Chamados abertos pelos lojistas"
        actions={
          <select className="input text-sm py-1.5" value={statusFilter} onChange={e => setStatusFilter(e.target.value as SupportTicketStatus | '')}>
            <option value="">Todos os status</option>
            {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        }
      />

      <div className="card">
        {loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : tickets.length === 0 ? (
          <p className="text-gray-400 text-center py-16">Nenhum chamado de suporte ainda.</p>
        ) : (
          <div className="divide-y divide-surface-700">
            {tickets.map(t => (
              <Link key={t.id} href={`/plataforma/suporte/${t.id}`} className="flex items-center justify-between py-3 hover:bg-surface-700/30 -mx-2 px-2 rounded-lg">
                <div>
                  <p className="text-white font-medium">{t.subject}</p>
                  <p className="text-xs text-gray-400">
                    <span className="text-brand-300 font-medium">{t.tenantSlug}</span> · {t.createdByUserName} · {fmtDateTime(t.createdAt)}
                  </p>
                </div>
                <span className={clsx('text-xs font-medium px-2 py-0.5 rounded-full border', STATUS_STYLES[t.status])}>{t.status}</span>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
