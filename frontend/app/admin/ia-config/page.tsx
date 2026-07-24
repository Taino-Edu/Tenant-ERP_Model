'use client'

import { useEffect, useState } from 'react'
import { aiConfigApi, AiConfigDto, getErrorMessage } from '@/lib/api'
import toast, { Toaster } from 'react-hot-toast'
import clsx from 'clsx'
import { Sparkles, Save, Loader2, Info, ExternalLink } from 'lucide-react'

const DEFAULT: AiConfigDto = { isActive: false, hasKey: false }

export default function AiConfigPage() {
  const [cfg, setCfg]         = useState<AiConfigDto>(DEFAULT)
  const [apiKey, setApiKey]   = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving]   = useState(false)

  useEffect(() => {
    aiConfigApi.get()
      .then(({ data }) => setCfg({ ...DEFAULT, ...data }))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar configuração do assistente de IA')))
      .finally(() => setLoading(false))
  }, [])

  async function save() {
    setSaving(true)
    try {
      const { data } = await aiConfigApi.save({
        geminiApiKey: apiKey || undefined,
        isActive:     cfg.isActive,
      })
      setCfg({ ...DEFAULT, ...data })
      setApiKey('')
      toast.success('Configuração do assistente de IA salva!')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar configuração do assistente de IA'))
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
          <Sparkles className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">Assistente de IA</h1>
          <p className="text-sm text-gray-400">Use sua própria chave do Gemini, em vez da conta compartilhada da plataforma</p>
        </div>
      </div>

      <div className="card p-5 flex flex-col gap-4">
        <div className="flex items-center justify-between p-3 rounded-xl bg-surface-800/50 border border-surface-700/50">
          <div>
            <p className="text-sm font-semibold text-white">Usar minha própria chave</p>
            <p className="text-xs text-gray-500">Desligado: continua usando a chave padrão da plataforma, sem perder o que já salvou aqui.</p>
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

        <div>
          <label className="text-xs text-gray-400 font-semibold mb-1 block">Chave do Gemini</label>
          <input
            type="password"
            value={apiKey}
            onChange={e => setApiKey(e.target.value)}
            placeholder={cfg.hasKey ? '•••• configurada — deixe em branco pra manter' : 'Chave do Google AI Studio'}
            className="input w-full" />
        </div>

        <div className="flex items-start gap-3 p-4 bg-surface-800/50 rounded-2xl border border-surface-700/50 text-sm text-gray-400">
          <Info className="w-4 h-4 text-brand-400 flex-shrink-0 mt-0.5" />
          <div>
            <p>Gere uma chave gratuita no Google AI Studio pra usar sua própria cota, sem depender do limite compartilhado da plataforma.</p>
            <a href="https://aistudio.google.com/apikey" target="_blank" rel="noopener noreferrer"
              className="flex items-center gap-1 text-brand-400 hover:text-brand-300 mt-1">
              Gerar chave no Google AI Studio <ExternalLink className="w-3 h-3" />
            </a>
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
