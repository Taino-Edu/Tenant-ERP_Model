'use client'
// =============================================================================
// ImpostosTab.tsx — Comparativo Simples Nacional x Lucro Presumido sobre a
// mesma receita do período, com o histórico de receita que formou o RBT12.
//
// É estimativa de apuração, não guia recolhida — a tela repete isso em vez de
// esconder no rodapé, porque o número tem cara de definitivo.
// =============================================================================
import { Calculator, Scale, TrendingDown, Info, BarChart3 } from 'lucide-react'
import clsx from 'clsx'
import type { ApuracaoTributariaDto } from '@/lib/api'
import StatCard from '@/components/admin/StatCard'
import Badge from '@/components/admin/ui/Badge'
import Spinner from '@/components/admin/ui/Spinner'
import EmptyState from '@/components/admin/ui/EmptyState'
import { fmtReais, fmtPercent, isoParaBr, SecaoHeader, Aviso, PeriodoFields } from './contador-shared'

interface Props {
  apuracao: ApuracaoTributariaDto | null
  loading: boolean
  inicio: string
  fim: string
  onInicio: (v: string) => void
  onFim: (v: string) => void
}

export default function ImpostosTab({ apuracao, loading, inicio, fim, onInicio, onFim }: Props) {
  return (
    <div className="space-y-5">
      <div className="card space-y-4">
        <SecaoHeader
          icon={Scale}
          titulo="Comparativo de regimes"
          descricao={`Simples Nacional e Lucro Presumido sobre a receita de ${isoParaBr(inicio)} a ${isoParaBr(fim)}.`}
        />
        <div className="flex flex-wrap items-end gap-3">
          <PeriodoFields inicio={inicio} fim={fim} onInicio={onInicio} onFim={onFim} />
        </div>
      </div>

      {loading ? (
        <div className="card"><Spinner block size="lg" /></div>
      ) : !apuracao ? (
        <div className="card">
          <EmptyState icon={Calculator} message="Não foi possível apurar o período selecionado." compact />
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <StatCard icon={BarChart3} tone="brand" label="Receita do período"
                      value={fmtReais(apuracao.receitaBrutaPeriodo)} />
            <StatCard icon={Calculator} tone="neutral" label="RBT12 (12 meses anteriores)"
                      value={fmtReais(apuracao.rbt12)}
                      sub={apuracao.rbt12Parcial
                        ? `proporcionalizado de ${apuracao.mesesComReceita} mês(es)`
                        : `${apuracao.mesesComReceita} mês(es) com venda`} />
            <StatCard
              icon={TrendingDown}
              tone={apuracao.regimeMaisEconomico === apuracao.regimeAtual ? 'success' : 'warning'}
              label="Diferença entre os regimes"
              value={fmtReais(apuracao.economia)}
              sub={apuracao.regimeMaisEconomico === 'SimplesNacional'
                ? 'Simples sai mais barato no período'
                : 'Presumido sai mais barato no período'}
            />
          </div>

          {apuracao.regimeMaisEconomico !== apuracao.regimeAtual && (
            <Aviso tone="warning">
              <p>
                A loja está enquadrada como <strong>{rotuloRegime(apuracao.regimeAtual)}</strong>, mas neste
                período o <strong>{rotuloRegime(apuracao.regimeMaisEconomico)}</strong> sairia{' '}
                {fmtReais(apuracao.economia)} mais barato. Um único mês não decide enquadramento —
                compare alguns meses antes de considerar a mudança.
              </p>
            </Aviso>
          )}

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {/* ── Simples Nacional ────────────────────────────────────────── */}
            <section className="card space-y-3">
              <SecaoHeader
                icon={Calculator}
                titulo="Simples Nacional"
                acoes={<Badge tone="brand">Anexo {apuracao.simples.anexoAplicado} · faixa {apuracao.simples.faixa}</Badge>}
              />

              <div className="grid grid-cols-2 gap-2 text-xs">
                <Info2 label="Alíquota nominal" valor={fmtPercent(apuracao.simples.aliquotaNominal)} />
                <Info2 label="Parcela a deduzir" valor={fmtReais(apuracao.simples.parcelaDeduzir)} />
                <Info2 label="Alíquota efetiva" valor={fmtPercent(apuracao.simples.aliquotaEfetiva)} destaque />
                {apuracao.simples.fatorR != null && (
                  <Info2
                    label="Fator R"
                    valor={fmtPercent(apuracao.simples.fatorR)}
                    sub={apuracao.simples.fatorR >= 28 ? '≥ 28% → Anexo III' : '< 28% → Anexo V'}
                  />
                )}
              </div>

              {apuracao.simples.anexoAplicado !== apuracao.simples.anexoConfigurado && (
                <p className="text-xs text-amber-400">
                  Configurado como Anexo {apuracao.simples.anexoConfigurado}; o fator R reclassificou
                  para o Anexo {apuracao.simples.anexoAplicado}.
                </p>
              )}

              <TabelaTributos linhas={apuracao.simples.linhas} />

              <div className="flex justify-between items-baseline border-t-2 border-surface-500 pt-3">
                <strong className="text-white">DAS estimado</strong>
                <strong className="font-mono text-lg text-brand-300">{fmtReais(apuracao.simples.valorDas)}</strong>
              </div>
            </section>

            {/* ── Lucro Presumido ─────────────────────────────────────────── */}
            <section className="card space-y-3">
              <SecaoHeader
                icon={Calculator}
                titulo="Lucro Presumido"
                acoes={<Badge tone="neutral">{fmtPercent(apuracao.presumido.aliquotaEfetiva)} efetivo</Badge>}
              />

              <div className="grid grid-cols-2 gap-2 text-xs">
                <Info2 label="Base do IRPJ" valor={fmtReais(apuracao.presumido.baseIrpj)} />
                <Info2 label="Base da CSLL" valor={fmtReais(apuracao.presumido.baseCsll)} />
              </div>

              <TabelaTributos linhas={apuracao.presumido.linhas} />

              <div className="flex justify-between items-baseline border-t-2 border-surface-500 pt-3">
                <strong className="text-white">Total estimado</strong>
                <strong className="font-mono text-lg text-brand-300">{fmtReais(apuracao.presumido.total)}</strong>
              </div>
            </section>
          </div>

          <section className="card space-y-3">
            <SecaoHeader
              icon={BarChart3}
              titulo="Receita mês a mês"
              descricao="Base do RBT12 — os 12 meses anteriores à competência, mais o mês corrente."
            />
            <HistoricoReceita historico={apuracao.historicoReceita} />
          </section>

          {apuracao.alertas.length > 0 && (
            <Aviso tone="info">
              <p className="font-semibold text-gray-300 flex items-center gap-1.5">
                <Info className="w-3.5 h-3.5" /> Ressalvas desta apuração
              </p>
              <ul className="list-disc pl-4 space-y-1">
                {apuracao.alertas.map(a => <li key={a}>{a}</li>)}
              </ul>
            </Aviso>
          )}
        </>
      )}
    </div>
  )
}

