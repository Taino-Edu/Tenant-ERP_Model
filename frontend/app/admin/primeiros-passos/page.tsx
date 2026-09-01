'use client'

import Link from 'next/link'
import PageHeader from '@/components/admin/PageHeader'
import {
  ArrowRight,
  Banknote,
  BookOpen,
  CheckCircle2,
  Compass,
  FileCheck2,
  Keyboard,
  Lightbulb,
  Package,
  Rocket,
  ShoppingBag,
  TrendingUp,
} from 'lucide-react'

const PASSOS = [
  {
    fase: 'Orientar-se',
    icon: Compass,
    cor: '#38BDF8',
    titulo: 'Encontre qualquer função sem se perder',
    onde: '/admin/dashboard',
    ondeLabel: 'Ir para Início',
    passos: [
      'Escolha uma das 8 áreas do menu: Início, Vendas, Produtos, Clientes e equipe, Financeiro, Comunicação, Configurações ou Ajuda',
      'As páginas daquela área aparecem no topo; no celular, toque em “Trocar página”',
      'O menu mostra somente o que o seu perfil e os módulos ativos permitem usar',
    ],
    dica: 'No celular, Caixa, Comanda, Início e Estoque ficam sempre acessíveis embaixo; use Menu para abrir as outras áreas.',
  },
  {
    fase: 'Preparar',
    icon: Package,
    cor: '#FB923C',
    titulo: 'Cadastre um produto completo',
    onde: '/admin/estoque',
    ondeLabel: 'Ir para Estoque',
    passos: [
      'Abra Estoque → Novo Produto',
      'Informe nome, categoria, custo, preço, estoque inicial e estoque mínimo',
      'Se emitir nota, copie o NCM da NF-e de entrada do fornecedor',
    ],
    dica: 'Não adivinhe o NCM: ele precisa vir do documento de compra ou ser confirmado pelo contador.',
  },
  {
    fase: 'Vender',
    icon: ShoppingBag,
    cor: '#4ADE80',
    titulo: 'Simule uma venda no PDV',
    onde: '/admin/venda-avulsa',
    ondeLabel: 'Ir para Frente de Caixa',
    passos: [
      'Escolha o cliente (opcional), adicione o produto e confira a quantidade disponível',
      'Selecione a forma de pagamento e informe o desconto, se houver',
      'Confirme a venda e verifique se o estoque baixou exatamente uma vez',
    ],
    dica: 'Faça o primeiro teste em ambiente de homologação e com um produto criado especificamente para teste.',
  },
  {
    fase: 'Receber',
    icon: Banknote,
    cor: '#10B981',
    titulo: 'Confira dinheiro e troco',
    onde: '/admin/venda-avulsa',
    ondeLabel: 'Testar pagamento',
    passos: [
      'Em Dinheiro, informe o valor que o cliente realmente entregou',
      'Confira o valor devido e o troco calculado antes de confirmar',
      'Em pagamento dividido, valide os valores das duas formas',
    ],
    dica: 'O troco não é desconto. Na NFC-e, valor recebido e troco são enviados e exibidos separadamente.',
  },
  {
    fase: 'Fiscal',
    icon: FileCheck2,
    cor: '#EAB308',
    titulo: 'Valide a emissão fiscal',
    onde: '/admin/fiscal',
    ondeLabel: 'Ir para Fiscal',
    passos: [
      'Confirme empresa, certificado A1, ambiente e natureza de operação',
      'Marque “Emitir cupom fiscal” somente na venda de homologação',
      'Confira status autorizado, XML, DANFE, forma de pagamento, valor recebido, troco e QR Code',
    ],
    dica: 'Pontos e cashback não podem ser usados em novas vendas; registros antigos com fidelidade bloqueiam a emissão até regularização.',
  },
  {
    fase: 'Conferir',
    icon: TrendingUp,
    cor: '#A78BFA',
    titulo: 'Feche a conferência do dia',
    onde: '/admin/dashboard',
    ondeLabel: 'Ir para Início',
    passos: [
      'Compare vendas, formas de pagamento, comandas abertas e estoque',
      'Confira no Financeiro se os recebimentos entraram no período correto',
      'Use Relatórios para revisar ou compartilhar os números',
    ],
    dica: 'Diferença de caixa deve ser investigada pela venda e pela forma de pagamento, sem editar o valor fiscal já autorizado.',
  },
  {
    fase: 'Ganhar tempo',
    icon: Keyboard,
    cor: '#38BDF8',
    titulo: 'Use atalhos sem interromper a escrita',
    onde: '/admin/manual',
    ondeLabel: 'Ver manual e atalhos',
    passos: [
      'Pressione ? fora de campos de texto para ver os atalhos disponíveis',
      'Use A para abrir o Assistente de IA e H para abrir o manual',
      'Enquanto você digita ou usa o chat, todos os atalhos globais ficam pausados',
    ],
    dica: 'O menu mostra apenas atalhos permitidos para o seu perfil e para os módulos ativos da loja.',
  },
]

export default function PrimeirosPassosPage() {
  return (
    <div className="max-w-5xl space-y-6 p-4 sm:p-6">
      <PageHeader
        icon={Rocket}
        title="Primeiros Passos"
        description="Aprenda a navegar e conclua a primeira operação passo a passo"
      />

      <div className="grid gap-4 lg:grid-cols-2">
        {PASSOS.map(({ fase, icon: Icon, cor, titulo, onde, ondeLabel, passos, dica }, index) => (
          <article key={titulo} className="card flex h-full flex-col">
            <div className="mb-3 flex items-start gap-3">
              <div
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-sm font-bold"
                style={{ background: `${cor}20`, color: cor, border: `1px solid ${cor}40` }}
              >
                {index + 1}
              </div>
              <div className="min-w-0 flex-1">
                <p className="mb-0.5 text-[10px] font-bold uppercase tracking-widest" style={{ color: cor }}>{fase}</p>
                <div className="flex items-center gap-2">
                  <Icon className="h-4 w-4 shrink-0" style={{ color: cor }} />
                  <h2 className="font-bold text-white">{titulo}</h2>
                </div>
              </div>
            </div>

            <ol className="mb-3 space-y-2">
              {passos.map(passo => (
                <li key={passo} className="flex items-start gap-2 text-sm text-gray-300">
                  <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0 text-gray-600" />
                  <span>{passo}</span>
                </li>
              ))}
            </ol>

            <p className="mb-4 flex items-start gap-2 rounded-lg border-l-2 bg-surface-900 px-3 py-2 text-xs leading-relaxed text-gray-400" style={{ borderColor: cor }}>
              <Lightbulb className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden />
              <span>{dica}</span>
            </p>

            <Link
              href={onde}
              className="mt-auto inline-flex items-center gap-1.5 text-sm font-semibold transition-all hover:gap-2.5"
              style={{ color: cor }}
            >
              {ondeLabel} <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </article>
        ))}
      </div>

      <div className="card flex items-center gap-4 bg-surface-800">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-brand-500/30 bg-brand-500/20">
          <BookOpen className="h-5 w-5 text-brand-400" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-white">Precisa do procedimento completo?</p>
          <p className="mt-0.5 text-xs text-gray-400">O manual atualizado detalha operação, fiscal, estoque, financeiro e atalhos.</p>
        </div>
        <Link href="/admin/manual" className="btn-secondary shrink-0 py-2 text-sm" target="_blank">
          Abrir manual
        </Link>
      </div>
    </div>
  )
}
