'use client'

import { useEffect, useRef, useState } from 'react'
import {
  ArrowRight, BarChart3, Boxes, Check, CreditCard, FileCheck2,
  PackagePlus, Palette, Pause, Play, ReceiptText, RotateCcw, Search, Smartphone,
  TrendingUp, WalletCards, X,
} from 'lucide-react'
import Logo from '@/components/Logo'
import RestaurantModuleDemo from '@/components/institucional/RestaurantModuleDemo'
import type { InstitucionalTheme } from '@/lib/institucional'

export type ModuleDemoId = 'pdv' | 'estoque' | 'financeiro' | 'relatorios' | 'experiencia' | 'restaurante'

type DemoConfig = {
  title: string
  subtitle: string
  steps: readonly string[]
  descriptions: readonly string[]
  icon: typeof ReceiptText
}

const DEMOS: Record<Exclude<ModuleDemoId, 'restaurante'>, DemoConfig> = {
  pdv: {
    title: 'PDV e fiscal por dentro',
    subtitle: 'Venda, pagamento e NFC-e no mesmo fluxo',
    steps: ['Adicionar itens', 'Receber', 'Emitir NFC-e'],
    descriptions: [
      'O operador busca ou bipa os produtos e confere o carrinho em uma única tela.',
      'A forma de pagamento entra no fechamento sem redigitar o valor da venda.',
      'A NFC-e é emitida no mesmo movimento e fica disponível para consulta.',
    ],
    icon: ReceiptText,
  },
  estoque: {
    title: 'Estoque por dentro',
    subtitle: 'Produto, variante e movimentação conectados',
    steps: ['Produtos', 'Movimentar', 'Acompanhar'],
    descriptions: [
      'Cada produto reúne tamanhos, cores e quantidades sem duplicar cadastros.',
      'Entradas, saídas e ajustes ficam registrados com motivo e responsável.',
      'Alertas mostram o que está baixo ou esgotado antes de afetar a venda.',
    ],
    icon: Boxes,
  },
  financeiro: {
    title: 'Financeiro por dentro',
    subtitle: 'Caixa, crediário e recebimentos organizados',
    steps: ['Recebíveis', 'Baixar conta', 'Fechar caixa'],
    descriptions: [
      'As contas a receber aparecem com cliente, vencimento e situação atual.',
      'O pagamento dá baixa na conta e atualiza o saldo automaticamente.',
      'O fechamento compara vendas, recebimentos e valores informados no caixa.',
    ],
    icon: WalletCards,
  },
  relatorios: {
    title: 'Relatórios por dentro',
    subtitle: 'Indicadores que viram decisões práticas',
    steps: ['Visão geral', 'Analisar período', 'Encontrar oportunidades'],
    descriptions: [
      'Faturamento, ticket médio e quantidade de vendas ficam visíveis de imediato.',
      'O período pode ser comparado para entender tendência e sazonalidade.',
      'Produtos com melhor giro e itens parados orientam as próximas compras.',
    ],
    icon: BarChart3,
  },
  experiencia: {
    title: 'Sua marca por dentro',
    subtitle: 'Site e app com a identidade do cliente',
    steps: ['Personalizar', 'Publicar vitrine', 'Instalar app'],
    descriptions: [
      'Nome, logo e cores da empresa são aplicados sem perder a estrutura do sistema.',
      'A vitrine publica produtos no endereço da própria empresa e recebe pedidos.',
      'O cliente instala a experiência como aplicativo diretamente pelo navegador.',
    ],
    icon: Smartphone,
  },
}

function MiniHeader({ label }: { label: string }) {
  return (
    <div className="flex items-center border-b border-white/10 bg-[#18181c] px-4 py-3">
      <div className="flex items-center gap-2 text-white"><Logo className="h-6 w-6" title="Octus" /><strong className="text-sm">{label}</strong></div>
      <div className="ml-auto flex items-center gap-2 text-[11px] text-slate-500"><span className="h-2 w-2 rounded-full bg-emerald-400" />Dados sincronizados</div>
    </div>
  )
}

