'use client'
// =============================================================================
// /parceiros — Programa de Afiliados.
//
// Até aqui o programa só existia por dentro: a plataforma emitia um convite com
// token e a pessoa caía direto em /parceiros/convite, um formulário de aceite.
// Quem ouvia falar do programa não tinha onde ler as regras antes de pedir o
// convite, e quem recebia o link assinava um contrato cujos percentuais via
// pela primeira vez na hora de assinar.
//
// Os números desta página não são material de marketing solto: saem das mesmas
// constantes que o backend usa para gerar a comissão (ver COMISSAO abaixo) e do
// texto do regulamento em ReferralPartnerTerms.cs. Mudou lá, muda aqui.
// =============================================================================

import { FormEvent, useState } from 'react'
import Link from 'next/link'
import {
  ArrowRight, BadgeCheck, Ban, Calculator, CheckCircle2, ChevronDown,
  FileSignature, HandCoins, Handshake, Loader2, MessageCircle, Repeat,
  ShieldCheck, UserPlus, Wallet,
} from 'lucide-react'
import { getErrorMessage } from '@/lib/api'
import { PLANOS, formatarReais, formatarReaisExato } from '@/lib/planos'
import SiteFooter from '@/components/institucional/SiteFooter'
import SiteHeader from '@/components/institucional/SiteHeader'
import SystemShowcase from '@/components/institucional/SystemShowcase'
import { CONTACTS, submitLead, useInstitucionalTheme } from '@/lib/institucional'
import { trackMarketingEvent } from '@/lib/marketing'

/**
 * Percentuais padrão da parceria.
 *
 * Espelham os defaults de `ReferralPartnerInvitation` e as cláusulas 2, 3 e 4
 * do regulamento. São o PADRÃO, não um teto: cada indicação pode ter percentual
 * próprio gravado no sistema, e é por isso que a página fala em "a partir do
 * padrão" em vez de prometer um número fixo para sempre.
 */
const COMISSAO = {
  implantacaoPercent: 30,
  mensalidadePercent: 5,
  carenciaDias: 5,
} as const

const PARA_QUEM = [
  {
    icon: Calculator,
    title: 'Contadores',
    desc: 'Você já sabe quais clientes sofrem com nota fiscal e fechamento. O Portal do Contador ainda reduz o seu próprio retrabalho com XML.',
  },
  {
    icon: Handshake,
    title: 'Consultores e representantes',
    desc: 'Some o Octus ao que você já apresenta para o varejo e para restaurantes, sem assumir suporte nem implantação.',
  },
  {
    icon: ShieldCheck,
    title: 'Técnicos de TI e agências',
    desc: 'Quem instala PDV, cuida da rede ou faz o site do comércio costuma ser o primeiro a ouvir "preciso de um sistema".',
  },
]

/** Gatilhos que o afiliado consegue ouvir sem entender de software. Cada um é
 *  uma dor que o sistema resolve de fato — não promessa genérica. */
const SINAIS = [
  {
    fala: 'Controlo o estoque numa planilha, mas nunca bate.',
    porque: 'Planilha não desconta a venda sozinha. No Octus a baixa acontece no PDV, e variante (tamanho, cor, numeração) tem estoque próprio.',
  },
  {
    fala: 'Meu contador vive pedindo XML e eu vivo procurando.',
    porque: 'O Portal do Contador dá acesso próprio aos documentos autorizados. Acaba o vai e volta de arquivo por WhatsApp.',
  },
  {
    fala: 'Vendo fiado num caderno e às vezes esqueço de cobrar.',
    porque: 'Crediário e contas a receber com parcelas, vencimento e cobrança organizada, dentro do mesmo sistema da venda.',
  },
  {
    fala: 'Queria vender pela internet, mas não quero marketplace.',
    porque: 'Cada cliente recebe vitrine e app instalável com nome, cores, logo e domínio próprios. A marca que aparece é a dele.',
  },
  {
    fala: 'Emito nota num programa e registro a venda em outro.',
    porque: 'PDV e NFC-e no mesmo fluxo. É a dor mais cara de todas: digitar a mesma venda duas vezes e ainda errar em uma.',
  },
  {
    fala: 'Tenho restaurante e o sistema da loja não serve.',
    porque: 'O módulo de restaurante (comandas e mesas) é opcional e ativado só para quem usa — não polui a operação de quem não precisa.',
  },
]

