'use client'

import { useEffect, useState } from 'react'
import {
  Info, Tag, Calendar, CheckCircle, Wrench, Zap,
  BookOpen, ChevronDown, ChevronUp, FileDown, Rocket,
  LayoutDashboard, ShoppingBag, ShoppingCart, Package,
  Users, CreditCard, BarChart2, Layers, Megaphone, Settings, Keyboard,
  Shirt, BookmarkCheck, Store, Hourglass, UserCog, Wallet, Receipt, Bot, MessageSquare, Palette,
  Puzzle, ArrowRightLeft, Calculator, Globe, Building2,
} from 'lucide-react'
import Link from 'next/link'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { MANUAL_SECOES, type ManualSectionData } from '@/lib/manualContent'

// ── Minimal Markdown Renderer ──────────────────────────────────────────────────

type Block =
  | { type: 'h1'; text: string }
  | { type: 'h2'; text: string }
  | { type: 'h3'; text: string }
  | { type: 'li'; text: string }
  | { type: 'hr' }
  | { type: 'p'; text: string }
  | { type: 'blank' }

function parseMd(raw: string): Block[] {
  return raw.split('\n').map(line => {
    if (/^# /.test(line))   return { type: 'h1', text: line.slice(2).trim() }
    if (/^## /.test(line))  return { type: 'h2', text: line.slice(3).trim() }
    if (/^### /.test(line)) return { type: 'h3', text: line.slice(4).trim() }
    if (/^- /.test(line))   return { type: 'li', text: line.slice(2).trim() }
    if (/^---/.test(line))  return { type: 'hr' }
    if (line.trim() === '') return { type: 'blank' }
    return { type: 'p', text: line.trim() }
  })
}

function renderInline(text: string): React.ReactNode {
  const parts = text.split(/(\*\*[^*]+\*\*)/)
  return parts.map((p, i) =>
    p.startsWith('**') && p.endsWith('**')
      ? <strong key={i} className="font-semibold text-white">{p.slice(2, -2)}</strong>
      : <span key={i}>{p}</span>
  )
}

function extractVersion(text: string) {
  const m = text.match(/\[([^\]]+)\]/)
  return m ? m[1] : text
}
function extractDate(text: string) {
  const m = text.match(/—\s*(.+)$/)
  return m ? m[1].trim() : ''
}

const sectionIcons: Record<string, React.ReactNode> = {
  'Adicionado': <Zap className="w-3.5 h-3.5" />,
  'Corrigido':  <Wrench className="w-3.5 h-3.5" />,
}
const sectionColors: Record<string, string> = {
  'Adicionado': 'text-accent-green',
  'Corrigido':  'text-yellow-400',
}

function ChangelogView({ blocks }: { blocks: Block[] }) {
  const nodes: React.ReactNode[] = []
  let key = 0

  for (let i = 0; i < blocks.length; i++) {
    const b = blocks[i]
    if (b.type === 'h1') {
      // skip
    } else if (b.type === 'h2') {
      const ver  = extractVersion(b.text)
      const date = extractDate(b.text)
      nodes.push(
        <div key={key++} className="flex items-center gap-3 mt-6 mb-3 first:mt-0">
          <span className="px-3 py-1 rounded-full bg-brand-500/20 border border-brand-500/30 text-brand-400 text-sm font-bold font-mono">
            {ver}
          </span>
          {date && (
            <span className="flex items-center gap-1 text-xs text-gray-500">
              <Calendar className="w-3 h-3" />
              {date}
            </span>
          )}
          <div className="flex-1 h-px bg-surface-500" />
        </div>
      )
    } else if (b.type === 'h3') {
      const icon  = sectionIcons[b.text]
      const color = sectionColors[b.text] ?? 'text-gray-400'
      nodes.push(
        <div key={key++} className={`flex items-center gap-1.5 mt-3 mb-1.5 text-xs font-semibold uppercase tracking-wider ${color}`}>
          {icon}
          {b.text}
        </div>
      )
    } else if (b.type === 'li') {
      nodes.push(
        <div key={key++} className="flex items-start gap-2 text-sm text-gray-300 mb-1 pl-2">
          <CheckCircle className="w-3.5 h-3.5 mt-0.5 shrink-0 text-gray-600" />
          <span>{renderInline(b.text)}</span>
        </div>
      )
    } else if (b.type === 'hr') {
      nodes.push(<hr key={key++} className="border-surface-500 my-4" />)
    } else if (b.type === 'p' && b.text) {
      nodes.push(
        <p key={key++} className="text-sm text-gray-400 mb-1">{renderInline(b.text)}</p>
      )
    }
  }
  return <>{nodes}</>
}

// ── Manual do Usuário ─────────────────────────────────────────────────────────

const MANUAL_ICONS: Record<string, React.ReactNode> = {
  '01': <LayoutDashboard className="w-4 h-4" />,
  '02': <ShoppingBag className="w-4 h-4" />,
  '03': <ShoppingCart className="w-4 h-4" />,
  '04': <Package className="w-4 h-4" />,
  '05': <Users className="w-4 h-4" />,
  '06': <CreditCard className="w-4 h-4" />,
  '07': <Shirt className="w-4 h-4" />,
  '07b': <BookmarkCheck className="w-4 h-4" />,
  '07c': <Store className="w-4 h-4" />,
  '07d': <Hourglass className="w-4 h-4" />,
  '08': <BarChart2 className="w-4 h-4" />,
  '09': <UserCog className="w-4 h-4" />,
  '10': <Megaphone className="w-4 h-4" />,
  '11': <Keyboard className="w-4 h-4" />,
  '12': <Settings className="w-4 h-4" />,
  '13': <Wallet className="w-4 h-4" />,
  '14': <Receipt className="w-4 h-4" />,
  '15': <Layers className="w-4 h-4" />,
  '16': <Bot className="w-4 h-4" />,
  '17': <MessageSquare className="w-4 h-4" />,
  '18': <Palette className="w-4 h-4" />,
  '18b': <Puzzle className="w-4 h-4" />,
  '18c': <ArrowRightLeft className="w-4 h-4" />,
  '18d': <Calculator className="w-4 h-4" />,
  '18e': <Globe className="w-4 h-4" />,
  '18f': <Building2 className="w-4 h-4" />,
}

function ManualSection({ section }: { section: ManualSectionData }) {
  const [open, setOpen] = useState(false)
  const icon = MANUAL_ICONS[section.num] ?? <BookOpen className="w-4 h-4" />

  return (
    <div className={`border rounded-xl overflow-hidden transition-all ${open ? 'border-surface-400' : 'border-surface-600'}`}>
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center gap-3 px-4 py-3.5 text-left hover:bg-surface-700/50 transition-colors"
      >
        <span className="shrink-0" style={{ color: section.cor }}>{icon}</span>
        <span className="font-semibold text-white text-sm flex-1">{section.titulo}</span>
        {open
          ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" />
          : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />
        }
      </button>

      {open && (
        <div className="px-4 pb-4 space-y-3 border-t border-surface-600">
          <div className="pt-3 space-y-2.5">
            {section.itens.map((item, i) => (
              <div key={i} className="flex gap-3">
                <span className="shrink-0 w-5 h-5 rounded-full bg-surface-700 border border-surface-500 flex items-center justify-center text-[10px] font-bold text-gray-400 mt-0.5">
                  {i + 1}
                </span>
                <div>
                  <p className="text-sm font-medium text-white">{item.t}</p>
                  <p className="text-xs text-gray-400 mt-0.5 leading-relaxed">{item.d}</p>
                </div>
              </div>
            ))}
          </div>

          {section.dicas && section.dicas.length > 0 && (
            <div className="mt-3 pt-3 border-t border-surface-600 space-y-1.5">
              <p className="text-[10px] font-semibold text-brand-400 uppercase tracking-wider">Dicas</p>
              {section.dicas.map((dica, i) => (
                <div key={i} className="flex items-start gap-2 text-xs text-gray-400">
                  <span className="text-brand-400 shrink-0 mt-0.5">→</span>
                  {dica}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function SobrePage() {
  const { site } = useSiteConfig()
  const [raw,     setRaw]     = useState<string | null>(null)
  const [error,   setError]   = useState(false)
  const [version, setVersion] = useState('—')
  const [tab,     setTab]     = useState<'manual' | 'changelog'>('manual')

  useEffect(() => {
    fetch('/CHANGELOG.md')
      .then(r => { if (!r.ok) throw new Error(); return r.text() })
      .then(text => {
        setRaw(text)
        const m = text.match(/^## \[([^\]]+)\]/m)
        if (m) setVersion(m[1])
      })
      .catch(() => setError(true))
  }, [])

  const blocks = raw ? parseMd(raw) : []

  return (
    <div className="p-4 sm:p-6 max-w-3xl mx-auto">
      {/* Header */}
      <div className="flex items-start gap-4 mb-6">
        <div className="w-12 h-12 rounded-2xl bg-brand-500/10 border border-brand-500/20 flex items-center justify-center shrink-0">
          <Info className="w-6 h-6 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-white">Sobre o Sistema</h1>
          <p className="text-sm text-gray-500 mt-0.5">{site.siteName} — Plataforma de Gestão</p>
        </div>
        <div className="ml-auto flex items-center gap-3">
          <Tag className="w-4 h-4 text-brand-400" />
          <span className="text-sm font-mono font-semibold text-brand-400">{version}</span>
          <Link
            href="/admin/primeiros-passos"
            className="flex items-center gap-1.5 text-xs font-semibold px-3 py-1.5 rounded-lg bg-accent-green/10 border border-accent-green/20 text-accent-green hover:bg-accent-green/20 transition-colors"
          >
            <Rocket className="w-3.5 h-3.5" /> Primeiros Passos
          </Link>
          <Link
            href="/admin/manual"
            target="_blank"
            className="flex items-center gap-1.5 text-xs font-semibold px-3 py-1.5 rounded-lg bg-brand-500/10 border border-brand-500/20 text-brand-400 hover:bg-brand-500/20 transition-colors"
          >
            <FileDown className="w-3.5 h-3.5" /> Manual PDF
          </Link>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 bg-surface-800 p-1 rounded-xl mb-6">
        <button
          onClick={() => setTab('manual')}
          className={`flex-1 flex items-center justify-center gap-2 py-2 rounded-lg text-sm font-medium transition-all ${
            tab === 'manual' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200'
          }`}
        >
          <BookOpen className="w-4 h-4" /> Manual do Usuário
        </button>
        <button
          onClick={() => setTab('changelog')}
          className={`flex-1 flex items-center justify-center gap-2 py-2 rounded-lg text-sm font-medium transition-all ${
            tab === 'changelog' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200'
          }`}
        >
          <Zap className="w-4 h-4" /> Atualizações
        </button>
      </div>

      {/* Manual */}
      {tab === 'manual' && (
        <div className="space-y-2">
          <p className="text-xs text-gray-500 mb-4">
            Clique em qualquer módulo para ver como usar. Tudo aqui foi pensado para ser simples e direto.
          </p>
          {MANUAL_SECOES.map(section => (
            <ManualSection key={section.num} section={section} />
          ))}
        </div>
      )}

      {/* Changelog */}
      {tab === 'changelog' && (
        <div className="card p-6">
          <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-4">
            Histórico de Atualizações
          </h2>

          {error && (
            <p className="text-sm text-red-400">
              Não foi possível carregar o changelog. Verifique se o arquivo
              <code className="mx-1 px-1 bg-surface-700 rounded text-xs">public/CHANGELOG.md</code>
              existe.
            </p>
          )}

          {!raw && !error && (
            <div className="space-y-3 animate-pulse">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="h-4 bg-surface-700 rounded" style={{ width: `${60 + (i % 3) * 15}%` }} />
              ))}
            </div>
          )}

          {raw && <ChangelogView blocks={blocks} />}
        </div>
      )}
    </div>
  )
}
