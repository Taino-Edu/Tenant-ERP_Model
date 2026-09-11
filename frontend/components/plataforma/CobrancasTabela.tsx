'use client'

// =============================================================================
// CobrancasTabela — a lista de cobranças da plataforma, com as ações de cada uma.
//
// Existe porque a mesma lista passou a aparecer em dois lugares: no Financeiro
// (todas as lojas de um mês) e na aba Cobranças da loja (todos os meses de uma
// loja). Copiar tabela e ações para a segunda tela repetiria o problema que o
// próprio Financeiro já tinha resolvido uma vez: duas marcações da mesma lista,
// iguais só enquanto alguém lembrar de mudar as duas.
// =============================================================================

import { useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, Check, ExternalLink, Pencil, Trash2, Undo2 } from 'lucide-react'
import { getErrorMessage, platformBillingApi, type TenantChargeDto } from '@/lib/api'
import Button from '@/components/admin/ui/Button'
import ConfirmDialog from '@/components/admin/ui/ConfirmDialog'
import DataTable, { type Column } from '@/components/admin/ui/DataTable'
import CobrancaFormModal from '@/components/plataforma/CobrancaFormModal'

export const brl = (v: number) =>
  v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

/** Data só-dia, sem fuso: as datas de cobrança vêm em UTC 00:00 e o
 *  toLocaleDateString do navegador jogaria pro dia anterior em fusos negativos
 *  (o Brasil inteiro). Formatar a partir das partes evita o vencimento aparecer
 *  um dia antes do que é. */
export function dataCurta(iso: string): string {
  const [ano, mes, dia] = iso.slice(0, 10).split('-')
  return `${dia}/${mes}/${ano}`
}

const mesAno = (iso: string) => iso.slice(0, 7).split('-').reverse().join('/')

const rotuloTipo = (c: TenantChargeDto) => c.tipo === 'Implantacao' ? 'Implantação' : 'Mensalidade'

