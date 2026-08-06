'use client'
// =============================================================================
// AlertasFiscaisCard.tsx — Painel de pendências fiscais (CON-002).
//
// A lista vem reconciliada do backend: o que está aberto aqui é uma pendência
// que existe agora, e some sozinha quando a condição some. Por isso o card não
// tem "marcar como lido" — tem "resolver com observação", que é confirmação
// auditável e não silencia o fato: se o problema continuar, o alerta volta.
// =============================================================================

import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, Check, Loader2, RefreshCw, ShieldAlert, UserCheck, UserMinus } from 'lucide-react'
import clsx from 'clsx'
import toast from 'react-hot-toast'
import { fiscalApi, getErrorMessage, type AlertaFiscalDto, type PainelAlertasFiscaisDto } from '@/lib/api'

const SEVERIDADE_INFO: Record<string, { label: string; badge: string; barra: string }> = {
  Critica: {
    label: 'Crítico',
    badge: 'bg-red-500/15 text-red-400 border-red-500/30',
    barra: 'bg-red-500',
  },
  Alta: {
    label: 'Alto',
    badge: 'bg-orange-500/15 text-orange-400 border-orange-500/30',
    barra: 'bg-orange-500',
  },
  Media: {
    label: 'Médio',
    badge: 'bg-amber-500/15 text-amber-300 border-amber-500/30',
    barra: 'bg-amber-400',
  },
}

const TIPO_LABEL: Record<string, string> = {
  ResultadoIncerto:         'Resultado incerto',
  ContingenciaPendente:     'Contingência',
  NotaRejeitada:            'Rejeição',
  VendaSemDocumento:        'Venda sem nota',
  LacunaNumeracao:          'Numeração',
  ExportacaoMensalPendente: 'Exportação',
}

/** Idade do FATO, não da detecção — é o que o lojista precisa ver. */
function idadeLegivel(horas: number) {
  if (horas < 1) return 'agora há pouco'
  if (horas < 24) return `há ${horas}h`
  const dias = Math.floor(horas / 24)
  return dias === 1 ? 'há 1 dia' : `há ${dias} dias`
}

