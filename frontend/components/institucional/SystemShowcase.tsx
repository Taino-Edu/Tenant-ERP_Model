'use client'
// =============================================================================
// SystemShowcase.tsx — As telas do Octus, para quem vai indicar o Octus.
//
// O afiliado precisa saber o que está apresentando. Descrição em texto não
// resolve: "PDV integrado ao fiscal" não desenha nada na cabeça de um contador
// que nunca abriu o sistema.
//
// As telas abaixo são DESENHADAS EM CÓDIGO, não capturas de tela. A escolha é
// deliberada e tem três motivos:
//
//  1. Captura envelhece. Toda vez que um botão do painel muda de lugar, o PNG
//     na landing passa a mostrar um produto que não existe mais — e ninguém
//     lembra de recapturar.
//  2. Captura não tem tema. O site troca claro/escuro; um PNG claro colado numa
//     página escura vira o mesmo borrão que a arte do hero era.
//  3. Peso. Quatro capturas em retina somam alguns MB numa página cujo objetivo
//     é carregar rápido no celular de quem clicou num link de indicação.
//
// O que está aqui é a estrutura real de cada tela (as colunas do estoque, os
// KPIs do relatório, o fluxo do PDV), não um layout inventado. Para trocar por
// capturas reais depois, o ponto de substituição é o miolo de cada função
// Tela*: a moldura, as abas e o enquadramento continuam valendo.
// =============================================================================

import { useState } from 'react'
import { Search, ShoppingCart, TrendingUp } from 'lucide-react'
import type { InstitucionalTheme } from '@/lib/institucional'

const NAVY = '#071f3d'
const CYAN = '#28B0D6'

type Aba = { id: string; label: string; titulo: string; desc: string; tela: () => JSX.Element }

/** Moldura de navegador. Existe para o miolo ler como "sistema", e não como
 *  um bloco de HTML solto no meio da página. */
function Moldura({ url, children, theme }: { url: string; children: React.ReactNode; theme: InstitucionalTheme }) {
  return (
    <div className={`overflow-hidden rounded-2xl border shadow-2xl ${theme.border}`} style={{ backgroundColor: NAVY }}>
      <div className="flex items-center gap-2 border-b border-white/10 px-4 py-3">
        <span className="flex gap-1.5">
          {['#ff5f57', '#febc2e', '#28c840'].map(cor => (
            <span key={cor} className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: cor }} />
          ))}
        </span>
        <span className="ml-2 flex-1 truncate rounded-md bg-white/5 px-3 py-1 text-[11px] text-slate-400">{url}</span>
      </div>
      <div className="p-4 sm:p-6">{children}</div>
    </div>
  )
}

const Rotulo = ({ children }: { children: React.ReactNode }) => (
  <p className="text-[10px] font-extrabold uppercase tracking-[0.16em] text-slate-500">{children}</p>
)

function TelaPdv() {
  const itens = [
    ['Camiseta básica — P', '2', '79,80'],
    ['Boné aba reta', '1', '59,90'],
    ['Meia cano alto — 39/42', '3', '44,70'],
  ]
  return (
    <div className="grid gap-4 sm:grid-cols-[1.4fr_1fr]">
      <div className="space-y-3">
        <div className="flex items-center gap-2 rounded-lg bg-white/5 px-3 py-2.5">
          <Search size={14} className="text-slate-500" />
          <span className="text-xs text-slate-400">Bipe o código ou busque o produto…</span>
        </div>
        <div className="space-y-1.5">
          {itens.map(([nome, qtd, valor]) => (
            <div key={nome} className="flex items-center gap-3 rounded-lg bg-white/[0.04] px-3 py-2.5">
              <span className="flex-1 truncate text-xs font-semibold text-slate-200">{nome}</span>
              <span className="rounded bg-white/10 px-1.5 py-0.5 text-[10px] font-bold text-slate-300">{qtd}x</span>
              <span className="text-xs font-bold tabular-nums text-slate-100">R$ {valor}</span>
            </div>
          ))}
        </div>
      </div>
      <div className="flex flex-col justify-between rounded-xl border border-white/10 bg-white/5 p-4">
        <div>
          <Rotulo>Total da venda</Rotulo>
          <p className="mt-1.5 text-3xl font-black tabular-nums text-white">R$ 184,40</p>
          <div className="mt-4 flex flex-wrap gap-1.5">
            {['PIX', 'Débito', 'Crédito', 'Crediário'].map((forma, i) => (
              <span
                key={forma}
                className="rounded-md px-2 py-1 text-[10px] font-bold"
                style={i === 0
                  ? { backgroundColor: CYAN, color: NAVY }
                  : { backgroundColor: 'rgba(255,255,255,.08)', color: '#cbd5e1' }}
              >
                {forma}
              </span>
            ))}
          </div>
        </div>
        <div className="mt-5 flex items-center justify-center gap-2 rounded-lg py-2.5 text-xs font-black"
          style={{ backgroundColor: CYAN, color: NAVY }}>
          <ShoppingCart size={14} /> Finalizar e emitir NFC-e
        </div>
      </div>
    </div>
  )
}