export default function CobrancasTabela({
  cobrancas, podeLancar, mostrarLoja = true, onAlterado, className,
}: {
  cobrancas: TenantChargeDto[]
  /** `platform.finance.manage`. Sem ela a lista é só leitura. */
  podeLancar: boolean
  /** No Financeiro a lista mistura lojas de um mês; na página da loja, meses de
   *  uma loja. A primeira coluna troca de acordo. */
  mostrarLoja?: boolean
  /** Chamado depois de dar baixa, reabrir, alterar ou excluir. */
  onAlterado: () => void | Promise<void>
  className?: string
}) {
  const [salvandoId, setSalvandoId] = useState<string | null>(null)
  const [editando, setEditando]     = useState<TenantChargeDto | null>(null)
  const [excluindo, setExcluindo]   = useState<TenantChargeDto | null>(null)
  const [removendo, setRemovendo]   = useState(false)

  async function alternarPagamento(c: TenantChargeDto) {
    setSalvandoId(c.id)
    try {
      const hoje = new Date().toISOString().slice(0, 10)
      await platformBillingApi.definirPagamento(c.id, c.pagoEm ? null : hoje)
      await onAlterado()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra atualizar a cobrança.'))
    } finally {
      setSalvandoId(null)
    }
  }

  async function excluirCobranca() {
    if (!excluindo) return
    setRemovendo(true)
    try {
      await platformBillingApi.excluirCobranca(excluindo.id)
      toast.success('Cobrança excluída.')
      setExcluindo(null)
      await onAlterado()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não deu pra excluir a cobrança.'))
    } finally {
      setRemovendo(false)
    }
  }

  const primeiraColuna: Column<TenantChargeDto> = mostrarLoja
    ? {
        key: 'loja', header: 'Loja', mobile: 'title',
        cell: c => (
          <>
            <p className="font-medium text-white">{c.tenantNome}</p>
            <p className="text-xs text-gray-500">{c.tenantSlug}</p>
          </>
        ),
      }
    : {
        key: 'competencia', header: 'Competência', mobile: 'title',
        cell: c => <p className="font-medium text-white tabular-nums">{mesAno(c.competencia)}</p>,
      }

  return (
    <>
      <DataTable
        className={className}
        rows={cobrancas}
        rowKey={c => c.id}
        // `undefined` e não uma função que devolve null: é assim que o
        // DataTable deixa de reservar a coluna de ações inteira.
        rowActions={podeLancar ? c => (
          <div className="flex items-center gap-1.5">
            <AcaoPagamento c={c} salvando={salvandoId === c.id} onClick={() => alternarPagamento(c)} />
            {/* Editar e excluir só aparecem em cobrança EM ABERTO. A API
                recusa as duas numa cobrança paga — a baixa já pode ter
                liberado comissão de parceiro —, e oferecer o botão para
                depois devolver erro é pior que não oferecer. */}
            {!c.pagoEm && (
              <>
                <Button size="sm" variant="secondary" onClick={() => setEditando(c)}
                  aria-label={`Editar cobrança de ${c.tenantNome}`}>
                  <Pencil className="h-3.5 w-3.5" />
                </Button>
                <Button size="sm" variant="secondary" onClick={() => setExcluindo(c)}
                  aria-label={`Excluir cobrança de ${c.tenantNome}`}>
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </>
            )}
          </div>
        ) : undefined}
        columns={[
          primeiraColuna,
          { key: 'valor', header: 'Valor', align: 'right', mobile: 'trailing',
            cell: c => <span className="font-semibold tabular-nums text-white">{brl(c.valor)}</span> },
          { key: 'tipo', header: 'Tipo', mobile: 'meta', className: 'text-gray-300', cell: rotuloTipo },
          { key: 'vencimento', header: 'Vencimento', mobile: 'meta', className: 'text-gray-300',
            cell: c => <>vence {dataCurta(c.vencimento)}</> },
          { key: 'situacao', header: 'Situação', mobile: 'field', cell: c => <Situacao c={c} /> },
          { key: 'fatura', header: 'Fatura', mobile: 'field', cell: c => <Fatura c={c} /> },
        ]}
      />

      {editando && (
        <CobrancaFormModal
          cobranca={editando}
          competencia={editando.competencia.slice(0, 7)}
          onClose={() => setEditando(null)}
          onSalvo={onAlterado}
        />
      )}

      {excluindo && (
        <ConfirmDialog
          title="Excluir cobrança"
          message={
            <>
              Excluir a cobrança de <strong>{brl(excluindo.valor)}</strong> de{' '}
              <strong>{excluindo.tenantNome}</strong>? A cobrança some do histórico da loja
              e do faturado do mês. Isso não pode ser desfeito.
              {excluindo.emitidaNoGateway && (
                <> Ela <strong>já foi emitida no Asaas</strong>: cancele a fatura lá também,
                senão o lojista continua podendo pagá-la.</>
              )}
            </>
          }
          confirmLabel="Excluir"
          loading={removendo}
          onConfirm={excluirCobranca}
          onClose={() => setExcluindo(null)}
        />
      )}
    </>
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

function Fatura({ c }: { c: TenantChargeDto }) {
  if (c.linkPagamento) {
    return (
      <a href={c.linkPagamento} target="_blank" rel="noreferrer"
        className="inline-flex items-center gap-1 text-xs font-semibold text-brand-300 hover:text-brand-200">
        <ExternalLink className="h-3 w-3" /> Abrir fatura
      </a>
    )
  }
  if (c.emitidaNoGateway) return <span className="text-xs text-gray-300">Emitida</span>
  // Cobrança paga ou de R$ 0 (cortesia) nunca vai ao gateway: "não emitida"
  // nelas seria alarme falso.
  if (c.pagoEm || c.valor === 0) return <span className="text-xs text-gray-500">—</span>
  return <span className="text-xs font-semibold text-amber-300">Não emitida</span>
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
