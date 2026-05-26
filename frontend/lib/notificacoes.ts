// =============================================================================
// lib/notificacoes.ts — Notificações sonoras + badge na aba do browser
// Usado pelo admin quando um cliente abre/atualiza comanda
// =============================================================================

// ── Badge na aba ──────────────────────────────────────────────────────────────
let badgeCount = 0

export function incrementBadge() {
  badgeCount++
  atualizarTitulo()
}

export function clearBadge() {
  badgeCount = 0
  atualizarTitulo()
}

function atualizarTitulo() {
  const base = 'Admin — Santuário Nerd'
  document.title = badgeCount > 0 ? `(${badgeCount}) ${base}` : base
}

// ── Som de notificação (Web Audio API — sem arquivo externo) ──────────────────
export function tocarSom(tipo: 'nova' | 'fechada' = 'nova') {
  try {
    const ctx = new (window.AudioContext || (window as any).webkitAudioContext)()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.connect(gain)
    gain.connect(ctx.destination)

    if (tipo === 'nova') {
      // Dois bips curtos ascendentes — nova comanda
      osc.frequency.setValueAtTime(520, ctx.currentTime)
      osc.frequency.setValueAtTime(780, ctx.currentTime + 0.12)
      gain.gain.setValueAtTime(0.25, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.35)
      osc.start(ctx.currentTime)
      osc.stop(ctx.currentTime + 0.35)
    } else {
      // Bip descendente suave — comanda fechada
      osc.frequency.setValueAtTime(600, ctx.currentTime)
      osc.frequency.exponentialRampToValueAtTime(300, ctx.currentTime + 0.3)
      gain.gain.setValueAtTime(0.15, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3)
      osc.start(ctx.currentTime)
      osc.stop(ctx.currentTime + 0.3)
    }
  } catch {
    // Browser sem suporte — ignora silenciosamente
  }
}

// ── Notificação nativa do browser (requer permissão) ─────────────────────────
export async function pedirPermissaoNotificacao() {
  if (!('Notification' in window)) return
  if (Notification.permission === 'default') {
    await Notification.requestPermission()
  }
}

export function notificarBrowser(titulo: string, corpo: string) {
  if (typeof window === 'undefined') return
  if (!('Notification' in window)) return
  if (Notification.permission !== 'granted') return

  try {
    new Notification(titulo, {
      body:    corpo,
      icon:    '/favicon.ico',
      silent:  true, // o som vem do nosso tocarSom()
      tag:     'santuario-nerd', // substitui notificação anterior em vez de empilhar
    })
  } catch {
    // ignora (ex: Firefox em modo privado)
  }
}