function TelaEstoque() {
  const linhas: [string, string, string, 'ok' | 'baixo' | 'zero'][] = [
    ['Camiseta básica', 'Preta · M', '42', 'ok'],
    ['Boné aba reta', 'Único', '7', 'baixo'],
    ['Meia cano alto', '39/42', '128', 'ok'],
    ['Moletom capuz', 'Cinza · G', '0', 'zero'],
  ]
  const cor = { ok: ['#28c840', 'Em estoque'], baixo: ['#febc2e', 'Baixo'], zero: ['#ff5f57', 'Esgotado'] } as const
  return (
    <div className="space-y-1.5">
      <div className="grid grid-cols-[1.6fr_1fr_.5fr_.9fr] gap-3 px-3 pb-1">
        {['Produto', 'Variante', 'Qtd', 'Situação'].map(h => <Rotulo key={h}>{h}</Rotulo>)}
      </div>
      {linhas.map(([produto, variante, qtd, status]) => (
        <div key={produto} className="grid grid-cols-[1.6fr_1fr_.5fr_.9fr] items-center gap-3 rounded-lg bg-white/[0.04] px-3 py-2.5">
          <span className="truncate text-xs font-semibold text-slate-200">{produto}</span>
          <span className="truncate text-xs text-slate-400">{variante}</span>
          <span className="text-xs font-bold tabular-nums text-slate-100">{qtd}</span>
          <span className="flex items-center gap-1.5 text-[10px] font-bold" style={{ color: cor[status][0] }}>
            <span className="h-1.5 w-1.5 rounded-full" style={{ backgroundColor: cor[status][0] }} />
            {cor[status][1]}
          </span>
        </div>
      ))}
    </div>
  )
}

