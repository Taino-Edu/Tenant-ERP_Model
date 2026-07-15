// =============================================================================
// shared.ts — Helpers e tipos compartilhados entre os componentes de Comanda
// extraídos de app/admin/comanda/page.tsx. Não é lib global de propósito —
// só existe pra não duplicar essas poucas funções entre os arquivos irmãos.
// =============================================================================

import toast from 'react-hot-toast'
import { ComandaDto } from '@/lib/api'

export const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`

/** Data de hoje no fuso de Brasília como YYYY-MM-DD (nunca usa UTC). */
export const brToday = () => new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())

export function elapsedLabel(openedAt: string) {
  const mins = Math.floor((Date.now() - new Date(openedAt).getTime()) / 60000)
  return mins < 60 ? `${mins}min` : `${Math.floor(mins / 60)}h${mins % 60}min`
}

export function elapsedColor(openedAt: string) {
  const mins = Math.floor((Date.now() - new Date(openedAt).getTime()) / 60000)
  if (mins < 20) return 'text-accent-green'
  if (mins < 45) return 'text-amber-400'
  return 'text-red-400'
}

/** Após um fechamento com "Emitir cupom fiscal" marcado: abre o cupom sozinho se autorizou,
 * ou avisa o motivo se rejeitou/ficou pendente (SEFAZ fora do ar — o retry automático tenta de novo). */
export function handleNotaFiscalResult(notaId?: string | null, status?: string | null, motivo?: string | null) {
  if (!status) return
  if (status === 'Autorizada' && notaId) {
    window.open(`/admin/fiscal/cupom/${notaId}`, '_blank')
  } else {
    toast.error(`Nota fiscal não autorizou ainda (${status})${motivo ? ' — ' + motivo : ''}. O sistema tenta de novo automaticamente.`)
  }
}

/** Comprovante térmico (80mm) do fechamento de comanda — mesmo padrão de
 * frontend/app/admin/venda-avulsa/page.tsx, pra quem fecha sem emitir NFC-e
 * (loja sem módulo Fiscal, ou nota não marcada) ainda sair com algum papel. */
export function printComandaReceiptPDF(comanda: ComandaDto, payLabel: string, siteName: string) {
  const w = window.open('', '_blank', 'width=420,height=640')
  if (!w) { alert('Permita pop-ups para gerar o PDF'); return }

  const date = new Date(comanda.closedAt ?? Date.now()).toLocaleString('pt-BR')
  const itemsHTML = comanda.items.map(it => `
    <tr>
      <td>${it.quantity}× ${it.itemNameSnapshot}</td>
      <td align="right">R$&nbsp;${it.subtotalInReais.toFixed(2).replace('.', ',')}</td>
    </tr>
  `).join('')

  const discountRow = comanda.discountInCents > 0 ? `
    <tr style="color:#16a34a;">
      <td>Desconto</td>
      <td align="right">−R$&nbsp;${(comanda.discountInCents / 100).toFixed(2).replace('.', ',')}</td>
    </tr>
  ` : ''

  w.document.write(`<!DOCTYPE html>
<html lang="pt-BR"><head>
<meta charset="UTF-8">
<title>Comprovante — ${siteName}</title>
<style>
  @page { size: 80mm auto; margin: 6mm; }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Courier New', monospace; font-size: 11px; color: #111; padding: 8px; }
  h1 { font-size: 15px; text-align: center; letter-spacing: 1px; }
  .sub { text-align: center; font-size: 10px; color: #555; margin-bottom: 8px; }
  hr { border: none; border-top: 1px dashed #999; margin: 6px 0; }
  table { width: 100%; border-collapse: collapse; }
  td { padding: 2px 0; vertical-align: top; }
  .total-row td { font-size: 14px; font-weight: bold; padding-top: 4px; }
  .payment { font-size: 10px; color: #555; margin-top: 4px; }
  .footer { text-align: center; font-size: 10px; color: #777; margin-top: 10px; }
  @media print { body { padding: 0; } }
</style>
</head><body>
<h1>${siteName}</h1>
<p class="sub">${date}</p>
<p class="sub">Cliente: <strong>${comanda.userName}</strong>${comanda.tableIdentifier ? ` — ${comanda.tableIdentifier}` : ''}</p>
<hr>
<table>
  ${itemsHTML}
  ${discountRow}
</table>
<hr>
<table>
  <tr class="total-row">
    <td>TOTAL</td>
    <td align="right">R$&nbsp;${comanda.totalInReais.toFixed(2).replace('.', ',')}</td>
  </tr>
</table>
<p class="payment">Pagamento: ${payLabel}</p>
<hr>
<p class="footer">Obrigado pela preferência!</p>
<script>window.onload = function() { window.print(); }<\/script>
</body></html>`)
  w.document.close()
}

// Métodos aceitos como segundo pagamento (Crediario não faz sentido como secundário)
export const SECOND_PAYMENT_METHODS = [
  { value: 'Cashback',      label: 'Cashback (Saldo)' },
  { value: 'Pontos',        label: 'Pontos de Fidelidade' },
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'Pix',           label: 'Pix' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
]

export interface EditItemState {
  id?: string         // undefined = novo item
  productId?: string
  itemName: string
  unitPriceInCents: number
  quantity: number
  remover: boolean
}
