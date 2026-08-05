'use client'
// =============================================================================
// ConciliacaoTab.tsx — Toda venda do período com o documento fiscal que tem, ou
// a falta dele (CON-001).
//
// A tela existe para responder uma pergunta que nenhum outro relatório responde:
// "que venda ficou sem nota?". Os demais partem das notas emitidas e nunca
// enxergam o que não foi criado. Por isso o destaque visual é a venda SEM
// documento, não o total de notas autorizadas.
// =============================================================================
import { AlertTriangle, CheckCircle2, FileWarning, Receipt, Scale } from 'lucide-react'
import clsx from 'clsx'
import type { ConciliacaoFiscalDto, SituacaoFiscalVenda, VendaConciliadaDto } from '@/lib/api'
import StatCard from '@/components/admin/StatCard'
import Badge, { type BadgeTone } from '@/components/admin/ui/Badge'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'
import { fmtReais, isoParaBr, SecaoHeader, Aviso, PeriodoFields } from './contador-shared'

interface Props {
  conciliacao: ConciliacaoFiscalDto | null
  loading: boolean
  inicio: string
  fim: string
  onInicio: (v: string) => void
  onFim: (v: string) => void
}

const SITUACAO_ROTULO: Record<SituacaoFiscalVenda, string> = {
  Autorizada:     'Autorizada',
  EmContingencia: 'Em contingência',
  Pendente:       'Pendente',
  Rejeitada:      'Rejeitada',
  NotaCancelada:  'Nota cancelada',
  SemDocumento:   'Sem documento',
  VendaCancelada: 'Venda cancelada',
}

const SITUACAO_TOM: Record<SituacaoFiscalVenda, BadgeTone> = {
  Autorizada:     'success',
  EmContingencia: 'warning',
  Pendente:       'warning',
  Rejeitada:      'danger',
  NotaCancelada:  'neutral',
  SemDocumento:   'danger',
  VendaCancelada: 'neutral',
}

export default function ConciliacaoTab({ conciliacao, loading, inicio, fim, onInicio, onFim }: Props) {
  return (
    <div className="space-y-5">
      <div className="card space-y-4">
        <SecaoHeader
          icon={Scale}
          titulo="Conciliação de vendas e documentos"
          descricao={`Toda venda de ${isoParaBr(inicio)} a ${isoParaBr(fim)}, com a nota que tem — ou a falta dela.`}
        />
        <div className="flex flex-wrap items-end gap-3">
          <PeriodoFields inicio={inicio} fim={fim} onInicio={onInicio} onFim={onFim} />
        </div>
      </div>

      {loading ? (
        <div className="card"><Spinner block size="lg" /></div>
      ) : !conciliacao ? (
        <div className="card">
          <EmptyState icon={Scale} message="Não foi possível carregar a conciliação do período." compact />
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <StatCard icon={Receipt} tone="brand" label="Vendas no período"
                      value={conciliacao.totalVendas}
                      sub={fmtReais(conciliacao.valorTotalVendas)} />
            <StatCard
              icon={FileWarning}
              tone={conciliacao.valorSemDocumento > 0 ? 'danger' : 'success'}
              label="Vendas sem documento"
              value={fmtReais(conciliacao.valorSemDocumento)}
              sub={`${conciliacao.porSituacao?.SemDocumento?.quantidade ?? 0} venda(s)`}
            />
            <StatCard
              icon={AlertTriangle}
              tone={conciliacao.quantidadePendencias > 0 ? 'warning' : 'success'}
              label="Exigem atenção"
              value={conciliacao.quantidadePendencias}
              sub="sem nota, pendente, rejeitada ou divergente"
            />
          </div>

          {conciliacao.valorSemDocumento > 0 && (
            <Aviso tone="warning">
              <p>
                {fmtReais(conciliacao.valorSemDocumento)} em vendas do período <strong>não têm documento
                fiscal nenhum</strong>. Emitir a NFC-e é uma escolha no fechamento, e essa escolha não fica
                registrada — então essas vendas não aparecem em nenhum outro relatório. Confirme com o
                lojista se a não emissão foi intencional antes de fechar a competência.
              </p>
            </Aviso>
          )}

          <section className="card space-y-3">
            <SecaoHeader icon={Scale} titulo="Resumo por situação" />
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-sm">
              {(Object.keys(SITUACAO_ROTULO) as SituacaoFiscalVenda[]).map(situacao => {
                const resumo = conciliacao.porSituacao?.[situacao]
                const qtd = resumo?.quantidade ?? 0
                return (
                  <div key={situacao} className={clsx(
                    'rounded-xl border p-3',
                    qtd === 0 ? 'border-surface-700 bg-surface-800/30 opacity-60'
                              : 'border-surface-600 bg-surface-800/60',
                  )}>
                    <p className="text-xs text-gray-500">{SITUACAO_ROTULO[situacao]}</p>
                    <p className="text-lg font-black text-white">{qtd}</p>
                    <p className="text-[11px] text-gray-500 font-mono">{fmtReais(resumo?.valor ?? 0)}</p>
                  </div>
                )
              })}
            </div>
          </section>

          <section className="card space-y-3">
            <SecaoHeader
              icon={AlertTriangle}
              titulo="Pendências"
              descricao="Vendas que exigem ação antes do fechamento contábil."
              acoes={<span className="text-xs text-gray-400">{conciliacao.pendencias.length} item(ns)</span>}
            />
            {conciliacao.pendencias.length === 0 ? (
              <div className="flex items-center gap-2 text-sm text-emerald-400">
                <CheckCircle2 className="w-4 h-4" /> Nenhuma pendência no período.
              </div>
            ) : (
              <TabelaVendas vendas={conciliacao.pendencias} />
            )}
          </section>

          <section className="card space-y-3">
            <SecaoHeader icon={Receipt} titulo="Todas as vendas"
                         acoes={<span className="text-xs text-gray-400">{conciliacao.vendas.length}</span>} />
            {conciliacao.vendas.length === 0 ? (
              <EmptyState icon={Receipt} message="Nenhuma venda no período." compact />
            ) : (
              <TabelaVendas vendas={conciliacao.vendas} />
            )}
          </section>
        </>
      )}
    </div>
  )
}

