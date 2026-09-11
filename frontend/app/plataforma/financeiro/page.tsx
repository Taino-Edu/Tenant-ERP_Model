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
  Calendar, Store, Plus, Send, X,
} from 'lucide-react'
import {
  platformBillingApi, platformApi, getErrorMessage,
  type BillingResumoDto, type TenantChargeDto, type TenantSummary,
} from '@/lib/api'
import { usePlatformPermissions } from '@/hooks/usePlatformPermissions'
import Button from '@/components/admin/ui/Button'
import CobrancaFormModal from '@/components/plataforma/CobrancaFormModal'
import CobrancasTabela, { brl } from '@/components/plataforma/CobrancasTabela'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'

/** "2026-07" — o input month e a API trabalham no mesmo formato. */
function competenciaAtual(): string {
  const hoje = new Date()
  return `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`
}

export default function FinanceiroPlataformaPage() {
  const [competencia, setCompetencia] = useState(competenciaAtual)
  const [resumo, setResumo]           = useState<BillingResumoDto | null>(null)
  const [cobrancas, setCobrancas]     = useState<TenantChargeDto[]>([])
  const [loading, setLoading]         = useState(true)
  const [gerando, setGerando]         = useState(false)
  const [emitindo, setEmitindo]       = useState(false)
  // Pendências da última emissão (loja sem CNPJ, gateway recusou). Ficam na
  // tela até alguém fechar: num toast sumiriam antes de dar para anotar quais
  // lojas precisam de ação.
  const [pendencias, setPendencias]   = useState<string[]>([])
  const [criando, setCriando]         = useState(false)
  // Só é buscada quando o formulário de lançamento abre: a tela de financeiro
  // não precisa da lista de lojas para nada além disso, e carregá-la no load
  // pagaria a consulta em toda visita para um botão que quase nunca é clicado.
  const [lojas, setLojas]             = useState<TenantSummary[]>([])
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

  async function emitirPendentes() {
    setEmitindo(true)
    try {
      const { data } = await platformBillingApi.emitirPendentes()
      setPendencias(data.pendencias)
      // Mesma regra do "Gerar mensalidades": dizer o que aconteceu inclusive
      // quando não aconteceu nada, senão o clique parece não ter funcionado.
      if (data.emitidas > 0) {
        toast.success(`${data.emitidas} cobrança(s) emitida(s) no Asaas.`)
      } else if (data.pendencias.length === 0) {
        toast('Nada a emitir: as cobranças em aberto já estão no Asaas.')
      } else {
        toast.error('Nenhuma cobrança foi emitida. Veja as pendências na tela.')
      }
      await carregar(competencia)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra emitir as cobranças.'))
    } finally {
      setEmitindo(false)
    }
  }

  async function abrirLancamento() {
    setCriando(true)
    if (lojas.length === 0) {
      try {
        const { data } = await platformApi.listTenants()
        setLojas(data)
      } catch {
        // O formulário abre mesmo assim, mostrando o aviso de lista vazia —
        // fechar o modal por causa disso deixaria o clique sem resposta.
      }
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
            <>
              <Button onClick={gerarMensalidades} loading={gerando}>
                <RefreshCw className="w-4 h-4" />
                Gerar mensalidades
              </Button>
              {/* Sem isto, cobrança lançada à mão só ia ao Asaas na rodada
                  automática, até 12 horas depois. */}
              <Button variant="secondary" onClick={emitirPendentes} loading={emitindo}
                title="Manda ao Asaas as cobranças em aberto que ainda não foram emitidas">
                <Send className="w-4 h-4" />
                Emitir no Asaas
              </Button>
              <Button variant="secondary" onClick={abrirLancamento}>
                <Plus className="w-4 h-4" />
                Nova cobrança
              </Button>
            </>
          )}
        </div>
      </div>

      {pendencias.length > 0 && (
        <div role="status" className="rounded-xl border border-amber-500/40 bg-amber-500/10 p-4">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="flex items-center gap-2 text-sm font-semibold text-amber-200">
                <AlertTriangle className="h-4 w-4" />
                Não foram ao Asaas na última emissão
              </p>
              <ul className="mt-2 space-y-1 text-sm text-amber-100/90">
                {/* Índice como chave: a mesma loja pode repetir a mesma razão,
                    uma vez por cobrança recusada. */}
                {pendencias.map((p, i) => <li key={i}>{p}</li>)}
              </ul>
            </div>
            <button type="button" onClick={() => setPendencias([])} aria-label="Fechar pendências"
              className="text-amber-200 hover:text-white">
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}

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
              /* A tabela, as ações de cada linha e os diálogos de alterar e
                 excluir moram em CobrancasTabela: a aba Cobranças da página da
                 loja usa a mesma lista, e duas cópias só ficariam iguais por
                 disciplina. */
              <CobrancasTabela
                className="pt-3 sm:pt-0"
                cobrancas={cobrancas}
                podeLancar={podeLancar}
                onAlterado={() => carregar(competencia)}
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

      {criando && (
        <CobrancaFormModal
          cobranca={null}
          lojas={lojas}
          competencia={competencia}
          onClose={() => setCriando(false)}
          onSalvo={() => carregar(competencia)}
        />
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
