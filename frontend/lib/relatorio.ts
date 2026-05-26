// =============================================================================
// lib/relatorio.ts — Gerador de relatório PDF do Controle Financeiro
// Usa jsPDF + jspdf-autotable para gerar PDF vetorial limpo (não screenshot)
// =============================================================================

import { FinanceiroDto } from './api'

// jsPDF tem problema com SSR — só importa no browser
async function getJsPDF() {
  const { default: jsPDF } = await import('jspdf')
  await import('jspdf-autotable')
  return jsPDF
}

// ── Paleta de cores ───────────────────────────────────────────────────────────
const PURPLE    = [88,  56,  250] as [number, number, number]
const DARK      = [18,  18,  26]  as [number, number, number]
const GRAY_DARK = [60,  60,  80]  as [number, number, number]
const GRAY_MID  = [120, 120, 140] as [number, number, number]
const WHITE     = [255, 255, 255] as [number, number, number]
const GREEN     = [34,  197, 94]  as [number, number, number]
const AMBER     = [245, 158, 11]  as [number, number, number]
const RED       = [239, 68,  68]  as [number, number, number]

function fmt(v: number) {
  return `R$ ${v.toFixed(2).replace('.', ',').replace(/\B(?=(\d{3})+(?!\d))/g, '.')}`
}

function fmtData(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  })
}

const FORMA_LABEL: Record<string, string> = {
  Dinheiro:      'Dinheiro',
  Pix:           'Pix',
  CartaoCredito: 'Cartão de Crédito',
  CartaoDebito:  'Cartão de Débito',
  Crediario:     'Crediário',
}

