'use client'
// Curva ABC de produtos — seção + explicador. Extraído de financeiro/page.tsx.
import { useMemo, useState } from 'react'
import { BarChart2, ChevronDown, ChevronUp, Lightbulb, X } from 'lucide-react'
import { fmt, fmtShort } from './financeiro-shared'

// ── Curva ABC ─────────────────────────────────────────────────────────────────
type AbcClass = 'A' | 'B' | 'C'

interface AbcProduto {
  nome: string; receita: number; qtd: number; custo: number
  categoria?: string; abcClass: AbcClass; cumPct: number; pct: number
}

const ABC_COLORS: Record<AbcClass, { bar: string; text: string; bg: string; border: string }> = {
  A: { bar: '#10b981', text: 'text-emerald-400', bg: 'bg-emerald-500/15', border: 'border-emerald-500/40' },
  B: { bar: '#f59e0b', text: 'text-yellow-400',  bg: 'bg-yellow-500/15',  border: 'border-yellow-500/40'  },
  C: { bar: '#ef4444', text: 'text-red-400',     bg: 'bg-red-500/15',     border: 'border-red-500/40'     },
}

function classifyABC(produtos: { nome: string; receita: number; qtd: number; custo: number; categoria?: string }[]): AbcProduto[] {
  const sorted = [...produtos].sort((a, b) => b.receita - a.receita)
  const total  = sorted.reduce((s, p) => s + p.receita, 0)
  let cum = 0
  return sorted.map(p => {
    cum += p.receita
    const cumPct = total > 0 ? (cum / total) * 100 : 0
    const pct    = total > 0 ? (p.receita / total) * 100 : 0
    const abcClass: AbcClass = cumPct <= 80 ? 'A' : cumPct <= 95 ? 'B' : 'C'
    return { nome: p.nome, receita: p.receita, qtd: p.qtd, custo: p.custo, categoria: p.categoria, abcClass, cumPct, pct }
  })
}

type AbcSortCol = 'receita' | 'qtd' | 'margemPct' | 'precoMedio'