export default function AlertasFiscaisCard() {
  const [painel, setPainel] = useState<PainelAlertasFiscaisDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [sincronizando, setSincronizando] = useState(false)
  const [incluirResolvidos, setIncluirResolvidos] = useState(false)
  const [acaoId, setAcaoId] = useState<string | null>(null)
  const [resolvendoId, setResolvendoId] = useState<string | null>(null)
  const [observacao, setObservacao] = useState('')

  const carregar = useCallback(async (comResolvidos: boolean) => {
    setLoading(true)
    try {
      const { data } = await fiscalApi.listAlertas(comResolvidos)
      setPainel(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível carregar as pendências fiscais.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { carregar(incluirResolvidos) }, [carregar, incluirResolvidos])

  async function sincronizar() {
    setSincronizando(true)
    try {
      const { data } = await fiscalApi.sincronizarAlertas()
      if (!incluirResolvidos) setPainel(data)
      else await carregar(true)
      toast.success('Pendências recalculadas.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível recalcular as pendências.'))
    } finally {
      setSincronizando(false)
    }
  }

  async function assumir(alerta: AlertaFiscalDto) {
    setAcaoId(alerta.id)
    try {
      if (alerta.responsavelUserId) await fiscalApi.liberarAlerta(alerta.id)
      else await fiscalApi.assumirAlerta(alerta.id)
      await carregar(incluirResolvidos)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível alterar o responsável.'))
    } finally {
      setAcaoId(null)
    }
  }

  async function resolver(alerta: AlertaFiscalDto) {
    if (observacao.trim().length < 5) {
      toast.error('Descreva em poucas palavras o que foi feito.')
      return
    }
    setAcaoId(alerta.id)
    try {
      await fiscalApi.resolverAlerta(alerta.id, observacao.trim())
      setResolvendoId(null)
      setObservacao('')
      await carregar(incluirResolvidos)
      toast.success('Pendência marcada como resolvida.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível registrar a resolução.'))
    } finally {
      setAcaoId(null)
    }
  }

  const alertas = painel?.alertas ?? []

  return (
    <div id="secao-alertas-fiscais" className="card p-5">
      <div className="flex items-center justify-between mb-3 gap-3 flex-wrap">
        <h3 className="font-bold text-white flex items-center gap-2">
          <ShieldAlert className="w-4 h-4 text-brand-400" /> Pendências Fiscais
        </h3>
        <div className="flex items-center gap-2">
          <label className="flex items-center gap-1.5 text-xs text-gray-400 cursor-pointer">
            <input
              type="checkbox" className="accent-brand-500"
              checked={incluirResolvidos}
              onChange={e => setIncluirResolvidos(e.target.checked)}
            />
            Mostrar resolvidas
          </label>
          <button
            onClick={sincronizar} disabled={sincronizando}
            title="Recalcular agora, sem esperar o ciclo automático"
            className="p-2 rounded-lg bg-surface-700 hover:bg-surface-500 text-gray-400"
          >
            <RefreshCw className={clsx('w-4 h-4', (sincronizando || loading) && 'animate-spin')} />
          </button>
        </div>
      </div>

      {painel && painel.totalAbertos > 0 && (
        <div className="flex items-center gap-3 flex-wrap text-xs mb-3">
          {painel.criticos > 0 && (
            <span className="px-2 py-1 rounded-lg border bg-red-500/15 text-red-400 border-red-500/30 font-bold">
              {painel.criticos} crítica(s)
            </span>
          )}
          {painel.altos > 0 && (
            <span className="px-2 py-1 rounded-lg border bg-orange-500/15 text-orange-400 border-orange-500/30 font-bold">
              {painel.altos} alta(s)
            </span>
          )}
          {painel.medios > 0 && (
            <span className="px-2 py-1 rounded-lg border bg-amber-500/15 text-amber-300 border-amber-500/30 font-bold">
              {painel.medios} média(s)
            </span>
          )}
          {painel.semResponsavel > 0 && (
            <span className="text-gray-500">{painel.semResponsavel} sem responsável</span>
          )}
        </div>
      )}

      {loading ? (
        <div className="flex justify-center py-8"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
      ) : alertas.length === 0 ? (
        <p className="text-sm text-gray-500 text-center py-4 flex items-center justify-center gap-2">
          <Check className="w-4 h-4 text-green-400" />
          Nenhuma pendência fiscal aberta.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {alertas.map(a => {
            const sev = SEVERIDADE_INFO[a.severidade] ?? SEVERIDADE_INFO.Media
            const resolvido = !a.estaAberto
            return (
              <div
                key={a.id}
                className={clsx(
                  'flex gap-3 rounded-xl p-3 border',
                  resolvido
                    ? 'bg-surface-800/30 border-surface-700/40 opacity-70'
                    : 'bg-surface-800/50 border-surface-700/50',
                )}
              >
                <div className={clsx('w-1 rounded-full shrink-0', resolvido ? 'bg-surface-600' : sev.barra)} />

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className={clsx(
                      'text-[10px] font-bold px-2 py-0.5 rounded-full border uppercase',
                      resolvido ? 'bg-surface-700 text-gray-400 border-surface-600' : sev.badge,
                    )}>
                      {resolvido ? 'Resolvida' : sev.label}
                    </span>
                    <span className="text-[10px] uppercase tracking-wide text-gray-500 font-bold">
                      {TIPO_LABEL[a.tipo] ?? a.tipo}
                    </span>
                    <span className="text-sm text-white font-semibold">{a.titulo}</span>
                    <span className="text-xs text-gray-500">{idadeLegivel(a.idadeEmHoras)}</span>
                  </div>

                  <p className="text-xs text-gray-400 mt-1">{a.detalhe}</p>

                  <div className="flex items-center gap-3 flex-wrap mt-1.5 text-[11px] text-gray-500">
                    {a.responsavelNome && <span>Responsável: <strong className="text-gray-300">{a.responsavelNome}</strong></span>}
                    {a.reaberturas > 0 && (
                      <span className="text-amber-400 flex items-center gap-1">
                        <AlertTriangle className="w-3 h-3" />
                        reaberta {a.reaberturas}× — o problema voltou a ser detectado
                      </span>
                    )}
                    {resolvido && (
                      <span>
                        {a.resolvidoAutomaticamente
                          ? 'Resolvida automaticamente: a condição deixou de existir.'
                          : `Resolvida por ${a.resolvidoPorNome ?? 'usuário'}: ${a.resolucaoObservacao}`}
                      </span>
                    )}
                  </div>

                  {resolvendoId === a.id && (
                    <div className="flex gap-2 mt-2">
                      <input
                        className="input flex-1 text-sm"
                        autoFocus
                        placeholder="O que foi feito? (fica na trilha de auditoria)"
                        value={observacao}
                        onChange={e => setObservacao(e.target.value)}
                        onKeyDown={e => { if (e.key === 'Enter') resolver(a) }}
                      />
                      <button
                        onClick={() => resolver(a)} disabled={acaoId === a.id}
                        className="btn-primary px-3 text-sm"
                      >
                        {acaoId === a.id ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Confirmar'}
                      </button>
                      <button
                        onClick={() => { setResolvendoId(null); setObservacao('') }}
                        className="px-3 rounded-lg bg-surface-700 hover:bg-surface-500 text-sm text-gray-300"
                      >
                        Cancelar
                      </button>
                    </div>
                  )}
                </div>

                {!resolvido && resolvendoId !== a.id && (
                  <div className="flex items-start gap-2 shrink-0">
                    <button
                      onClick={() => assumir(a)} disabled={acaoId === a.id}
                      title={a.responsavelUserId ? 'Devolver para a fila' : 'Assumir esta pendência'}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-surface-700 hover:bg-surface-500 border border-surface-600 text-sm text-gray-300"
                    >
                      {acaoId === a.id
                        ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        : a.responsavelUserId
                          ? <UserMinus className="w-3.5 h-3.5" />
                          : <UserCheck className="w-3.5 h-3.5" />}
                      {a.responsavelUserId ? 'Liberar' : 'Assumir'}
                    </button>
                    <button
                      onClick={() => { setResolvendoId(a.id); setObservacao('') }}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-green-500/10 hover:bg-green-500/20 border border-green-500/30 text-sm text-green-400"
                    >
                      <Check className="w-3.5 h-3.5" /> Resolver
                    </button>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
