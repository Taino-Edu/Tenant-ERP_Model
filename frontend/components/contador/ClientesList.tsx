'use client'
// =============================================================================
// ClientesList.tsx — Primeira tela do portal: lojas vinculadas a este contador
// e o formulário de solicitação de acesso a mais uma.
// =============================================================================
import { useState } from 'react'
import { Building2, ChevronRight, Clock, AlertTriangle, Plus, FileText } from 'lucide-react'
import clsx from 'clsx'
import type { ContadorClienteDto } from '@/lib/api'
import Badge from '@/components/admin/ui/Badge'
import Button from '@/components/admin/ui/Button'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'
import { diasAte, diasDesde } from './contador-shared'

interface Props {
  clientes: ContadorClienteDto[]
  loading: boolean
  onSelecionar: (cliente: ContadorClienteDto) => void
  onSolicitarAcesso: (slug: string) => Promise<void>
}

export default function ClientesList({ clientes, loading, onSelecionar, onSolicitarAcesso }: Props) {
  const [novoSlug, setNovoSlug] = useState('')
  const [solicitando, setSolicitando] = useState(false)

  async function solicitar(e: React.FormEvent) {
    e.preventDefault()
    if (!novoSlug.trim()) return
    setSolicitando(true)
    try {
      await onSolicitarAcesso(novoSlug.trim().toLowerCase())
      setNovoSlug('')
    } finally {
      setSolicitando(false)
    }
  }

  return (
    <div className="space-y-5">
      <form onSubmit={solicitar} className="card p-4 flex flex-col sm:flex-row gap-3 sm:items-end">
        <div className="flex-1">
          <label className="label" htmlFor="slug-loja">Solicitar acesso a mais uma loja</label>
          <input
            id="slug-loja"
            className="input w-full"
            placeholder="slug-da-loja"
            value={novoSlug}
            onChange={e => setNovoSlug(e.target.value)}
          />
        </div>
        <Button type="submit" loading={solicitando} className="justify-center">
          {!solicitando && <Plus className="w-4 h-4" />}
          Solicitar acesso
        </Button>
      </form>

      <div className="card p-0 overflow-hidden">
        {loading ? (
          <Spinner block size="lg" />
        ) : clientes.length === 0 ? (
          <EmptyState
            icon={Building2}
            message="Você ainda não tem nenhuma loja vinculada. Peça o slug ao lojista e solicite acesso acima."
          />
        ) : (
          <ul className="divide-y divide-surface-700">
            {clientes.map(cliente => (
              <li key={cliente.tenantId}>
                <ClienteRow cliente={cliente} onSelecionar={() => onSelecionar(cliente)} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

function ClienteRow({ cliente, onSelecionar }: { cliente: ContadorClienteDto; onSelecionar: () => void }) {
  const aprovado   = cliente.status === 'Approved'
  const diasCert   = aprovado ? diasAte(cliente.certificadoValidade) : null
  const semNotaHa  = aprovado ? diasDesde(cliente.ultimaNotaEm) : null

  const conteudo = (
    <>
      <div className="flex items-center gap-3 min-w-0">
        <div className={clsx('p-2 rounded-xl shrink-0', aprovado ? 'bg-brand-500/10' : 'bg-surface-700')}>
          <Building2 className={clsx('w-4 h-4', aprovado ? 'text-brand-400' : 'text-gray-500')} />
        </div>
        <div className="min-w-0">
          <p className="text-white font-medium truncate">{cliente.slug}</p>
          <p className="text-xs text-gray-500">
            {aprovado
              ? cliente.ultimaNotaEm
                ? `Última nota há ${semNotaHa} dia(s)`
                : 'Nenhuma nota emitida ainda'
              : 'Aguardando o lojista liberar o acesso'}
          </p>
        </div>
      </div>

      <div className="flex items-center gap-2 flex-wrap justify-end">
        {diasCert !== null && diasCert <= 30 && (
          <Badge tone={diasCert <= 7 ? 'danger' : 'warning'}>
            <AlertTriangle className="w-3 h-3 mr-1" />
            {diasCert < 0 ? 'Certificado vencido' : `Certificado vence em ${diasCert}d`}
          </Badge>
        )}
        {aprovado && (cliente.ultimaNotaEm == null || (semNotaHa !== null && semNotaHa > 7)) && (
          <Badge tone="warning">
            <FileText className="w-3 h-3 mr-1" />
            {cliente.ultimaNotaEm == null ? 'Sem notas' : `Sem nota há ${semNotaHa}d`}
          </Badge>
        )}
        {aprovado ? (
          <>
            <Badge tone="success">Aprovado</Badge>
            {/* Seta explícita: a linha inteira é clicável, mas sem affordance
                visual o contador não descobria que dava pra abrir o cliente. */}
            <ChevronRight className="w-4 h-4 text-gray-500 shrink-0" />
          </>
        ) : (
          <Badge tone="warning">
            <Clock className="w-3 h-3 mr-1" /> Aguardando aprovação
          </Badge>
        )}
      </div>
    </>
  )

  if (!aprovado) {
    return (
      <div className="w-full flex items-center justify-between gap-3 px-4 py-4 flex-wrap opacity-70">
        {conteudo}
      </div>
    )
  }

  return (
    <button
      onClick={onSelecionar}
      className="group w-full flex items-center justify-between gap-3 px-4 py-4 text-left flex-wrap
                 transition-colors hover:bg-surface-700/60 focus:outline-none focus:bg-surface-700/60"
    >
      {conteudo}
    </button>
  )
}