const rotuloRegime = (regime: string) =>
  regime === 'SimplesNacional' ? 'Simples Nacional' : 'Lucro Presumido'

function Info2({ label, valor, sub, destaque }: { label: string; valor: string; sub?: string; destaque?: boolean }) {
  return (
    <div className={clsx('rounded-lg border p-2', destaque ? 'border-brand-500/30 bg-brand-500/5' : 'border-surface-600 bg-surface-800/40')}>
      <p className="text-gray-500">{label}</p>
      <p className={clsx('font-mono font-bold', destaque ? 'text-brand-300' : 'text-white')}>{valor}</p>
      {sub && <p className="text-[10px] text-gray-500 mt-0.5">{sub}</p>}
    </div>
  )
}

function TabelaTributos({ linhas }: { linhas: ApuracaoTributariaDto['simples']['linhas'] }) {
  return (
    <div className="table-scroll">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-gray-500 border-b border-surface-600">
            <th className="py-2 font-medium">Tributo</th>
            <th className="py-2 font-medium text-right">Alíquota</th>
            <th className="py-2 font-medium text-right">Valor</th>
          </tr>
        </thead>
        <tbody>
          {linhas.map(linha => (
            <tr key={linha.tributo} className="border-b border-surface-700 last:border-0 align-top">
              <td className="py-2">
                <p className="text-white">{linha.tributo}</p>
                {linha.observacao && <p className="text-[11px] text-gray-500">{linha.observacao}</p>}
              </td>
              <td className="py-2 text-right font-mono text-gray-400">{fmtPercent(linha.aliquota)}</td>
              <td className="py-2 text-right font-mono text-white">{fmtReais(linha.valor)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function HistoricoReceita({ historico }: { historico: ApuracaoTributariaDto['historicoReceita'] }) {
  const maior = Math.max(...historico.map(h => h.receitaBruta), 1)
  return (
    <div className="space-y-1.5">
      {historico.map((mes, indice) => (
        <div key={mes.competencia} className="flex items-center gap-3 text-xs">
          <span className="w-16 shrink-0 text-gray-500 font-mono">{mes.competencia}</span>
          <div className="flex-1 h-4 rounded bg-surface-800 overflow-hidden">
            <div
              // O último item é o mês corrente (fora do RBT12) — tom mais fraco
              // pra não parecer que entrou na base da alíquota.
              className={clsx('h-full rounded', indice === historico.length - 1 ? 'bg-brand-500/40' : 'bg-brand-500/80')}
              style={{ width: `${Math.max(2, (mes.receitaBruta / maior) * 100)}%` }}
            />
          </div>
          <span className="w-28 shrink-0 text-right font-mono text-white">{fmtReais(mes.receitaBruta)}</span>
        </div>
      ))}
      <p className="text-[11px] text-gray-500 pt-1">
        A última barra é a competência apurada; as 12 anteriores formam o RBT12.
      </p>
    </div>
  )
}
