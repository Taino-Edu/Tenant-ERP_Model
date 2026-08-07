'use client'
import { useEffect, useState } from 'react'
import clsx from 'clsx'

interface NumberInputProps {
  value: number | null
  onChange: (value: number | null) => void
  /** Valor quando o campo é esvaziado e perde o foco. `null` deixa vazio. */
  fallback?: number | null
  min?: number
  max?: number
  step?: number | string
  placeholder?: string
  disabled?: boolean
  className?: string
  id?: string
  autoFocus?: boolean
  title?: string
  /** Casas decimais aceitas. 0 = só inteiro (padrão). */
  decimals?: number
}

/**
 * Campo numérico que aceita ficar VAZIO enquanto o usuário digita.
 *
 * O padrão que isto substitui era `value={n}` + `onChange={e =>
 * set(Number(e.target.value))}`, espalhado por ~20 telas. Ele tem dois defeitos
 * que aparecem juntos e que o usuário sente como um só:
 *
 *  1. `Number('')` é `0`. Apagar o conteúdo do campo grava 0 no estado, que
 *     volta como "0" na tela — o zero é **impossível de apagar**, ele renasce a
 *     cada tecla.
 *  2. Com "0" preso no campo, digitar 5 produz "05" ou "50" conforme onde o
 *     cursor estava. O número certo depende de o usuário lembrar de selecionar
 *     tudo antes de digitar.
 *
 * A correção é separar o que está DIGITADO do que está VALIDADO: o texto vive
 * aqui dentro e pode ser "", "-" ou "1." (estados intermediários legítimos de
 * quem está digitando); o estado do formulário só recebe número quando há
 * número. O `fallback` fecha o ciclo no blur, para o campo não ficar vazio
 * depois que o usuário sai dele.
 *
 * Também desliga a roda do mouse sobre o campo: com `type="number"` focado, um
 * scroll na página altera o valor sem ninguém perceber — em campo de preço ou
 * de quantidade isso vira erro silencioso de cadastro.
 */
export default function NumberInput({
  value, onChange, fallback = null,
  min, max, step, placeholder, disabled, className, id, decimals = 0,
  autoFocus, title,
}: NumberInputProps) {
  const [texto, setTexto] = useState(value === null ? '' : String(value))

  // Mudança vinda de FORA (carregou do servidor, resetou o formulário) precisa
  // aparecer. Comparar pelo número, não pelo texto: enquanto o usuário digita
  // "1." o valor já é 1, e reescrever o texto aqui apagaria o ponto no meio da
  // digitação.
  useEffect(() => {
    const atual = texto.trim() === '' ? null : Number(texto)
    if (atual !== value && !(Number.isNaN(atual as number) && value === null))
      setTexto(value === null ? '' : String(value))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value])

  function handleChange(bruto: string) {
    const limpo = decimals > 0 ? bruto.replace(',', '.') : bruto
    const padrao = decimals > 0 ? /^-?\d*\.?\d*$/ : /^-?\d*$/
    if (limpo !== '' && !padrao.test(limpo)) return   // ignora caractere inválido

    setTexto(limpo)

    if (limpo === '' || limpo === '-' || limpo.endsWith('.')) {
      // Estado intermediário: ainda não é número, mas é digitação legítima.
      // Só reporta null pra quem tolera vazio; senão espera o blur.
      if (limpo === '') onChange(null)
      return
    }
    const n = Number(limpo)
    if (!Number.isNaN(n)) onChange(n)
  }

  function handleBlur() {
    if (texto.trim() === '' || texto === '-') {
      setTexto(fallback === null ? '' : String(fallback))
      onChange(fallback)
      return
    }
    let n = Number(texto)
    if (Number.isNaN(n)) { setTexto(fallback === null ? '' : String(fallback)); onChange(fallback); return }
    // Limites só no blur: aplicar durante a digitação impede chegar em "25"
    // quando o mínimo é 10, porque o "2" sozinho já seria corrigido pra 10.
    if (min !== undefined && n < min) n = min
    if (max !== undefined && n > max) n = max
    setTexto(String(n))
    onChange(n)
  }

  return (
    <input
      id={id}
      type="text"
      inputMode={decimals > 0 ? 'decimal' : 'numeric'}
      value={texto}
      onChange={e => handleChange(e.target.value)}
      onBlur={handleBlur}
      min={min}
      max={max}
      step={step}
      placeholder={placeholder}
      disabled={disabled}
      autoFocus={autoFocus}
      title={title}
      className={clsx('input', className)}
    />
  )
}
