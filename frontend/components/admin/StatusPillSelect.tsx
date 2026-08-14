'use client'
// =============================================================================
// StatusPillSelect.tsx — Select de status que já É o badge colorido, em vez
// de um <select> nativo ao lado de uma pill redundante mostrando o mesmo
// texto duas vezes.
// =============================================================================

import clsx from 'clsx'

export default function StatusPillSelect<T extends string>({
  value, options, styles, disabled, onChange,
}: {
  value: T
  options: readonly T[]
  styles: Record<T, string>
  disabled?: boolean
  onChange: (v: T) => void
}) {
  return (
    <select
      value={value}
      disabled={disabled}
      onChange={e => onChange(e.target.value as T)}
      className={clsx(
        'text-xs font-medium pl-2.5 pr-6 py-1 rounded-full border cursor-pointer',
        // Alvo de toque: como pill de `py-1` o controle fica com 33px de altura,
        // abaixo do mínimo confortável — e ele muda o status de PAGAMENTO de uma
        // loja, então errar o alvo tem consequência. `min-h` em vez de padding
        // maior porque o formato de pill (raio total) tem que ser preservado.
        // Só no celular: no desktop a densidade da tabela é intencional.
        'max-md:min-h-[44px]',
        'disabled:opacity-60 disabled:cursor-not-allowed',
        styles[value],
      )}
    >
      {options.map(o => (
        <option key={o} value={o} className="bg-surface-800 text-white">{o}</option>
      ))}
    </select>
  )
}
