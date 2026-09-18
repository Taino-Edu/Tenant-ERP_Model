'use client'

// =============================================================================
// CondicoesComerciaisPainel — o combinado com a loja, na aba Cobranças.
//
// Separa o que foi negociado (mensalidade, dia de vencimento, implantação
// parcelada, descontos com vigência) das cobranças que isso gera. Quem negocia
// mexe aqui uma vez; o gerador cobra certo nos meses seguintes, e a prévia
// mostra o que vai sair antes de sair.
//
// Toda gravação devolve o que aconteceu com as cobranças em aberto. Pendência
// (fatura emitida que o Asaas não deixou cancelar) fica num quadro na tela, e
// não num toast que some: é justamente o caso em que alguém precisa conferir.
// =============================================================================

import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, CalendarClock, Handshake, Loader2, Pencil, Percent, Plus, Trash2 } from 'lucide-react'
import clsx from 'clsx'
import {
  getErrorMessage, platformBillingApi,
  type AlteracaoCondicoesResultDto, type CondicoesComerciaisDto, type DescontoDto, type PreviaItemDto, type TipoDesconto,
} from '@/lib/api'
import Button from '@/components/admin/ui/Button'
import ConfirmDialog from '@/components/admin/ui/ConfirmDialog'
import Modal from '@/components/admin/ui/Modal'
import { brl, dataCurta } from '@/components/plataforma/CobrancasTabela'

const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

/** "out/2026" a partir de "2026-10-01T00:00:00Z" ou "2026-10". */
function mesCurto(iso: string): string {
  const [ano, mes] = iso.slice(0, 7).split('-').map(Number)
  return `${MESES[mes - 1]}/${ano}`
}

/** Meses entre duas competências "AAAA-MM", contando as duas pontas. */
function mesesEntre(inicio: string, fim: string): number {
  const [a1, m1] = inicio.slice(0, 7).split('-').map(Number)
  const [a2, m2] = fim.slice(0, 7).split('-').map(Number)
  return (a2 - a1) * 12 + (m2 - m1) + 1
}

/** "AAAA-MM" somando meses, sem passar por Date (fuso). */
function somarMeses(mes: string, quantidade: number): string {
  const [ano, m] = mes.split('-').map(Number)
  const total = ano * 12 + (m - 1) + quantidade
  return `${Math.floor(total / 12)}-${String((total % 12) + 1).padStart(2, '0')}`
}

function mesLocalAtual(): string {
  const hoje = new Date()
  return `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`
}

const valorDesconto = (d: { tipo: TipoDesconto; valor: number }) =>
  d.tipo === 'Percentual' ? `${d.valor.toLocaleString('pt-BR')}%` : brl(d.valor)

function resumirResultado(r: AlteracaoCondicoesResultDto['cobrancas']): string {
  const partes = [
    r.criadas && `${r.criadas} criada(s)`,
    r.atualizadas && `${r.atualizadas} atualizada(s)`,
    r.removidas && `${r.removidas} removida(s)`,
  ].filter(Boolean)
  return partes.length ? `Cobranças em aberto: ${partes.join(', ')}.` : 'Nenhuma cobrança em aberto precisou mudar.'
}

