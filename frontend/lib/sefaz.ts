export function secondsUntil(isoDate: string | undefined, nowMs: number) {
  if (!isoDate) return 0
  return Math.max(0, Math.ceil((new Date(isoDate).getTime() - nowMs) / 1000))
}

export function formatCountdown(totalSeconds: number) {
  const seconds = Math.max(0, totalSeconds)
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  return [h, m, s].map(value => String(value).padStart(2, '0')).join(':')
}
