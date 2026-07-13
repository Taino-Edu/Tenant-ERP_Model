'use client'
import { useEffect, useState, useCallback } from 'react'
import { useParams } from 'next/navigation'
import Link from 'next/link'
import { supportApi, SupportTicketDetailDto, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import AttachImageButton from '@/components/support/AttachImageButton'
import toast from 'react-hot-toast'
import { ArrowLeft, LifeBuoy, Loader2, Send } from 'lucide-react'
import clsx from 'clsx'

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function AdminSupportTicketPage() {
  const params = useParams<{ id: string }>()
  const ticketId = params.id

  const [ticket, setTicket] = useState<SupportTicketDetailDto | null>(null)
  const [reply, setReply] = useState('')
  const [replyImage, setReplyImage] = useState<string | null>(null)
  const [sending, setSending] = useState(false)

  const fetchTicket = useCallback(() => {
    supportApi.getTicket(ticketId)
      .then(r => setTicket(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar chamado')))
  }, [ticketId])

  useEffect(() => { fetchTicket() }, [fetchTicket])

  async function handleReply(e: React.FormEvent) {
    e.preventDefault()
    if (!reply.trim() && !replyImage) return
    setSending(true)
    try {
      await supportApi.addMessage(ticketId, reply.trim(), replyImage ?? undefined)
      setReply('')
      setReplyImage(null)
      fetchTicket()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao enviar mensagem.'))
    } finally {
      setSending(false)
    }
  }

  if (!ticket) return <div className="flex justify-center py-16"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>

  return (
    <div className="space-y-5">
      <Link href="/admin/suporte" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white">
        <ArrowLeft className="w-4 h-4" /> Voltar pra Suporte
      </Link>

      <PageHeader icon={LifeBuoy} title={ticket.subject} description={`Status: ${ticket.status} · aberto em ${fmtDateTime(ticket.createdAt)}`} />

      <div className="card space-y-4">
        {ticket.messages.map(m => (
          <div key={m.id} className={clsx('max-w-lg rounded-xl px-4 py-3', m.authorRole === 'Tenant' ? 'ml-auto bg-brand-600/15 border border-brand-500/30' : 'bg-surface-700/60 border border-surface-600')}>
            <p className="text-xs text-gray-400 mb-1">{m.authorRole === 'Platform' ? 'Plataforma' : m.authorName} · {fmtDateTime(m.createdAt)}</p>
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
          placeholder="Escreva sua mensagem..."
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
