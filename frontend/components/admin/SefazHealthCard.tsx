'use client'
import { useCallback, useEffect, useState } from 'react'
import { Activity, Loader2 } from 'lucide-react'
import clsx from 'clsx'
import toast from 'react-hot-toast'
import { api, getErrorMessage } from '@/lib/api'

export type SefazHealth = {
  configured: boolean
  online: boolean
  cStat?: number
  message: string
  checkedAt: string
  latencyMs: number
  ambiente?: string
  uf?: string
  sefazReceivedAt?: string
}

/**
 * Indicador de saúde do serviço de autorização da SEFAZ, com medição sob
 * demanda.
 *
 * Existia só em Admin → Integrações, onde está o cadastro do certificado. Mas
 * quem está emitindo NFC-e abre o painel FISCAL, e ali não havia nenhum sinal
 * de que a SEFAZ da UF está no ar — a pessoa descobria pela nota rejeitada.
 * Daí o componente: um lugar só, usado nas duas telas.
 *
 * O backend distingue três estados, e a UI precisa dos três: não configurado
 * (sem certificado — nada a medir), online (cStat 107, "Serviço em Operação") e
 * indisponível. Colapsar os dois últimos em "erro" faria certificado ausente
 * parecer SEFAZ fora do ar.
 *
 * `GET /sefaz/health` tem cache curto no servidor; o botão chama
 * `POST /sefaz/test`, que força nova medição. Nenhum dos dois toca em NSU,
 * cooldown ou quota de notas destinadas.
 */
export default function SefazHealthCard({ className }: { className?: string }) {
  const [health, setHealth] = useState<SefazHealth | null>(null)
  const [testing, setTesting] = useState(false)

  useEffect(() => {
    api.get<SefazHealth>('/api/contas-receber/sefaz/health')
      .then(r => setHealth(r.data))
      // Silencioso de propósito: sem certificado o endpoint recusa, e isso não
      // é um erro que mereça toast ao abrir a tela.
      .catch(() => {})
  }, [])

  const testar = useCallback(async () => {
    setTesting(true)
    try {
      const { data } = await api.post<SefazHealth>('/api/contas-receber/sefaz/test')
      setHealth(data)
      if (data.online) toast.success(`SEFAZ ${data.uf ?? ''} online — resposta em ${data.latencyMs} ms.`)
      else toast.error(data.message || 'A SEFAZ não está respondendo como operacional.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível testar a SEFAZ.'))
    } finally {
      setTesting(false)
    }
  }, [])

  const naoConfigurado = health !== null && !health.configured

  return (
    <div className={clsx('flex flex-wrap items-start justify-between gap-3', className)}>
      <div className={clsx(
        'flex items-start gap-2 rounded-lg border p-2 text-xs',
        !health && 'border-surface-600 bg-surface-700/40 text-gray-400',
        health?.online && 'border-emerald-500/20 bg-emerald-500/10 text-emerald-300',
        health && !health.online && !naoConfigurado && 'border-red-500/20 bg-red-500/10 text-red-300',
        naoConfigurado && 'border-amber-500/20 bg-amber-500/10 text-amber-300',
      )}>
        <span className={clsx(
          'mt-0.5 h-2.5 w-2.5 shrink-0 rounded-full',
          !health && 'bg-gray-500',
          health?.online && 'bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.7)]',
          health && !health.online && !naoConfigurado && 'bg-red-400',
          naoConfigurado && 'bg-amber-400',
        )} />
        <div className="min-w-0">
          <p className="font-semibold">
            {!health          ? 'Saúde da SEFAZ ainda não verificada'
            : naoConfigurado  ? 'Certificado não configurado'
            : health.online   ? `SEFAZ ${health.uf ?? ''} online`
                              : `SEFAZ ${health.uf ?? ''} indisponível`}
          </p>
          {health && (
            <p className="mt-0.5 opacity-80">
              {health.message}
              {health.cStat ? ` · cStat ${health.cStat}` : ''}
              {health.configured ? ` · ${health.latencyMs} ms` : ''}
              {health.ambiente ? ` · ${health.ambiente}` : ''}
            </p>
          )}
        </div>
      </div>

      <button
        onClick={testar}
        disabled={testing}
        className="btn-secondary shrink-0 text-sm"
      >
        {testing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Activity className="h-3.5 w-3.5" />}
        {testing ? 'Testando…' : 'Testar SEFAZ'}
      </button>
    </div>
  )
}