export default function CondicoesComerciaisPainel({
  tenantId, podeEditar, onCobrancasAlteradas, versaoCobrancas = 0,
}: {
  tenantId: string
  /** `platform.finance.manage`. Sem ela o painel é só leitura. */
  podeEditar: boolean
  /** Recarrega a lista de cobranças e o resumo da loja depois de uma gravação. */
  onCobrancasAlteradas: () => void | Promise<void>
  /** Muda quando alguém mexe numa cobrança pela lista (baixa, edição,
   *  exclusão): a prévia mostra as cobranças existentes como estão, então
   *  precisa ser relida. */
  versaoCobrancas?: number
}) {
  const [condicoes, setCondicoes] = useState<CondicoesComerciaisDto | null>(null)
  const [pendencias, setPendencias] = useState<string[]>([])
  const [descontoEditado, setDescontoEditado] = useState<DescontoDto | 'novo' | null>(null)
  const [descontoExcluido, setDescontoExcluido] = useState<DescontoDto | null>(null)
  const [excluindo, setExcluindo] = useState(false)

  const carregar = useCallback(async () => {
    try {
      const { data } = await platformBillingApi.condicoes(tenantId)
      setCondicoes(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar as condições comerciais'))
    }
  }, [tenantId])

  useEffect(() => { carregar() }, [carregar, versaoCobrancas])

  async function aplicarResultado(resultado: AlteracaoCondicoesResultDto, mensagem: string) {
    setCondicoes(resultado.condicoes)
    setPendencias(resultado.cobrancas.pendencias)
    toast.success(`${mensagem} ${resumirResultado(resultado.cobrancas)}`)
    await onCobrancasAlteradas()
  }

  async function excluirDesconto() {
    if (!descontoExcluido) return
    setExcluindo(true)
    try {
      const { data } = await platformBillingApi.excluirDesconto(tenantId, descontoExcluido.id)
      setDescontoExcluido(null)
      await aplicarResultado(data, 'Desconto excluído.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra excluir o desconto.'))
    } finally {
      setExcluindo(false)
    }
  }

  if (!condicoes) {
    return <div className="flex justify-center py-6"><Loader2 className="h-5 w-5 animate-spin text-brand-400" /></div>
  }

  return (
    <div className="space-y-4">
      {pendencias.length > 0 && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-3 text-sm text-amber-200">
          <p className="flex items-center gap-2 font-semibold">
            <AlertTriangle className="h-4 w-4 shrink-0" /> Algumas cobranças ficaram como estavam
          </p>
          <ul className="mt-1.5 list-disc space-y-0.5 pl-6 text-xs">
            {pendencias.map(p => <li key={p}>{p}</li>)}
          </ul>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <FormularioCondicoes
          key={JSON.stringify([condicoes.mensalidade, condicoes.inicioCobranca, condicoes.diaVencimento, condicoes.implantacao])}
          tenantId={tenantId}
          condicoes={condicoes}
          podeEditar={podeEditar}
          onSalvo={resultado => aplicarResultado(resultado, 'Condições salvas.')}
        />

        <section className="rounded-xl border border-surface-600 p-4">
          <div className="mb-3 flex items-center justify-between gap-2">
            <h3 className="flex items-center gap-2 font-semibold text-white">
              <Percent className="h-4 w-4 text-brand-400" /> Descontos
            </h3>
            {podeEditar && (
              <Button size="sm" variant="secondary" onClick={() => setDescontoEditado('novo')}>
                <Plus className="h-3.5 w-3.5" /> Adicionar
              </Button>
            )}
          </div>

          {condicoes.descontos.length === 0 ? (
            <p className="text-sm text-gray-400">
              Nenhum desconto. Eles se somam: os percentuais incidem juntos sobre a mensalidade,
              e depois os valores fixos são abatidos.
            </p>
          ) : (
            <ul className="space-y-2">
              {condicoes.descontos.map(d => (
                <li key={d.id} className={clsx(
                  'flex items-center justify-between gap-3 rounded-lg bg-surface-900 px-3 py-2',
                  d.situacao === 'Encerrado' && 'opacity-60',
                )}>
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-white">
                      {d.descricao} <span className="tabular-nums text-brand-300">−{valorDesconto(d)}</span>
                    </p>
                    <p className="text-xs text-gray-500">
                      {d.competenciaFinal
                        ? `${mesCurto(d.competenciaInicial)} a ${mesCurto(d.competenciaFinal)}`
                        : `a partir de ${mesCurto(d.competenciaInicial)}, sem fim`}
                      {' · '}
                      <span className={clsx(
                        d.situacao === 'Vigente' && 'text-accent-green',
                        d.situacao === 'Futuro' && 'text-sky-300',
                      )}>{d.situacao.toLowerCase()}</span>
                      {d.criadoPor && ` · por ${d.criadoPor}`}
                    </p>
                  </div>
                  {podeEditar && (
                    <div className="flex shrink-0 gap-1.5">
                      <Button size="sm" variant="secondary" onClick={() => setDescontoEditado(d)}
                        aria-label={`Editar desconto ${d.descricao}`}>
                        <Pencil className="h-3.5 w-3.5" />
                      </Button>
                      <Button size="sm" variant="secondary" onClick={() => setDescontoExcluido(d)}
                        aria-label={`Excluir desconto ${d.descricao}`}>
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <Previa condicoes={condicoes} />

      {descontoEditado && (
        <DescontoModal
          tenantId={tenantId}
          desconto={descontoEditado === 'novo' ? null : descontoEditado}
          onClose={() => setDescontoEditado(null)}
          onSalvo={async resultado => {
            setDescontoEditado(null)
            await aplicarResultado(resultado, 'Desconto salvo.')
          }}
        />
      )}

      {descontoExcluido && (
        <ConfirmDialog
          title="Excluir desconto"
          message={
            <>
              Excluir o desconto <strong>{descontoExcluido.descricao}</strong>? As cobranças em aberto
              deste mês em diante voltam ao valor sem ele. Cobranças já pagas não mudam.
              Para só encerrar a partir de um mês, edite o desconto e defina a duração.
            </>
          }
          confirmLabel="Excluir"
          loading={excluindo}
          onConfirm={excluirDesconto}
          onClose={() => setDescontoExcluido(null)}
        />
      )}
    </div>
  )
}

// ── Formulário das condições ─────────────────────────────────────────────────

function FormularioCondicoes({
  tenantId, condicoes, podeEditar, onSalvo,
}: {
  tenantId: string
  condicoes: CondicoesComerciaisDto
  podeEditar: boolean
  onSalvo: (resultado: AlteracaoCondicoesResultDto) => Promise<void>
}) {
  const impl = condicoes.implantacao
  const [mensalidade, setMensalidade] = useState(String(condicoes.mensalidade))
  const [inicio, setInicio] = useState(condicoes.inicioCobranca?.slice(0, 10) ?? '')
  const [dia, setDia] = useState(condicoes.diaVencimento ? String(condicoes.diaVencimento) : '')
  const [implValor, setImplValor] = useState(String(impl.valor))
  const [parcelas, setParcelas] = useState(String(impl.parcelas))
  const [primeiroVenc, setPrimeiroVenc] = useState(impl.primeiroVencimento?.slice(0, 10) ?? '')
  const [salvando, setSalvando] = useState(false)

  const implantacaoTravada = impl.lancadaManualmente
  const valorImplantacao = Number(implValor) || 0
  const qtdParcelas = Number(parcelas) || 1

  async function submeter(e: FormEvent) {
    e.preventDefault()
    setSalvando(true)
    try {
      const { data } = await platformBillingApi.salvarCondicoes(tenantId, {
        mensalidade: Number(mensalidade) || 0,
        inicioCobranca: inicio || null,
        diaVencimento: dia ? Number(dia) : null,
        implantacaoValor: valorImplantacao,
        implantacaoParcelas: qtdParcelas,
        implantacaoPrimeiroVencimento: primeiroVenc || null,
      })
      await onSalvo(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra salvar as condições.'))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <form onSubmit={submeter} className="rounded-xl border border-surface-600 p-4">
      <h3 className="mb-3 flex items-center gap-2 font-semibold text-white">
        <Handshake className="h-4 w-4 text-brand-400" /> Condições comerciais
      </h3>

      <fieldset disabled={!podeEditar || salvando} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-3">
          <label className="block text-sm font-medium text-gray-300">
            Mensalidade (R$)
            <input type="number" min="0" step="0.01" value={mensalidade}
              onChange={e => setMensalidade(e.target.value)} className="input mt-1.5 tabular-nums" />
          </label>
          <label className="block text-sm font-medium text-gray-300">
            1ª mensalidade
            <input type="date" value={inicio} onChange={e => setInicio(e.target.value)} className="input mt-1.5" />
          </label>
          <label className="block text-sm font-medium text-gray-300">
            Vence todo dia
            <select value={dia} onChange={e => setDia(e.target.value)} className="input mt-1.5">
              <option value="">{inicio ? `${Number(inicio.slice(8, 10))} (da 1ª)` : 'Mesmo da 1ª'}</option>
              {Array.from({ length: 31 }, (_, i) => i + 1).map(d => (
                <option key={d} value={d}>{d}</option>
              ))}
            </select>
          </label>
        </div>
        {Number(dia) > 28 && (
          <p className="-mt-2 text-xs text-gray-500">Em mês mais curto, vence no último dia.</p>
        )}

        <div className="border-t border-surface-600 pt-4">
          <p className="mb-2 text-sm font-medium text-gray-300">Implantação</p>
          {implantacaoTravada ? (
            <p className="rounded-lg bg-surface-900 p-3 text-xs text-gray-400">
              Esta loja já tem uma implantação lançada à mão, e ela vale como está. Para cobrar a
              implantação por aqui (com parcelamento), exclua antes aquela cobrança na lista abaixo.
            </p>
          ) : (
            <>
              <div className="grid gap-3 sm:grid-cols-3">
                <label className="block text-sm font-medium text-gray-300">
                  Valor total (R$)
                  <input type="number" min="0" step="0.01" value={implValor}
                    onChange={e => setImplValor(e.target.value)} className="input mt-1.5 tabular-nums" />
                </label>
                <label className="block text-sm font-medium text-gray-300">
                  Parcelas
                  <select value={parcelas} onChange={e => setParcelas(e.target.value)} className="input mt-1.5"
                    disabled={valorImplantacao <= 0}>
                    {Array.from({ length: 12 }, (_, i) => i + 1).map(n => (
                      <option key={n} value={n}>{n}×</option>
                    ))}
                  </select>
                </label>
                <label className="block text-sm font-medium text-gray-300">
                  1º vencimento
                  <input type="date" value={primeiroVenc} onChange={e => setPrimeiroVenc(e.target.value)}
                    className="input mt-1.5" disabled={valorImplantacao <= 0} required={valorImplantacao > 0} />
                </label>
              </div>
              <p className="mt-1.5 text-xs text-gray-500">
                {valorImplantacao > 0
                  ? `${qtdParcelas}× de ${brl(Math.floor((valorImplantacao / qtdParcelas) * 100) / 100)}, uma por mês`
                  : 'Zero = sem implantação.'}
                {impl.parcelasPagas > 0 && ` · ${impl.parcelasPagas} parcela(s) paga(s), ${brl(impl.valorPago)}: o saldo se divide entre as outras.`}
              </p>
            </>
          )}
        </div>
      </fieldset>

      {podeEditar && (
        <div className="mt-4 flex justify-end">
          <Button type="submit" loading={salvando}>Salvar condições</Button>
        </div>
      )}
    </form>
  )
}

// ── Desconto ─────────────────────────────────────────────────────────────────

function DescontoModal({
  tenantId, desconto, onClose, onSalvo,
}: {
  tenantId: string
  desconto: DescontoDto | null
  onClose: () => void
  onSalvo: (resultado: AlteracaoCondicoesResultDto) => Promise<void>
}) {
  const [descricao, setDescricao] = useState(desconto?.descricao ?? '')
  const [tipo, setTipo] = useState<TipoDesconto>(desconto?.tipo ?? 'Percentual')
  const [valor, setValor] = useState(desconto ? String(desconto.valor) : '')
  const [inicio, setInicio] = useState(desconto?.competenciaInicial.slice(0, 7) ?? mesLocalAtual())
  // Duração em meses, e não "mês final": "3 meses de boas-vindas" é como a
  // negociação é dita. Vazio = sem fim.
  const [meses, setMeses] = useState(
    desconto?.competenciaFinal ? String(mesesEntre(desconto.competenciaInicial, desconto.competenciaFinal)) : '')
  const [salvando, setSalvando] = useState(false)

  const qtdMeses = Number(meses) || 0
  const fim = qtdMeses > 0 && inicio ? somarMeses(inicio, qtdMeses - 1) : null

  async function submeter(e: FormEvent) {
    e.preventDefault()
    setSalvando(true)
    const body = {
      descricao: descricao.trim(),
      tipo,
      valor: Number(valor),
      competenciaInicial: `${inicio}-01`,
      competenciaFinal: fim ? `${fim}-01` : null,
    }
    try {
      const { data } = desconto
        ? await platformBillingApi.atualizarDesconto(tenantId, desconto.id, body)
        : await platformBillingApi.criarDesconto(tenantId, body)
      await onSalvo(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra salvar o desconto.'))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Modal onClose={onClose} maxWidth="md" closeOnBackdrop={false}
      title={desconto ? 'Editar desconto' : 'Novo desconto'} icon={Percent}>
      <form onSubmit={submeter} className="space-y-4 p-4">
        <label className="block text-sm font-medium text-gray-300">
          Nome
          <input required maxLength={60} value={descricao} onChange={e => setDescricao(e.target.value)}
            placeholder="Parceiro, Boas-vindas, Campanha..." className="input mt-2" />
          <span className="mt-1 block text-xs text-gray-500">Aparece na fatura do lojista, junto do valor.</span>
        </label>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-sm font-medium text-gray-300">Tipo</p>
            <div className="mt-2 grid grid-cols-2 gap-1 rounded-lg bg-surface-900 p-1" role="radiogroup">
              {(['Percentual', 'ValorFixo'] as const).map(t => (
                <button key={t} type="button" role="radio" aria-checked={tipo === t} onClick={() => setTipo(t)}
                  className={clsx('rounded-md py-1.5 text-sm font-medium transition-colors',
                    tipo === t ? 'bg-brand-500 text-white' : 'text-gray-400 hover:text-white')}>
                  {t === 'Percentual' ? '%' : 'R$'}
                </button>
              ))}
            </div>
          </div>
          <label className="block text-sm font-medium text-gray-300">
            {tipo === 'Percentual' ? 'Percentual' : 'Valor (R$)'}
            <input required type="number" min="0.01" step="0.01" max={tipo === 'Percentual' ? 100 : undefined}
              value={valor} onChange={e => setValor(e.target.value)} className="input mt-2 tabular-nums" />
          </label>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block text-sm font-medium text-gray-300">
            A partir de
            <input required type="month" value={inicio} onChange={e => setInicio(e.target.value)} className="input mt-2" />
          </label>
          <label className="block text-sm font-medium text-gray-300">
            Por quantos meses
            <input type="number" min="1" max="120" step="1" value={meses} onChange={e => setMeses(e.target.value)}
              placeholder="Sem fim" className="input mt-2 tabular-nums" />
          </label>
        </div>
        <p className="text-xs text-gray-500">
          {inicio && (fim
            ? `Vale de ${mesCurto(inicio)} a ${mesCurto(fim)}.`
            : `Vale a partir de ${mesCurto(inicio)}, até alguém encerrar.`)}
          {' '}Cobranças em aberto desses meses são recalculadas; as já pagas não mudam.
        </p>

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" loading={salvando}>{desconto ? 'Salvar desconto' : 'Adicionar desconto'}</Button>
        </div>
      </form>
    </Modal>
  )
}

// ── Prévia ───────────────────────────────────────────────────────────────────

const SITUACAO: Record<PreviaItemDto['situacao'], { rotulo: string; classe: string }> = {
  Prevista:    { rotulo: 'prevista',     classe: 'text-gray-400' },
  SemCobranca: { rotulo: 'sem cobrança', classe: 'text-gray-500' },
  EmAberto:    { rotulo: 'gerada',       classe: 'text-amber-300' },
  Emitida:     { rotulo: 'emitida',      classe: 'text-sky-300' },
  Paga:        { rotulo: 'paga',         classe: 'text-accent-green' },
}

function Previa({ condicoes }: { condicoes: CondicoesComerciaisDto }) {
  // Meses vazios no fim (loja sem mensalidade, implantação já quitada) só
  // alongariam a tabela.
  const meses = useMemo(() => {
    const ultimoComItem = condicoes.previa.map(m => m.itens.length > 0).lastIndexOf(true)
    return condicoes.previa.slice(0, Math.max(ultimoComItem + 1, 1))
  }, [condicoes.previa])

  return (
    <section className="rounded-xl border border-surface-600 p-4">
      <h3 className="mb-1 flex items-center gap-2 font-semibold text-white">
        <CalendarClock className="h-4 w-4 text-brand-400" /> Próximos meses
      </h3>
      <p className="mb-3 text-xs text-gray-500">
        O que as condições vão cobrar. Cada mês é gerado e enviado ao Asaas sozinho, na virada.
      </p>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[520px] text-sm">
          <thead>
            <tr className="border-b border-surface-600 text-left text-xs uppercase tracking-wide text-gray-500">
              <th className="py-2 pr-3 font-medium">Mês</th>
              <th className="py-2 pr-3 font-medium">Cobranças</th>
              <th className="py-2 text-right font-medium">Total</th>
            </tr>
          </thead>
          <tbody>
            {meses.map(mes => (
              <tr key={mes.competencia} className="border-b border-surface-700 align-top last:border-0">
                <td className="whitespace-nowrap py-2.5 pr-3 font-medium text-white">{mesCurto(mes.competencia)}</td>
                <td className="py-2.5 pr-3">
                  {mes.itens.length === 0 ? (
                    <span className="text-gray-500">—</span>
                  ) : (
                    <ul className="space-y-1">
                      {mes.itens.map(item => (
                        <li key={`${item.tipo}-${item.descricao}`} className="flex flex-wrap items-baseline gap-x-2">
                          <span className="text-gray-300">{item.descricao}</span>
                          <span className="tabular-nums text-white">{brl(item.valor)}</span>
                          {item.desconto > 0 && item.valorBruto !== null && (
                            <span className="text-xs text-gray-500">
                              <s>{brl(item.valorBruto)}</s> {item.descricaoDesconto}
                            </span>
                          )}
                          <span className="text-xs text-gray-500">vence {dataCurta(item.vencimento)}</span>
                          <span className={clsx('text-xs', SITUACAO[item.situacao].classe)}>
                            {SITUACAO[item.situacao].rotulo}{item.manual ? ' · manual' : ''}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </td>
                <td className="whitespace-nowrap py-2.5 text-right font-semibold tabular-nums text-white">
                  {brl(mes.total)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
