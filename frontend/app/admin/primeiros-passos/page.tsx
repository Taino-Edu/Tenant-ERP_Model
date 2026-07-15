'use client'
import Link from 'next/link'
import PageHeader from '@/components/admin/PageHeader'
import {
  Rocket, Package, Users, ShoppingBag, TrendingUp, Palette,
  ArrowRight, BookOpen, CheckCircle2,
} from 'lucide-react'

const PASSOS = [
  {
    num: 1,
    icon: Package,
    cor: '#FB923C',
    titulo: 'Cadastre seu primeiro produto',
    onde: '/admin/estoque',
    ondeLabel: 'Ir pro Estoque',
    passos: [
      'Vá em Estoque → botão "Novo Produto"',
      'Preencha nome, categoria, preço de venda e estoque inicial',
      'Salve — o produto já aparece disponível pra venda',
    ],
    dica: 'Preço de custo é opcional, mas preenchendo você já vê a margem de lucro calculada automaticamente.',
  },
  {
    num: 2,
    icon: Users,
    cor: '#3EC2F2',
    titulo: 'Abra e feche uma comanda',
    onde: '/admin/comanda',
    ondeLabel: 'Ir pra Comanda',
    passos: [
      'O cliente escaneia o QR Code da mesa e abre a comanda sozinho — ou você abre manualmente pelo botão "Abrir Comanda"',
      'Adicione itens direto no card da comanda, buscando pelo nome ou lendo o código de barras',
      'Quando o cliente for embora, clique em "Fechar", escolha a forma de pagamento e confirme',
    ],
    dica: 'Dá pra dividir o pagamento em duas formas diferentes (ex: metade Pix, metade dinheiro) direto no fechamento.',
  },
  {
    num: 3,
    icon: ShoppingBag,
    cor: '#4ADE80',
    titulo: 'Faça uma venda avulsa (balcão)',
    onde: '/admin/venda-avulsa',
    ondeLabel: 'Ir pra Frente de Caixa',
    passos: [
      'Use isso pra vendas rápidas sem QR Code — cliente chega, paga e vai embora',
      'O assistente guia em 3 passos: cliente (opcional) → produtos → pagamento',
      'Confirme e pronto — estoque já desconta automaticamente',
    ],
    dica: 'Sem selecionar cliente, a venda entra como anônima — funciona normal, só não conta pontos/cashback pra ninguém.',
  },
  {
    num: 4,
    icon: TrendingUp,
    cor: '#A78BFA',
    titulo: 'Acompanhe o financeiro',
    onde: '/admin/dashboard',
    ondeLabel: 'Ir pro Painel Geral',
    passos: [
      'O Painel Geral já mostra os KPIs do dia assim que você abre: comandas ativas, receita, valor em aberto',
      'Pra detalhe por período, curva ABC e comparação com meses anteriores, use o menu Financeiro',
      'Relatórios em PDF (financeiro, estoque, crediário) ficam em Relatórios',
    ],
    dica: 'O intervalo de atualização automática do painel é configurável em Configurações → Dashboard.',
  },
  {
    num: 5,
    icon: Palette,
    cor: '#F59E0B',
    titulo: 'Personalize a cara da sua loja',
    onde: '/admin/site',
    ondeLabel: 'Ir pra Personalizar Site',
    passos: [
      'Nome, cores, textos e contato da sua página pública, tudo num formulário só',
      'Uma prévia ao vivo mostra o resultado antes de salvar',
      'Clique em "Ver site" pra abrir a página pública de verdade numa aba nova',
    ],
    dica: 'Enquanto nada é preenchido, sua loja continua com a aparência padrão — nada quebra por deixar em branco.',
  },
]

export default function PrimeirosPassosPage() {
  return (
    <div className="p-4 sm:p-6 space-y-5 max-w-3xl">
      <PageHeader
        icon={Rocket}
        title="Primeiros Passos"
        description="Os 5 fluxos mais comuns pra começar a usar o sistema"
      />

      <div className="space-y-3">
        {PASSOS.map(({ num, icon: Icon, cor, titulo, onde, ondeLabel, passos, dica }) => (
          <div key={num} className="card">
            <div className="flex items-start gap-4">
              <div
                className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 font-bold text-sm"
                style={{ background: `${cor}20`, color: cor, border: `1px solid ${cor}40` }}
              >
                {num}
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-2">
                  <Icon className="w-4 h-4 shrink-0" style={{ color: cor }} />
                  <h2 className="font-bold text-white">{titulo}</h2>
                </div>
                <ol className="space-y-1.5 mb-3">
                  {passos.map((p, i) => (
                    <li key={i} className="flex items-start gap-2 text-sm text-gray-300">
                      <CheckCircle2 className="w-3.5 h-3.5 mt-0.5 shrink-0 text-gray-600" />
                      <span>{p}</span>
                    </li>
                  ))}
                </ol>
                {dica && (
                  <p className="text-xs text-gray-500 bg-surface-900 rounded-lg px-3 py-2 mb-3 border-l-2" style={{ borderColor: cor }}>
                    💡 {dica}
                  </p>
                )}
                <Link
                  href={onde}
                  className="inline-flex items-center gap-1.5 text-sm font-semibold hover:gap-2.5 transition-all"
                  style={{ color: cor }}
                >
                  {ondeLabel} <ArrowRight className="w-3.5 h-3.5" />
                </Link>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* CTA pro manual completo */}
      <div className="card flex items-center gap-4 bg-surface-800">
        <div className="w-10 h-10 rounded-xl bg-brand-500/20 border border-brand-500/30 flex items-center justify-center shrink-0">
          <BookOpen className="w-5 h-5 text-brand-400" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-white">Precisa de mais detalhe?</p>
          <p className="text-xs text-gray-400 mt-0.5">O manual completo cobre todas as telas do sistema, uma por uma.</p>
        </div>
        <Link href="/admin/manual" className="btn-secondary text-sm py-2 shrink-0" target="_blank">
          Abrir manual
        </Link>
      </div>
    </div>
  )
}
