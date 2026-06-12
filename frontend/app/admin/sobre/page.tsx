'use client'

import { useEffect, useState } from 'react'
import { Info, Tag, Calendar, CheckCircle, Wrench, Zap } from 'lucide-react'

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

// extracts version from "## [v1.5.0] — 2026-06-12" → "v1.5.0"
function extractVersion(text: string) {
  const m = text.match(/\[([^\]]+)\]/)
  return m ? m[1] : text
}
// extracts date from "## [v1.5.0] — 2026-06-12" → "2026-06-12"
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
      // skip — title shown in page header
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

// ── Page ──────────────────────────────────────────────────────────────────────

export default function SobrePage() {
  const [raw,     setRaw]     = useState<string | null>(null)
  const [error,   setError]   = useState(false)
  const [version, setVersion] = useState('—')

  useEffect(() => {
    fetch('/CHANGELOG.md')
      .then(r => { if (!r.ok) throw new Error(); return r.text() })
      .then(text => {
        setRaw(text)
        // extract latest version from first h2
        const m = text.match(/^## \[([^\]]+)\]/m)
        if (m) setVersion(m[1])
      })
      .catch(() => setError(true))
  }, [])

  const blocks = raw ? parseMd(raw) : []

  return (
    <div className="p-6 max-w-3xl mx-auto">
      {/* Header */}
      <div className="flex items-start gap-4 mb-6">
        <div className="w-12 h-12 rounded-2xl bg-brand-500/10 border border-brand-500/20 flex items-center justify-center shrink-0">
          <Info className="w-6 h-6 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-white">Sobre o Sistema</h1>
          <p className="text-sm text-gray-500 mt-0.5">Santuário Nerd — Plataforma de Gestão</p>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Tag className="w-4 h-4 text-brand-400" />
          <span className="text-sm font-mono font-semibold text-brand-400">{version}</span>
        </div>
      </div>

      {/* Changelog Card */}
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
    </div>
  )
}