function PdvScreen({ step }: { step: number }) {
  return (
    <div className="grid min-h-[390px] gap-4 p-4 sm:p-5 lg:grid-cols-[1.4fr_.8fr]">
      <section>
        <div className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/5 px-3 py-2.5"><Search size={15} className="text-slate-500" /><span className="text-xs text-slate-400">Bipe o código ou busque o produto...</span></div>
        <div className="mt-3 space-y-2">
          {[['2x', 'Camiseta básica - P', 'R$ 79,80'], ['1x', 'Boné aba reta', 'R$ 59,90'], ['3x', 'Meia cano alto', 'R$ 44,70']].map(row => <div key={row[1]} className="grid grid-cols-[36px_1fr_auto] items-center gap-2 rounded-lg border border-white/10 bg-[#1a1a1f] px-3 py-3 text-xs"><strong className="text-octus-300">{row[0]}</strong><span className="text-slate-200">{row[1]}</span><strong className="text-white">{row[2]}</strong></div>)}
        </div>
      </section>
      <aside className={`flex flex-col justify-between rounded-xl border p-4 transition ${step > 0 ? 'border-octus-400/60 bg-octus-500/[0.06]' : 'border-white/10 bg-[#1a1a1f]'}`}>
        <div><p className="text-[10px] font-bold uppercase text-slate-500">Total da venda</p><p className="mt-1 text-3xl font-black text-white">R$ 184,40</p><div className="mt-5 grid grid-cols-2 gap-2">{['PIX', 'Débito', 'Crédito', 'Dinheiro'].map((item, i) => <span key={item} className={`rounded-lg border px-2 py-2 text-center text-xs font-bold ${step === 1 && i === 0 ? 'border-octus-400 bg-octus-500/20 text-octus-300' : 'border-white/10 text-slate-400'}`}>{item}</span>)}</div></div>
        <div className={`mt-5 flex items-center justify-center gap-2 rounded-lg py-3 text-sm font-black ${step === 2 ? 'bg-emerald-500 text-white' : 'bg-octus-500 text-[#071f3d]'}`}>{step === 2 ? <><Check size={16} />NFC-e emitida</> : <><CreditCard size={16} />Finalizar venda</>}</div>
      </aside>
    </div>
  )
}

function StockScreen({ step }: { step: number }) {
  const rows = [
    ['Camiseta básica', 'Preta · M', '42', 'Em estoque', 'text-emerald-400'],
    ['Boné aba reta', 'Único', '7', 'Estoque baixo', 'text-amber-300'],
    ['Meia cano alto', '39/42', '128', 'Em estoque', 'text-emerald-400'],
    ['Moletom capuz', 'Cinza · G', '0', 'Esgotado', 'text-red-400'],
  ]
  return (
    <div className="min-h-[390px] p-4 sm:p-5">
      <div className="flex flex-wrap gap-2"><div className="flex flex-1 items-center gap-2 rounded-lg border border-white/10 bg-white/5 px-3 py-2.5"><Search size={15} className="text-slate-500" /><span className="text-xs text-slate-400">Buscar produto ou variante</span></div><button className={`flex items-center gap-2 rounded-lg px-3 text-xs font-bold ${step === 1 ? 'bg-octus-500 text-[#071f3d]' : 'border border-white/10 text-slate-300'}`}><PackagePlus size={15} />Nova movimentação</button></div>
      <div className="mt-4 overflow-hidden rounded-xl border border-white/10">
        <div className="hidden grid-cols-[1.4fr_1fr_.4fr_.8fr] gap-3 bg-white/5 px-4 py-2 text-[10px] font-bold uppercase text-slate-500 sm:grid"><span>Produto</span><span>Variante</span><span>Qtd.</span><span>Situação</span></div>
        {rows.map((row, i) => <div key={row[0]} className={`grid gap-1 border-t border-white/10 px-4 py-3 text-xs sm:grid-cols-[1.4fr_1fr_.4fr_.8fr] sm:gap-3 ${step === 2 && i > 0 && i % 2 === 1 ? 'bg-amber-500/[0.06]' : 'bg-[#1a1a1f]'}`}><strong className="text-white">{row[0]}</strong><span className="text-slate-400">{row[1]}</span><span className="font-bold text-white">{row[2]}</span><span className={`font-bold ${row[4]}`}>{row[3]}</span></div>)}
      </div>
    </div>
  )
}

