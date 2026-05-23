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

// ── Som de GOL (estilo Sofascore) ─────────────────────────────────────────
// Sequência ascendente: dun — dun — dun — DUN! + fanfarra final
export function playGoalSound() {
  const ctx = getAudioContext()
  if (!ctx) return

  const master = ctx.createGain()
  master.gain.setValueAtTime(0.4, ctx.currentTime)
  master.connect(ctx.destination)

  // Notas da sequência (Hz): subindo como o Sofascore
  const notes = [
    { freq: 330, start: 0.00, dur: 0.18 },
    { freq: 392, start: 0.20, dur: 0.18 },
    { freq: 494, start: 0.40, dur: 0.18 },
    { freq: 659, start: 0.62, dur: 0.55 }, // nota longa final
  ]

  notes.forEach(({ freq, start, dur }) => {
    const osc  = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.type = 'sine'
    osc.frequency.setValueAtTime(freq, ctx.currentTime + start)

    // Leve pitch-up em cada nota
    osc.frequency.exponentialRampToValueAtTime(freq * 1.04, ctx.currentTime + start + dur * 0.8)

    gain.gain.setValueAtTime(0, ctx.currentTime + start)
    gain.gain.linearRampToValueAtTime(0.9, ctx.currentTime + start + 0.02)
    gain.gain.linearRampToValueAtTime(0.7, ctx.currentTime + start + dur * 0.6)
    gain.gain.linearRampToValueAtTime(0, ctx.currentTime + start + dur)

    osc.connect(gain)
    gain.connect(master)

    osc.start(ctx.currentTime + start)
    osc.stop(ctx.currentTime + start + dur + 0.05)
  })

  // Acorde de fanfarra no final (harmônicos juntos)
  const chordFreqs = [659, 830, 988]
  chordFreqs.forEach((freq, i) => {
    const osc  = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.type = i === 0 ? 'sine' : 'triangle'
    osc.frequency.setValueAtTime(freq, ctx.currentTime + 1.18)

    gain.gain.setValueAtTime(0, ctx.currentTime + 1.18)
    gain.gain.linearRampToValueAtTime(0.5 - i * 0.1, ctx.currentTime + 1.22)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 1.90)

    osc.connect(gain)
    gain.connect(master)

    osc.start(ctx.currentTime + 1.18)
    osc.stop(ctx.currentTime + 1.95)
  })

  // "Woosh" de fundo — ruído filtrado simulando torcida
  try {
    const bufferSize = ctx.sampleRate * 0.8
    const buffer     = ctx.createBuffer(1, bufferSize, ctx.sampleRate)
    const data       = buffer.getChannelData(0)
    for (let i = 0; i < bufferSize; i++) data[i] = Math.random() * 2 - 1

    const noise  = ctx.createBufferSource()
    const filter = ctx.createBiquadFilter()
    const nGain  = ctx.createGain()

    noise.buffer = buffer
    filter.type  = 'bandpass'
    filter.frequency.setValueAtTime(800, ctx.currentTime + 0.6)
    filter.Q.setValueAtTime(0.5, ctx.currentTime + 0.6)

    nGain.gain.setValueAtTime(0, ctx.currentTime + 0.6)
    nGain.gain.linearRampToValueAtTime(0.07, ctx.currentTime + 0.75)
    nGain.gain.linearRampToValueAtTime(0.12, ctx.currentTime + 1.1)
    nGain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 1.9)

    noise.connect(filter)
    filter.connect(nGain)
    nGain.connect(master)

    noise.start(ctx.currentTime + 0.6)
    noise.stop(ctx.currentTime + 1.95)
  } catch { /* ruído opcional */ }
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
