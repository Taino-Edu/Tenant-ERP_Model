'use client'
import { useEffect, useState, useCallback } from 'react'
import { useParams } from 'next/navigation'
import Link from 'next/link'
import { platformApi, SupportTicketDetailDto, SupportTicketStatus, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import AttachImageButton from '@/components/support/AttachImageButton'
import StatusPillSelect from '@/components/admin/StatusPillSelect'
import toast from 'react-hot-toast'
import { ArrowLeft, LifeBuoy, Loader2, Send } from 'lucide-react'
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

export default function PlataformaSupportTicketPage() {
  const params = useParams<{ id: string }>()
  const ticketId = params.id

  const [ticket, setTicket] = useState<SupportTicketDetailDto | null>(null)
  const [reply, setReply] = useState('')
  const [replyImage, setReplyImage] = useState<string | null>(null)
  const [sending, setSending] = useState(false)
  const [updatingStatus, setUpdatingStatus] = useState(false)

  const fetchTicket = useCallback(() => {
    platformApi.getSupportTicket(ticketId)
      .then(r => setTicket(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar chamado')))
  }, [ticketId])

  useEffect(() => { fetchTicket() }, [fetchTicket])

  async function handleReply(e: React.FormEvent) {
    e.preventDefault()
    if (!reply.trim() && !replyImage) return
    setSending(true)
    try {
      await platformApi.replySupportTicket(ticketId, reply.trim(), replyImage ?? undefined)
      setReply('')
      setReplyImage(null)
      fetchTicket()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao enviar resposta.'))
    } finally {
      setSending(false)
    }
  }

  async function handleStatusChange(status: SupportTicketStatus) {
    setUpdatingStatus(true)
    try {
      await platformApi.updateSupportTicketStatus(ticketId, status)
      fetchTicket()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar status.'))
    } finally {
      setUpdatingStatus(false)
    }
  }

  if (!ticket) return <div className="flex justify-center py-16"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>

  return (
    <div className="space-y-5">
      <Link href="/plataforma/suporte" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white">
        <ArrowLeft className="w-4 h-4" /> Voltar pra Suporte
      </Link>

      <PageHeader
        icon={LifeBuoy}
        title={ticket.subject}
        description={`${ticket.tenantSlug} · aberto por ${ticket.createdByUserName} · ${fmtDateTime(ticket.createdAt)}`}
        actions={
          <StatusPillSelect
            value={ticket.status}
            options={STATUS_OPTIONS}
            styles={STATUS_STYLES}
            disabled={updatingStatus}
            onChange={handleStatusChange}
          />
        }
      />

      <div className="card space-y-4">
        {ticket.messages.map(m => (
          <div key={m.id} className={clsx('max-w-lg rounded-xl px-4 py-3', m.authorRole === 'Platform' ? 'ml-auto bg-brand-600/15 border border-brand-500/30' : 'bg-surface-700/60 border border-surface-600')}>
            <p className="text-xs text-gray-400 mb-1">{m.authorName} · {fmtDateTime(m.createdAt)}</p>
            {m.body && <p className="text-sm text-white whitespace-pre-wrap">{m.body}</p>}
            {m.imageUrl && (
              <a href={m.imageUrl} target="_blank" rel="noopener noreferrer">
                <img src={m.imageUrl} alt="Anexo" className={clsx('rounded-lg max-h-64 border border-surface-600', m.body && 'mt-2')} />
              </a>
            )}
          </div>
        ))}
      </div>

      <form onSubmit={handleReply} className="card flex items-end gap-3">
        <AttachImageButton value={replyImage} onChange={setReplyImage} />
        <textarea
          className="input flex-1 resize-none"
          rows={2}
          placeholder="Responder ao lojista..."
          value={reply}
          onChange={e => setReply(e.target.value)}
        />
        <button type="submit" disabled={sending || (!reply.trim() && !replyImage)} className="btn-primary py-2.5">
          {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
        </button>
      </form>
    </div>
  )
}