function FinanceScreen({ step }: { step: number }) {
  return (
    <div className="min-h-[390px] p-4 sm:p-5">
      <div className="grid gap-3 sm:grid-cols-3">{[['A receber', 'R$ 8.420,00'], ['Recebido hoje', 'R$ 2.184,40'], ['Em atraso', 'R$ 640,00']].map((item, i) => <div key={item[0]} className={`rounded-xl border p-4 ${step === 2 && i === 1 ? 'border-emerald-400/60 bg-emerald-500/10' : 'border-white/10 bg-[#1a1a1f]'}`}><p className="text-[10px] font-bold uppercase text-slate-500">{item[0]}</p><p className="mt-1 text-xl font-black text-white">{item[1]}</p></div>)}</div>
      <div className="mt-4 overflow-hidden rounded-xl border border-white/10"><div className="bg-white/5 px-4 py-3 text-sm font-semibold text-white">Contas a receber</div>{[['Mariana Costa', 'Hoje', 'R$ 184,40'], ['Loja Avenida', '23/08', 'R$ 920,00'], ['Carlos Mendes', '25/08', 'R$ 269,00']].map((row, i) => <div key={row[0]} className="flex flex-wrap items-center gap-3 border-t border-white/10 bg-[#1a1a1f] px-4 py-3 text-xs"><span className="min-w-32 flex-1 font-semibold text-white">{row[0]}</span><span className="text-slate-500">{row[1]}</span><strong className="text-amber-300">{row[2]}</strong><span className={`rounded-full px-2 py-1 font-bold ${step === 1 && i === 0 ? 'bg-emerald-500/15 text-emerald-400' : 'bg-white/5 text-slate-400'}`}>{step === 1 && i === 0 ? 'Baixada' : 'Pendente'}</span></div>)}</div>
    </div>
  )
}

function ReportsScreen({ step }: { step: number }) {
  const bars = [42, 58, 47, 74, 65, 92, 81]
  return (
    <div className="min-h-[390px] p-4 sm:p-5"><div className="grid gap-3 sm:grid-cols-3">{[['Faturamento', 'R$ 41.280'], ['Ticket médio', 'R$ 87,40'], ['Vendas', '472']].map(item => <div key={item[0]} className="rounded-xl border border-white/10 bg-[#1a1a1f] p-4"><p className="text-[10px] font-bold uppercase text-slate-500">{item[0]}</p><p className="mt-1 text-xl font-black text-white">{item[1]}</p></div>)}</div><div className={`mt-4 rounded-xl border p-4 transition ${step === 1 ? 'border-octus-400/60 bg-octus-500/[0.06]' : 'border-white/10 bg-[#1a1a1f]'}`}><div className="flex items-center gap-2 text-sm font-semibold text-white"><TrendingUp size={16} className="text-octus-400" />Vendas nos últimos 7 dias</div><div className="mt-5 flex h-40 items-end gap-2">{bars.map((h, i) => <div key={i} className="flex h-full flex-1 items-end"><div className="w-full rounded-t bg-octus-500 transition-all duration-500" style={{height: `${step === 0 ? Math.max(20, h - 22) : h}%`, opacity: .45 + i * .07}} /></div>)}</div></div>{step === 2 ? <div className="mt-3 flex items-center gap-3 rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-4 py-3 text-xs text-emerald-300"><FileCheck2 size={16} />Camiseta básica lidera o giro e merece reposição.</div> : null}</div>
  )
}

