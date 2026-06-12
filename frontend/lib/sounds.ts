// =============================================================================
// sounds.ts — Sons do sistema via Web Audio API (sem arquivos externos)
// =============================================================================

// Reutiliza um único AudioContext (browsers limitam a quantidade)
let _ctx: AudioContext | null = null

function getAudioContext(): AudioContext | null {
  try {
    if (!_ctx || _ctx.state === 'closed') {
      _ctx = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)()
    }
    // Resume se estava suspenso (política de autoplay do browser)
    if (_ctx.state === 'suspended') {
      _ctx.resume()
    }
    return _ctx
  } catch {
    return null
  }
}

// ── Som de nova comanda — trinca suave ascendente (~0.45s) ───────────────────
export function playGoalSound() {
  const ctx = getAudioContext()
  if (!ctx) return

  const master = ctx.createGain()
  master.gain.setValueAtTime(0.18, ctx.currentTime)
  master.connect(ctx.destination)

  // Três notas em dó-mi-sol (C5-E5-G5): limpo, curto, agradável
  const notes = [
    { freq: 523, start: 0.00, dur: 0.13 },
    { freq: 659, start: 0.15, dur: 0.13 },
    { freq: 784, start: 0.30, dur: 0.18 },
  ]

  notes.forEach(({ freq, start, dur }) => {
    const osc  = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.type = 'sine'
    osc.frequency.setValueAtTime(freq, ctx.currentTime + start)

    gain.gain.setValueAtTime(0, ctx.currentTime + start)
    gain.gain.linearRampToValueAtTime(0.7, ctx.currentTime + start + 0.012)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + start + dur)

    osc.connect(gain)
    gain.connect(master)

    osc.start(ctx.currentTime + start)
    osc.stop(ctx.currentTime + start + dur + 0.02)
  })
}

// ── Som de erro (bipe curto descendente) ─────────────────────────────────
export function playErrorSound() {
  const ctx = getAudioContext()
  if (!ctx) return

  const osc  = ctx.createOscillator()
  const gain = ctx.createGain()

  osc.type = 'sine'
  osc.frequency.setValueAtTime(440, ctx.currentTime)
  osc.frequency.exponentialRampToValueAtTime(220, ctx.currentTime + 0.2)

  gain.gain.setValueAtTime(0.3, ctx.currentTime)
  gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.25)

  osc.connect(gain)
  gain.connect(ctx.destination)
  osc.start(ctx.currentTime)
  osc.stop(ctx.currentTime + 0.3)
}

// ── Som de sucesso simples (dois bipes ascendentes) ───────────────────────
export function playSuccessSound() {
  const ctx = getAudioContext()
  if (!ctx) return

  ;[{ f: 523, t: 0 }, { f: 659, t: 0.12 }].forEach(({ f, t }) => {
    const osc  = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.type = 'sine'
    osc.frequency.setValueAtTime(f, ctx.currentTime + t)
    gain.gain.setValueAtTime(0.25, ctx.currentTime + t)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + t + 0.15)

    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.start(ctx.currentTime + t)
    osc.stop(ctx.currentTime + t + 0.18)
  })
}
