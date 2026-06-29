'use client'

import { useEffect, useRef, useState, useCallback } from 'react'
import { timerApi, TimerDto } from '@/lib/api'
import { RotateCcw, Timer, X } from 'lucide-react'

// ── Audio ─────────────────────────────────────────────────────────────────────
const freqMap: Record<string, number> = { beep: 1100, bell: 880, buzzer: 180 }

function playBeep(preset: string): number {
  if (preset === 'none') return 0
  try {
    const freq = freqMap[preset] ?? 880
    const ctx  = new AudioContext()
    const osc  = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.connect(gain); gain.connect(ctx.destination)
    osc.frequency.value = freq
    gain.gain.setValueAtTime(0.4, ctx.currentTime)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 1.2)
    osc.start(); osc.stop(ctx.currentTime + 1.2)
    // segunda nota
    setTimeout(() => {
      try {
        const c2 = new AudioContext(); const o2 = c2.createOscillator(); const g2 = c2.createGain()
        o2.connect(g2); g2.connect(c2.destination)
        o2.frequency.value = freq * 1.25
        g2.gain.setValueAtTime(0.4, c2.currentTime)
        g2.gain.exponentialRampToValueAtTime(0.001, c2.currentTime + 0.8)
        o2.start(); o2.stop(c2.currentTime + 0.8)
      } catch {}
    }, 400)
    return 2200 // delay até próxima repetição
  } catch { return 0 }
}

function calcRemaining(t: TimerDto): number {
  if (t.state === 'paused')   return t.pausedRemaining ?? t.durationSeconds
  if (t.state === 'finished') return 0
  if (t.state === 'running' && t.startedAt) {
    const elapsed = (Date.now() - new Date(t.startedAt).getTime()) / 1000
    return Math.max(0, t.durationSeconds - elapsed)
  }
  return t.durationSeconds
}

function fmt(sec: number) {
  const s = Math.ceil(sec)
  return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`
}

// ── Componente ────────────────────────────────────────────────────────────────
export default function TimerAlarmOverlay() {
  const [timers,   setTimers]   = useState<TimerDto[]>([])
  const [alarming, setAlarming] = useState<Set<string>>(new Set())
  const [dismissed, setDismissed] = useState<Set<string>>(new Set())

  const stopFnsRef = useRef<Map<string, () => void>>(new Map())
  const knownStateRef = useRef<Map<string, string>>(new Map())

  // Inicia loop de som para um timer
  const startAlarm = useCallback((id: string, preset: string) => {
    if (stopFnsRef.current.has(id)) return // já tocando
    let stopped = false
    function loop() {
      if (stopped) return
      const delay = playBeep(preset)
      setTimeout(() => { if (!stopped) loop() }, delay + 300)
    }
    loop()
    stopFnsRef.current.set(id, () => { stopped = true })
    setAlarming(prev => new Set(prev).add(id))
    setDismissed(prev => { const n = new Set(prev); n.delete(id); return n })
  }, [])

  const stopAlarm = useCallback((id: string) => {
    stopFnsRef.current.get(id)?.()
    stopFnsRef.current.delete(id)
    setAlarming(prev => { const n = new Set(prev); n.delete(id); return n })
  }, [])

  // Polling: busca timers a cada 5s
  const poll = useCallback(async () => {
    try {
      const { data } = await timerApi.list()
      setTimers(data)

      data.forEach(t => {
        const prevState = knownStateRef.current.get(t.id)
        const isFinished = t.state === 'finished' || (t.state === 'running' && calcRemaining(t) <= 0)

        if (isFinished && prevState !== 'finished') {
          startAlarm(t.id, t.soundPreset)
        }
        if (!isFinished && stopFnsRef.current.has(t.id)) {
          stopAlarm(t.id)
        }
        knownStateRef.current.set(t.id, t.state)
      })

      // Para alarmes de timers que sumiram
      stopFnsRef.current.forEach((_, id) => {
        if (!data.find(t => t.id === id)) stopAlarm(id)
      })
    } catch {
      // silencia — pode estar sem sessão
    }
  }, [startAlarm, stopAlarm])

  useEffect(() => {
    poll()
    const id = setInterval(poll, 5000)
    return () => {
      clearInterval(id)
      stopFnsRef.current.forEach(fn => fn())
    }
  }, [poll])

  async function handleReset(id: string) {
    stopAlarm(id)
    try {
      const { data } = await timerApi.update(id, { action: 'reset' })
      setTimers(prev => prev.map(t => t.id === id ? data as TimerDto : t))
      knownStateRef.current.set(id, 'stopped')
    } catch {}
  }

  function handleDismiss(id: string) {
    stopAlarm(id)
    setDismissed(prev => new Set(prev).add(id))
  }

  // Timers que estão em alarme e não foram dispensados
  const visible = timers.filter(t =>
    (alarming.has(t.id) || t.state === 'finished') && !dismissed.has(t.id)
  )

  if (visible.length === 0) return null

  return (
    <div className="fixed bottom-4 right-4 z-[9999] flex flex-col gap-2 max-w-xs w-full pointer-events-none">
      {visible.map(t => (
        <div
          key={t.id}
          className="pointer-events-auto bg-red-950 border border-red-500/60 rounded-2xl shadow-2xl shadow-red-900/40 p-4 flex items-start gap-3 animate-pulse-border"
          style={{ animation: 'pulse-red 1.5s ease-in-out infinite' }}
        >
          <div className="mt-0.5 w-8 h-8 rounded-full bg-red-500/20 flex items-center justify-center shrink-0">
            <Timer className="w-4 h-4 text-red-400" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-bold text-white text-sm truncate">{t.name}</p>
            <p className="text-red-300 text-xs mt-0.5">⏰ Tempo esgotado!</p>
            <div className="flex gap-2 mt-2">
              <button
                onClick={() => handleReset(t.id)}
                className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-red-500 hover:bg-red-400 text-white text-xs font-semibold transition-colors"
              >
                <RotateCcw className="w-3 h-3" /> Resetar
              </button>
              <button
                onClick={() => handleDismiss(t.id)}
                className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-surface-700 hover:bg-surface-600 text-gray-300 text-xs font-semibold transition-colors"
              >
                <X className="w-3 h-3" /> Dispensar
              </button>
            </div>
          </div>
        </div>
      ))}
      <style>{`
        @keyframes pulse-red {
          0%, 100% { box-shadow: 0 0 0 0 rgba(239,68,68,0.3), 0 25px 50px -12px rgba(127,29,29,0.5); }
          50%       { box-shadow: 0 0 0 6px rgba(239,68,68,0), 0 25px 50px -12px rgba(127,29,29,0.5); }
        }
      `}</style>
    </div>
  )
}
