// =============================================================================
// planos.ts — O catálogo de planos, em UM lugar só.
//
// Antes existiam dois, e eles divergiram: o site institucional vendia
// "Essencial / Completo / Avançado" com preço, e o painel oferecia presets
// chamados "Mar" e "Lagoa" sem valor nenhum. O resultado em produção foi loja
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
  /** Texto do limite de usuários, pro site. */
  usuarios: string
  /** Limite real gravado no Tenant.MaxUsers — null = ilimitado. */
  maxUsers: number | null
  /** Módulos que o plano libera (valores de TENANT_MODULES). */
  modules: string[]
  inclui: string[]
}

/** Implantação é sempre 2 mensalidades — a regra está escrita no site, então
 *  fica derivada aqui em vez de digitada de novo e sujeita a divergir. */
export const taxaImplantacao = (precoMensal: number) => precoMensal * 2

export const PLANOS: Plano[] = [
  {
    nome: 'Essencial',
    preco: 120,
    publico: 'Pra loja que quer sair da planilha e do caderno.',
    destaque: false,
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
    nome: 'Completo',
    preco: 269,
    publico: 'A operação que já vende todo dia e precisa de controle.',
    destaque: true,
    usuarios: '6 usuários no painel',
    maxUsers: 6,
    modules: ['fiscal', 'estoque', 'restaurante', 'pontos', 'contador', 'eventos'],
    inclui: [
      'Tudo do Essencial',
      'Crediário e contas a receber',
      'Financeiro completo, com fechamento de caixa',
      'Programa de fidelidade por pontos',
      'Portal do contador (ele acessa direto, sem você exportar nada)',
      'Gestão de eventos com cobrança de entrada',
      'Perfis de acesso por funcionário',
    ],
  },
  {
    nome: 'Avançado',
    preco: 487,
    publico: 'Pra quem tem mais de um ponto ou quer automatizar.',
    destaque: false,
    usuarios: 'Usuários ilimitados',
    maxUsers: null,
    modules: TENANT_MODULES.map(m => m.value),
    inclui: [
      'Tudo do Completo',
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