function AbcExplainer() {
  const [open, setOpen] = useState(false)
  return (
    <div className="rounded-xl border border-surface-600 bg-surface-800 overflow-hidden">
      <button
        onClick={() => setOpen(v => !v)}
        className="w-full flex items-center justify-between px-4 py-3 hover:bg-surface-700 transition-colors text-left"
      >
        <div className="flex items-center gap-2">
          <Lightbulb className="w-4 h-4 text-yellow-400 shrink-0" />
          <span className="text-sm font-semibold text-gray-300">O que é a Curva ABC?</span>
          <span className="text-[10px] bg-surface-600 text-gray-400 px-2 py-0.5 rounded-full">conceito</span>
        </div>
        {open ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
      </button>

      {open && (
        <div className="px-4 pb-4 space-y-4 border-t border-surface-700">
          <p className="text-xs text-gray-400 leading-relaxed pt-3">
            A <strong className="text-white">Curva ABC</strong> (ou <strong className="text-white">Princípio de Pareto</strong>) é uma técnica
            que classifica seus produtos pelo impacto na receita. A ideia central é: <em className="text-brand-300">poucos produtos geram a maior parte do faturamento</em>.
          </p>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="rounded-xl bg-emerald-500/10 border border-emerald-500/25 p-3 space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-2xl font-black text-emerald-400">A</span>
                <span className="text-xs font-semibold text-emerald-300">Produtos Vitais</span>
              </div>
              <p className="text-xs text-gray-400 leading-relaxed">
                Respondem por <strong className="text-emerald-300">~80% da receita</strong> com poucos itens.
                São os mais importantes — nunca podem faltar no estoque e merecem atenção especial no preço e na margem.
              </p>
              <p className="text-[10px] text-emerald-500 mt-1">→ Monitore de perto, negocie melhor com fornecedor</p>
            </div>

            <div className="rounded-xl bg-yellow-500/10 border border-yellow-500/25 p-3 space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-2xl font-black text-yellow-400">B</span>
                <span className="text-xs font-semibold text-yellow-300">Produtos Importantes</span>
              </div>
              <p className="text-xs text-gray-400 leading-relaxed">
                Representam <strong className="text-yellow-300">~15% da receita</strong> (acumulado 80–95%).
                Têm bom potencial — podem virar classe A com ações de marketing ou ajuste de preço.
              </p>
              <p className="text-[10px] text-yellow-500 mt-1">→ Avalie promoções ou combos para alavancar</p>
            </div>

            <div className="rounded-xl bg-red-500/10 border border-red-500/25 p-3 space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-2xl font-black text-red-400">C</span>
                <span className="text-xs font-semibold text-red-300">Produtos Periféricos</span>
              </div>
              <p className="text-xs text-gray-400 leading-relaxed">
                Os últimos <strong className="text-red-300">~5% da receita</strong> com muitos itens.
                Vale questionar: custam estoque? Têm giro? Talvez seja hora de descontinuar alguns.
              </p>
              <p className="text-[10px] text-red-400 mt-1">→ Revise mix, considere descontinuar ou promover</p>
            </div>
          </div>

          <div className="rounded-xl bg-brand-500/8 border border-brand-500/20 p-3">
            <p className="text-xs text-gray-400 leading-relaxed">
              <strong className="text-brand-300">Como ler o gráfico Pareto:</strong> As barras mostram a receita de cada produto (da maior para a menor).
              A <span className="text-brand-300 font-semibold">linha azul</span> mostra o percentual <em>acumulado</em> — onde ela cruza
              a linha <span className="text-emerald-400 font-semibold">verde (80%)</span> termina a classe A,
              e onde cruza a <span className="text-yellow-400 font-semibold">amarela (95%)</span> termina a classe B.
              O restante é C.
            </p>
          </div>
        </div>
      )}
    </div>
  )
}

export function CurvaABCSection({ produtos, targetPct }: {
  produtos: { nome: string; receita: number; qtd: number; custo: number; categoria?: string }[]
  targetPct: number
}) {
  const [hoveredIdx,  setHoveredIdx]  = useState<number | null>(null)
  const [sortCol,     setSortCol]     = useState<AbcSortCol>('receita')
  const [sortDir,     setSortDir]     = useState<'desc' | 'asc'>('desc')
  const [filterClass, setFilterClass] = useState<AbcClass | null>(null)
  const [filterCat,   setFilterCat]   = useState<string | null>(null)

  const abcData  = useMemo(() => classifyABC(produtos), [produtos])
  const grandTotal = useMemo(() => produtos.reduce((s, p) => s + p.receita, 0), [produtos])

  const catWeights = useMemo(() => {
    const map: Record<string, { receita: number; qtd: number; count: number }> = {}
    abcData.forEach(p => {
      const cat = p.categoria || 'Sem categoria'
      if (!map[cat]) map[cat] = { receita: 0, qtd: 0, count: 0 }
      map[cat].receita += p.receita
      map[cat].qtd     += p.qtd
      map[cat].count   += 1
    })
    const cats = Object.entries(map).sort((a, b) => b[1].receita - a[1].receita)
    let cumCat = 0
    return cats.map(([cat, v]) => {
      cumCat += v.receita
      const cumPct = grandTotal > 0 ? (cumCat / grandTotal) * 100 : 0
      const pct    = grandTotal > 0 ? (v.receita  / grandTotal) * 100 : 0
      const cls: AbcClass = cumPct <= 80 ? 'A' : cumPct <= 95 ? 'B' : 'C'
      return { cat, ...v, pct, cumPct, cls }
    })
  }, [abcData, grandTotal])

  const tableData = useMemo(() => {
    let rows = [...abcData]
    if (filterClass) rows = rows.filter(p => p.abcClass === filterClass)
    if (filterCat)   rows = rows.filter(p => (p.categoria || 'Sem categoria') === filterCat)
    rows.sort((a, b) => {
      let va: number, vb: number
      if (sortCol === 'qtd') { va = a.qtd; vb = b.qtd }
      else if (sortCol === 'precoMedio') {
        va = a.qtd > 0 ? a.receita / a.qtd : 0
        vb = b.qtd > 0 ? b.receita / b.qtd : 0
      } else if (sortCol === 'margemPct') {
        const pm = (x: AbcProduto) => x.qtd > 0 ? x.receita / x.qtd : 0
        const cm = (x: AbcProduto) => x.qtd > 0 ? x.custo  / x.qtd : 0
        va = pm(a) > 0 && cm(a) > 0 ? ((pm(a) - cm(a)) / pm(a)) * 100 : -999
        vb = pm(b) > 0 && cm(b) > 0 ? ((pm(b) - cm(b)) / pm(b)) * 100 : -999
      } else { va = a.receita; vb = b.receita }
      return sortDir === 'desc' ? vb - va : va - vb
    })
    return rows
  }, [abcData, filterClass, filterCat, sortCol, sortDir])

  function toggleSort(col: AbcSortCol) {
    if (sortCol === col) setSortDir(d => d === 'desc' ? 'asc' : 'desc')
    else { setSortCol(col); setSortDir('desc') }
  }

  const chartData  = abcData.slice(0, 30)
  const maxReceita = chartData[0]?.receita ?? 1
  const W = 700, H = 200, PAD = { top: 14, right: 46, bottom: 36, left: 54 }
  const chartW = W - PAD.left - PAD.right
  const chartH = H - PAD.top  - PAD.bottom
  const barW   = Math.max(5, chartW / (chartData.length || 1) - 2)

  const linePoints = chartData.map((p, i) => {
    const slotW = chartW / chartData.length
    const x = PAD.left + slotW * i + slotW / 2
    const y = PAD.top  + chartH * (1 - p.cumPct / 100)
    return `${x.toFixed(1)},${y.toFixed(1)}`
  }).join(' ')

  const hasFilters = filterClass !== null || filterCat !== null

  return (
    <div className="space-y-4">
      {/* Painel explicativo — o que é Curva ABC */}
      <AbcExplainer />

      {/* Resumo A/B/C */}
      <div className="grid grid-cols-3 gap-3">
        {(['A', 'B', 'C'] as AbcClass[]).map(cls => {
          const items = abcData.filter(p => p.abcClass === cls)
          const clsRec = items.reduce((s, p) => s + p.receita, 0)
          const c = ABC_COLORS[cls]
          const isActive = filterClass === cls
          return (
            <button key={cls} onClick={() => setFilterClass(isActive ? null : cls)}
              className={`rounded-xl p-4 border text-left transition-all ${isActive ? `${c.bg} ${c.border} border-2` : 'bg-surface-800 border-surface-600 hover:border-surface-400'}`}
            >
              <div className="flex items-center justify-between mb-1.5">
                <span className={`text-2xl font-black ${c.text}`}>{cls}</span>
                <span className="text-[10px] text-gray-500">{items.length} produto{items.length !== 1 ? 's' : ''}</span>
              </div>
              <p className={`text-sm font-bold font-mono ${c.text}`}>{fmt(clsRec)}</p>
              <p className="text-[10px] text-gray-500 mt-0.5">
                {cls === 'A' ? 'Vitais · 80% receita' : cls === 'B' ? 'Importantes · 15%' : 'Periféricos · 5%'}
              </p>
            </button>
          )
        })}
      </div>

      {/* Pareto Chart */}
      <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
        <div className="flex items-center justify-between mb-3">
          <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wider flex items-center gap-2">
            <BarChart2 className="w-3.5 h-3.5 text-brand-400" /> Curva ABC — Pareto
            {chartData.length < abcData.length && <span className="text-gray-600 font-normal">(top {chartData.length})</span>}
          </h4>
          <div className="flex items-center gap-3 text-[10px] text-gray-500">
            {(['A','B','C'] as AbcClass[]).map(cls => (
              <span key={cls} className="flex items-center gap-1">
                <span className="inline-block w-2.5 h-2.5 rounded-sm" style={{ background: ABC_COLORS[cls].bar }} />
                {cls}
              </span>
            ))}
            <span className="flex items-center gap-1">
              <span className="inline-block w-4 h-0.5 bg-brand-400" />% Acum.
            </span>
          </div>
        </div>
        <div className="overflow-x-auto">
          <svg viewBox={`0 0 ${W} ${H}`} className="w-full" style={{ minWidth: Math.max(300, chartData.length * 20) }}>
            {[0.25, 0.5, 0.75, 1].map(f => (
              <g key={f}>
                <line x1={PAD.left} y1={PAD.top + chartH * (1 - f)} x2={W - PAD.right} y2={PAD.top + chartH * (1 - f)} stroke="#32323f" strokeWidth="1" />
                <text x={PAD.left - 4} y={PAD.top + chartH * (1 - f) + 4} textAnchor="end" fontSize="8" fill="#6b7280">
                  {fmtShort(maxReceita * f)}
                </text>
              </g>
            ))}
            {([80, 95] as const).map(pct => {
              const y = PAD.top + chartH * (1 - pct / 100)
              const col = pct === 80 ? '#10b981' : '#f59e0b'
              return (
                <g key={pct}>
                  <line x1={PAD.left} y1={y} x2={W - PAD.right} y2={y} stroke={col} strokeWidth="1" strokeDasharray="4,3" opacity="0.7" />
                  <text x={W - PAD.right + 3} y={y + 3} fontSize="7" fill={col}>{pct}%</text>
                </g>
              )
            })}
            {[25, 50, 75, 100].map(pct => (
              <text key={pct} x={W - PAD.right + 3} y={PAD.top + chartH * (1 - pct / 100) + 3} fontSize="7" fill="#4b5563">{pct}%</text>
            ))}
            {chartData.map((p, i) => {
              const slotW = chartW / chartData.length
              const x = PAD.left + slotW * i + (slotW - barW) / 2
              const bH = Math.max(2, (p.receita / maxReceita) * chartH)
              const color = ABC_COLORS[p.abcClass].bar
              const isHov = hoveredIdx === i
              return (
                <g key={p.nome} onMouseEnter={() => setHoveredIdx(i)} onMouseLeave={() => setHoveredIdx(null)} className="cursor-pointer">
                  <rect x={x} y={PAD.top + chartH - bH} width={barW} height={bH}
                    fill={color} opacity={hoveredIdx === null || isHov ? 1 : 0.35} rx="2"
                    style={{ filter: isHov ? 'brightness(1.2)' : undefined }} />
                  {chartData.length <= 20 && (
                    <text x={x + barW / 2} y={H - 2} textAnchor="middle" fontSize="5.5" fill={isHov ? '#d1d5db' : '#6b7280'}>
                      {p.nome.slice(0, 6)}
                    </text>
                  )}
                </g>
              )
            })}
            {chartData.length > 1 && (
              <polyline points={linePoints} fill="none" stroke="#42B6EE" strokeWidth="1.5" strokeLinejoin="round" />
            )}
            {hoveredIdx !== null && chartData[hoveredIdx] && (() => {
              const p     = chartData[hoveredIdx]
              const slotW = chartW / chartData.length
              const cx2   = PAD.left + slotW * hoveredIdx + slotW / 2
              const cy2   = PAD.top + chartH * (1 - p.cumPct / 100)
              const boxW  = 144, boxH = 40
              // Horizontal: cabe à direita? senão à esquerda
              const bxRaw = cx2 - boxW / 2
              const bx    = Math.max(PAD.left, Math.min(bxRaw, W - PAD.right - boxW))
              // Vertical: acima da linha cumulativa, mas nunca sair pelo topo
              const byRaw = cy2 - boxH - 8
              const by    = Math.max(PAD.top + 2, byRaw)
              return (
                <g style={{ pointerEvents: 'none' }}>
                  <circle cx={cx2} cy={cy2} r="3.5" fill="#42B6EE" stroke="#1e1e2e" strokeWidth="1" />
                  <rect x={bx} y={by} width={boxW} height={boxH} rx="5" fill="#1e1e2e" stroke="#32323f" strokeWidth="1" opacity="0.97" />
                  <text x={bx + boxW / 2} y={by + 13} textAnchor="middle" fontSize="8" fill="white" fontWeight="bold">
                    {p.nome.length > 22 ? p.nome.slice(0, 22) + '…' : p.nome}
                  </text>
                  <text x={bx + boxW / 2} y={by + 25} textAnchor="middle" fontSize="7.5" fill={ABC_COLORS[p.abcClass].bar}>
                    {fmt(p.receita)} · {p.pct.toFixed(1)}% · Acum: {p.cumPct.toFixed(1)}%
                  </text>
                  <text x={bx + boxW / 2} y={by + 36} textAnchor="middle" fontSize="7" fill="#9ca3af">
                    Classe {p.abcClass}
                  </text>
                </g>
              )
            })()}
          </svg>
        </div>
      </div>

      {/* Peso por categoria */}
      <div className="bg-surface-800 rounded-xl border border-surface-600 overflow-hidden">
        <div className="px-4 py-3 border-b border-surface-600">
          <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Peso por Categoria</h4>
        </div>
        <div className="divide-y divide-surface-700">
          {catWeights.map(cw => {
            const clr = ABC_COLORS[cw.cls]
            const isFiltered = filterCat === cw.cat
            return (
              <button key={cw.cat} onClick={() => setFilterCat(isFiltered ? null : cw.cat)}
                className={`w-full flex items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-surface-700 ${isFiltered ? 'bg-surface-700' : ''}`}
              >
                <span className={`text-xs font-black w-4 ${clr.text}`}>{cw.cls}</span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs text-gray-200 truncate">{cw.cat}</span>
                    <span className={`text-xs font-mono font-bold ml-3 shrink-0 ${clr.text}`}>{cw.pct.toFixed(1)}%</span>
                  </div>
                  <div className="h-1.5 bg-surface-700 rounded-full overflow-hidden">
                    <div className="h-full rounded-full" style={{ width: `${cw.pct}%`, background: clr.bar }} />
                  </div>
                </div>
                <div className="text-right shrink-0 ml-2">
                  <p className="text-xs font-mono text-gray-200">{fmt(cw.receita)}</p>
                  <p className="text-[10px] text-gray-500">{cw.count} prod.</p>
                </div>
              </button>
            )
          })}
        </div>
      </div>

      {/* Tabela com headers clicáveis */}
      <div className="rounded-xl border border-surface-600 overflow-x-auto">
        {hasFilters && (
          <div className="px-4 py-2 bg-surface-800 border-b border-surface-600 flex items-center gap-2 text-xs text-gray-400">
            Filtrando:
            {filterClass && <span className={`font-bold ${ABC_COLORS[filterClass].text}`}>Classe {filterClass}</span>}
            {filterCat   && <span className="text-brand-300">{filterCat}</span>}
            <button onClick={() => { setFilterClass(null); setFilterCat(null) }}
              className="ml-auto flex items-center gap-1 hover:text-gray-200 transition-colors">
              <X className="w-3.5 h-3.5" /> Limpar
            </button>
          </div>
        )}
        <table className="w-full text-sm">
          <thead className="bg-surface-800">
            <tr className="text-left">
              <th className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold">#</th>
              <th className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold">Produto</th>
              <th className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold text-center">ABC</th>
              {([
                ['qtd',       'Qtd'],
                ['precoMedio','Preço Médio'],
                ['margemPct', 'Margem'],
                ['receita',   'Receita'],
              ] as [AbcSortCol, string][]).map(([col, label]) => (
                <th key={col}
                  className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold whitespace-nowrap cursor-pointer hover:text-gray-300 select-none transition-colors"
                  onClick={() => toggleSort(col)}
                >
                  <span className="flex items-center gap-1">
                    {label}
                    {sortCol === col
                      ? sortDir === 'desc' ? <ChevronDown className="w-3 h-3 text-brand-400" /> : <ChevronUp className="w-3 h-3 text-brand-400" />
                      : <ChevronDown className="w-3 h-3 opacity-20" />}
                  </span>
                </th>
              ))}
              <th className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold whitespace-nowrap">% Acum.</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-600">
            {tableData.map((p, i) => {
              const precoMedio = p.qtd > 0 ? p.receita / p.qtd : 0
              const custoMedio = p.qtd > 0 ? p.custo  / p.qtd : 0
              const margemPct  = precoMedio > 0 && custoMedio > 0 ? ((precoMedio - custoMedio) / precoMedio) * 100 : null
              const precoSug   = custoMedio > 0 ? custoMedio / (1 - targetPct / 100) : null
              const clr = ABC_COLORS[p.abcClass]
              return (
                <tr key={p.nome} className="hover:bg-surface-700 transition-colors">
                  <td className="px-4 py-2.5 text-gray-500 text-xs font-mono">{i + 1}</td>
                  <td className="px-4 py-2.5 font-medium text-white max-w-[180px]">
                    <p className="truncate">{p.nome}</p>
                    {p.categoria && <span className="text-[10px] bg-surface-600 text-gray-400 px-1.5 rounded">{p.categoria}</span>}
                  </td>
                  <td className="px-4 py-2.5 text-center">
                    <span className={`text-sm font-black ${clr.text}`}>{p.abcClass}</span>
                  </td>
                  <td className="px-4 py-2.5 text-gray-300 text-xs font-mono font-semibold">{p.qtd}x</td>
                  <td className="px-4 py-2.5 font-mono text-gray-200 text-xs">
                    {precoMedio > 0 ? (
                      <span>
                        {fmt(precoMedio)}
                        {precoSug !== null && Math.abs(precoSug - precoMedio) >= 0.50 && (
                          <span className={`ml-1 text-[10px] ${precoSug > precoMedio ? 'text-red-400' : 'text-emerald-400'}`}>
                            ({precoSug > precoMedio ? '↑' : '↓'}{fmt(precoSug)})
                          </span>
                        )}
                      </span>
                    ) : '—'}
                  </td>
                  <td className="px-4 py-2.5">
                    {margemPct !== null ? (
                      <span className={`text-xs font-mono font-bold ${margemPct >= targetPct ? 'text-emerald-400' : margemPct >= 0 ? 'text-yellow-400' : 'text-red-400'}`}>
                        {margemPct.toFixed(0)}%
                      </span>
                    ) : <span className="text-gray-600 text-xs">—</span>}
                  </td>
                  <td className="px-4 py-2.5">
                    <span className={`font-mono font-bold text-xs ${clr.text}`}>{fmt(p.receita)}</span>
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="flex items-center gap-2">
                      <div className="w-14 h-1.5 bg-surface-600 rounded-full overflow-hidden">
                        <div className="h-full rounded-full" style={{ width: `${p.cumPct}%`, background: clr.bar }} />
                      </div>
                      <span className="text-[10px] text-gray-500 font-mono">{p.cumPct.toFixed(0)}%</span>
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        <div className="px-4 py-2.5 border-t border-surface-600 flex flex-wrap gap-4 text-[10px] text-gray-500">
          <span><span className="text-emerald-400 font-bold">A</span> = produtos vitais que formam 80% da receita</span>
          <span><span className="text-yellow-400 font-bold">B</span> = importantes · 80–95%</span>
          <span><span className="text-red-400 font-bold">C</span> = periféricos · acima de 95%</span>
          <span className="ml-auto">Clique no cabeçalho para ordenar · clique nas classes/categorias para filtrar</span>
        </div>
      </div>
    </div>
  )
}

// ── Modal com gráfico de evolução ─────────────────────────────────────────────
