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
import DataTable from '@/components/admin/ui/DataTable'
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

          <section className="card-sm-up space-y-3">
            <SecaoHeader icon={FileText} titulo="Notas emitidas"
                         acoes={<span className="text-xs text-gray-400">{notas.length} no período</span>} />
            {notas.length === 0 ? (
              <EmptyState icon={FileText} message="Nenhuma nota fiscal no período selecionado." compact />
            ) : (
              /* No card: o número da nota identifica a linha e o valor fica à
                 direita; data e origem viram chips; o status desce como
                 rótulo/valor porque é o que se confere por último. */
              <DataTable
                rows={notas}
                rowKey={n => n.id}
                minWidth="600px"
                columns={[
                  { key: 'numero', header: 'Número', mobile: 'title', className: 'text-white',
                    cell: n => n.serie && n.numero ? `${n.serie}/${n.numero}` : '—' },
                  { key: 'valor', header: 'Valor', align: 'right', mobile: 'trailing',
                    cell: n => <span className="text-white font-mono">{fmtCentavos(n.valorTotalEmCentavos)}</span> },
                  { key: 'data', header: 'Data', mobile: 'meta', className: 'text-gray-400',
                    cell: n => new Date(n.createdAt).toLocaleDateString('pt-BR') },
                  { key: 'origem', header: 'Origem', mobile: 'meta', className: 'text-gray-400',
                    cell: n => n.origem },
                  { key: 'status', header: 'Status', mobile: 'field',
                    cell: n => <Badge tone={STATUS_NOTA_TONE[n.status] ?? 'neutral'}>{n.status}</Badge> },
                ]}
              />
            )}
          </section>

          <section className="card-sm-up space-y-3">
            <SecaoHeader
              icon={Package}
              titulo="NF-e de entrada"
              descricao="Documentos de fornecedores, contas geradas e conferência física do estoque."
              acoes={<span className="text-xs text-gray-400">{notasRecebidas.length} no período</span>}
            />
            {notasRecebidas.length === 0 ? (
              <EmptyState icon={Package} message="Nenhuma NF-e de entrada no período." compact />
            ) : (
              /* Card da NF-e de entrada: fornecedor identifica, valor à direita.
                 "Financeiro" e "Estoque" descem como rótulo/valor — são os dois
                 estados que o contador precisa conferir item a item, e como
                 rótulo explícito eles se leem sem depender do cabeçalho. */
              <DataTable
                rows={notasRecebidas}
                rowKey={n => n.id}
                minWidth="700px"
                columns={[
                  { key: 'fornecedor', header: 'Fornecedor', mobile: 'title',
                    cell: n => (
                      <>
                        <p className="text-white">{n.emitenteNome ?? 'Fornecedor'}</p>
                        <p className="text-[10px] text-gray-600 font-mono">
                          {n.chaveAcesso.slice(0, 6)}…{n.chaveAcesso.slice(-8)}
                        </p>
                      </>
                    ) },
                  { key: 'valor', header: 'Valor', align: 'right', mobile: 'trailing',
                    cell: n => <span className="font-mono text-white">{fmtReais(n.valor)}</span> },
                  { key: 'emissao', header: 'Emissão', mobile: 'meta', className: 'text-gray-400',
                    cell: n => n.dataEmissao ? new Date(n.dataEmissao).toLocaleDateString('pt-BR') : '—' },
                  { key: 'financeiro', header: 'Financeiro', mobile: 'field', className: 'text-gray-400',
                    cell: n => n.contasGeradas > 0 ? `${n.contasGeradas} conta(s)` : 'Pendente' },
                  { key: 'estoque', header: 'Estoque', mobile: 'field',
                    cell: n => n.estoqueRecebidoEm
                      ? <span className="text-emerald-400">✓ {n.itensEstoqueRecebidos} un.</span>
                      : <span className="text-amber-400">Aguardando conferência</span> },
                ]}
              />
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
