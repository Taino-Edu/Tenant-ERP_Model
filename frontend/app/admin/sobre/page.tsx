'use client'

import { useEffect, useState } from 'react'
import {
  Info, Tag, Calendar, CheckCircle, Wrench, Zap,
  BookOpen, ChevronDown, ChevronUp,
  LayoutDashboard, ShoppingBag, ShoppingCart, Package,
  Users, CreditCard, Trophy, BarChart2, Layers, Megaphone,
} from 'lucide-react'

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

interface ManualSection {
  icon: React.ReactNode
  title: string
  color: string
  steps: { title: string; desc: string }[]
  tips?: string[]
}

const MANUAL: ManualSection[] = [
  {
    icon: <LayoutDashboard className="w-4 h-4" />,
    title: 'Dashboard',
    color: 'text-brand-400',
    steps: [
      { title: 'Visão geral financeira', desc: 'Mostra o faturamento do dia, semana e mês. Os cartões no topo resumem receita, ticket médio e número de vendas.' },
      { title: 'Gráfico de receita', desc: 'Barras diárias dos últimos 30 dias. Passe o mouse sobre a barra para ver o valor exato do dia.' },
      { title: 'Comandas abertas', desc: 'Lista em tempo real das comandas que ainda estão abertas. Clique em qualquer uma para ir direto ao painel de comandas.' },
      { title: 'Top produtos', desc: 'Ranking dos itens mais vendidos no período selecionado.' },
    ],
    tips: ['O dashboard atualiza sozinho a cada vez que você abre a página.'],
  },
  {
    icon: <ShoppingBag className="w-4 h-4" />,
    title: 'Comandas (Mesa / QR Code)',
    color: 'text-blue-400',
    steps: [
      { title: 'Como funciona', desc: 'O cliente escaneia o QR Code da mesa e abre a própria comanda. O sistema identifica o cliente automaticamente.' },
      { title: 'Adicionar itens', desc: 'No painel admin, clique na comanda aberta e pesquise o produto. Você também pode adicionar itens manualmente com nome e preço.' },
      { title: 'Fechar a comanda', desc: 'Clique em "Fechar Comanda", escolha a forma de pagamento (Dinheiro, Pix, Cartão, Crediário, Pontos ou Cashback) e confirme. O sistema desconta o estoque automaticamente.' },
      { title: 'Split de pagamento', desc: 'É possível usar duas formas de pagamento ao mesmo tempo. Selecione a segunda forma e informe o valor dela.' },
      { title: 'Cancelar comanda', desc: 'Se o cliente desistir, use "Cancelar". O estoque não é alterado e a comanda some do painel.' },
      { title: 'Agrupar itens', desc: 'Itens iguais são somados automaticamente na visualização para facilitar a conferência.' },
    ],
    tips: [
      'Comandas com status "Aberta" ainda não têm itens. "Em Andamento" já tem pelo menos um item.',
      'O admin pode abrir uma comanda manualmente em nome de um cliente pelo botão "Nova Comanda".',
    ],
  },
  {
    icon: <ShoppingCart className="w-4 h-4" />,
    title: 'Venda Avulsa — Frente de Caixa (PDV)',
    color: 'text-emerald-400',
    steps: [
      { title: 'Quando usar', desc: 'Para vendas rápidas no balcão, sem precisar de QR Code ou comanda. Ideal para clientes que chegam e pagam na hora.' },
      { title: 'Adicionar produtos', desc: 'Pesquise o produto pelo nome ou escaneie o código de barras. Ajuste a quantidade e clique em adicionar.' },
      { title: 'Selecionar cliente (opcional)', desc: 'Se o cliente é cadastrado, selecione-o para vincular a venda. Isso acumula pontos Maikon automaticamente e permite Crediário, Pontos e Cashback.' },
      { title: 'Finalizar venda', desc: 'Escolha a forma de pagamento e confirme. No celular, use a barra fixa no rodapé para finalizar sem rolar a tela.' },
      { title: 'Desconto', desc: 'Aplique desconto percentual antes de finalizar. O sistema mostra o valor original e o valor com desconto.' },
    ],
    tips: [
      'Vendas avulsas com cliente identificado ficam no histórico do cliente.',
      'Sem cliente selecionado, a venda é anônima — aparece apenas nos relatórios gerais.',
    ],
  },
  {
    icon: <Package className="w-4 h-4" />,
    title: 'Estoque (Produtos)',
    color: 'text-orange-400',
    steps: [
      { title: 'Cadastrar produto', desc: 'Vá em Estoque → Novo Produto. Preencha nome, categoria, preço de custo, preço de venda e estoque inicial.' },
      { title: 'Estoque mínimo', desc: 'Defina um estoque mínimo para receber alertas quando o produto estiver baixo. Aparece em destaque no painel.' },
      { title: 'Promoção', desc: 'Ative o campo "Em Promoção" e informe o preço promocional. O sistema usa o preço com desconto automaticamente nas vendas.' },
      { title: 'Código de barras', desc: 'Cadastre o código de barras para agilizar vendas no caixa — basta escanear para adicionar o produto.' },
      { title: 'Ajuste de estoque', desc: 'Para corrigir o estoque (entrada de mercadoria, inventário), use o botão de ajuste manual no produto.' },
      { title: 'Desativar produto', desc: 'Produtos inativos não aparecem nas vendas, mas o histórico é mantido. Use isso em vez de excluir.' },
    ],
    tips: [
      'O estoque é descontado automaticamente a cada venda — comanda ou PDV.',
      'Produtos com estoque zerado ainda podem ser vendidos manualmente, mas aparecem com aviso.',
    ],
  },
  {
    icon: <Users className="w-4 h-4" />,
    title: 'Clientes & Cashback',
    color: 'text-purple-400',
    steps: [
      { title: 'Cadastrar cliente', desc: 'Clique em "Novo Cliente". Nome é obrigatório; CPF, WhatsApp e e-mail são opcionais mas ajudam na identificação.' },
      { title: 'Pontos Maikon', desc: 'A cada R$1 gasto, o cliente ganha 1 ponto. Os pontos expiram em 30 dias após a última compra. Use para dar desconto em comandas e vendas avulsas.' },
      { title: 'Adicionar pontos manualmente', desc: 'Selecione o cliente no painel e informe a quantidade de pontos e o motivo (ex: "Campeonato de Pokémon").' },
      { title: 'Cashback (Saldo)', desc: 'Diferente de pontos — é saldo em reais que o cliente pode usar como pagamento. Crédite ou débite manualmente pelo painel.' },
      { title: 'Histórico completo', desc: 'Clique em "Ver Histórico" no painel do cliente para ver todas as comandas, vendas no caixa, crediários e campeonatos do cliente em um único lugar.' },
      { title: 'Clientes inativos', desc: 'A aba "Inativos" mostra clientes que não aparecem há mais de 30 dias — útil para campanhas de reativação.' },
    ],
    tips: [
      'Pontos e cashback são coisas diferentes: pontos têm validade e são 1:1 com centavos; cashback é saldo em reais sem validade.',
      'Para redefinir a senha de um cliente, use o botão "Redefinir Senha" no painel lateral.',
    ],
  },
  {
    icon: <CreditCard className="w-4 h-4" />,
    title: 'Crediário',
    color: 'text-orange-400',
    steps: [
      { title: 'O que é', desc: 'O cliente leva os produtos e paga depois. O sistema cria uma dívida vinculada ao cliente com vencimento em 30 dias.' },
      { title: 'Como abrir', desc: 'Ao fechar uma comanda ou venda avulsa, selecione "Crediário" como forma de pagamento. O cliente precisa estar cadastrado.' },
      { title: 'Um crediário por vez', desc: 'Cada cliente só pode ter um crediário aberto. Novas compras no crediário acumulam no mesmo saldo.' },
      { title: 'Ver os itens', desc: 'No painel de crediário, expanda o card do cliente para ver exatamente o que ele levou em todas as visitas.' },
      { title: 'Registrar pagamento parcial', desc: 'Clique em "Registrar Pagamento" e informe o valor pago e a forma de pagamento. O saldo é atualizado automaticamente.' },
      { title: 'Quitar o crediário', desc: 'Quando o valor total for pago, clique em "Marcar como Pago". O cliente fica liberado para abrir um novo.' },
      { title: 'Crediário vencido', desc: 'Aparece em vermelho no painel quando passou dos 30 dias sem pagamento. Use para cobrar os clientes em atraso.' },
    ],
    tips: [
      'O vencimento é renovado automaticamente sempre que o cliente faz um novo pagamento parcial.',
      'Crediários criados via venda avulsa e via comanda aparecem juntos no mesmo painel.',
    ],
  },
  {
    icon: <Trophy className="w-4 h-4" />,
    title: 'Campeonatos',
    color: 'text-yellow-400',
    steps: [
      { title: 'Criar campeonato', desc: 'Vá em Campeonatos → Novo. Informe nome, jogo (Pokémon, Magic, etc.), data, taxa de inscrição e número máximo de participantes.' },
      { title: 'Status do campeonato', desc: '"Planejado" → "Inscrições Abertas" → "Em Andamento" → "Finalizado". Mude o status conforme o evento avança.' },
      { title: 'Inscrever participantes', desc: 'No painel do campeonato, adicione participantes manualmente ou deixe que eles se inscrevam pela landing page pública.' },
      { title: 'Pré-inscrições da landing page', desc: 'Clientes sem conta podem se pré-inscrever pelo link público do campeonato. Você aprova ou rejeita as pré-inscrições no painel.' },
      { title: 'Definir colocações', desc: 'Após o campeonato, clique no participante e informe o lugar (1º, 2º, 3º...). O sistema monta o pódio automaticamente.' },
      { title: 'Pódio público', desc: 'O resultado final aparece na página pública do campeonato para todos verem.' },
    ],
    tips: [
      'A taxa de inscrição é registrada manualmente — o sistema não cobra automaticamente.',
      'Use "Cancelado" para campeonatos que não aconteceram para manter o histórico limpo.',
    ],
  },
  {
    icon: <BarChart2 className="w-4 h-4" />,
    title: 'Relatórios',
    color: 'text-cyan-400',
    steps: [
      { title: 'Relatório PDV', desc: 'Mostra todas as vendas avulsas do período: receita dia a dia, top produtos vendidos e formas de pagamento usadas.' },
      { title: 'Relatório de Clientes', desc: 'Lista todos os clientes com pontos, cashback e status de atividade. Ajuda a identificar quem está ativo e quem parou de visitar.' },
      { title: 'Comandas Abertas', desc: 'Mostra as comandas que estão há mais dias abertas. Útil para identificar clientes que ainda não fecharam a conta.' },
      { title: 'Relatório Financeiro', desc: 'Visão consolidada de receitas por período, formas de pagamento e ticket médio.' },
      { title: 'Exportar PDF', desc: 'Cada relatório tem botão de exportação em PDF para imprimir ou compartilhar.' },
    ],
  },
  {
    icon: <Layers className="w-4 h-4" />,
    title: 'Catálogo TCG',
    color: 'text-pink-400',
    steps: [
      { title: 'O que é', desc: 'Catálogo integrado de cartas de jogos de coleção (Pokémon, Magic, etc.) com preços de mercado atualizados.' },
      { title: 'Buscar cartas', desc: 'Pesquise por nome da carta e jogo. O sistema busca na base de dados externa e mostra preços de referência.' },
      { title: 'Adicionar à comanda', desc: 'Encontrou a carta? Adicione diretamente na comanda aberta do cliente, com preço de mercado como referência.' },
    ],
    tips: ['Os preços do catálogo são referência de mercado — você define o preço final na comanda.'],
  },
  {
    icon: <Megaphone className="w-4 h-4" />,
    title: 'Anúncios e Banners',
    color: 'text-rose-400',
    steps: [
      { title: 'Criar anúncio', desc: 'Vá em Anúncios → Novo. Escolha o tipo (Banner, Aviso ou Destaque), escreva o texto e defina se tem imagem e data de expiração.' },
      { title: 'Banners', desc: 'Aparecem em destaque na área do cliente — ideal para promoções e eventos.' },
      { title: 'Avisos', desc: 'Mensagens informativas que aparecem em menor destaque. Bom para horários especiais ou comunicados.' },
      { title: 'Expiração automática', desc: 'Defina uma data de expiração e o anúncio some automaticamente. Útil para promoções com prazo.' },
    ],
  },
]

