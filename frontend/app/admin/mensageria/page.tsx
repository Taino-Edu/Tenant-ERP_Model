'use client'
import { useEffect, useState } from 'react'
import { mensageriaApi, MensageriaClient, MensageriaSegment } from '@/lib/api'
import toast from 'react-hot-toast'
import {
  Send, Users, User, Mail, Bell, BellRing,
  Search, X, CheckCircle, ChevronDown, ChevronUp,
} from 'lucide-react'
import clsx from 'clsx'

type Channel    = 'inapp' | 'email' | 'both'
type TargetMode = 'segment' | 'specific'

export default function MensageriaPage() {
  const [title,      setTitle]      = useState('')
  const [body,       setBody]       = useState('')
  const [link,       setLink]       = useState('')
  const [channel,    setChannel]    = useState<Channel>('inapp')
  const [targetMode, setTargetMode] = useState<TargetMode>('segment')
  const [segment,    setSegment]    = useState('all')
  const [segments,   setSegments]   = useState<MensageriaSegment[]>([])
  const [clients,    setClients]    = useState<MensageriaClient[]>([])
  const [selected,   setSelected]   = useState<Set<string>>(new Set())
  const [search,     setSearch]     = useState('')
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
    setSelected(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n })
  }

  async function handleSend(e: React.FormEvent) {
    e.preventDefault()
    if (!title.trim() || !body.trim()) { toast.error('Preencha título e mensagem.'); return }
    if (targetMode === 'specific' && selected.size === 0) { toast.error('Selecione ao menos um cliente.'); return }
    setLoading(true); setResult(null)
    try {
      const r = await mensageriaApi.send({
        title, body, link: link || undefined, channel,
        segment: targetMode === 'segment' ? segment : undefined,
        userIds: targetMode === 'specific' ? [...selected] : undefined,
      })
      setResult(r.data)
      setTitle(''); setBody(''); setLink(''); setSelected(new Set())
      toast.success('Mensagem enviada!')
    } catch { toast.error('Erro ao enviar mensagem.') }
    finally  { setLoading(false) }
  }

  const channelOpts: { value: Channel; label: string; icon: React.ReactNode }[] = [
    { value: 'inapp', label: 'Notificação no site', icon: <Bell    className="w-4 h-4" /> },
    { value: 'email', label: 'Somente e-mail',       icon: <Mail    className="w-4 h-4" /> },
    { value: 'both',  label: 'Site + e-mail',        icon: <BellRing className="w-4 h-4" /> },
  ]

  return (
    <div className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold text-white">Mensageria</h1>
        <p className="text-sm text-gray-400 mt-1">Envie notificações in-app e/ou e-mail para clientes.</p>
      </div>

      {/* Resultado */}
      {result && (
        <div className="card flex items-center gap-3 border-emerald-500/30 bg-emerald-500/10">
          <CheckCircle className="w-5 h-5 text-emerald-400 flex-shrink-0" />
          <div className="text-sm">
            <p className="font-bold text-emerald-400">Enviado com sucesso!</p>
            <p className="text-gray-400">
              {result.total} cliente{result.total !== 1 ? 's' : ''}
              {result.inApp  > 0 && ` — ${result.inApp} notificações in-app`}
              {result.emails > 0 && ` — ${result.emails} e-mails`}
            </p>
          </div>
        </div>
      )}

      <form onSubmit={handleSend} className="space-y-4">

        {/* Conteúdo */}
        <div className="card space-y-4">
          <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wide">Mensagem</h2>

          <div>
            <label className="label">Título</label>
            <input className="input" placeholder="Ex: Promoção de fim de semana"
              value={title} onChange={e => setTitle(e.target.value)} maxLength={120} required />
          </div>

          <div>
            <label className="label">Texto da notificação <span className="text-gray-500 font-normal">({body.length}/500)</span></label>
            <textarea className="input min-h-[80px] resize-y" placeholder="Texto que o cliente vai ler…"
              value={body} onChange={e => setBody(e.target.value)} maxLength={500} required />
          </div>

          <div>
            <label className="label">Link (opcional) — botão "Ver" na notificação</label>
            <input className="input" placeholder="/produtos ou https://..."
              value={link} onChange={e => setLink(e.target.value)} />
          </div>
        </div>

        {/* Canal */}
        <div className="card space-y-3">
          <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wide">Canal de envio</h2>
          <div className="grid grid-cols-3 gap-2">
            {channelOpts.map(opt => (
              <button key={opt.value} type="button" onClick={() => setChannel(opt.value)}
                className={clsx(
                  'flex flex-col items-center gap-2 p-3 rounded-xl border text-xs font-medium transition-all',
                  channel === opt.value
                    ? 'border-brand-500 bg-brand-500/10 text-brand-400'
                    : 'border-surface-500 text-gray-400 hover:border-brand-500/50 hover:text-gray-300'
                )}>
                {opt.icon}
                {opt.label}
              </button>
            ))}
          </div>
        </div>

        {/* Destinatários */}
        <div className="card space-y-4">
          <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wide">Destinatários</h2>

          <div className="flex gap-2">
            {(['segment', 'specific'] as TargetMode[]).map(m => (
              <button key={m} type="button" onClick={() => setTargetMode(m)}
                className={clsx(
                  'flex-1 flex items-center justify-center gap-2 py-2 rounded-xl border text-sm font-medium transition-all',
                  targetMode === m
                    ? 'border-brand-500 bg-brand-500/10 text-brand-400'
                    : 'border-surface-500 text-gray-400 hover:border-brand-500/50'
                )}>
                {m === 'segment' ? <Users className="w-4 h-4" /> : <User className="w-4 h-4" />}
                {m === 'segment' ? 'Por segmento' : 'Selecionar clientes'}
              </button>
            ))}
          </div>

          {targetMode === 'segment' && (
            <div className="space-y-2">
              {segments.map(s => (
                <label key={s.id}
                  className={clsx(
                    'flex items-center gap-3 px-4 py-3 rounded-xl border cursor-pointer transition-all',
                    segment === s.id
                      ? 'border-brand-500 bg-brand-500/10'
                      : 'border-surface-500 hover:border-surface-400'
                  )}>
                  <input type="radio" name="segment" value={s.id} checked={segment === s.id}
                    onChange={() => setSegment(s.id)} className="accent-brand-500" />
                  <span className={clsx('text-sm font-medium', segment === s.id ? 'text-brand-400' : 'text-gray-300')}>
                    {s.label}
                  </span>
                </label>
              ))}
            </div>
          )}

          {targetMode === 'specific' && (
            <div className="space-y-2">
              <button type="button" onClick={() => setShowPicker(p => !p)}
                className="flex items-center gap-2 text-sm text-brand-400 hover:text-brand-300 transition-colors">
                {showPicker ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                {selected.size > 0
                  ? `${selected.size} cliente${selected.size !== 1 ? 's' : ''} selecionado${selected.size !== 1 ? 's' : ''}`
                  : 'Selecionar clientes'}
              </button>

              {showPicker && (
                <div className="border border-surface-500 rounded-xl overflow-hidden">
                  <div className="p-2 border-b border-surface-500 relative">
                    <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                    <input className="input pl-8 py-1.5 text-sm"
                      placeholder="Buscar por nome, e-mail ou WhatsApp…"
                      value={search} onChange={e => setSearch(e.target.value)} />
                  </div>
                  <div className="max-h-52 overflow-y-auto divide-y divide-surface-600">
                    {filtered.length === 0
                      ? <p className="text-center text-sm text-gray-500 py-6">Nenhum cliente encontrado.</p>
                      : filtered.map(c => (
                          <label key={c.id}
                            className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-surface-700 transition-colors">
                            <input type="checkbox" checked={selected.has(c.id)}
                              onChange={() => toggleClient(c.id)} className="accent-brand-500" />
                            <div className="flex-1 min-w-0">
                              <p className="text-sm font-medium text-white truncate">{c.name}</p>
                              <p className="text-xs text-gray-500 truncate">{c.email ?? c.whatsApp ?? '—'}</p>
                            </div>
                            <span className="text-xs text-brand-400 flex-shrink-0">{c.pointsBalance} pts</span>
                          </label>
                        ))
                    }
                  </div>
                  {selected.size > 0 && (
                    <div className="p-2 border-t border-surface-500 flex items-center justify-between">
                      <span className="text-xs text-gray-500">{selected.size} selecionado{selected.size !== 1 ? 's' : ''}</span>
                      <button type="button" onClick={() => setSelected(new Set())}
                        className="flex items-center gap-1 text-xs text-red-400 hover:text-red-300 transition-colors">
                        <X className="w-3 h-3" /> Limpar seleção
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5">
          {loading
            ? <><span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" /> Enviando…</>
            : <><Send className="w-4 h-4" /> Enviar mensagem</>
          }
        </button>
      </form>
    </div>
  )
}
