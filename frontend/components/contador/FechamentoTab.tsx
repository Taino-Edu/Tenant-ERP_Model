'use client'
// =============================================================================
// FechamentoTab.tsx — Fechamento da competência: escolhe o mês, revê o que o
// sistema apurou, resolve pendências, baixa o pacote (XMLs + relatórios) e
// trava o mês. Fechar grava um snapshot imutável — reabrir é explícito.
// =============================================================================
import { useCallback, useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import {
  CalendarCheck, Download, Lock, Unlock, Printer, AlertTriangle, CheckCircle2, History,
} from 'lucide-react'
import clsx from 'clsx'
import {
  contadorApi, getErrorMessage,
  type ApuracaoTributariaDto, type FechamentoMensalDto, type FinanceiroDto,
  type ContadorNotaDto, type ContadorNotaRecebidaDto, type ContadorProdutoDto,
} from '@/lib/api'
import StatCard from '@/components/admin/StatCard'
import Badge from '@/components/admin/ui/Badge'
import Button from '@/components/admin/ui/Button'
import EmptyState from '@/components/admin/ui/EmptyState'
import Modal from '@/components/admin/ui/Modal'
import Spinner from '@/components/admin/ui/Spinner'
import {
  fmtCentavos, fmtReais, fmtPercent, MESES, LinhaValor, SecaoHeader, Aviso, baixarBlob,
} from './contador-shared'

interface Props {
  tenantId: string
  slug: string
  ano: number
  mes: number
  onCompetencia: (ano: number, mes: number) => void
  /** Dados do período já carregados pelo workspace (a competência escolhida). */
  dre: FinanceiroDto | null
  notas: ContadorNotaDto[]
  notasRecebidas: ContadorNotaRecebidaDto[]
  produtos: ContadorProdutoDto[]
  apuracao: ApuracaoTributariaDto | null
  loading: boolean
}

/** Últimas 24 competências, da mais recente pra trás. */
function competenciasDisponiveis(): Array<{ ano: number; mes: number; label: string }> {
  const hoje = new Date()
  const lista = []
  for (let i = 0; i < 24; i++) {
    const d = new Date(hoje.getFullYear(), hoje.getMonth() - i, 1)
    lista.push({ ano: d.getFullYear(), mes: d.getMonth() + 1, label: `${MESES[d.getMonth()]} de ${d.getFullYear()}` })
  }
  return lista
}

export default function FechamentoTab({
  tenantId, slug, ano, mes, onCompetencia,
  dre, notas, notasRecebidas, produtos, apuracao, loading,
}: Props) {
  const [fechamentos, setFechamentos] = useState<FechamentoMensalDto[]>([])
  const [carregandoFechamentos, setCarregandoFechamentos] = useState(true)
  const [baixando, setBaixando] = useState(false)
  const [confirmando, setConfirmando] = useState(false)
  const [observacao, setObservacao] = useState('')
  const [salvando, setSalvando] = useState(false)

  const carregar = useCallback(() => {
    setCarregandoFechamentos(true)
    contadorApi.listFechamentos(tenantId)
      .then(r => setFechamentos(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar os fechamentos')))
      .finally(() => setCarregandoFechamentos(false))
  }, [tenantId])

  useEffect(() => { carregar() }, [carregar])

  const fechamentoAtual = fechamentos.find(f => f.ano === ano && f.mes === mes)

  const autorizadas    = notas.filter(n => n.status === 'Autorizada' || n.status === 'AutorizadaContingencia')
  const canceladas     = notas.filter(n => n.status === 'Cancelada')
  const semNcm         = produtos.filter(p => !p.ncm).length
  const semConferencia = notasRecebidas.filter(n => !n.estoqueRecebidoEm && n.status !== 'cancelada').length
  const semClassificar = dre?.lancamentosNaoClassificados ?? 0
  const notasProblema  = notas.filter(n => n.status === 'PendenteEmissao' || n.status === 'Rejeitada').length

  const pendencias = [
    semNcm > 0 && `${semNcm} produto(s) ativo(s) sem NCM.`,
    semConferencia > 0 && `${semConferencia} NF-e de entrada sem conferência física do estoque.`,
    semClassificar > 0 && `${fmtReais(semClassificar)} em lançamentos sem classificação contábil.`,
    notasProblema > 0 && `${notasProblema} nota(s) pendente(s) ou rejeitada(s) na competência.`,
  ].filter(Boolean) as string[]

  async function baixarPacote() {
    setBaixando(true)
    try {
      const { data } = await contadorApi.baixarPacoteMensal(tenantId, ano, mes)
      baixarBlob(data as Blob, `fechamento-${slug}-${ano}-${String(mes).padStart(2, '0')}.zip`)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar o pacote do mês'))
    } finally {
      setBaixando(false)
    }
  }

  async function fechar() {
    setSalvando(true)
    try {
      await contadorApi.fecharCompetencia(tenantId, { ano, mes, observacao: observacao.trim() || undefined })
      toast.success(`Competência ${String(mes).padStart(2, '0')}/${ano} fechada.`)
      setConfirmando(false)
      setObservacao('')
      carregar()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao fechar a competência'))
    } finally {
      setSalvando(false)
    }
  }

  async function reabrir(fechamento: FechamentoMensalDto) {
    try {
      await contadorApi.reabrirCompetencia(tenantId, fechamento.id)
      toast.success(`Competência ${fechamento.competencia} reaberta.`)
      carregar()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao reabrir a competência'))
    }
  }

  return (
    <div className="space-y-5">
      <div className="card space-y-4 print:hidden">
        <SecaoHeader
          icon={CalendarCheck}
          titulo="Competência"
          descricao="Escolha o mês para conferir, baixar o pacote e travar o fechamento."
          acoes={
            <>
              <Button variant="secondary" onClick={() => window.print()}>
                <Printer className="w-4 h-4" /> Imprimir / PDF
              </Button>
              <Button variant="secondary" onClick={baixarPacote} loading={baixando}>
                {!baixando && <Download className="w-4 h-4" />} Pacote do mês
              </Button>
              {fechamentoAtual ? (
                <Button variant="danger" onClick={() => reabrir(fechamentoAtual)}>
                  <Unlock className="w-4 h-4" /> Reabrir
                </Button>
              ) : (
                <Button onClick={() => setConfirmando(true)} disabled={loading}>
                  <Lock className="w-4 h-4" /> Fechar competência
                </Button>
              )}
            </>
          }
        />

        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="label" htmlFor="competencia">Mês de referência</label>
            <select
              id="competencia"
              className="input"
              value={`${ano}-${mes}`}
              onChange={e => {
                const [a, m] = e.target.value.split('-').map(Number)
                onCompetencia(a, m)
              }}
            >
              {competenciasDisponiveis().map(c => (
                <option key={`${c.ano}-${c.mes}`} value={`${c.ano}-${c.mes}`}>{c.label}</option>
              ))}
            </select>
          </div>
          {fechamentoAtual && (
            <Badge tone="success" className="mb-2">
              <Lock className="w-3 h-3 mr-1" />
              Fechada em {new Date(fechamentoAtual.fechadoEm).toLocaleDateString('pt-BR')}
              {fechamentoAtual.fechadoPorNome ? ` por ${fechamentoAtual.fechadoPorNome}` : ''}
            </Badge>
          )}
        </div>

        {fechamentoAtual && (
          <Aviso tone="info">
            <p>
              Os números abaixo são recalculados a cada visita. O que ficou <strong>travado</strong> no
              fechamento está no histórico no fim desta página — é ele que vale como base declarada.
            </p>
          </Aviso>
        )}
      </div>

      {loading ? (
        <div className="card"><Spinner block size="lg" /></div>
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            <StatCard icon={CalendarCheck} tone="brand" label="Receita bruta"
                      value={dre ? fmtReais(dre.receitaBruta) : '—'} />
            <StatCard icon={CheckCircle2} tone="success" label="Notas autorizadas"
                      value={autorizadas.length}
                      sub={fmtCentavos(autorizadas.reduce((a, n) => a + n.valorTotalEmCentavos, 0))} />
            <StatCard icon={AlertTriangle} tone={canceladas.length > 0 ? 'warning' : 'neutral'}
                      label="Canceladas" value={canceladas.length} />
            <StatCard icon={History} tone={dre && dre.resultadoLiquido >= 0 ? 'success' : 'danger'}
                      label="Resultado líquido" value={dre ? fmtReais(dre.resultadoLiquido) : '—'} />
          </div>

          <section className="card space-y-3">
            <SecaoHeader
              icon={AlertTriangle}
              titulo="Checklist antes de fechar"
              descricao="Nada aqui bloqueia o fechamento — mas fica registrado no snapshot."
            />
            {pendencias.length === 0 ? (
              <div className="flex items-center gap-2 text-sm text-emerald-400">
                <CheckCircle2 className="w-4 h-4" /> Nenhuma pendência encontrada nesta competência.
              </div>
            ) : (
              <ul className="space-y-2">
                {pendencias.map(p => (
                  <li key={p} className="flex items-start gap-2 text-sm text-amber-400">
                    <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" /> {p}
                  </li>
                ))}
              </ul>
            )}
          </section>

          {dre && (
            <section className="card space-y-3">
              <SecaoHeader icon={CalendarCheck} titulo="Resumo do mês" />
              <div className="space-y-2 text-sm">
                <LinhaValor label="Receita bruta" valor={fmtReais(dre.receitaBruta)} />
                <LinhaValor label="(−) Deduções e impostos sobre vendas"
                            valor={fmtReais(dre.deducoes + dre.impostosSobreVendas)} tone="negativo" negativo />
                <LinhaValor label="Receita líquida" valor={fmtReais(dre.receitaLiquidaDre)} tone="positivo" destaque />
                <LinhaValor label="(−) CMV" valor={fmtReais(dre.custo)} tone="negativo" negativo />
                <LinhaValor label="(−) Despesas operacionais" valor={fmtReais(dre.despesasOperacionais)} tone="negativo" negativo />
                <LinhaValor label="Resultado operacional" valor={fmtReais(dre.resultadoOperacional)}
                            tone={dre.resultadoOperacional >= 0 ? 'positivo' : 'negativo'} destaque />
                <LinhaValor label="Resultado líquido" valor={fmtReais(dre.resultadoLiquido)}
                            tone={dre.resultadoLiquido >= 0 ? 'positivo' : 'negativo'} destaque />
                {apuracao && (
                  <LinhaValor
                    label={`Imposto estimado (${apuracao.regimeAtual === 'SimplesNacional' ? 'Simples Nacional' : 'Lucro Presumido'})`}
                    valor={fmtReais(apuracao.regimeAtual === 'SimplesNacional'
                      ? apuracao.simples.valorDas
                      : apuracao.presumido.total)}
                    tone="brand"
                    destaque
                  />
                )}
              </div>
            </section>
          )}
        </>
      )}

      <section className="card space-y-3">
        <SecaoHeader icon={History} titulo="Competências fechadas"
                     descricao="Snapshots travados — os valores não mudam se um lançamento antigo for editado." />
        {carregandoFechamentos ? (
          <Spinner block />
        ) : fechamentos.length === 0 ? (
          <EmptyState icon={Lock} message="Nenhuma competência fechada ainda." compact />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-sm">
              <thead>
                <tr className="text-left text-gray-500 border-b border-surface-600">
                  <th className="py-2 font-medium">Competência</th>
                  <th className="py-2 font-medium text-right">Receita bruta</th>
                  <th className="py-2 font-medium text-right">Resultado</th>
                  <th className="py-2 font-medium text-right">Imposto apurado</th>
                  <th className="py-2 font-medium">Fechado por</th>
                  <th className="py-2 font-medium text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {fechamentos.map(f => (
                  <tr key={f.id} className={clsx(
                    'border-b border-surface-700 last:border-0',
                    f.ano === ano && f.mes === mes && 'bg-brand-500/5',
                  )}>
                    <td className="py-3 text-white font-medium">{f.competencia}</td>
                    <td className="py-3 text-right font-mono text-white">{fmtReais(f.receitaBruta)}</td>
                    <td className={clsx('py-3 text-right font-mono',
                      f.resultadoLiquido >= 0 ? 'text-emerald-400' : 'text-red-400')}>
                      {fmtReais(f.resultadoLiquido)}
                    </td>
                    <td className="py-3 text-right font-mono text-brand-300">
                      {fmtReais(f.impostoApurado)}
                      <span className="text-gray-500 text-xs"> ({fmtPercent(f.aliquotaEfetiva)})</span>
                    </td>
                    <td className="py-3 text-gray-400">
                      {f.fechadoPorNome ?? '—'}
                      <span className="block text-[11px] text-gray-600">
                        {new Date(f.fechadoEm).toLocaleDateString('pt-BR')}
                      </span>
                    </td>
                    <td className="py-3 text-right">
                      <Button variant="secondary" size="sm" onClick={() => reabrir(f)}>
                        <Unlock className="w-3.5 h-3.5" /> Reabrir
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {confirmando && (
        <Modal onClose={() => setConfirmando(false)} title={`Fechar ${MESES[mes - 1]} de ${ano}`} icon={Lock}>
          <div className="p-4 space-y-4">
            <p className="text-sm text-gray-400">
              O fechamento grava um snapshot dos números desta competência. Depois disso, editar
              um lançamento antigo não altera mais o que ficou registrado aqui — para refazer, é
              preciso reabrir a competência.
            </p>

            {pendencias.length > 0 && (
              <Aviso tone="warning">
                <p className="font-semibold">Fechando com {pendencias.length} pendência(s):</p>
                <ul className="list-disc pl-4">
                  {pendencias.map(p => <li key={p}>{p}</li>)}
                </ul>
              </Aviso>
            )}

            <div>
              <label className="label" htmlFor="observacao-fechamento">Observação (opcional)</label>
              <textarea
                id="observacao-fechamento"
                className="input w-full h-24 resize-none"
                maxLength={2000}
                placeholder="Ex.: receita de agosto conferida com o extrato; ICMS-ST ajustado fora do sistema."
                value={observacao}
                onChange={e => setObservacao(e.target.value)}
              />
            </div>

            <div className="flex gap-2 justify-end">
              <Button variant="secondary" onClick={() => setConfirmando(false)}>Cancelar</Button>
              <Button onClick={fechar} loading={salvando}>
                {!salvando && <Lock className="w-4 h-4" />} Fechar competência
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