function TelaRelatorios() {
  const barras = [38, 52, 44, 67, 59, 81, 73]
  const dias = ['S', 'T', 'Q', 'Q', 'S', 'S', 'D']
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-3 gap-3">
        {[['Faturamento', 'R$ 41.280'], ['Ticket médio', 'R$ 87,40'], ['Vendas', '472']].map(([rotulo, valor]) => (
          <div key={rotulo} className="rounded-xl border border-white/10 bg-white/5 p-3">
            <Rotulo>{rotulo}</Rotulo>
            <p className="mt-1 text-lg font-black tabular-nums text-white">{valor}</p>
          </div>
        ))}
      </div>
      <div className="rounded-xl border border-white/10 bg-white/5 p-4">
        <div className="flex items-center gap-2">
          <TrendingUp size={14} style={{ color: CYAN }} />
          <Rotulo>Vendas nos últimos 7 dias</Rotulo>
        </div>
        {/* `h-full justify-end` na coluna não é enfeite: a altura das barras é
            percentual, e porcentagem só resolve contra um pai de altura
            definida. Sem isso o wrapper fica com altura automática, cada barra
            calcula 38% de zero e o gráfico some. */}
        <div className="mt-4 flex h-24 items-end gap-2">
          {barras.map((altura, i) => (
            <div key={i} className="flex h-full flex-1 flex-col justify-end gap-1.5">
              <div
                className="w-full rounded-t"
                style={{ height: `${altura}%`, backgroundColor: CYAN, opacity: 0.45 + (altura / 100) * 0.55 }}
              />
              <span className="text-center text-[9px] font-bold text-slate-500">{dias[i]}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

function TelaVitrine() {
  return (
    <div className="flex justify-center">
      <div className="w-[190px] overflow-hidden rounded-[20px] border-4 border-white/15 bg-white">
        <div className="px-3 py-2.5 text-center" style={{ backgroundColor: NAVY }}>
          <p className="text-[11px] font-black text-white">Loja do seu cliente</p>
          <p className="text-[8px] text-slate-400">com a marca dela, não a nossa</p>
        </div>
        <div className="grid grid-cols-2 gap-1.5 bg-[#EBF7FD] p-2">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="rounded-lg bg-white p-1.5">
              <div className="aspect-square rounded" style={{ backgroundColor: `rgba(40,176,214,${0.10 + i * 0.06})` }} />
              <div className="mt-1 h-1 w-3/4 rounded bg-slate-200" />
              <div className="mt-1 h-1.5 w-1/2 rounded" style={{ backgroundColor: CYAN }} />
            </div>
          ))}
        </div>
        <div className="flex justify-end p-2" style={{ backgroundColor: '#EBF7FD' }}>
          <span className="flex h-6 w-6 items-center justify-center rounded-full" style={{ backgroundColor: '#25D366' }}>
            <span className="h-2.5 w-2.5 rounded-full bg-white" />
          </span>
        </div>
      </div>
    </div>
  )
}

const ABAS: Aba[] = [
  {
    id: 'pdv', label: 'PDV e caixa', tela: TelaPdv,
    titulo: 'A venda e a nota fiscal no mesmo movimento',
    desc: 'O operador bipa, escolhe a forma de pagamento e finaliza. A NFC-e sai do mesmo fluxo, sem redigitar nada em outro programa — que é onde a loja costuma perder tempo e errar.',
  },
  {
    id: 'estoque', label: 'Estoque', tela: TelaEstoque,
    titulo: 'Produto, variante e situação em uma tela',
    desc: 'Tamanho, cor e numeração são variantes do mesmo produto, com estoque próprio. O alerta de baixo e esgotado aparece antes de o cliente descobrir na hora da compra.',
  },
  {
    id: 'relatorios', label: 'Relatórios', tela: TelaRelatorios,
    titulo: 'O que vende, o que gira e o que parou',
    desc: 'Faturamento, ticket médio e giro por período. É o argumento que costuma fechar a indicação: o dono descobre que estava decidindo compra no achismo.',
  },
  {
    id: 'vitrine', label: 'Loja do cliente', tela: TelaVitrine,
    titulo: 'A loja online sai com a marca dele',
    desc: 'Nome, cores, logo e domínio são do cliente — o Octus não assina a vitrine. Junto vem o app instalável no celular e o atalho de WhatsApp para o atendimento.',
  },
]

export default function SystemShowcase({ theme }: { theme: InstitucionalTheme }) {
  const [ativa, setAtiva] = useState(0)
  const aba = ABAS[ativa]

  return (
    <div className="mt-12 grid gap-8 lg:grid-cols-[.85fr_1.15fr] lg:items-center">
      <div>
        {/* `role="tablist"` de verdade: são quatro botões que trocam um mesmo
            painel, e sem a semântica o leitor de tela anuncia "botão PDV" sem
            dizer que existe um conteúdo associado nem qual está selecionado. */}
        <div role="tablist" aria-label="Telas do sistema" className="flex flex-wrap gap-2">
          {ABAS.map((item, index) => (
            <button
              key={item.id}
              type="button"
              role="tab"
              id={`aba-${item.id}`}
              aria-selected={index === ativa}
              aria-controls={`painel-${item.id}`}
              onClick={() => setAtiva(index)}
              className={`rounded-xl border px-4 py-2.5 text-sm font-bold outline-none transition focus-visible:ring-2 focus-visible:ring-octus-500 ${
                index === ativa ? 'border-octus-500 bg-octus-500/15 octus-accent' : `${theme.border} ${theme.body}`
              }`}
            >
              {item.label}
            </button>
          ))}
        </div>

        <h3 className={`mt-7 text-2xl font-black tracking-[-0.02em] ${theme.heading}`}>{aba.titulo}</h3>
        <p className={`mt-3 leading-8 ${theme.body}`}>{aba.desc}</p>
      </div>

      <div role="tabpanel" id={`painel-${aba.id}`} aria-labelledby={`aba-${aba.id}`}>
        <Moldura url={`octus.app · ${aba.label.toLowerCase()}`} theme={theme}>
          <aba.tela />
        </Moldura>
      </div>
    </div>
  )
}