const PASSOS = [
  { icon: MessageCircle, title: 'Você se candidata', desc: 'Preenche o formulário desta página. A equipe avalia o perfil e libera um convite nominal.' },
  { icon: FileSignature, title: 'Assina o regulamento', desc: 'O convite abre o contrato completo. O aceite é confirmado por um código de 6 dígitos enviado ao seu e-mail, e o documento assinado fica disponível para download.' },
  { icon: UserPlus, title: 'Apresenta o contato', desc: 'Você indica quem pode ter interesse. Proposta, negociação, desconto e implantação são conduzidos pela 3E Systen.' },
  { icon: Wallet, title: 'Recebe por PIX', desc: `A comissão é gerada quando o cliente paga e fica disponível ${COMISSAO.carenciaDias} dias corridos depois da liquidação.` },
]

const REGRAS = [
  { icon: Repeat, title: 'A recorrência acompanha o cliente', desc: 'Enquanto o contrato, a indicação e o pagamento seguirem ativos, cada mensalidade liquidada gera comissão. Não é bônus de uma vez só.' },
  { icon: Ban, title: 'Inadimplência não gera comissão', desc: 'Valores em atraso, cancelados, estornados, contestados ou devolvidos ficam de fora. Regularizado o débito, a contagem recomeça na data da liquidação real.' },
  { icon: BadgeCheck, title: 'Sem meta e sem exclusividade', desc: 'A parceria não cria emprego, representação comercial, sociedade ou jornada. Você decide se e quando apresenta uma indicação.' },
  { icon: ShieldCheck, title: 'Pagamento com documento fiscal', desc: 'PJ emite NFS-e quando exigível; PF fornece os dados para recibo ou RPA, com as retenções legais. Nenhum repasse sai sem registro contábil.' },
]

const COMPARATIVO = [
  ['Para quem é', 'Quem indica e não precisa ser cliente', 'Quem já usa o Octus'],
  ['O que você recebe', 'Comissão em dinheiro, por PIX', 'Desconto na sua própria mensalidade'],
  ['Quanto', `${COMISSAO.implantacaoPercent}% da implantação e ${COMISSAO.mensalidadePercent}% de cada mensalidade paga`, '+10% de desconto por indicação fechada, até 100%'],
  ['Por quanto tempo', 'Enquanto a indicação seguir ativa e pagando', 'Nas quatro primeiras mensalidades'],
  ['Como entra', 'Candidatura e convite com contrato assinado', 'Automático para clientes de São Paulo'],
]

const FAQS: [string, string][] = [
  ['Preciso ser cliente do Octus para indicar?', 'Não. O programa é aberto a qualquer pessoa física ou jurídica aprovada pela 3E Systen. Se você já é cliente, o Programa Clientes Fundadores é o caminho mais vantajoso, porque abate a sua própria mensalidade.'],
  ['Quanto o afiliado recebe?', `São ${COMISSAO.implantacaoPercent}% do valor líquido da taxa de implantação efetivamente paga pelo cliente e ${COMISSAO.mensalidadePercent}% de cada mensalidade liquidada, enquanto o contrato, a indicação e o pagamento seguirem ativos. Percentuais diferentes podem ser combinados por escrito para uma indicação específica.`],
  ['Quando a comissão fica disponível?', `${COMISSAO.carenciaDias} dias corridos após a liquidação do pagamento do cliente. Não há comissão sobre valores em atraso, cancelados, estornados ou devolvidos.`],
  ['Preciso vender ou negociar preço?', 'Não. O afiliado apenas apresenta o contato. Proposta, negociação, desconto, cobrança e implantação são conduzidos pela equipe da 3E Systen.'],
  ['Existe meta, exclusividade ou vínculo empregatício?', 'Não. A parceria não cria emprego, representação comercial, sociedade ou exclusividade. Você decide livremente se e quando apresenta uma indicação.'],
  ['Como recebo o pagamento?', 'Por PIX na chave cadastrada, mediante o documento fiscal aplicável: pessoa jurídica emite NFS-e quando exigível e pessoa física fornece os dados para recibo ou RPA, com as retenções legais.'],
]

