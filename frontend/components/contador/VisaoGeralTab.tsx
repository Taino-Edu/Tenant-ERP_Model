'use client'
// =============================================================================
// VisaoGeralTab.tsx — Retrato do cliente no período: saúde fiscal, DRE, notas
// de saída e de entrada. É a aba que responde "esta loja está em dia?".
// =============================================================================
import { Calculator, Download, FileText, Package, Building2, TrendingUp, XCircle, Receipt } from 'lucide-react'
import clsx from 'clsx'
import type { ContadorConfigDto, ContadorNotaDto, ContadorNotaRecebidaDto, FinanceiroDto } from '@/lib/api'
import StatCard from '@/components/admin/StatCard'
import Badge from '@/components/admin/ui/Badge'
import Button from '@/components/admin/ui/Button'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'
import {
  fmtCentavos, fmtReais, isoParaBr, LinhaValor, SecaoHeader, STATUS_NOTA_TONE, PeriodoFields, baixarCsv,
} from './contador-shared'

interface Props {
  slug: string
  config: ContadorConfigDto | null
  dre: FinanceiroDto | null
  notas: ContadorNotaDto[]
  notasRecebidas: ContadorNotaRecebidaDto[]
  loading: boolean
  inicio: string
  fim: string
  onInicio: (v: string) => void
  onFim: (v: string) => void
  exportando: boolean
  onExportarXmls: () => void
}

