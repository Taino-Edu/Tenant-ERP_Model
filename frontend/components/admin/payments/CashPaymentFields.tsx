'use client'

import { useEffect, useState } from 'react'
import { Banknote, Coins } from 'lucide-react'
import clsx from 'clsx'

export type CashPaymentState = {
  cashReceivedInCents: number
  changeInCents: number
  roundingDiscountInCents: number
  valid: boolean
}

const money = (cents: number) =>
  (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

/**
 * Captura o numerário real. O arredondamento eleva o troco ao próximo múltiplo
 * de R$ 0,05 e vira desconto — nunca reduz o troco do consumidor.
 */
export default function CashPaymentFields({
  cashDueInCents, onChange,
}: {
  cashDueInCents: number
  onChange: (state: CashPaymentState) => void
}) {
  const [received, setReceived] = useState(() => (cashDueInCents / 100).toFixed(2))
  const [rounding, setRounding] = useState(false)

  const receivedCents = Math.max(0, Math.round((parseFloat(received.replace(',', '.')) || 0) * 100))
  const exactChange = Math.max(0, receivedCents - cashDueInCents)
  const roundingDiscount = rounding && receivedCents >= cashDueInCents
    ? (5 - (exactChange % 5)) % 5
    : 0
  const change = Math.max(0, receivedCents - (cashDueInCents - roundingDiscount))
  const valid = cashDueInCents > 0 && receivedCents >= cashDueInCents

  useEffect(() => {
    onChange({
      cashReceivedInCents: receivedCents,
      changeInCents: change,
      roundingDiscountInCents: roundingDiscount,
      valid,
    })
  }, [receivedCents, change, roundingDiscount, valid, onChange])

  return (
    <div className="space-y-3 rounded-xl border border-emerald-500/25 bg-emerald-500/5 p-3">
      <div>
        <label className="mb-1 flex items-center gap-1.5 text-xs font-semibold text-emerald-300">
          <Banknote className="h-3.5 w-3.5" /> Valor entregue em dinheiro
        </label>
        <input
          type="number" min="0" step="0.01" inputMode="decimal"
          className="input w-full font-mono text-sm"
          value={received}
          onChange={e => setReceived(e.target.value)}
        />
        <p className="mt-1 text-[11px] text-gray-500">Devido em dinheiro: {money(cashDueInCents)}</p>
      </div>

      <div className={clsx(
        'flex items-center justify-between rounded-lg px-3 py-2 text-sm font-semibold',
        valid ? 'bg-emerald-500/10 text-emerald-300' : 'bg-red-500/10 text-red-400',
      )}>
        <span>{valid ? 'Troco' : 'Falta'}</span>
        <span className="font-mono">{money(valid ? change : cashDueInCents - receivedCents)}</span>
      </div>

      {valid && exactChange % 5 !== 0 && (
        <button
          type="button"
          onClick={() => setRounding(v => !v)}
          className={clsx(
            'flex w-full items-start gap-2 rounded-lg border px-3 py-2 text-left text-xs transition-colors',
            rounding
              ? 'border-amber-400/40 bg-amber-500/10 text-amber-200'
              : 'border-surface-500 text-gray-400 hover:border-amber-400/30',
          )}
        >
          <Coins className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>
            <strong className="block">Sem moedas para o troco exato</strong>
            Arredondar em favor do cliente para {money(exactChange + ((5 - exactChange % 5) % 5))}
            {roundingDiscount > 0 && ` (desconto adicional de ${money(roundingDiscount)})`}.
          </span>
        </button>
      )}
    </div>
  )
}
