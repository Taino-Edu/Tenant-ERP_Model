// Utilitários de cor compartilhados — antes duplicados em app/page.tsx e
// app/admin/site/page.tsx.

/** Mistura duas cores hex (`a` com `ratio` de `b`) — ex: mixHex('#3EC2F2', '#ffffff', 0.2). */
export function mixHex(a: string, b: string, ratio: number): string {
  const parse = (h: string) => {
    const m = /^#?([0-9a-f]{6})$/i.exec(h.trim())
    if (!m) return null
    const n = parseInt(m[1], 16)
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255]
  }
  const pa = parse(a), pb = parse(b)
  if (!pa || !pb) return a
  const mix = pa.map((c, i) => Math.round(c * (1 - ratio) + pb[i] * ratio))
  return '#' + mix.map(c => c.toString(16).padStart(2, '0')).join('')
}

/** Converte hex em "R G B" (espaço-separado) — formato exigido pelo padrão
 * `rgb(var(--x) / <alpha-value>)` do Tailwind pra cor dinâmica com suporte a opacidade. */
export function hexToRgbTriplet(hex: string): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return '62 194 242' // fallback = brand-500 padrão (#3EC2F2)
  const n = parseInt(m[1], 16)
  return `${(n >> 16) & 255} ${(n >> 8) & 255} ${n & 255}`
}

/** Deriva os tons 400/600 a partir da cor primária configurada pelo tenant —
 * não reproduz pixel-a-pixel a paleta estática original (esses tons eram
 * escolhidos à mão, não derivados de uma fórmula), mas mantém a mesma relação
 * "mais claro/mais escuro" pra qualquer cor base configurada. */
export function deriveBrandRamp(base: string): { 400: string; 500: string; 600: string } {
  return {
    400: mixHex(base, '#ffffff', 0.28),
    500: base,
    600: mixHex(base, '#000000', 0.18),
  }
}

/** Pipeline completo: cor primária configurada → mapa de CSS custom properties
 * pronto pra aplicar via style.setProperty (ou cachear em localStorage). */
export function brandCssVars(primaryHex: string): Record<'--brand-400' | '--brand-500' | '--brand-600', string> {
  const ramp = deriveBrandRamp(primaryHex)
  return {
    '--brand-400': hexToRgbTriplet(ramp[400]),
    '--brand-500': hexToRgbTriplet(ramp[500]),
    '--brand-600': hexToRgbTriplet(ramp[600]),
  }
}

/** Determina se uma cor hexadecimal é escura ou clara (YIQ). */
export function isDark(hex: string): boolean {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return false
  const n = parseInt(m[1], 16)
  const r = (n >> 16) & 255
  const g = (n >> 8) & 255
  const b = n & 255
  const yiq = ((r * 299) + (g * 587) + (b * 114)) / 1000
  return yiq < 128
}

/** Retorna a melhor cor de texto (branca ou escura) para contrastar com o fundo. */
export function getContrastText(bgHex: string, darkTextColor = '#0C3D5A', lightTextColor = '#FFFFFF'): string {
  return isDark(bgHex) ? lightTextColor : darkTextColor
}

