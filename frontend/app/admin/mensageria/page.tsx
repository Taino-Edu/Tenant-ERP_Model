'use client'
import { useEffect, useState } from 'react'
import { mensageriaApi, MensageriaClient, MensageriaSegment } from '@/lib/api'
import toast from 'react-hot-toast'
import {
  Send, Users, User, Mail, Bell, BellRing,
  Search, X, CheckCircle, ChevronDown, ChevronUp,
} from 'lucide-react'

type Channel = 'inapp' | 'email' | 'both'
type TargetMode = 'segment' | 'specific'

export default function MensageriaPage() {
  // Form
  const [title,   setTitle]   = useState('')
  const [body,    setBody]    = useState('')
  const [link,    setLink]    = useState('')
  const [channel, setChannel] = useState<Channel>('inapp')

  // Alvos
  const [targetMode, setTargetMode] = useState<TargetMode>('segment')
  const [segment,    setSegment]    = useState('all')
  const [segments,   setSegments]   = useState<MensageriaSegment[]>([])
  const [clients,    setClients]    = useState<MensageriaClient[]>([])
  const [selected,   setSelected]   = useState<Set<string>>(new Set())
  const [search,     setSearch]     = useState('')

  // UI
  const [loading,    setLoading]    = useState(false)
  const [result,     setResult]     = useState<{ inApp: number; emails: number; total: number } | null>(null)
  const [showPicker, setShowPicker] = useState(false)

  useEffect(() => {
    mensageriaApi.segments().then(r => setSegments(r.data)).catch(() => {})
    mensageriaApi.clients().then(r => setClients(r.data)).catch(() => {})
  }, [])

  const filtered = clients.filter(c =>
    c.name.toLowerCase().includes(search.toLowerCase()) ||
    c.email?.toLowerCase().includes(search.toLowerCase()) ||
    c.whatsApp?.includes(search)
  )

  function toggleClient(id: string) {
    setSelected(prev => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  async function handleSend(e: React.FormEvent) {
    e.preventDefault()
    if (!title.trim() || !body.trim()) { toast.error('Preencha título e mensagem.'); return }
    if (targetMode === 'specific' && selected.size === 0) {
      toast.error('Selecione pelo menos um cliente.'); return
    }

    setLoading(true)
    setResult(null)
    try {
      const r = await mensageriaApi.send({
        title, body,
        link: link || undefined,
        channel,
        segment: targetMode === 'segment' ? segment : undefined,
        userIds: targetMode === 'specific' ? [...selected] : undefined,
      })
      setResult(r.data)
      setTitle(''); setBody(''); setLink(''); setSelected(new Set())
      toast.success('Mensagem enviada!')
    } catch {
      toast.error('Erro ao enviar mensagem.')
    } finally {
      setLoading(false)
    }
  }

  const channelOpts: { value: Channel; label: string; icon: React.ReactNode }[] = [
    { value: 'inapp', label: 'Notificação no site',  icon: <Bell className="w-4 h-4" /> },
    { value: 'email', label: 'Somente e-mail',        icon: <Mail className="w-4 h-4" /> },
    { value: 'both',  label: 'Site + e-mail',         icon: <BellRing className="w-4 h-4" /> },
  ]

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-black text-[var(--text-primary)]">Mensageria</h1>
        <p className="text-sm text-[var(--text-muted)] mt-1">
          Envie notificações personalizadas para clientes — in-app, e-mail ou ambos.
        </p>
      </div>

      <form onSubmit={handleSend} className="space-y-5">

        {/* Resultado */}
        {result && (
          <div className="rounded-2xl border border-emerald-500/30 bg-emerald-500/10 p-4 flex items-center gap-3">
            <CheckCircle className="w-5 h-5 text-emerald-400 flex-shrink-0" />
            <div className="text-sm">
              <p className="font-bold text-emerald-400">Enviado com sucesso!</p>
              <p className="text-[var(--text-muted)]">
                {result.total} cliente{result.total !== 1 ? 's' : ''} —
                {result.inApp > 0 && ` ${result.inApp} notificações in-app`}
                {result.inApp > 0 && result.emails > 0 && ','}
                {result.emails > 0 && ` ${result.emails} e-mails`}
              </p>
            </div>
          </div>
        )}

        {/* Conteúdo */}
        <div className="rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-5 space-y-4">
          <h2 className="text-sm font-bold text-[var(--text-primary)] uppercase tracking-wide">Mensagem</h2>

          <div>
            <label className="text-xs font-medium text-[var(--text-muted)] mb-1 block">Título</label>
            <input
              className="w-full rounded-xl border border-[var(--border)] bg-[var(--surface-600)] px-3 py-2 text-sm text-[var(--text-primary)] placeholder-[var(--text-muted)] focus:outline-none focus:ring-2 focus:ring-violet-500"
              placeholder="Ex: Promoção especial de fim de semana"
              value={title} onChange={e => setTitle(e.target.value)}
              maxLength={120} required
            />
          </div>

          <div>
            <label className="text-xs font-medium text-[var(--text-muted)] mb-1 block">
              Mensagem <span className="text-[var(--text-muted)]">({body.length}/500)</span>
            </label>
            <textarea
              className="w-full rounded-xl border border-[var(--border)] bg-[var(--surface-600)] px-3 py-2 text-sm text-[var(--text-primary)] placeholder-[var(--text-muted)] focus:outline-none focus:ring-2 focus:ring-violet-500 resize-none"
              placeholder="Texto da notificação que o cliente vai ler…"
              rows={3} value={body} onChange={e => setBody(e.target.value)}
              maxLength={500} required
            />
          </div>

          <div>
            <label className="text-xs font-medium text-[var(--text-muted)] mb-1 block">
              Link (opcional) — aparece como "Ver" na notificação
            </label>
            <input
              className="w-full rounded-xl border border-[var(--border)] bg-[var(--surface-600)] px-3 py-2 text-sm text-[var(--text-primary)] placeholder-[var(--text-muted)] focus:outline-none focus:ring-2 focus:ring-violet-500"
              placeholder="/produtos ou https://..."
              value={link} onChange={e => setLink(e.target.value)}
            />
          </div>
        </div>

        {/* Canal */}
        <div className="rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-5 space-y-3">
          <h2 className="text-sm font-bold text-[var(--text-primary)] uppercase tracking-wide">Canal de envio</h2>
          <div className="grid grid-cols-3 gap-2">
            {channelOpts.map(opt => (
              <button key={opt.value} type="button"
                onClick={() => setChannel(opt.value)}
                className={`flex flex-col items-center gap-2 p-3 rounded-xl border text-xs font-medium transition-all ${
                  channel === opt.value
                    ? 'border-violet-500 bg-violet-500/10 text-violet-400'
                    : 'border-[var(--border)] text-[var(--text-muted)] hover:border-violet-500/50'
                }`}>
                {opt.icon}
                {opt.label}
              </button>
            ))}
          </div>
        </div>

        {/* Destinatários */}
        <div className="rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-5 space-y-4">
          <h2 className="text-sm font-bold text-[var(--text-primary)] uppercase tracking-wide">Destinatários</h2>

          <div className="flex gap-2">
            {(['segment', 'specific'] as TargetMode[]).map(m => (
              <button key={m} type="button"
                onClick={() => setTargetMode(m)}
                className={`flex-1 flex items-center justify-center gap-2 py-2 rounded-xl border text-sm font-medium transition-all ${
                  targetMode === m
                    ? 'border-violet-500 bg-violet-500/10 text-violet-400'
                    : 'border-[var(--border)] text-[var(--text-muted)] hover:border-violet-500/50'
                }`}>
                {m === 'segment' ? <Users className="w-4 h-4" /> : <User className="w-4 h-4" />}
                {m === 'segment' ? 'Por segmento' : 'Selecionar clientes'}
              </button>
            ))}
          </div>

          {/* Segmento */}
          {targetMode === 'segment' && (
            <div className="grid grid-cols-1 gap-2">
              {segments.map(s => (
                <button key={s.id} type="button"
                  onClick={() => setSegment(s.id)}
                  className={`flex items-center gap-3 px-4 py-3 rounded-xl border text-sm font-medium transition-all text-left ${
                    segment === s.id
                      ? 'border-violet-500 bg-violet-500/10 text-violet-400'
                      : 'border-[var(--border)] text-[var(--text-muted)] hover:border-violet-500/50'
                  }`}>
                  <div className={`w-4 h-4 rounded-full border-2 flex-shrink-0 ${segment === s.id ? 'border-violet-500 bg-violet-500' : 'border-[var(--border)]'}`} />
                  {s.label}
                </button>
              ))}
            </div>
          )}

          {/* Seleção individual */}
          {targetMode === 'specific' && (
            <div className="space-y-2">
              <button type="button" onClick={() => setShowPicker(p => !p)}
                className="flex items-center gap-2 text-sm text-violet-400 hover:text-violet-300">
                {showPicker ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                {selected.size > 0 ? `${selected.size} cliente${selected.size !== 1 ? 's' : ''} selecionado${selected.size !== 1 ? 's' : ''}` : 'Selecionar clientes'}
              </button>

              {showPicker && (
                <div className="border border-[var(--border)] rounded-xl overflow-hidden">
                  <div className="p-2 border-b border-[var(--border)] relative">
                    <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-muted)]" />
                    <input
                      className="w-full pl-8 pr-3 py-1.5 rounded-lg bg-[var(--surface-600)] text-sm text-[var(--text-primary)] placeholder-[var(--text-muted)] focus:outline-none"
                      placeholder="Buscar por nome, e-mail ou WhatsApp…"
                      value={search} onChange={e => setSearch(e.target.value)}
                    />
                  </div>
                  <div className="max-h-52 overflow-y-auto divide-y divide-[var(--border)]">
                    {filtered.map(c => (
                      <label key={c.id}
                        className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-[var(--surface-600)] transition-colors">
                        <input type="checkbox"
                          checked={selected.has(c.id)}
                          onChange={() => toggleClient(c.id)}
                          className="accent-violet-500"
                        />
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-[var(--text-primary)] truncate">{c.name}</p>
                          <p className="text-xs text-[var(--text-muted)] truncate">{c.email ?? c.whatsApp ?? '—'}</p>
                        </div>
                        <span className="text-xs text-violet-400 flex-shrink-0">{c.pointsBalance} pts</span>
                      </label>
                    ))}
                    {filtered.length === 0 && (
                      <p className="text-center text-sm text-[var(--text-muted)] py-6">Nenhum cliente encontrado.</p>
                    )}
                  </div>
                  {selected.size > 0 && (
                    <div className="p-2 border-t border-[var(--border)] flex items-center justify-between">
                      <span className="text-xs text-[var(--text-muted)]">{selected.size} selecionado{selected.size !== 1 ? 's' : ''}</span>
                      <button type="button" onClick={() => setSelected(new Set())}
                        className="flex items-center gap-1 text-xs text-red-400 hover:text-red-300">
                        <X className="w-3 h-3" /> Limpar
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Enviar */}
        <button type="submit" disabled={loading}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl font-bold text-white transition-all disabled:opacity-50"
          style={{ background: 'linear-gradient(135deg, #7C3AED, #6D28D9)', boxShadow: '0 8px 24px rgba(124,58,237,0.35)' }}>
          <Send className="w-5 h-5" />
          {loading ? 'Enviando…' : 'Enviar mensagem'}
        </button>
      </form>
    </div>
  )
}
