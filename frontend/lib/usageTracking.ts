// =============================================================================
// usageTracking.ts — fila de eventos de navegação (tela + duração) do admin,
// descarregada via sendBeacon (sobrevive a troca de aba/fechamento, não
// precisa esperar resposta). Ver CardGameStore/Controllers/UsageController.cs.
// =============================================================================

type QueuedEvent = { path: string; durationMs: number | null; occurredAt: string }

let queue: QueuedEvent[] = []

export function enqueueUsageEvent(path: string, durationMs: number | null) {
  queue.push({ path, durationMs, occurredAt: new Date().toISOString() })
}

export function flushUsageEvents() {
  if (queue.length === 0) return
  if (typeof navigator === 'undefined' || !navigator.sendBeacon) { queue = []; return }

  const batch = queue
  queue = []

  const blob = new Blob([JSON.stringify(batch)], { type: 'application/json' })
  navigator.sendBeacon('/api/usage/events', blob)
  // Não reenfileira em caso de falha: sendBeacon é fire-and-forget por natureza
  // (não dá pra saber se o servidor recebeu), e um retry indefinido de eventos
  // de analytics não vale o risco de acumular fila sem limite.
}