// Structured data gerada a partir do MESMO array que a página renderiza. O
// Google trata como conteúdo enganoso a marcação que promete uma resposta
// diferente da que o visitante lê — manter duas cópias do FAQ era garantir que
// uma delas ficasse para trás na primeira revisão de texto.
const faqSchema = {
  '@context': 'https://schema.org',
  '@type': 'FAQPage',
  mainEntity: FAQS.map(([name, text]) => ({
    '@type': 'Question', name,
    acceptedAnswer: { '@type': 'Answer', text },
  })),
}

export default function ParceirosPage() {
  const { isDark, toggleTheme, theme } = useInstitucionalTheme()

  // Simulador. Começa no plano em destaque, que é o mais contratado — abrir num
  // plano de ponta inflaria o número que o visitante vê primeiro.
  const [planoIdx, setPlanoIdx] = useState(() => Math.max(0, PLANOS.findIndex(p => p.destaque)))
  const [indicacoes, setIndicacoes] = useState(3)

  const [nome, setNome] = useState('')
  const [telefone, setTelefone] = useState('')
  const [email, setEmail] = useState('')
  const [perfil, setPerfil] = useState('')
  const [privacyAcknowledged, setPrivacyAcknowledged] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const plano = PLANOS[planoIdx]
  const comissaoImplantacao = (plano.taxaImplantacao * COMISSAO.implantacaoPercent) / 100
  const comissaoMensal = (plano.preco * COMISSAO.mensalidadePercent) / 100
  const totalImplantacao = comissaoImplantacao * indicacoes
  const totalMensal = comissaoMensal * indicacoes
  const totalPrimeiroAno = totalImplantacao + totalMensal * 12

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await submitLead(
        {
          nome: nome.trim(),
          telefone: telefone.trim(),
          email: email.trim() || undefined,
          // O perfil vai no corpo da mensagem porque o lead é uma tabela só: não
          // existe campo próprio para "tipo de parceiro", e criar um exigiria
          // migração para um dado que o time lê como texto de qualquer forma.
          mensagem: `Candidatura ao Programa de Afiliados.${perfil.trim() ? ` Perfil: ${perfil.trim()}` : ''}`,
          privacyNoticeAcknowledged: privacyAcknowledged,
        },
        // `kind` governa o registro de privacidade; `defaultCampaign` só a fila
        // do CRM. Ver submitLead.
        { kind: 'Afiliados', defaultCampaign: 'afiliados' },
      )
      setSubmitted(true)
      trackMarketingEvent('lead_submit', { form: 'parceiros', lead_kind: 'referral_partner' })
    } catch (submitError) {
      setError(getErrorMessage(submitError, 'Não foi possível enviar agora. Fale com o Marketing pelo WhatsApp.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className={`min-h-screen overflow-x-hidden ${theme.page}`}>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <SiteHeader theme={theme} isDark={isDark} onToggleTheme={toggleTheme} />

      {/* ── HERO ──────────────────────────────────────────────────────── */}
      <section id="conteudo" className={`scroll-mt-24 border-b px-5 py-20 lg:px-8 lg:py-24 ${theme.border}`}>
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[1.1fr_.9fr] lg:items-center">
          <div>
            <span className="inline-flex items-center gap-2 rounded-full bg-octus-500/10 px-4 py-2 text-xs font-extrabold uppercase tracking-[0.18em] octus-accent">
              <HandCoins size={15} /> Programa de Afiliados
            </span>
            <h1 className={`mt-6 text-4xl font-black leading-[1.05] tracking-[-0.045em] sm:text-5xl lg:text-6xl ${theme.heading}`}>
              Indique o Octus e receba <span className="octus-accent">todo mês</span>, não só na venda.
            </h1>
            <p className={`mt-6 max-w-2xl text-lg leading-8 ${theme.body}`}>
              Você apresenta o contato. A 3E Systen cuida de proposta, negociação, implantação e suporte.
              A comissão da recorrência continua caindo enquanto o cliente que você indicou seguir pagando.
            </p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <a href="#candidatura" className="inline-flex items-center justify-center gap-2 rounded-xl bg-octus-600 px-6 py-4 font-bold text-white shadow-xl shadow-octus-600/20 transition hover:bg-octus-700">
                Quero me candidatar <ArrowRight size={19} />
              </a>
              <a href="#o-sistema" className={`inline-flex items-center justify-center gap-2 rounded-xl border px-6 py-4 font-bold transition ${theme.outline}`}>
                Ver o sistema por dentro
              </a>
              <a href="#simulador" className={`inline-flex items-center justify-center gap-2 rounded-xl border px-6 py-4 font-bold transition ${theme.outline}`}>
                Simular quanto rende
              </a>
            </div>
            <p className={`mt-6 text-sm ${theme.muted}`}>
              Sem taxa de adesão, sem meta e sem exclusividade. Percentuais padrão do regulamento vigente.
            </p>
          </div>

          {/* Os três números que decidem se vale a pena ler o resto. */}
          <div className={`grid gap-4 rounded-[28px] border p-6 sm:grid-cols-3 sm:p-8 lg:grid-cols-1 ${theme.border} ${theme.soft}`}>
            {[
              [`${COMISSAO.implantacaoPercent}%`, 'da taxa de implantação paga pelo cliente indicado'],
              [`${COMISSAO.mensalidadePercent}%`, 'de cada mensalidade liquidada, mês após mês'],
              [`${COMISSAO.carenciaDias} dias`, 'de carência entre o pagamento do cliente e a liberação'],
            ].map(([valor, label]) => (
              <div key={valor} className={`rounded-2xl border p-5 ${theme.border} ${theme.surface}`}>
                <p className="text-3xl font-black octus-accent">{valor}</p>
                <p className={`mt-1.5 text-sm leading-6 ${theme.body}`}>{label}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── PARA QUEM É ───────────────────────────────────────────────── */}
      <section className="px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Para quem é</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>
              Quem já é ouvido pelo comerciante.
            </h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>
              A indicação que funciona vem de quem o dono da loja já consulta antes de decidir. Não é preciso
              entender de software: você apresenta a pessoa, a gente explica o sistema.
            </p>
          </div>
          <div className="mt-12 grid gap-5 md:grid-cols-3">
            {PARA_QUEM.map(({ icon: Icon, title, desc }) => (
              <article key={title} className={`rounded-2xl border p-7 transition ${theme.card}`}>
                <span className={`inline-flex rounded-xl border p-3 octus-accent ${theme.border} ${theme.soft}`}><Icon size={23} /></span>
                <h3 className={`mt-5 text-lg font-extrabold ${theme.heading}`}>{title}</h3>
                <p className={`mt-2 leading-7 ${theme.body}`}>{desc}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* ── O QUE VOCÊ VAI INDICAR ────────────────────────────────────── */}
      {/* Esta seção não existe por estética. O afiliado não é cliente: ele nunca
          abriu o painel, e "ERP com PDV integrado ao fiscal" não desenha nada na
          cabeça de quem vai ter que explicar isso para um lojista. Aqui ele vê
          as telas e sai com as frases prontas. */}
      <section id="o-sistema" className={`scroll-mt-24 border-y px-5 py-24 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">O que você vai indicar</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>
              Conheça o sistema por dentro.
            </h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>
              O Octus é um ERP para varejo e restaurantes: PDV, estoque, fiscal, financeiro, crediário e uma
              loja online com a marca do próprio cliente. Você não precisa saber operar — precisa reconhecer
              quem tem o problema que ele resolve.
            </p>
          </div>

          <SystemShowcase theme={theme} />
        </div>
      </section>

      {/* ── COMO RECONHECER UMA BOA INDICAÇÃO ─────────────────────────── */}
      <section className="px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Na prática</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>
              Como saber que aquele comércio é uma boa indicação.
            </h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>
              Você não precisa fazer diagnóstico. Se ouvir alguma destas frases, já vale apresentar o contato.
            </p>
          </div>

          <div className="mt-12 grid gap-5 md:grid-cols-2">
            {SINAIS.map(({ fala, porque }) => (
              <article key={fala} className={`rounded-2xl border p-7 ${theme.border} ${theme.surface}`}>
                <p className={`text-lg font-extrabold italic leading-8 ${theme.heading}`}>“{fala}”</p>
                <p className={`mt-3 leading-7 ${theme.body}`}>{porque}</p>
              </article>
            ))}
          </div>

          <div className={`mt-8 rounded-2xl border p-7 ${theme.border} ${theme.soft}`}>
            <h3 className={`text-lg font-extrabold ${theme.heading}`}>E o que você não precisa fazer</h3>
            <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {['Demonstrar o sistema', 'Falar de preço ou desconto', 'Cuidar da implantação', 'Atender suporte'].map(item => (
                <p key={item} className={`flex items-start gap-2.5 leading-7 ${theme.body}`}>
                  <Ban size={18} className="mt-1.5 shrink-0 text-slate-400" aria-hidden="true" />{item}
                </p>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ── COMO FUNCIONA ─────────────────────────────────────────────── */}
      <section id="como-funciona" className={`scroll-mt-24 border-y px-5 py-24 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Como funciona</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Quatro passos, do convite ao PIX.</h2>
          </div>
          <ol className="mt-12 grid gap-5 md:grid-cols-2 lg:grid-cols-4">
            {PASSOS.map(({ icon: Icon, title, desc }, index) => (
              <li key={title} className={`rounded-2xl border p-7 ${theme.border} ${theme.surface}`}>
                <div className="flex items-center gap-3">
                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-octus-600 text-sm font-black text-white">{index + 1}</span>
                  <Icon size={21} className="octus-accent" />
                </div>
                <h3 className={`mt-5 text-lg font-extrabold ${theme.heading}`}>{title}</h3>
                <p className={`mt-2 leading-7 ${theme.body}`}>{desc}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      {/* ── SIMULADOR ─────────────────────────────────────────────────── */}
      <section id="simulador" className="scroll-mt-24 px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Simulador</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Quanto rende, na tabela real.</h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>
              Os valores saem dos planos publicados nesta mesma página. Escolha o plano que seu indicado
              contrataria e quantas indicações você imagina fechar.
            </p>
          </div>

          <div className={`mt-12 grid gap-8 rounded-[28px] border p-7 sm:p-10 lg:grid-cols-[.85fr_1.15fr] ${theme.border} ${theme.soft}`}>
            <div className="space-y-8">
              <fieldset>
                <legend className={`text-sm font-extrabold uppercase tracking-wide ${theme.muted}`}>Plano do indicado</legend>
                <div className="mt-4 flex flex-col gap-2">
                  {PLANOS.map((item, index) => (
                    <label
                      key={item.nome}
                      className={`flex cursor-pointer items-center justify-between gap-3 rounded-xl border px-4 py-3.5 transition ${
                        index === planoIdx ? 'border-octus-500 bg-octus-500/15' : `${theme.border} ${theme.surface}`
                      }`}
                    >
                      <span className="flex items-center gap-3">
                        <input
                          type="radio"
                          name="plano"
                          checked={index === planoIdx}
                          onChange={() => setPlanoIdx(index)}
                          className="h-4 w-4 accent-octus-600"
                        />
                        <span className={`font-extrabold ${theme.heading}`}>{item.nome}</span>
                      </span>
                      <span className={`text-sm font-bold ${theme.muted}`}>{formatarReais(item.preco)}/mês</span>
                    </label>
                  ))}
                </div>
              </fieldset>

              <div>
                <label htmlFor="indicacoes" className={`text-sm font-extrabold uppercase tracking-wide ${theme.muted}`}>
                  Indicações fechadas
                </label>
                <div className="mt-4 flex items-center gap-4">
                  <input
                    id="indicacoes"
                    type="range"
                    min={1}
                    max={20}
                    value={indicacoes}
                    onChange={event => setIndicacoes(Number(event.target.value))}
                    className="h-2 w-full cursor-pointer accent-octus-600"
                  />
                  <output htmlFor="indicacoes" className={`w-12 shrink-0 text-right text-2xl font-black tabular-nums ${theme.heading}`}>
                    {indicacoes}
                  </output>
                </div>
              </div>
            </div>

            <div className={`rounded-2xl border p-7 ${theme.border} ${theme.surface}`}>
              <div className="grid gap-5 sm:grid-cols-2">
                <div>
                  <p className={`text-sm ${theme.muted}`}>Comissão de implantação</p>
                  <p className={`mt-1 text-3xl font-black tabular-nums ${theme.heading}`}>{formatarReaisExato(totalImplantacao)}</p>
                  <p className={`mt-1 text-xs ${theme.muted}`}>
                    {plano.taxaImplantacao === 0
                      // Regra do regulamento, não arredondamento: sem taxa não
                      // há base de cálculo. Nenhum plano de tabela está assim
                      // hoje, mas a implantação é negociável por loja (pode ser
                      // zerada no painel), então o caso continua existindo.
                      ? 'Sem taxa de implantação não há base de cálculo — a comissão recorrente segue normalmente.'
                      : `${COMISSAO.implantacaoPercent}% de ${formatarReais(plano.taxaImplantacao)}, valor de tabela, uma vez por indicação`}
                  </p>
                </div>
                <div>
                  <p className={`text-sm ${theme.muted}`}>Comissão recorrente</p>
                  <p className="mt-1 text-3xl font-black tabular-nums octus-accent">{formatarReaisExato(totalMensal)}<span className={`text-base font-bold ${theme.muted}`}>/mês</span></p>
                  <p className={`mt-1 text-xs ${theme.muted}`}>{COMISSAO.mensalidadePercent}% de {formatarReais(plano.preco)} por indicação ativa</p>
                </div>
              </div>

              <div className={`mt-7 border-t pt-6 ${theme.border}`}>
                <p className={`text-sm ${theme.muted}`}>Total nos 12 primeiros meses</p>
                <p className={`mt-1 text-4xl font-black tabular-nums ${theme.heading}`}>{formatarReaisExato(totalPrimeiroAno)}</p>
                <p className={`mt-3 text-xs leading-6 ${theme.muted}`}>
                  Projeção considerando {indicacoes === 1
                    ? 'a indicação ativa e adimplente'
                    : `as ${indicacoes} indicações ativas e adimplentes`} durante os doze meses.
                  Mensalidade em atraso, cancelada ou estornada não gera comissão, então o valor
                  real acompanha o pagamento do cliente.
                </p>
              </div>

              <a href="#candidatura" className="mt-7 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-octus-600 px-5 py-4 font-extrabold text-white transition hover:bg-octus-700">
                Quero me candidatar <ArrowRight size={18} />
              </a>
            </div>
          </div>
        </div>
      </section>

      {/* ── REGRAS ────────────────────────────────────────────────────── */}
      <section className={`border-y px-5 py-24 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">As regras, sem letra miúda</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>O que está no contrato que você vai assinar.</h2>
          </div>
          <div className="mt-12 grid gap-5 md:grid-cols-2">
            {REGRAS.map(({ icon: Icon, title, desc }) => (
              <article key={title} className={`flex gap-5 rounded-2xl border p-7 ${theme.border} ${theme.surface}`}>
                <span className="shrink-0 octus-accent"><Icon size={24} /></span>
                <div>
                  <h3 className={`text-lg font-extrabold ${theme.heading}`}>{title}</h3>
                  <p className={`mt-2 leading-7 ${theme.body}`}>{desc}</p>
                </div>
              </article>
            ))}
          </div>
          <p className={`mt-8 text-sm leading-7 ${theme.muted}`}>
            O regulamento completo é exibido na íntegra no convite, antes do aceite. A assinatura é eletrônica,
            confirmada por código enviado ao seu e-mail, e o documento final fica disponível para download.
          </p>
        </div>
      </section>

      {/* ── AFILIADO × CLIENTE FUNDADOR ───────────────────────────────── */}
      <section className="px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Não confunda</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Afiliado ou Cliente Fundador?</h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>
              São dois programas de indicação diferentes. O afiliado recebe dinheiro; o Cliente Fundador
              abate a própria mensalidade.
            </p>
          </div>

          <div className={`mt-12 overflow-x-auto rounded-2xl border ${theme.border}`}>
            <table className="w-full min-w-[640px] border-collapse text-sm">
              <thead>
                <tr className={`${theme.soft}`}>
                  <th scope="col" className={`border-b px-5 py-4 text-left text-xs font-extrabold uppercase tracking-wide sm:px-6 ${theme.border} ${theme.muted}`}>Comparativo</th>
                  <th scope="col" className={`border-b px-5 py-4 text-left text-xs font-extrabold uppercase tracking-wide octus-accent sm:px-6 ${theme.border}`}>Programa de Afiliados</th>
                  <th scope="col" className={`border-b px-5 py-4 text-left text-xs font-extrabold uppercase tracking-wide sm:px-6 ${theme.border} ${theme.muted}`}>Clientes Fundadores</th>
                </tr>
              </thead>
              {/* Mesmo motivo do comparativo da institucional: sem isto sobra
                  uma linha no rodapé da tabela. */}
              <tbody className="[&>tr:last-child>*]:border-b-0">
                {COMPARATIVO.map(([label, afiliado, fundador]) => (
                  <tr key={label}>
                    <th scope="row" className={`border-b px-5 py-4 text-left align-top font-bold sm:px-6 ${theme.border} ${theme.heading}`}>{label}</th>
                    <td className={`border-b px-5 py-4 align-top leading-6 sm:px-6 ${theme.border} ${theme.body}`}>{afiliado}</td>
                    <td className={`border-b px-5 py-4 align-top leading-6 sm:px-6 ${theme.border} ${theme.body}`}>{fundador}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className={`mt-6 text-sm ${theme.muted}`}>
            Já é cliente do Octus?{' '}
            <Link href="/institucional#fundadores" className="font-bold octus-accent hover:underline">
              Veja o Programa Clientes Fundadores
            </Link>.
          </p>
        </div>
      </section>

      {/* ── CANDIDATURA ───────────────────────────────────────────────── */}
      <section id="candidatura" className="scroll-mt-24 bg-[#071f3d] px-5 py-24 text-white lg:px-8">
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[.9fr_1.1fr]">
          <div>
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] text-octus-400">Candidatura</p>
            <h2 className="mt-4 text-4xl font-black tracking-[-0.035em] sm:text-5xl">Peça seu convite de parceiro.</h2>
            <p className="mt-5 text-lg leading-8 text-slate-300">
              A equipe avalia o perfil e envia um convite nominal com o regulamento completo. Você lê tudo
              antes de assinar — e assinar não obriga a indicar ninguém.
            </p>
            <div className="mt-8 space-y-3 text-sm text-slate-300">
              <a href={CONTACTS.marketingWhatsapp} target="_blank" rel="noreferrer" className="flex items-center gap-3 transition hover:text-white">
                <MessageCircle size={18} className="text-octus-400" />Marketing · {CONTACTS.marketingPhone}
              </a>
              <p className="flex items-center gap-3"><ShieldCheck size={18} className="text-octus-400" />Sem taxa de adesão e sem custo para se candidatar.</p>
            </div>
          </div>

          <div className="rounded-2xl border border-white/10 bg-white/5 p-6 sm:p-8">
            {submitted ? (
              <div className="flex min-h-80 flex-col items-center justify-center text-center">
                <CheckCircle2 size={42} className="text-emerald-400" />
                <h3 className="mt-5 text-2xl font-black">Candidatura recebida.</h3>
                <p className="mt-2 text-slate-300">
                  A equipe vai analisar seu perfil e, aprovado, o convite com o regulamento chega no e-mail
                  ou no WhatsApp que você informou.
                </p>
              </div>
            ) : (
              <form onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2">
                <label className="text-sm font-bold">
                  Nome
                  <input required maxLength={150} value={nome} onChange={event => setNome(event.target.value)}
                    className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400"
                    placeholder="Nome completo ou razão social" />
                </label>
                <label className="text-sm font-bold">
                  WhatsApp
                  <input required maxLength={30} value={telefone} onChange={event => setTelefone(event.target.value)}
                    className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400"
                    placeholder="(17) 99999-9999" />
                </label>
                <label className="text-sm font-bold sm:col-span-2">
                  E-mail <span className="font-normal text-slate-400">(o convite é enviado para ele)</span>
                  <input type="email" maxLength={255} value={email} onChange={event => setEmail(event.target.value)}
                    className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400"
                    placeholder="voce@empresa.com.br" />
                </label>
                <label className="text-sm font-bold sm:col-span-2">
                  Como você chega nos comerciantes? <span className="font-normal text-slate-400">(opcional)</span>
                  <textarea rows={3} maxLength={800} value={perfil} onChange={event => setPerfil(event.target.value)}
                    className="mt-2 w-full resize-none rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400"
                    placeholder="Ex.: sou contador e atendo 40 lojas em Rio Preto." />
                </label>
                <label className="flex items-start gap-3 text-xs leading-relaxed text-slate-300 sm:col-span-2">
                  <input required type="checkbox" checked={privacyAcknowledged} onChange={event => setPrivacyAcknowledged(event.target.checked)} className="mt-0.5 h-4 w-4 accent-octus-500" />
                  <span>
                    Li e estou ciente da <Link href="/privacidade" target="_blank" className="font-bold text-octus-300 underline">Política de Privacidade</Link>,
                    inclusive sobre o uso dos dados para avaliar esta candidatura. Esta ciência não autoriza marketing opcional.
                  </span>
                </label>
                {error && <p className="text-sm text-red-300 sm:col-span-2">{error}</p>}
                <button disabled={submitting} className="inline-flex items-center justify-center gap-2 rounded-xl bg-octus-600 px-5 py-4 font-extrabold text-white transition hover:bg-octus-500 disabled:opacity-60 sm:col-span-2">
                  {submitting ? <><Loader2 size={18} className="animate-spin" />Enviando...</> : <>Pedir meu convite <ArrowRight size={18} /></>}
                </button>
              </form>
            )}
          </div>
        </div>
      </section>

      {/* ── FAQ ───────────────────────────────────────────────────────── */}
      <section className={`px-5 py-20 lg:px-8 ${theme.surface}`}>
        <div className="mx-auto max-w-4xl">
          <p className="text-center text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Dúvidas frequentes</p>
          <h2 className={`mt-4 text-center text-3xl font-black ${theme.heading}`}>Sobre o programa</h2>
          <div className="mt-9 space-y-3">
            {FAQS.map(([question, answer]) => (
              <details key={question} className={`group rounded-2xl border p-5 ${theme.card}`}>
                <summary className={`flex cursor-pointer list-none items-center justify-between gap-4 font-extrabold ${theme.heading}`}>
                  {question}
                  <ChevronDown size={18} className="shrink-0 transition group-open:rotate-180" />
                </summary>
                <p className={`mt-3 leading-7 ${theme.body}`}>{answer}</p>
              </details>
            ))}
          </div>
        </div>
      </section>

      <SiteFooter theme={theme} />
    </main>
  )
}
