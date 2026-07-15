'use client'

import { useEffect, useState } from 'react'
import { emailConfigApi, EmailConfigDto, getErrorMessage } from '@/lib/api'
import toast, { Toaster } from 'react-hot-toast'
import clsx from 'clsx'
import { Mail, Save, Loader2, Info, ExternalLink } from 'lucide-react'

const DEFAULT: EmailConfigDto = {
  smtpHost: '', smtpPort: 587, smtpUsername: '', fromName: '', isActive: false, hasPassword: false,
}

export default function EmailConfigPage() {
  const [cfg, setCfg]         = useState<EmailConfigDto>(DEFAULT)
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving]   = useState(false)

  useEffect(() => {
    emailConfigApi.get()
      .then(({ data }) => setCfg({ ...DEFAULT, ...data }))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar configuração de e-mail')))
      .finally(() => setLoading(false))
  }, [])

  async function save() {
    setSaving(true)
    try {
      const { data } = await emailConfigApi.save({
        smtpHost:     cfg.smtpHost || undefined,
        smtpPort:     cfg.smtpPort ?? undefined,
        smtpUsername: cfg.smtpUsername || undefined,
        smtpPassword: password || undefined,
        fromName:     cfg.fromName || undefined,
        isActive:     cfg.isActive,
      })
      setCfg({ ...DEFAULT, ...data })
      setPassword('')
      toast.success('Configuração de e-mail salva!')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar configuração de e-mail'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <div className="flex justify-center py-16"><Loader2 className="w-8 h-8 animate-spin text-brand-400" /></div>
  }

  return (
    <div className="p-4 md:p-6 max-w-2xl mx-auto">
      <Toaster />

      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-xl bg-brand-500/10">
          <Mail className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">E-mail da Loja</h1>
          <p className="text-sm text-gray-400">Use seu próprio Gmail para enviar e-mails, em vez da conta compartilhada da plataforma</p>
        </div>
      </div>

      <div className="card p-5 flex flex-col gap-4">
        <div className="flex items-center justify-between p-3 rounded-xl bg-surface-800/50 border border-surface-700/50">
          <div>
            <p className="text-sm font-semibold text-white">Usar meu próprio e-mail</p>
            <p className="text-xs text-gray-500">Desligado: continua usando a conta padrão da plataforma, sem perder o que já salvou aqui.</p>
          </div>
          <button
            role="switch"
            aria-checked={cfg.isActive}
            onClick={() => setCfg(c => ({ ...c, isActive: !c.isActive }))}
            className={clsx(
              'relative w-11 h-6 rounded-full transition-colors shrink-0 ml-3',
              cfg.isActive ? 'bg-brand-500' : 'bg-surface-600',
            )}
          >
            <span className={clsx(
              'absolute top-0.5 w-5 h-5 rounded-full bg-white transition-transform',
              cfg.isActive ? 'translate-x-[22px]' : 'translate-x-0.5',
            )} />
          </button>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Servidor SMTP</label>
            <input
              value={cfg.smtpHost ?? ''}
              onChange={e => setCfg(c => ({ ...c, smtpHost: e.target.value }))}
              placeholder="smtp.gmail.com" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Porta</label>
            <input
              type="number"
              value={cfg.smtpPort ?? 587}
              onChange={e => setCfg(c => ({ ...c, smtpPort: Number(e.target.value) || 587 }))}
              placeholder="587" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Nome do remetente</label>
            <input
              value={cfg.fromName ?? ''}
              onChange={e => setCfg(c => ({ ...c, fromName: e.target.value }))}
              placeholder="Nome da loja" className="input w-full" />
          </div>
          <div className="col-span-2">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Seu Gmail</label>
            <input
              value={cfg.smtpUsername ?? ''}
              onChange={e => setCfg(c => ({ ...c, smtpUsername: e.target.value }))}
              placeholder="voce@gmail.com" className="input w-full" />
          </div>
          <div className="col-span-2">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Senha de app</label>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder={cfg.hasPassword ? '•••• configurada — deixe em branco pra manter' : 'Senha de app do Google'}
              className="input w-full" />
          </div>
        </div>

        <div className="flex items-start gap-3 p-4 bg-surface-800/50 rounded-2xl border border-surface-700/50 text-sm text-gray-400">
          <Info className="w-4 h-4 text-brand-400 flex-shrink-0 mt-0.5" />
          <div>
            <p>
              O Gmail não aceita a senha normal da conta pra isso — é preciso gerar uma
              {' '}<strong>Senha de app</strong> (exige verificação em duas etapas ativada).
            </p>
            <a href="https://support.google.com/accounts/answer/185833" target="_blank" rel="noopener noreferrer"
              className="flex items-center gap-1 text-brand-400 hover:text-brand-300 mt-1">
              Como gerar uma senha de app <ExternalLink className="w-3 h-3" />
            </a>
            <p className="mt-2">O plano grátis do Gmail permite até ~500 e-mails por dia.</p>
          </div>
        </div>

        <button onClick={save} disabled={saving}
          className="py-3 rounded-xl bg-brand-500 hover:bg-brand-400 disabled:opacity-50
                     text-white text-sm font-bold transition-colors flex items-center justify-center gap-2">
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          Salvar
        </button>
      </div>
    </div>
  )
}
