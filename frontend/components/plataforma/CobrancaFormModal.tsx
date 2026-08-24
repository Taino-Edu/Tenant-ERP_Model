'use client'

// =============================================================================
// CobrancaFormModal — lançamento e alteração manual de cobrança da plataforma.
//
// Um componente para os dois casos porque os campos são quase os mesmos e o que
// muda é pequeno e explícito: ao EDITAR, loja, tipo e competência viram texto.
// Os três compõem o índice único que impede cobrar o mesmo mês duas vezes
// (ix_tenant_charges_tenant_kind_competencia); deixá-los editáveis
// transformaria um ajuste de valor numa colisão de chave descoberta no meio do
// fluxo. Cobrança emitida no mês errado se exclui e se refaz.
// =============================================================================

import { useState, type FormEvent } from 'react'
import toast from 'react-hot-toast'
import { CircleDollarSign } from 'lucide-react'
import { platformBillingApi, getErrorMessage, type TenantChargeDto, type TenantSummary } from '@/lib/api'
import Button from '@/components/admin/ui/Button'
import Modal from '@/components/admin/ui/Modal'

/** "2026-07-01" a partir de "2026-07". */
const primeiroDia = (mes: string) => `${mes}-01`

/** Último dia do mês seguinte à competência: é o vencimento que o gerador
 *  automático usa como referência e o palpite certo na esmagadora maioria dos
 *  lançamentos manuais. */
function vencimentoSugerido(mes: string): string {
  const [ano, m] = mes.split('-').map(Number)
  const proximo = new Date(Date.UTC(ano, m, 10))
  return proximo.toISOString().slice(0, 10)
}

export default function CobrancaFormModal({
  cobranca, lojas, competencia, onClose, onSalvo,
}: {
  /** null = nova cobrança; preenchido = alteração. */
  cobranca: TenantChargeDto | null
  lojas: TenantSummary[]
  /** Competência aberta na tela, no formato "2026-07". */
  competencia: string
  onClose: () => void
  onSalvo: () => void
}) {
  const editando = cobranca !== null

  const [tenantId, setTenantId]     = useState(cobranca?.tenantId ?? '')
  const [tipo, setTipo]             = useState<'Mensalidade' | 'Implantacao'>(
    (cobranca?.tipo as 'Mensalidade' | 'Implantacao') ?? 'Mensalidade')
  const [valor, setValor]           = useState(cobranca ? String(cobranca.valor) : '')
  const [vencimento, setVencimento] = useState(
    cobranca ? cobranca.vencimento.slice(0, 10) : vencimentoSugerido(competencia))
  const [observacao, setObservacao] = useState(cobranca?.observacao ?? '')
  const [salvando, setSalvando]     = useState(false)

  async function submeter(event: FormEvent) {
    event.preventDefault()
    const valorNumerico = Number(valor.replace(',', '.'))
    if (!Number.isFinite(valorNumerico) || valorNumerico < 0) {
      toast.error('Informe um valor válido.')
      return
    }

    setSalvando(true)
    try {
      if (editando) {
        await platformBillingApi.atualizarCobranca(cobranca.id, {
          valor: valorNumerico, vencimento, observacao: observacao.trim() || undefined,
        })
        toast.success('Cobrança alterada.')
      } else {
        await platformBillingApi.criarCobranca({
          tenantId, tipo, valor: valorNumerico,
          competencia: primeiroDia(competencia), vencimento,
          observacao: observacao.trim() || undefined,
        })
        toast.success('Cobrança lançada.')
      }
      onSalvo()
      onClose()
    } catch (err) {
      // A API devolve a razão pronta e específica ("já existe uma cobrança de
      // Mensalidade para esta loja em 03/2026"), então ela vai inteira para a
      // tela em vez de virar um "não foi possível salvar" genérico.
      toast.error(getErrorMessage(err, 'Não deu pra salvar a cobrança.'))
    } finally {
      setSalvando(false)
    }
  }

  const rotuloCompetencia = editando
    ? cobranca.competencia.slice(0, 7).split('-').reverse().join('/')
    : competencia.split('-').reverse().join('/')

  return (
    <Modal onClose={onClose} maxWidth="md" closeOnBackdrop={false}
      title={editando ? 'Alterar cobrança' : 'Nova cobrança'} icon={CircleDollarSign}>
      <form onSubmit={submeter} className="space-y-4 p-4">
        {editando ? (
          <div className="rounded-xl border border-surface-600 bg-surface-900 p-3 text-sm">
            <p className="font-medium text-white">{cobranca.tenantNome}</p>
            <p className="text-xs text-gray-500">
              {cobranca.tipo === 'Implantacao' ? 'Implantação' : 'Mensalidade'} · competência {rotuloCompetencia}
            </p>
            <p className="mt-2 text-xs text-gray-500">
              Loja, tipo e competência não mudam aqui. Se algum deles estiver errado,
              exclua esta cobrança e lance outra.
            </p>
          </div>
        ) : (
          <>
            <label className="block text-sm font-medium text-gray-300">
              Loja
              <select required value={tenantId} onChange={e => setTenantId(e.target.value)}
                className="input mt-2 w-full">
                <option value="" disabled>Selecione a loja</option>
                {/* `TenantSummary` não traz o nome de exibição, só o slug — que
                    é como a loja é identificada no resto deste painel. O plano
                    vai junto porque duas lojas de nome parecido são o caso em
                    que se lança cobrança na errada. */}
                {lojas.map(loja => (
                  <option key={loja.id} value={loja.id}>
                    {loja.slug}{loja.planName ? ` · ${loja.planName}` : ''}
                  </option>
                ))}
              </select>
              {lojas.length === 0 && (
                <span className="mt-1 block text-xs text-amber-300">
                  Não consegui carregar a lista de lojas. Feche e abra de novo.
                </span>
              )}
            </label>

            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block text-sm font-medium text-gray-300">
                Tipo
                <select value={tipo} onChange={e => setTipo(e.target.value as typeof tipo)}
                  className="input mt-2 w-full">
                  <option value="Mensalidade">Mensalidade</option>
                  <option value="Implantacao">Implantação</option>
                </select>
              </label>
              <label className="block text-sm font-medium text-gray-300">
                Competência
                <input readOnly value={rotuloCompetencia} className="input mt-2 w-full opacity-70" />
                <span className="mt-1 block text-xs text-gray-500">
                  É o mês aberto na tela. Troque lá em cima para lançar em outro.
                </span>
              </label>
            </div>
          </>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block text-sm font-medium text-gray-300">
            Valor (R$)
            <input required type="number" min="0" step="0.01" value={valor}
              onChange={e => setValor(e.target.value)} placeholder="269,00"
              className="input mt-2 w-full tabular-nums" />
            <span className="mt-1 block text-xs text-gray-500">
              Zero é aceito: registra cortesia sem sumir do histórico da loja.
            </span>
          </label>
          <label className="block text-sm font-medium text-gray-300">
            Vencimento
            <input required type="date" value={vencimento}
              onChange={e => setVencimento(e.target.value)} className="input mt-2 w-full" />
          </label>
        </div>

        <label className="block text-sm font-medium text-gray-300">
          Observação
          <input maxLength={500} value={observacao} onChange={e => setObservacao(e.target.value)}
            placeholder="Por que esta cobrança existe ou foi alterada"
            className="input mt-2 w-full" />
          <span className="mt-1 block text-xs text-gray-500">
            Opcional, mas é o que explica o lançamento para quem abrir daqui a seis meses.
          </span>
        </label>

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" loading={salvando}>
            {editando ? 'Salvar alteração' : 'Lançar cobrança'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
