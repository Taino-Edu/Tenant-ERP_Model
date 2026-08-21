// =============================================================================
// planos.ts — O catálogo de planos, em UM lugar só.
//
// Antes existiam dois, e eles divergiram: o site institucional vendia
// nomes comerciais e preços diferentes dos presets do painel. O resultado em produção foi loja
// cadastrada como plano "Mar" com mensalidade zero — o financeiro da
// plataforma calcula MRR a partir de MonthlyPrice, então esse tenant
// simplesmente não existia na receita.
//
// Aqui é a fonte única: o que o site promete é o que o painel cobra. Mudou
// preço? Muda neste arquivo e os dois lugares acompanham.
// =============================================================================

import { TENANT_MODULES } from './api'

export interface Plano {
  nome: string
  /** Mensalidade em reais. */
  preco: number
  publico: string
  destaque: boolean
  /** Valor de tabela da implantação, cobrado uma vez. É ponto de partida, não
   *  regra: a taxa é moeda de negociação e o painel da plataforma permite
   *  ajustá-la por loja, inclusive zerar. Zero aqui significaria implantação
   *  gratuita anunciada no site — hoje nenhum plano está assim. */
  taxaImplantacao: number
  /** Texto do limite de usuários, pro site. */
  usuarios: string
  /** Limite real gravado no Tenant.MaxUsers — null = ilimitado. */
  maxUsers: number | null
  /** Módulos que o plano libera (valores de TENANT_MODULES). */
  modules: string[]
  inclui: string[]
}

/** Duas mensalidades, em tabela e no personalizado — os três planos seguem a
 * mesma conta desde que o Mar deixou de ter implantação gratuita. O catálogo
 * continua sendo a fonte para os planos de tabela, pra que um valor negociado
 * possa divergir do dobro sem que este helper o sobrescreva. */
export const taxaImplantacao = (planoOuPreco: Plano | number) =>
  typeof planoOuPreco === 'number' ? planoOuPreco * 2 : planoOuPreco.taxaImplantacao

export const PLANOS: Plano[] = [
  {
    nome: 'Lagoa',
    preco: 129,
    publico: 'Pra loja que quer sair da planilha e do caderno.',
    destaque: false,
    taxaImplantacao: 258,
    usuarios: '2 usuários no painel',
    maxUsers: 2,
    modules: ['fiscal', 'estoque', 'restaurante'],
    inclui: [
      'PDV e comanda',
      'Emissão de NFC-e (fiscal completo)',
      'Controle de estoque com variantes',
      'Vitrine própria com subdomínio seu',
      'App instalável no celular (PWA), com sua marca',
      'Relatórios básicos de venda',
    ],
  },
  {
    nome: 'Rio',
    preco: 269,
    publico: 'A operação que já vende todo dia e precisa de controle.',
    destaque: true,
    taxaImplantacao: 538,
    usuarios: '6 usuários no painel',
    maxUsers: 6,
    modules: ['fiscal', 'estoque', 'restaurante', 'pontos', 'contador', 'eventos'],
    inclui: [
      'Tudo do Lagoa',
      'Crediário e contas a receber',
      'Financeiro completo, com fechamento de caixa',
      'Programa de fidelidade por pontos',
      'Portal do contador (ele acessa direto, sem você exportar nada)',
      'Gestão de eventos com cobrança de entrada',
      'Perfis de acesso por funcionário',
    ],
  },
  {
    nome: 'Mar',
    preco: 487,
    publico: 'Pra quem tem mais de um ponto ou quer automatizar.',
    destaque: false,
    taxaImplantacao: 974,
    usuarios: 'Usuários ilimitados',
    maxUsers: null,
    // Restaurante é adicional opt-in: nem o plano mais alto o liga sozinho.
    // O dono da plataforma precisa habilitá-lo explicitamente por tenant.
    modules: TENANT_MODULES.filter(m => m.value !== 'restaurante').map(m => m.value),
    inclui: [
      'Tudo do Rio',
      'Assistente de IA no painel (pergunte em português sobre sua loja)',
      'Domínio próprio (suamarca.com.br)',
      'Reservas e agendamento',
      'Prioridade no suporte',
    ],
  },
]

/** Nome usado quando o preço foi negociado fora da tabela. Existe porque venda
 *  B2B tem exceção — mas ela vira uma escolha explícita, não o efeito colateral
 *  de alguém digitar qualquer coisa num campo de texto livre. */
export const PLANO_PERSONALIZADO = 'Personalizado'

export const acharPlano = (nome: string | null | undefined) =>
  PLANOS.find(p => p.nome.toLowerCase() === (nome ?? '').trim().toLowerCase())

export const formatarReais = (valor: number) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: 0, maximumFractionDigits: 2 })

/** Igual ao acima, mas sempre com os centavos.
 *
 *  `formatarReais` omite as casas decimais para que a tabela de planos mostre
 *  "R$ 129" e não "R$ 129,00" — o que é certo para preço redondo de tabela e
 *  errado para valor calculado: 30% de R$ 538 saía como "R$ 484,2", que não é
 *  como se escreve dinheiro em lugar nenhum. Use este nas contas. */
export const formatarReaisExato = (valor: number) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: 2, maximumFractionDigits: 2 })