export default function VisaoGeralTab({
  slug, config, dre, notas, notasRecebidas, loading,
  inicio, fim, onInicio, onFim, exportando, onExportarXmls,
}: Props) {
  const autorizadas = notas.filter(n => n.status === 'Autorizada' || n.status === 'AutorizadaContingencia')
  const canceladas  = notas.filter(n => n.status === 'Cancelada')
  const somaAutorizadas = autorizadas.reduce((acc, n) => acc + n.valorTotalEmCentavos, 0)
  const somaCanceladas  = canceladas.reduce((acc, n) => acc + n.valorTotalEmCentavos, 0)

  function exportarDreCsv() {
    if (!dre) return
    const rows: Array<[string, number]> = [
      ['Receita bruta', dre.receitaBruta],
      ['(-) Descontos e abatimentos', -dre.deducoes],
      ['(-) Impostos sobre vendas', -dre.impostosSobreVendas],
      ['Receita líquida', dre.receitaLiquidaDre],
      ['(-) CMV', -dre.custo],
      ['Lucro bruto', dre.receitaLiquidaDre - dre.custo],
      ...dre.despesasPorCategoria.map(item => [`(-) ${item.categoria}`, -item.valor] as [string, number]),
      ['Resultado operacional', dre.resultadoOperacional],
      ['(+/-) Resultado financeiro', dre.resultadoFinanceiro],
      ['(-) IRPJ / CSLL', -dre.impostosSobreLucro],
      ['Resultado líquido', dre.resultadoLiquido],
    ]
    baixarCsv(`dre-${slug}-${inicio}-a-${fim}.csv`, [
      'Linha;Valor (R$)',
      ...rows.map(([label, value]) => `${label};${value.toFixed(2).replace('.', ',')}`),
    ])
  }

  return (
    <div className="space-y-5">
      <section className="card space-y-3">
        <SecaoHeader icon={Building2} titulo="Cadastro da empresa"
                     descricao="Dados que o sistema usa como emitente na NFC-e." />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
          <Campo label="Razão social" valor={config?.razaoSocial} />
          <Campo label="CNPJ" valor={config?.cnpj} />
          <Campo label="Inscrição estadual" valor={config?.inscricaoEstadual} />
          <Campo label="Regime tributário" valor={config?.regimeTributario} />
          <div className="sm:col-span-2">
            <Campo
              label="Endereço"
              valor={[config?.logradouro, config?.numero, config?.bairro, config?.municipio, config?.uf]
                .filter(Boolean).join(', ')}
            />
          </div>
        </div>
      </section>

      <div className="card space-y-4">
        <SecaoHeader
          icon={FileText}
          titulo="Período em análise"
          descricao={`Competência de ${isoParaBr(inicio)} a ${isoParaBr(fim)}.`}
          acoes={
            <Button onClick={onExportarXmls} loading={exportando}>
              {!exportando && <Download className="w-4 h-4" />}
              Exportar XMLs
            </Button>
          }
        />
        <div className="flex flex-wrap items-end gap-3">
          <PeriodoFields inicio={inicio} fim={fim} onInicio={onInicio} onFim={onFim} />
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <StatCard icon={TrendingUp} tone="success" label="Faturamento autorizado"
                  value={fmtCentavos(somaAutorizadas)} sub={`${autorizadas.length} nota(s)`} />
        <StatCard icon={Receipt} tone="brand" label="Notas no período" value={notas.length}
                  sub={`${notasRecebidas.length} de entrada`} />
        <StatCard icon={XCircle} tone="danger" label="Canceladas"
                  value={fmtCentavos(somaCanceladas)} sub={`${canceladas.length} nota(s)`} />
      </div>

      {loading ? (
        <div className="card"><Spinner block size="lg" /></div>
      ) : (
        <>
          {dre && (
            <section className="card space-y-3">
              <SecaoHeader
                icon={Calculator}
                titulo="DRE gerencial"
                descricao="Mesma base de cálculo que o lojista vê no financeiro."
                acoes={
                  <Button variant="secondary" size="sm" onClick={exportarDreCsv}>
                    <Download className="w-3.5 h-3.5" /> CSV
                  </Button>
                }
              />
              <div className="space-y-2 text-sm">
                <LinhaValor label="Receita bruta" valor={fmtReais(dre.receitaBruta)} />
                {dre.deducoes > 0 && (
                  <LinhaValor label="(−) Descontos e abatimentos" valor={fmtReais(dre.deducoes)} tone="negativo" negativo />
                )}
                {dre.impostosSobreVendas > 0 && (
                  <LinhaValor label="(−) Impostos sobre vendas" valor={fmtReais(dre.impostosSobreVendas)} tone="negativo" negativo />
                )}
                <LinhaValor label="Receita líquida" valor={fmtReais(dre.receitaLiquidaDre)} tone="positivo" destaque />
                <LinhaValor label="(−) CMV" valor={fmtReais(dre.custo)} tone="negativo" negativo />
                <LinhaValor label="Lucro bruto" valor={fmtReais(dre.receitaLiquidaDre - dre.custo)} tone="brand" destaque />
                {dre.despesasPorCategoria.map(item => (
                  <LinhaValor key={item.categoria} label={`(−) ${item.categoria}`}
                              valor={fmtReais(item.valor)} tone="negativo" negativo indent />
                ))}
                <LinhaValor label="Resultado operacional" valor={fmtReais(dre.resultadoOperacional)}
                            tone={dre.resultadoOperacional >= 0 ? 'positivo' : 'negativo'} destaque />
                {dre.resultadoFinanceiro !== 0 && (
                  <LinhaValor label="(+/−) Resultado financeiro" valor={fmtReais(dre.resultadoFinanceiro)} />
                )}
                {dre.impostosSobreLucro > 0 && (
                  <LinhaValor label="(−) IRPJ / CSLL" valor={fmtReais(dre.impostosSobreLucro)} tone="negativo" negativo />
                )}
                <LinhaValor label="Resultado líquido" valor={fmtReais(dre.resultadoLiquido)}
                            tone={dre.resultadoLiquido >= 0 ? 'positivo' : 'negativo'} destaque />
                {dre.lancamentosNaoClassificados > 0 && (
                  <p className="text-xs text-amber-400 pt-1">
                    {fmtReais(dre.lancamentosNaoClassificados)} aguardam classificação contábil e não entraram no resultado.
                  </p>
                )}
                <p className="text-[11px] text-gray-500 pt-1">
                  Compras de mercadoria formam estoque e entram no resultado pelo CMV conforme a venda;
                  extrato bancário permanece na conciliação de caixa.
                </p>
              </div>
            </section>
          )}

          <section className="card space-y-3">
            <SecaoHeader icon={FileText} titulo="Notas emitidas"
                         acoes={<span className="text-xs text-gray-400">{notas.length} no período</span>} />
            {notas.length === 0 ? (
              <EmptyState icon={FileText} message="Nenhuma nota fiscal no período selecionado." compact />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[600px] text-sm">
                  <thead>
                    <tr className="text-left text-gray-500 border-b border-surface-600">
                      <th className="py-2 font-medium">Data</th>
                      <th className="py-2 font-medium">Número</th>
                      <th className="py-2 font-medium">Origem</th>
                      <th className="py-2 font-medium">Status</th>
                      <th className="py-2 font-medium text-right">Valor</th>
                    </tr>
                  </thead>
                  <tbody>
                    {notas.map(n => (
                      <tr key={n.id} className="border-b border-surface-700 last:border-0">
                        <td className="py-3 text-gray-400">{new Date(n.createdAt).toLocaleDateString('pt-BR')}</td>
                        <td className="py-3 text-white">{n.serie && n.numero ? `${n.serie}/${n.numero}` : '—'}</td>
                        <td className="py-3 text-gray-400">{n.origem}</td>
                        <td className="py-3">
                          <Badge tone={STATUS_NOTA_TONE[n.status] ?? 'neutral'}>{n.status}</Badge>
                        </td>
                        <td className="py-3 text-right text-white font-mono">{fmtCentavos(n.valorTotalEmCentavos)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="card space-y-3">
            <SecaoHeader
              icon={Package}
              titulo="NF-e de entrada"
              descricao="Documentos de fornecedores, contas geradas e conferência física do estoque."
              acoes={<span className="text-xs text-gray-400">{notasRecebidas.length} no período</span>}
            />
            {notasRecebidas.length === 0 ? (
              <EmptyState icon={Package} message="Nenhuma NF-e de entrada no período." compact />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[700px] text-sm">
                  <thead>
                    <tr className="text-left text-gray-500 border-b border-surface-600">
                      <th className="py-2 font-medium">Emissão</th>
                      <th className="py-2 font-medium">Fornecedor</th>
                      <th className="py-2 font-medium">Financeiro</th>
                      <th className="py-2 font-medium">Estoque</th>
                      <th className="py-2 font-medium text-right">Valor</th>
                    </tr>
                  </thead>
                  <tbody>
                    {notasRecebidas.map(nota => (
                      <tr key={nota.id} className="border-b border-surface-700 last:border-0">
                        <td className="py-3 text-gray-400">
                          {nota.dataEmissao ? new Date(nota.dataEmissao).toLocaleDateString('pt-BR') : '—'}
                        </td>
                        <td className="py-3">
                          <p className="text-white">{nota.emitenteNome ?? 'Fornecedor'}</p>
                          <p className="text-[10px] text-gray-600 font-mono">
                            {nota.chaveAcesso.slice(0, 6)}…{nota.chaveAcesso.slice(-8)}
                          </p>
                        </td>
                        <td className="py-3 text-gray-400">
                          {nota.contasGeradas > 0 ? `${nota.contasGeradas} conta(s)` : 'Pendente'}
                        </td>
                        <td className="py-3">
                          {nota.estoqueRecebidoEm
                            ? <span className="text-emerald-400">✓ {nota.itensEstoqueRecebidos} un.</span>
                            : <span className="text-amber-400">Aguardando conferência</span>}
                        </td>
                        <td className="py-3 text-right font-mono text-white">{fmtReais(nota.valor)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </div>
  )
}

function Campo({ label, valor }: { label: string; valor?: string | null }) {
  return (
    <div>
      <span className="text-gray-500">{label}: </span>
      <span className={clsx(valor ? 'text-white' : 'text-gray-600')}>{valor || '—'}</span>
    </div>
  )
}
