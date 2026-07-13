'use client'
import { useEffect, useState, useCallback } from 'react'
import Link from 'next/link'
import { supportApi, SupportTicketDto, SupportTicketStatus, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import ImageUpload from '@/components/admin/ImageUpload'
import toast from 'react-hot-toast'
import { LifeBuoy, Loader2, Plus, X } from 'lucide-react'
import clsx from 'clsx'

const STATUS_STYLES: Record<SupportTicketStatus, string> = {
  Aberto:      'bg-red-500/10 text-red-400 border-red-500/30',
  EmAndamento: 'bg-amber-500/10 text-amber-400 border-amber-500/30',
  Resolvido:   'bg-accent-green/10 text-accent-green border-accent-green/30',
  Fechado:     'bg-gray-500/10 text-gray-400 border-gray-500/30',
}

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function NewTicketModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [subject, setSubject]   = useState('')
  const [body, setBody]         = useState('')
  const [imageUrl, setImageUrl] = useState('')
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      await supportApi.createTicket(subject.trim(), body.trim(), imageUrl || undefined)
      toast.success('Chamado aberto.')
      onCreated()
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao abrir chamado.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <LifeBuoy className="w-5 h-5 text-brand-400" /> Novo chamado
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <div>
            <label className="label">Assunto *</label>
            <input className="input" value={subject} onChange={e => setSubject(e.target.value)} required maxLength={150} />
          </div>
          <div>
            <label className="label">Mensagem *</label>
            <textarea className="input resize-none" rows={4} value={body} onChange={e => setBody(e.target.value)} required />
          </div>
          <ImageUpload currentUrl={imageUrl} onUpload={setImageUrl} label="Imagem (opcional)" hint="print da tela, erro, etc." />
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={loading} className="btn-primary flex-1 justify-center">
              {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />} Abrir chamado
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default function AdminSuportePage() {
  const [tickets, setTickets] = useState<SupportTicketDto[]>([])
  const [loading, setLoading] = useState(true)
  const [showNew, setShowNew] = useState(false)

  const fetchTickets = useCallback(() => {
    setLoading(true)
    supportApi.listTickets()
      .then(r => setTickets(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar chamados')))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { fetchTickets() }, [fetchTickets])

  return (
    <div className="space-y-5">
      <PageHeader
        icon={LifeBuoy}
        title="Suporte"
        description="Fale com a plataforma sobre problemas ou dúvidas do sistema"
        actions={
          <button onClick={() => setShowNew(true)} className="btn-primary text-sm py-1.5">
            <Plus className="w-4 h-4" /> Novo chamado
          </button>
        }
      />

      <div className="card">
        {loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : tickets.length === 0 ? (
          <p className="text-gray-400 text-center py-16">Nenhum chamado aberto ainda.</p>
        ) : (
          <div className="divide-y divide-surface-700">
            {tickets.map(t => (
              <Link key={t.id} href={`/admin/suporte/${t.id}`} className="flex items-center justify-between py-3 hover:bg-surface-700/30 -mx-2 px-2 rounded-lg">
                <div>
                  <p className="text-white font-medium">{t.subject}</p>
                  <p className="text-xs text-gray-400">{fmtDateTime(t.createdAt)}</p>
                </div>
                <span className={clsx('text-xs font-medium px-2 py-0.5 rounded-full border', STATUS_STYLES[t.status])}>{t.status}</span>
              </Link>
            ))}
          </div>
        )}
      </div>

      {showNew && <NewTicketModal onClose={() => setShowNew(false)} onCreated={fetchTickets} />}
    </div>
  )
}
