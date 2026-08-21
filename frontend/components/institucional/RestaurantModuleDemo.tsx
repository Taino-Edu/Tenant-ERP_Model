'use client'

import { useEffect, useRef, useState } from 'react'
import {
  ArrowRight, Check, ChefHat, Clock3, MessageSquare, Pause, Play,
  ReceiptText, RotateCcw, UtensilsCrossed, X,
} from 'lucide-react'
import Logo from '@/components/Logo'
import type { InstitucionalTheme } from '@/lib/institucional'

const STAGES = ['Comanda', 'Recebido', 'Preparando', 'Pronto'] as const
type Stage = (typeof STAGES)[number]

const STAGE_COPY: Record<Stage, string> = {
  Comanda: 'O garçom abre a comanda, inclui os itens e registra a observação do cliente.',
  Recebido: 'Ao salvar, cada item aparece automaticamente na fila da área responsável.',
  Preparando: 'A cozinha assume o pedido e o salão acompanha a mudança em tempo real.',
  Pronto: 'O pedido fica pronto para servir, sem papel, grito ou troca de aplicativo.',
}

function OrderCard({ stage, active, onAdvance }: { stage: Stage; active: boolean; onAdvance?: () => void }) {
  if (!active) return <p className="py-8 text-center text-xs text-slate-600">Fila vazia</p>

  return (
    <article className="space-y-2 rounded-lg border border-white/10 bg-[#1a1a1f] p-3 shadow-lg shadow-black/20">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-white">2x Smash bacon</p>
          <p className="mt-1 text-[11px] text-slate-500">Mesa 08 · Mariana · 4 min</p>
        </div>
        <span className="shrink-0 rounded-full bg-octus-500/10 px-2 py-1 text-[10px] font-bold text-octus-300">Cozinha</span>
      </div>
      <p className="flex gap-1.5 rounded-md bg-amber-500/10 px-2 py-1.5 text-xs text-amber-200">
        <MessageSquare className="mt-0.5 h-3 w-3 shrink-0" /> Sem cebola em um dos lanches
      </p>
      {onAdvance ? (
        <button type="button" onClick={onAdvance} className="flex w-full items-center justify-center gap-1.5 rounded-lg border border-white/10 bg-white/5 py-2 text-xs font-bold text-slate-200 transition hover:border-octus-400/50 hover:bg-octus-500/10">
          <ArrowRight className="h-3.5 w-3.5" />
          {stage === 'Pronto' ? 'Marcar servido' : `Mover para ${STAGES[STAGES.indexOf(stage) + 1].toLowerCase()}`}
        </button>
      ) : null}
    </article>
  )
}