function ExperienceScreen({ step }: { step: number }) {
  return (
    <div className="grid min-h-[390px] gap-4 p-4 sm:p-5 lg:grid-cols-[.8fr_1.2fr]">
      <aside className="rounded-xl border border-white/10 bg-[#1a1a1f] p-4"><div className="flex items-center gap-2 text-sm font-semibold text-white"><Palette size={16} className="text-octus-400" />Identidade visual</div><div className="mt-5 space-y-4"><div><p className="text-[10px] font-bold uppercase text-slate-500">Nome da empresa</p><div className="mt-1 rounded-lg border border-white/10 bg-black/10 px-3 py-2 text-xs text-white">Loja Horizonte</div></div><div><p className="text-[10px] font-bold uppercase text-slate-500">Cores da marca</p><div className="mt-2 flex gap-2"><span className="h-8 w-8 rounded-md bg-[#0B3261]" /><span className="h-8 w-8 rounded-md bg-[#28B0D6]" /><span className="h-8 w-8 rounded-md bg-white" /></div></div><div className={`flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-bold ${step > 0 ? 'bg-emerald-500/15 text-emerald-400' : 'bg-white/5 text-slate-400'}`}><Check size={15} />Identidade aplicada</div></div></aside>
      <section className={`overflow-hidden rounded-xl border bg-white transition ${step === 1 ? 'border-octus-400 shadow-lg shadow-octus-500/10' : 'border-white/10'}`}><div className="flex items-center bg-[#071f3d] px-4 py-3 text-white"><strong className="text-sm">Loja Horizonte</strong><span className="ml-auto text-[10px] text-slate-300">lojahorizonte.com.br</span></div><div className="bg-[#eef9fc] p-4"><div className="grid grid-cols-2 gap-3">{['Camiseta', 'Boné', 'Moletom', 'Tênis'].map((item, i) => <div key={item} className="rounded-lg bg-white p-2 shadow-sm"><div className="aspect-[4/3] rounded-md bg-octus-500/10" /><p className="mt-2 text-xs font-bold text-[#071f3d]">{item}</p><p className="text-[10px] text-octus-700">A partir de R$ {39 + i * 20},90</p></div>)}</div>{step === 2 ? <div className="mt-3 flex items-center justify-center gap-2 rounded-lg bg-[#071f3d] py-2.5 text-xs font-bold text-white"><Smartphone size={15} />App instalado na tela inicial</div> : null}</div></section>
    </div>
  )
}

function DemoScreen({ id, step }: { id: Exclude<ModuleDemoId, 'restaurante'>; step: number }) {
  if (id === 'pdv') return <PdvScreen step={step} />
  if (id === 'estoque') return <StockScreen step={step} />
  if (id === 'financeiro') return <FinanceScreen step={step} />
  if (id === 'relatorios') return <ReportsScreen step={step} />
  return <ExperienceScreen step={step} />
}