function ManualSection({ section }: { section: ManualSection }) {
  const [open, setOpen] = useState(false)

  return (
    <div className={`border rounded-xl overflow-hidden transition-all ${open ? 'border-surface-400' : 'border-surface-600'}`}>
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center gap-3 px-4 py-3.5 text-left hover:bg-surface-700/50 transition-colors"
      >
        <span className={`${section.color} shrink-0`}>{section.icon}</span>
        <span className="font-semibold text-white text-sm flex-1">{section.title}</span>
        {open
          ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" />
          : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />
        }
      </button>

      {open && (
        <div className="px-4 pb-4 space-y-3 border-t border-surface-600">
          <div className="pt-3 space-y-2.5">
            {section.steps.map((step, i) => (
              <div key={i} className="flex gap-3">
                <span className="shrink-0 w-5 h-5 rounded-full bg-surface-700 border border-surface-500 flex items-center justify-center text-[10px] font-bold text-gray-400 mt-0.5">
                  {i + 1}
                </span>
                <div>
                  <p className="text-sm font-medium text-white">{step.title}</p>
                  <p className="text-xs text-gray-400 mt-0.5 leading-relaxed">{step.desc}</p>
                </div>
              </div>
            ))}
          </div>

          {section.tips && section.tips.length > 0 && (
            <div className="mt-3 pt-3 border-t border-surface-600 space-y-1.5">
              <p className="text-[10px] font-semibold text-brand-400 uppercase tracking-wider">Dicas</p>
              {section.tips.map((tip, i) => (
                <div key={i} className="flex items-start gap-2 text-xs text-gray-400">
                  <span className="text-brand-400 shrink-0 mt-0.5">→</span>
                  {tip}
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
          <p className="text-sm text-gray-500 mt-0.5">Santuário Nerd — Plataforma de Gestão</p>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Tag className="w-4 h-4 text-brand-400" />
          <span className="text-sm font-mono font-semibold text-brand-400">{version}</span>
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
          {MANUAL.map(section => (
            <ManualSection key={section.title} section={section} />
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