function TabelaVendas({ vendas }: { vendas: VendaConciliadaDto[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[720px] text-sm">
        <thead>
          <tr className="text-left text-gray-500 border-b border-surface-600">
            <th className="py-2 font-medium">Data</th>
            <th className="py-2 font-medium">Origem</th>
            <th className="py-2 font-medium">Situação</th>
            <th className="py-2 font-medium">Documento</th>
            <th className="py-2 font-medium text-right">Venda</th>
            <th className="py-2 font-medium text-right">Nota</th>
          </tr>
        </thead>
        <tbody>
          {vendas.map(venda => (
            <tr key={venda.vendaId} className="border-b border-surface-700 last:border-0 align-top">
              <td className="py-3 text-gray-400 whitespace-nowrap">
                {new Date(venda.ocorridaEm).toLocaleDateString('pt-BR')}
              </td>
              <td className="py-3 text-gray-400">
                {venda.origem === 'Comanda' ? 'Comanda' : 'Venda avulsa'}
              </td>
              <td className="py-3">
                <Badge tone={SITUACAO_TOM[venda.situacao]}>{SITUACAO_ROTULO[venda.situacao]}</Badge>
                {venda.motivoRejeicao && (
                  <p className="text-[11px] text-red-400 mt-1 max-w-[260px]">{venda.motivoRejeicao}</p>
                )}
              </td>
              <td className="py-3 text-gray-400">
                {venda.numero ? `${venda.serie}/${venda.numero}` : '—'}
                {venda.chaveAcesso && (
                  <p className="text-[10px] text-gray-600 font-mono">
                    {venda.chaveAcesso.slice(0, 6)}…{venda.chaveAcesso.slice(-6)}
                  </p>
                )}
              </td>
              <td className="py-3 text-right font-mono text-white">{fmtReais(venda.valorVenda)}</td>
              <td className={clsx('py-3 text-right font-mono',
                venda.valorDivergente ? 'text-amber-400' : 'text-gray-400')}>
                {venda.valorNota != null ? fmtReais(venda.valorNota) : '—'}
                {venda.valorDivergente && (
                  <p className="text-[10px] text-amber-400">divergente</p>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
