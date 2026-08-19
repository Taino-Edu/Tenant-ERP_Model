'use client'

// =============================================================================
// /plataforma/financeiro — O nosso financeiro: o que cobramos das lojas e o que
// efetivamente entrou.
//
// Não confundir com /admin/financeiro, que é o financeiro DE DENTRO de uma loja.
// Este aqui só o dono da plataforma alcança (PlatformOwnerOnly na API).
// =============================================================================

import { useCallback, useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import {
  Wallet, TrendingUp, AlertTriangle, CircleDollarSign, RefreshCw,
  Check, Undo2, Calendar, Store,
} from 'lucide-react'
import {
  platformBillingApi, getErrorMessage,
  type BillingResumoDto, type TenantChargeDto,
} from '@/lib/api'
import { usePlatformPermissions } from '@/hooks/usePlatformPermissions'
import Button from '@/components/admin/ui/Button'
import DataTable from '@/components/admin/ui/DataTable'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'

const brl = (v: number) =>
  v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

/** "2026-07" — o input month e a API trabalham no mesmo formato. */
function competenciaAtual(): string {
  const hoje = new Date()
  return `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`
}

/** Data só-dia, sem fuso: as datas de cobrança vêm em UTC 00:00 e o
 *  toLocaleDateString do navegador jogaria pro dia anterior em fusos negativos
 *  (o Brasil inteiro). Formatar a partir das partes evita o vencimento aparecer
 *  um dia antes do que é. */
function dataCurta(iso: string): string {
  const [ano, mes, dia] = iso.slice(0, 10).split('-')
  return `${dia}/${mes}/${ano}`
}

export default function FinanceiroPlataformaPage() {
  const [competencia, setCompetencia] = useState(competenciaAtual)
  const [resumo, setResumo]           = useState<BillingResumoDto | null>(null)
  const [cobrancas, setCobrancas]     = useState<TenantChargeDto[]>([])
  const [loading, setLoading]         = useState(true)
  const [gerando, setGerando]         = useState(false)
  const [salvandoId, setSalvandoId]   = useState<string | null>(null)
  // A aba abre com `platform.finance.read`; gerar mensalidades e dar baixa
  // exigem `platform.finance.manage`. Sem essa distinção, o perfil de auditoria
  // via os dois botões e só descobria o limite depois do 403.
  const podeLancar = usePlatformPermissions()('platform.finance.manage')

  const carregar = useCallback(async (comp: string) => {
    setLoading(true)
    try {
      // A API aceita qualquer data dentro do mês e normaliza pro dia 1.
      const dataComp = `${comp}-01`
      const [r, c] = await Promise.all([
        platformBillingApi.resumo(dataComp),
        platformBillingApi.cobrancas(dataComp),
      ])
      setResumo(r.data)
      setCobrancas(c.data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra carregar o financeiro.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { carregar(competencia) }, [competencia, carregar])

  async function gerarMensalidades() {
    setGerando(true)
    try {
      const { data } = await platformBillingApi.gerarMensalidades(`${competencia}-01`)
      // A mensagem diz o que aconteceu de verdade, inclusive quando não fez
      // nada: "0 criadas" sem explicação parece bug, e o gerador é idempotente
      // de propósito — clicar de novo tem que ser inofensivo E compreensível.
      if (data.criadas > 0) {
        toast.success(`${data.criadas} mensalidade(s) gerada(s) — ${brl(data.totalGerado)}.`)
      } else if (data.jaExistiam > 0) {
        toast(`Nada a fazer: as ${data.jaExistiam} mensalidade(s) deste mês já estavam geradas.`)
      } else {
        toast('Nenhuma loja entrou em cobrança neste mês.')
      }
      await carregar(competencia)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra gerar as mensalidades.'))
    } finally {
      setGerando(false)
    }
  }

  async function alternarPagamento(c: TenantChargeDto) {
    setSalvandoId(c.id)
    try {
      const hoje = new Date().toISOString().slice(0, 10)
      await platformBillingApi.definirPagamento(c.id, c.pagoEm ? null : hoje)
      await carregar(competencia)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra atualizar a cobrança.'))
    } finally {
      setSalvandoId(null)
    }
  }

  return (
    // O <main> do PlataformaShell já dá o padding da área — repetir aqui
    // custava 32px de largura no celular.
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Wallet className="w-6 h-6 text-brand-400" />
            Financeiro da plataforma
          </h1>
          <p className="text-gray-400 text-sm mt-0.5">
            O que cobramos de cada loja e o que já entrou.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <label className="sr-only" htmlFor="competencia">Mês de competência</label>
          <div className="flex items-center gap-2 rounded-xl border border-surface-500 bg-surface-700 px-3 py-2">
            <Calendar className="w-4 h-4 text-gray-400" />
            <input
              id="competencia"
              type="month"
              value={competencia}
              onChange={e => setCompetencia(e.target.value)}
              className="bg-transparent text-sm text-white outline-none"
            />
          </div>
          {podeLancar && (
            <Button onClick={gerarMensalidades} loading={gerando}>
              <RefreshCw className="w-4 h-4" />
              Gerar mensalidades
            </Button>
          )}
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center py-20"><Spinner /></div>
      ) : (
        <>
          {/* ── Indicadores ────────────────────────────────────────────────── */}
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Card
              icon={TrendingUp}
              titulo="MRR contratado"
              valor={brl(resumo?.mrrContratado ?? 0)}
              detalhe={`${resumo?.lojasPagantes ?? 0} loja(s) pagante(s)${
                resumo?.lojasSemCobranca ? ` · ${resumo.lojasSemCobranca} sem cobrança` : ''
              }`}
            />
            <Card
              icon={CircleDollarSign}
              titulo="Recebido no mês"
              valor={brl(resumo?.recebido ?? 0)}
              detalhe={`de ${brl(resumo?.faturado ?? 0)} faturados`}
              tom="ok"
            />
            <Card
              icon={Wallet}
              titulo="Em aberto no mês"
              valor={brl(resumo?.emAberto ?? 0)}
              detalhe={`${resumo?.qtdCobrancas ?? 0} cobrança(s) na competência`}
            />
            <Card
              icon={AlertTriangle}
              titulo="Vencido acumulado"
              valor={brl(resumo?.vencidoAcumulado ?? 0)}
              detalhe="Todas as competências, não só esta"
              tom={resumo && resumo.vencidoAcumulado > 0 ? 'alerta' : undefined}
            />
          </div>

          {/* ── Cobranças ──────────────────────────────────────────────────── */}
          {/* Moldura só a partir de sm — no celular os cards da lista já são a
              superfície, e `bg-surface-800` é a mesma cor deles. */}
          <div className="rounded-xl sm:border sm:border-surface-500 sm:bg-surface-800">
            <div className="border-b border-surface-500 px-0 py-3 sm:px-4">
              <h2 className="font-semibold text-white">Cobranças da competência</h2>
            </div>

            {cobrancas.length === 0 ? (
              <EmptyState
                icon={Store}
                message={podeLancar
                  ? 'Nenhuma cobrança gerada para este mês. Use “Gerar mensalidades” para criá-las.'
                  : 'Nenhuma cobrança gerada para este mês.'}
              />
            ) : (
              /* Esta tela já tinha tabela e cards, mas escritos à mão em
                 paralelo — duas marcações pra mesma lista, que só ficariam
                 iguais por disciplina (o card já omitia o slug do tenant, por
                 exemplo). Agora as duas saem da mesma definição de colunas. */
              <DataTable
                className="pt-3 sm:pt-0"
                rows={cobrancas}
                rowKey={c => c.id}
                // `undefined` e não uma função que devolve null: é assim que o
                // DataTable deixa de reservar a coluna de ações inteira.
                rowActions={podeLancar ? c => (
                  <AcaoPagamento
                    c={c}
                    salvando={salvandoId === c.id}
                    onClick={() => alternarPagamento(c)}
                  />
                ) : undefined}
                columns={[
                  { key: 'loja', header: 'Loja', mobile: 'title',
                    cell: c => (
                      <>
                        <p className="font-medium text-white">{c.tenantNome}</p>
                        <p className="text-xs text-gray-500">{c.tenantSlug}</p>
                      </>
                    ) },
                  { key: 'valor', header: 'Valor', align: 'right', mobile: 'trailing',
                    cell: c => <span className="font-semibold tabular-nums text-white">{brl(c.valor)}</span> },
                  { key: 'tipo', header: 'Tipo', mobile: 'meta', className: 'text-gray-300',
                    cell: c => c.tipo === 'Implantacao' ? 'Implantação' : 'Mensalidade' },
                  { key: 'vencimento', header: 'Vencimento', mobile: 'meta', className: 'text-gray-300',
                    cell: c => <>vence {dataCurta(c.vencimento)}</> },
                  { key: 'situacao', header: 'Situação', mobile: 'field', cell: c => <Situacao c={c} /> },
                ]}
              />
            )}
          </div>

          <p className="text-xs text-gray-600">
            MRR contratado é receita <strong>esperada</strong> (soma das mensalidades ativas).
            Recebido é o que de fato entrou. Lucro real depende também das despesas da
            plataforma, que ainda não são registradas aqui.
          </p>
        </>
      )}
    </div>
  )
}

function Card({
  icon: Icon, titulo, valor, detalhe, tom,
}: {
  icon: typeof Wallet
  titulo: string
  valor: string
  detalhe?: string
  tom?: 'ok' | 'alerta'
}) {
  const cor =
    tom === 'ok'     ? 'text-accent-green' :
    tom === 'alerta' ? 'text-accent-red'   :
    'text-white'

  return (
    <div className="rounded-xl border border-surface-500 bg-surface-800 p-4">
      <div className="flex items-center gap-2 text-gray-400">
        <Icon className="h-4 w-4" />
        <span className="text-xs font-semibold uppercase tracking-wide">{titulo}</span>
      </div>
      <p className={`mt-2 text-2xl font-bold tabular-nums ${cor}`}>{valor}</p>
      {detalhe && <p className="mt-1 text-xs text-gray-500">{detalhe}</p>}
    </div>
  )
}

function Situacao({ c }: { c: TenantChargeDto }) {
  if (c.pagoEm) {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full bg-accent-green/15 px-2.5 py-1 text-xs font-semibold text-accent-green">
        Pago em {dataCurta(c.pagoEm)}
      </span>
    )
  }
  if (c.vencida) {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full bg-accent-red/15 px-2.5 py-1 text-xs font-semibold text-accent-red">
        <AlertTriangle className="h-3 w-3" /> Vencida
      </span>
    )
  }
  return (
    <span className="inline-flex items-center rounded-full bg-surface-600 px-2.5 py-1 text-xs font-semibold text-gray-300">
      Em aberto
    </span>
  )
}

function AcaoPagamento({
  c, salvando, onClick,
}: {
  c: TenantChargeDto
  salvando: boolean
  onClick: () => void
}) {
  return (
    <Button
      size="sm"
      variant={c.pagoEm ? 'secondary' : 'success'}
      loading={salvando}
      onClick={onClick}
      aria-label={c.pagoEm ? `Reabrir cobrança de ${c.tenantNome}` : `Dar baixa na cobrança de ${c.tenantNome}`}
    >
      {c.pagoEm ? <Undo2 className="h-3.5 w-3.5" /> : <Check className="h-3.5 w-3.5" />}
      {c.pagoEm ? 'Reabrir' : 'Dar baixa'}
    </Button>
  )
}