export default function RestaurantModuleDemo({ open, onClose, theme }: {
  open: boolean
  onClose: () => void
  theme: InstitucionalTheme
}) {
  const [stageIndex, setStageIndex] = useState(0)
  const [playing, setPlaying] = useState(true)
  const sceneRef = useRef<HTMLDivElement>(null)
  const stage = STAGES[stageIndex]

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => event.key === 'Escape' && onClose()
    document.addEventListener('keydown', onKeyDown)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = ''
    }
  }, [onClose, open])

  useEffect(() => {
    if (!open || !playing) return
    const timer = window.setInterval(() => {
      setStageIndex(current => current === STAGES.length - 1 ? 0 : current + 1)
    }, 2600)
    return () => window.clearInterval(timer)
  }, [open, playing])

  if (!open) return null

  function advance() {
    setPlaying(false)
    setStageIndex(current => current === STAGES.length - 1 ? 0 : current + 1)
  }

  function handlePointerMove(event: React.PointerEvent<HTMLDivElement>) {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches || !sceneRef.current) return
    const rect = event.currentTarget.getBoundingClientRect()
    const rotateY = ((event.clientX - rect.left) / rect.width - 0.5) * 2.2
    const rotateX = ((event.clientY - rect.top) / rect.height - 0.5) * -1.5
    sceneRef.current.style.transform = `perspective(1400px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`
  }

  function resetMovement() {
    if (sceneRef.current) sceneRef.current.style.transform = 'perspective(1400px) rotateX(0deg) rotateY(0deg)'
  }

  return (
    <div className="fixed inset-0 z-[90] flex items-center justify-center bg-[#020914]/85 p-3 backdrop-blur-md sm:p-6" role="dialog" aria-modal="true" aria-labelledby="restaurant-demo-title" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <div className={`flex max-h-[94vh] w-full max-w-7xl flex-col overflow-hidden rounded-2xl border shadow-2xl ${theme.border} ${theme.surface}`}>
        <header className={`flex items-center gap-3 border-b px-4 py-3 sm:px-5 ${theme.border}`}>
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[#071f3d]"><Logo className="h-7 w-7" title="Octus" /></span>
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <h2 id="restaurant-demo-title" className={`truncate font-black ${theme.heading}`}>Restaurante por dentro</h2>
              <span className="hidden rounded-full bg-emerald-500/10 px-2 py-1 text-[10px] font-bold text-emerald-500 sm:inline">DEMO INTERATIVA</span>
            </div>
            <p className={`truncate text-xs ${theme.muted}`}>Interface baseada no módulo real do Octus</p>
          </div>
          <button type="button" onClick={() => setPlaying(current => !current)} className={`flex h-10 items-center gap-2 rounded-lg border px-3 text-xs font-bold ${theme.outline}`} aria-label={playing ? 'Pausar demonstracao' : 'Continuar demonstracao'}>
            {playing ? <Pause size={15} /> : <Play size={15} />}<span className="hidden sm:inline">{playing ? 'Pausar' : 'Continuar'}</span>
          </button>
          <button type="button" autoFocus onClick={onClose} className={`flex h-10 w-10 items-center justify-center rounded-lg border ${theme.outline}`} aria-label="Fechar demonstração"><X size={18} /></button>
        </header>

        <div className="overflow-y-auto p-4 sm:p-6">
          <div className="mb-5 grid gap-4 lg:grid-cols-[1fr_auto] lg:items-end">
            <div>
              <p className="text-xs font-extrabold uppercase text-octus-500">Passo {stageIndex + 1} de {STAGES.length}</p>
              <h3 className={`mt-2 text-xl font-black sm:text-2xl ${theme.heading}`}>{stage}</h3>
              <p className={`mt-1 max-w-3xl text-sm leading-6 ${theme.body}`}>{STAGE_COPY[stage]}</p>
            </div>
            <div role="tablist" aria-label="Etapas do pedido" className="flex gap-1.5 overflow-x-auto pb-1">
              {STAGES.map((item, index) => (
                <button key={item} type="button" role="tab" aria-selected={index === stageIndex} onClick={() => { setPlaying(false); setStageIndex(index) }} className={`shrink-0 rounded-lg border px-3 py-2 text-xs font-bold transition ${index === stageIndex ? 'border-octus-500 bg-octus-500/15 text-octus-500' : `${theme.border} ${theme.muted}`}`}>{item}</button>
              ))}
            </div>
          </div>

          <div onPointerMove={handlePointerMove} onPointerLeave={resetMovement} className="[perspective:1400px]">
            <div ref={sceneRef} className="overflow-hidden rounded-xl border border-white/10 bg-[#121215] shadow-2xl shadow-black/35 transition-transform duration-150 ease-out will-change-transform">
              <div className="flex items-center border-b border-white/10 bg-[#18181c] px-4 py-3">
                <div className="flex items-center gap-2 text-white"><UtensilsCrossed size={17} className="text-octus-400" /><strong className="text-sm">Restaurante</strong></div>
                <div className="ml-auto flex items-center gap-2 text-[11px] text-slate-500"><span className="h-2 w-2 rounded-full bg-emerald-400" />Atualização em tempo real</div>
              </div>

              <div className="grid min-h-[430px] lg:grid-cols-[260px_1fr]">
                <aside className="hidden border-r border-white/10 bg-[#151519] p-4 lg:block">
                  <div className="flex items-center gap-3 border-b border-white/10 pb-4"><ReceiptText className="text-emerald-400" size={18} /><div><p className="text-sm font-semibold text-white">Operação do salão</p><p className="text-[11px] text-slate-500">Comandas e observações</p></div></div>
                  <article className={`mt-4 space-y-3 rounded-xl border p-4 transition ${stageIndex === 0 ? 'border-octus-400 bg-octus-500/10' : 'border-white/10 bg-[#1a1a1f]'}`}>
                    <div className="flex justify-between gap-2"><div><p className="text-sm font-semibold text-white">Mesa 08 · Mariana</p><p className="mt-1 flex items-center gap-1 text-[11px] text-slate-500"><Clock3 size={12} /> aberta há 4 min</p></div><strong className="text-xs text-amber-300">R$ 73,70</strong></div>
                    <div className="space-y-1 border-t border-white/10 pt-3 text-xs"><p className="flex justify-between text-slate-300"><span>2x Smash bacon</span><span className="text-slate-500">R$ 57,80</span></p><p className="flex justify-between text-slate-300"><span>1x Suco laranja</span><span className="text-slate-500">R$ 15,90</span></p></div>
                    <p className="flex gap-1.5 rounded-md bg-amber-500/10 px-2 py-1.5 text-xs text-amber-200"><MessageSquare className="mt-0.5 h-3 w-3 shrink-0" />Sem cebola em um dos lanches</p>
                  </article>
                </aside>

                <main className="p-4 sm:p-5">
                  <div className="mb-4 flex items-start gap-3"><ChefHat className="mt-0.5 text-orange-400" size={19} /><div><h4 className="font-semibold text-white">Fila de produção</h4><p className="text-xs text-slate-500">O pedido avança pela cozinha até ser servido.</p></div></div>
                  <div className="grid gap-3 sm:grid-cols-3">
                    {(['Recebido', 'Preparando', 'Pronto'] as Stage[]).map((column, columnIndex) => {
                      const active = stageIndex > 0 && stageIndex - 1 === columnIndex
                      return (
                        <section key={column} className={`min-h-44 rounded-xl border p-3 transition-colors ${active ? 'border-octus-400/60 bg-octus-500/[0.06]' : 'border-white/10 bg-black/10'}`}>
                          <div className="mb-3 flex items-center justify-between"><h5 className="text-xs font-black uppercase text-slate-300">{column}</h5><span className="rounded-full bg-white/5 px-2 py-0.5 text-[11px] text-slate-400">{active ? 1 : 0}</span></div>
                          <OrderCard stage={column} active={active} onAdvance={active ? advance : undefined} />
                        </section>
                      )
                    })}
                  </div>
                  {stageIndex === 0 ? <button type="button" onClick={advance} className="mx-auto mt-5 flex items-center gap-2 rounded-lg bg-octus-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-octus-500"><ReceiptText size={16} />Enviar pedido para a cozinha</button> : null}
                  {stageIndex === STAGES.length - 1 ? <button type="button" onClick={advance} className="mx-auto mt-5 flex items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-emerald-500"><Check size={16} />Servir e reiniciar</button> : null}
                </main>
              </div>
            </div>
          </div>

          <button type="button" onClick={() => { setPlaying(false); setStageIndex(0) }} className={`mx-auto mt-5 flex items-center gap-2 text-xs font-bold ${theme.muted}`}><RotateCcw size={14} />Recomeçar demonstração</button>
        </div>
      </div>
    </div>
  )
}