// ── Função principal ──────────────────────────────────────────────────────────
export async function gerarRelatorioPDF(
  d: FinanceiroDto,
  periodo: { inicio: string; fim: string },
) {
  const JsPDF = await getJsPDF()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const doc = new (JsPDF as any)({ orientation: 'portrait', unit: 'mm', format: 'a4' }) as any

  const PW = 210  // largura A4
  const ML = 14   // margem esquerda
  const MR = 14   // margem direita
  const CW = PW - ML - MR  // largura útil
  let y = 0

  // ── Cabeçalho roxo ─────────────────────────────────────────────────────────
  doc.setFillColor(...PURPLE)
  doc.rect(0, 0, PW, 38, 'F')

  doc.setTextColor(...WHITE)
  doc.setFontSize(20)
  doc.setFont('helvetica', 'bold')
  doc.text('Santuário Nerd', ML, 16)

  doc.setFontSize(10)
  doc.setFont('helvetica', 'normal')
  doc.text('Controle Financeiro — Relatório de Período', ML, 24)

  // Período no canto direito
  const iniStr = new Date(periodo.inicio + 'T00:00:00').toLocaleDateString('pt-BR')
  const fimStr = new Date(periodo.fim   + 'T00:00:00').toLocaleDateString('pt-BR')
  const periodoStr = iniStr === fimStr ? iniStr : `${iniStr} a ${fimStr}`
  doc.setFontSize(9)
  doc.setTextColor(200, 190, 255)
  doc.text(periodoStr, PW - MR, 16, { align: 'right' })

  const geradoEm = new Date().toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
  doc.text(`Gerado em ${geradoEm}`, PW - MR, 22, { align: 'right' })

  y = 46

  // ── KPIs principais ────────────────────────────────────────────────────────
  const kpis = [
    { label: 'Receita Total',     value: fmt(d.receita),      color: WHITE },
    { label: 'Custo Estimado',    value: fmt(d.custo),        color: AMBER },
    { label: 'Margem Bruta',      value: fmt(d.margem),       color: d.margem >= 0 ? GREEN : RED },
    { label: 'Crediários Abertos',value: fmt(d.crediarios),   color: AMBER },
  ]

  const kpiW = CW / kpis.length
  kpis.forEach((k, i) => {
    const x = ML + i * kpiW
    doc.setFillColor(...DARK)
    doc.roundedRect(x, y, kpiW - 3, 22, 2, 2, 'F')

    doc.setTextColor(...(k.color as [number, number, number]))
    doc.setFontSize(12)
    doc.setFont('helvetica', 'bold')
    doc.text(k.value, x + (kpiW - 3) / 2, y + 10, { align: 'center' })

    doc.setTextColor(...GRAY_MID)
    doc.setFontSize(7)
    doc.setFont('helvetica', 'normal')
    doc.text(k.label, x + (kpiW - 3) / 2, y + 17, { align: 'center' })
  })

  y += 28

  // ── Origem da Receita ──────────────────────────────────────────────────────
  const pctComanda = d.receita > 0 ? (d.receitaComandas / d.receita * 100).toFixed(1) : '0.0'
  const pctAvulsa  = d.receita > 0 ? (d.receitaAvulsa  / d.receita * 100).toFixed(1) : '0.0'

  sectionTitle(doc, 'Origem da Receita', y, ML)
  y += 7

  doc.autoTable({
    startY: y,
    margin: { left: ML, right: MR },
    head: [['Origem', 'Valor', '% do Total']],
    body: [
      ['Comandas (Mesas)',    fmt(d.receitaComandas), `${pctComanda}%`],
      ['Venda Avulsa (Balcão)', fmt(d.receitaAvulsa), `${pctAvulsa}%`],
      ['Total', fmt(d.receita), '100%'],
    ],
    headStyles:  { fillColor: PURPLE, textColor: WHITE, fontStyle: 'bold', fontSize: 9 },
    bodyStyles:  { fontSize: 9, textColor: DARK },
    alternateRowStyles: { fillColor: [240, 238, 255] },
    columnStyles: { 1: { halign: 'right' }, 2: { halign: 'right' } },
    foot: [],
    didParseCell: (data: any) => {
      // Última linha (Total) em negrito
      if (data.row.index === 2) {
        data.cell.styles.fontStyle = 'bold'
        data.cell.styles.fillColor = [220, 215, 255]
      }
    },
  })

  y = doc.lastAutoTable.finalY + 8

  // ── Formas de Pagamento ────────────────────────────────────────────────────
  if (d.pagamentosPorForma.length > 0) {
    sectionTitle(doc, 'Recebimentos por Forma de Pagamento', y, ML)
    y += 7

    const rows = d.pagamentosPorForma.map(f => [
      FORMA_LABEL[f.forma] ?? f.forma,
      String(f.quantidade),
      fmt(f.total),
    ])

    doc.autoTable({
      startY: y,
      margin: { left: ML, right: MR },
      head: [['Forma de Pagamento', 'Qtd. Transações', 'Total']],
      body: rows,
      headStyles: { fillColor: PURPLE, textColor: WHITE, fontStyle: 'bold', fontSize: 9 },
      bodyStyles: { fontSize: 9, textColor: DARK },
      alternateRowStyles: { fillColor: [240, 238, 255] },
      columnStyles: { 1: { halign: 'center' }, 2: { halign: 'right' } },
    })

    y = doc.lastAutoTable.finalY + 8

    // Sub-tabelas de transações por forma (drill-down)
    for (const f of d.pagamentosPorForma) {
      if (!f.transacoes || f.transacoes.length === 0) continue

      const formaLabel = FORMA_LABEL[f.forma] ?? f.forma

      // Verifica se cabe na página, senão adiciona nova
      if (y > 240) { doc.addPage(); y = 16 }

      doc.setFontSize(8)
      doc.setFont('helvetica', 'bold')
      doc.setTextColor(...GRAY_DARK)
      doc.text(`Transações — ${formaLabel}`, ML, y)
      y += 5

      const txRows = f.transacoes.map(t => [
        t.origem === 'Comanda' ? 'Mesa' : 'Balcão',
        t.cliente ?? '—',
        fmtData(t.data),
        fmt(t.valor),
      ])

      doc.autoTable({
        startY: y,
        margin: { left: ML + 4, right: MR },
        head: [['Origem', 'Cliente', 'Data/Hora', 'Valor']],
        body: txRows,
        headStyles: { fillColor: GRAY_DARK, textColor: WHITE, fontSize: 8 },
        bodyStyles: { fontSize: 8, textColor: DARK },
        alternateRowStyles: { fillColor: [248, 248, 255] },
        columnStyles: { 3: { halign: 'right' } },
      })

      y = doc.lastAutoTable.finalY + 6
    }
  }

  // ── Top Produtos ───────────────────────────────────────────────────────────
  if (d.topProdutos.length > 0) {
    if (y > 220) { doc.addPage(); y = 16 }

    sectionTitle(doc, 'Top Produtos', y, ML)
    y += 7

    doc.autoTable({
      startY: y,
      margin: { left: ML, right: MR },
      head: [['Produto', 'Qtd', 'Receita', 'Custo', 'Margem']],
      body: d.topProdutos.map(p => [
        p.nome,
        String(p.qtd),
        fmt(p.receita),
        fmt(p.custo),
        fmt(p.margem),
      ]),
      headStyles: { fillColor: PURPLE, textColor: WHITE, fontStyle: 'bold', fontSize: 9 },
      bodyStyles: { fontSize: 9, textColor: DARK },
      alternateRowStyles: { fillColor: [240, 238, 255] },
      columnStyles: {
        1: { halign: 'center' },
        2: { halign: 'right' },
        3: { halign: 'right' },
        4: { halign: 'right' },
      },
    })

    y = doc.lastAutoTable.finalY + 8
  }

  // ── Receita Dia a Dia ──────────────────────────────────────────────────────
  if (d.diaDia.length > 1) {
    if (y > 220) { doc.addPage(); y = 16 }

    sectionTitle(doc, 'Receita Dia a Dia', y, ML)
    y += 7

    doc.autoTable({
      startY: y,
      margin: { left: ML, right: MR },
      head: [['Data', 'Receita', 'Custo', 'Margem']],
      body: d.diaDia.map(dia => [
        dia.dia,
        fmt(dia.receita),
        fmt(dia.custo),
        fmt(dia.receita - dia.custo),
      ]),
      headStyles: { fillColor: PURPLE, textColor: WHITE, fontStyle: 'bold', fontSize: 9 },
      bodyStyles: { fontSize: 9, textColor: DARK },
      alternateRowStyles: { fillColor: [240, 238, 255] },
      columnStyles: {
        1: { halign: 'right' },
        2: { halign: 'right' },
        3: { halign: 'right' },
      },
    })
  }

  // ── Rodapé em todas as páginas ─────────────────────────────────────────────
  const total = doc.internal.getNumberOfPages()
  for (let i = 1; i <= total; i++) {
    doc.setPage(i)
    doc.setFillColor(...DARK)
    doc.rect(0, 288, PW, 9, 'F')
    doc.setFontSize(7)
    doc.setTextColor(...GRAY_MID)
    doc.setFont('helvetica', 'normal')
    doc.text('Santuário Nerd — Relatório Financeiro Confidencial', ML, 293)
    doc.text(`Página ${i} de ${total}`, PW - MR, 293, { align: 'right' })
  }

  // ── Download ───────────────────────────────────────────────────────────────
  const nomePeriodo = iniStr === fimStr
    ? iniStr.replace(/\//g, '-')
    : `${iniStr.replace(/\//g, '-')}_${fimStr.replace(/\//g, '-')}`
  doc.save(`relatorio-financeiro-${nomePeriodo}.pdf`)
}

// ── Helper: título de seção ───────────────────────────────────────────────────
function sectionTitle(doc: any, texto: string, y: number, ml: number) {
  doc.setFontSize(10)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...DARK)
  doc.text(texto, ml, y)
  doc.setDrawColor(...PURPLE)
  doc.setLineWidth(0.5)
  doc.line(ml, y + 1.5, ml + 182, y + 1.5)
}