export default function PlatformModuleDemo({ moduleId, onClose, theme }: { moduleId: ModuleDemoId; onClose: () => void; theme: InstitucionalTheme }) {
  const [step, setStep] = useState(0)
  const [playing, setPlaying] = useState(true)
  const sceneRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    setStep(0)
    setPlaying(true)
  }, [moduleId])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => event.key === 'Escape' && onClose()
    document.addEventListener('keydown', onKeyDown)
    document.body.style.overflow = 'hidden'
    return () => { document.removeEventListener('keydown', onKeyDown); document.body.style.overflow = '' }
  }, [onClose])

  useEffect(() => {
    if (!playing || moduleId === 'restaurante') return
    const timer = window.setInterval(() => setStep(current => (current + 1) % 3), 2600)
    return () => window.clearInterval(timer)
  }, [moduleId, playing])

  if (moduleId === 'restaurante') return <RestaurantModuleDemo open onClose={onClose} theme={theme} />

  const config = DEMOS[moduleId]
  const Icon = config.icon

  function moveScene(event: React.PointerEvent<HTMLDivElement>) {
    if (!sceneRef.current || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const rect = event.currentTarget.getBoundingClientRect()
    const y = ((event.clientX - rect.left) / rect.width - .5) * 2.2
    const x = ((event.clientY - rect.top) / rect.height - .5) * -1.5
    sceneRef.current.style.transform = `perspective(1400px) rotateX(${x}deg) rotateY(${y}deg)`
  }

  return (
    <div className="fixed inset-0 z-[90] flex items-center justify-center bg-[#020914]/85 p-3 backdrop-blur-md sm:p-6" role="dialog" aria-modal="true" aria-labelledby="module-demo-title" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <div className={`flex max-h-[94vh] w-full max-w-6xl flex-col overflow-hidden rounded-2xl border shadow-2xl ${theme.border} ${theme.surface}`}>
        <header className={`flex items-center gap-3 border-b px-4 py-3 sm:px-5 ${theme.border}`}><span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[#071f3d]"><Icon className="text-octus-300" size={21} /></span><div className="min-w-0 flex-1"><h2 id="module-demo-title" className={`truncate font-black ${theme.heading}`}>{config.title}</h2><p className={`truncate text-xs ${theme.muted}`}>{config.subtitle}</p></div><button type="button" onClick={() => setPlaying(current => !current)} className={`flex h-10 items-center gap-2 rounded-lg border px-3 text-xs font-bold ${theme.outline}`} aria-label={playing ? 'Pausar demonstração' : 'Continuar demonstração'}>{playing ? <Pause size={15} /> : <Play size={15} />}<span className="hidden sm:inline">{playing ? 'Pausar' : 'Continuar'}</span></button><button type="button" autoFocus onClick={onClose} className={`flex h-10 w-10 items-center justify-center rounded-lg border ${theme.outline}`} aria-label="Fechar demonstração"><X size={18} /></button></header>
        <div className="overflow-y-auto p-4 sm:p-6"><div className="mb-5 grid gap-4 lg:grid-cols-[1fr_auto] lg:items-end"><div><p className="text-xs font-extrabold uppercase text-octus-500">Passo {step + 1} de 3</p><h3 className={`mt-2 text-xl font-black sm:text-2xl ${theme.heading}`}>{config.steps[step]}</h3><p className={`mt-1 max-w-3xl text-sm leading-6 ${theme.body}`}>{config.descriptions[step]}</p></div><div role="tablist" aria-label="Etapas da demonstração" className="flex gap-1.5 overflow-x-auto pb-1">{config.steps.map((label, index) => <button key={label} type="button" role="tab" aria-selected={step === index} onClick={() => {setPlaying(false); setStep(index)}} className={`shrink-0 rounded-lg border px-3 py-2 text-xs font-bold ${step === index ? 'border-octus-500 bg-octus-500/15 text-octus-500' : `${theme.border} ${theme.muted}`}`}>{label}</button>)}</div></div>
          <div onPointerMove={moveScene} onPointerLeave={() => {if (sceneRef.current) sceneRef.current.style.transform = 'perspective(1400px) rotateX(0deg) rotateY(0deg)'}} className="[perspective:1400px]"><div ref={sceneRef} className="overflow-hidden rounded-xl border border-white/10 bg-[#121215] shadow-2xl shadow-black/35 transition-transform duration-150 ease-out will-change-transform"><MiniHeader label={config.title.replace(' por dentro', '')} /><DemoScreen id={moduleId} step={step} /></div></div>
          <div className="mt-5 flex items-center justify-center gap-3"><button type="button" onClick={() => {setPlaying(false); setStep(0)}} className={`flex items-center gap-2 text-xs font-bold ${theme.muted}`}><RotateCcw size={14} />Recomeçar</button><button type="button" onClick={() => {setPlaying(false); setStep(current => (current + 1) % 3)}} className="flex items-center gap-2 rounded-lg bg-octus-600 px-4 py-2.5 text-xs font-bold text-white hover:bg-octus-500">Próximo passo <ArrowRight size={14} /></button></div>
        </div>
      </div>
    </div>
  )
}
