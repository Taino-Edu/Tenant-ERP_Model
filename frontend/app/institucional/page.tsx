'use client'

import Image from 'next/image'
import dynamic from 'next/dynamic'
import Link from 'next/link'
import { FormEvent, useEffect, useRef, useState } from 'react'
import {
  ArrowRight, BarChart3, Boxes, Building2, Calculator, Check,
  CheckCircle2, ChevronDown, ExternalLink, FileCheck2, HandCoins, Headphones,
  Layers3, Loader2, Mail, MessageCircle, ReceiptText, Send, Smartphone,
  UtensilsCrossed, X,
} from 'lucide-react'
import {
  publicAssistantApi, publicDirectoryApi,
  type PublicTenantDto,
} from '@/lib/api'
import { PLANOS } from '@/lib/planos'
import SiteFooter from '@/components/institucional/SiteFooter'
import SiteHeader from '@/components/institucional/SiteHeader'
import SystemShowcase from '@/components/institucional/SystemShowcase'
import Logo from '@/components/Logo'
import { CONTACTS, ROOT_DOMAIN, publicFormErrorMessage, submitLead, telHref, useInstitucionalTheme } from '@/lib/institucional'
import type { ModuleDemoId } from '@/components/institucional/PlatformModuleDemo'
import { trackMarketingEvent } from '@/lib/marketing'
import { COOKIE_CONSENT_EVENT } from '@/lib/cookieConsent'

const MARKETING_WHATSAPP = CONTACTS.marketingWhatsapp
const PlatformModuleDemo = dynamic(() => import('@/components/institucional/PlatformModuleDemo'), { ssr: false })

const RECURSOS = [
  { id: 'pdv', icon: ReceiptText, title: 'PDV e fiscal', desc: 'Venda, caixa e emissão de NFC-e no mesmo fluxo, sem redigitar informações.' },
  { id: 'estoque', icon: Boxes, title: 'Estoque organizado', desc: 'Produtos, variantes, movimentações, alertas e cadastro fiscal em um só lugar.' },
  { id: 'financeiro', icon: Calculator, title: 'Financeiro claro', desc: 'Crediário, contas a receber, fechamento de caixa e visão real da operação.' },
  { id: 'relatorios', icon: BarChart3, title: 'Decisões com dados', desc: 'Relatórios e indicadores que mostram o que vende, o que gira e o que precisa de atenção.' },
  { id: 'experiencia', icon: Smartphone, title: 'Experiência própria', desc: 'Site e app instalável com nome, cores, logo e domínio da sua empresa.' },
  { id: 'restaurante', icon: UtensilsCrossed, title: 'Módulo restaurante', desc: 'Comandas e operação de restaurante como adicional opcional, ativado apenas para quem precisa.' },
]

const COMPARATIVO = [
  ['Identidade da sua empresa', true, false, false],
  ['PDV, estoque, fiscal e financeiro integrados', true, true, false],
  ['Portal direto para o contador', true, false, false],
  ['Módulos opcionais sem poluir a operação', true, false, false],
  ['Dados da loja em ambiente isolado', true, null, null],
  ['Suporte humano próximo', true, null, false],
] as const

const FAQS = [
  ['Quando começa a cobrança?', 'Todos os planos têm 15 dias grátis. A primeira mensalidade é cobrada no 16º dia.'],
  ['O Octus substitui a marca da minha loja?', 'Não. Octus é a identidade padrão da plataforma; nome, logo, cores e domínio personalizados pelo cliente sempre têm prioridade.'],
  ['O sistema atende restaurantes?', 'Sim. O módulo de restaurante é opcional e só aparece para os clientes que decidirem utilizá-lo.'],
  ['Como funciona o Programa Clientes Fundadores?', 'Clientes do estado de São Paulo têm 30% de desconto nas quatro primeiras mensalidades, além dos 15 dias grátis. Cada indicação fechada acrescenta 10% de desconto no mesmo período, até 100%.'],
]

type ChatMessage = { role: 'assistant' | 'user'; text: string }

const CHAT_FALLBACK_MESSAGE = 'Não consegui responder agora. Nosso Marketing pode te ajudar pelo WhatsApp.'

function normalizeAssistantReply(reply: unknown): string {
  return typeof reply === 'string' && reply.trim().length > 0
    ? reply.trim()
    : CHAT_FALLBACK_MESSAGE
}

export default function InstitucionalPage() {
  const { isDark, toggleTheme, theme } = useInstitucionalTheme()
  const [tenants, setTenants] = useState<PublicTenantDto[]>([])
  const [leadNome, setLeadNome] = useState('')
  const [leadTelefone, setLeadTelefone] = useState('')
  const [leadEmail, setLeadEmail] = useState('')
  const [leadMensagem, setLeadMensagem] = useState('')
  const [privacyAcknowledged, setPrivacyAcknowledged] = useState(false)
  const [leadSubmitting, setLeadSubmitting] = useState(false)
  const [leadSubmitted, setLeadSubmitted] = useState(false)
  const [leadError, setLeadError] = useState<string | null>(null)
  const [chatOpen, setChatOpen] = useState(false)
  const [activeModuleDemo, setActiveModuleDemo] = useState<ModuleDemoId | null>(null)
  const [chatInput, setChatInput] = useState('')
  const [chatLoading, setChatLoading] = useState(false)
  const [messages, setMessages] = useState<ChatMessage[]>([
    { role: 'assistant', text: 'Oi! Eu sou o Assistente Octus. Posso explicar os planos, recursos e o Programa Clientes Fundadores.' },
  ])
  const chatEndRef = useRef<HTMLDivElement>(null)
  const pricingTracked = useRef(false)

  useEffect(() => {
    publicDirectoryApi.listTenants().then(response => setTenants(response.data)).catch(() => {})
  }, [])

  useEffect(() => {
    // Não devolver o resultado de scrollIntoView: o React trata qualquer retorno
    // não vazio de um efeito como função de limpeza na próxima renderização.
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages.length, chatLoading])

  useEffect(() => {
    const pricing = document.getElementById('planos')
    if (!pricing) return
    const observer = new IntersectionObserver(entries => {
      if (!pricingTracked.current && entries.some(entry => entry.isIntersecting)) {
        pricingTracked.current = trackMarketingEvent('view_pricing', { page_path: window.location.pathname })
        if (pricingTracked.current) observer.disconnect()
      }
    }, { threshold: 0, rootMargin: '0px 0px -40px 0px' })
    observer.observe(pricing)
    const reconsider = () => {
      if (!pricingTracked.current) {
        observer.unobserve(pricing)
        observer.observe(pricing)
      }
    }
    window.addEventListener(COOKIE_CONSENT_EVENT, reconsider)
    return () => {
      observer.disconnect()
      window.removeEventListener(COOKIE_CONSENT_EVENT, reconsider)
    }
  }, [])

  async function handleLeadSubmit(event: FormEvent) {
    event.preventDefault()
    setLeadSubmitting(true)
    setLeadError(null)
    try {
      await submitLead({
        nome: leadNome.trim(), telefone: leadTelefone.trim(),
        email: leadEmail.trim() || undefined, mensagem: leadMensagem.trim() || undefined,
        privacyNoticeAcknowledged: privacyAcknowledged,
      })
      setLeadSubmitted(true)
      trackMarketingEvent('lead_submit', { form: 'institucional', lead_kind: 'trial' })
    } catch (error) {
      setLeadError(publicFormErrorMessage(error, 'Não foi possível enviar agora. Fale com o Marketing pelo WhatsApp.'))
    } finally {
      setLeadSubmitting(false)
    }
  }

  async function handleChatSubmit(event: FormEvent) {
    event.preventDefault()
    const message = chatInput.trim()
    if (message.length < 2 || chatLoading) return
    setChatInput('')
    setMessages(current => [...current, { role: 'user', text: message }])
    setChatLoading(true)
    try {
      const response = await publicAssistantApi.ask(message)
      setMessages(current => [...current, {
        role: 'assistant',
        text: normalizeAssistantReply(response.data?.reply),
      }])
    } catch {
      setMessages(current => [...current, {
        role: 'assistant',
        text: CHAT_FALLBACK_MESSAGE,
      }])
    } finally {
      setChatLoading(false)
    }
  }

  return (
    <main className={`min-h-screen overflow-x-hidden ${theme.page}`}>
      <SiteHeader theme={theme} isDark={isDark} onToggleTheme={toggleTheme} />

      <section id="conteudo" className={`relative isolate min-h-[690px] scroll-mt-24 overflow-hidden border-b ${theme.border}`}>
        <Image
          src="/institutional/octus-hero-waves.png"
          alt=""
          fill
          priority
          sizes="100vw"
          className="-z-20 object-cover object-right transition duration-500"
          style={{
            // A arte nasceu num azul-royal que não é o da marca, e no escuro ela
            // era tratada com `invert + brightness(.5) + opacity(.4)`: o invert
            // já escurece tudo, e escurecer de novo pela metade e ainda diluir
            // a 40% apagava as ondas — sobrava um borrão cinza sobre o navy.
            //
            // Agora o giro de matiz leva o royal para o ciano da logo nos DOIS
            // temas, e no escuro o invert vem acompanhado de brilho e saturação
            // PARA CIMA, não para baixo, então as ondas voltam a aparecer.
            // Os ângulos saíram de comparação lado a lado, não de conta.
            filter: isDark
              ? 'invert(1) hue-rotate(155deg) saturate(1.3) brightness(1.05)'
              : 'hue-rotate(-28deg) saturate(1.2)',
          }}
        />
        {/* Véu entre a arte e o texto.
            Antes o contraste do título era resolvido baixando a opacidade da
            imagem no celular (`opacity-[0.55]`), o que desbota a arte inteira
            para proteger um bloco de texto que ocupa só a esquerda. O degradê
            escurece exatamente onde o texto está e deixa a onda intacta do lado
            direito — e no celular, onde o texto atravessa a tela toda, ele
            fecha mais até a borda. */}
        <div
          aria-hidden="true"
          className={`absolute inset-0 -z-10 ${
            isDark
              ? 'bg-gradient-to-r from-[#08192d] via-[#08192d]/85 to-[#08192d]/30 sm:to-transparent'
              : 'bg-gradient-to-r from-white via-white/90 to-white/40 sm:to-transparent'
          }`}
        />
        <div className="mx-auto flex min-h-[690px] max-w-7xl items-center px-5 py-20 lg:px-8">
          <div className="max-w-3xl">
            <p className="mb-5 text-sm font-extrabold uppercase tracking-[0.22em] octus-accent">Octus · gestão que veste a sua marca</p>
            <h1 className={`text-5xl font-black leading-[1.02] tracking-[-0.045em] sm:text-6xl lg:text-7xl ${theme.heading}`}>
              Tudo o que seu negócio precisa, <span className="octus-accent">numa tela só.</span>
            </h1>
            <p className={`mt-7 max-w-2xl text-lg leading-8 sm:text-xl ${theme.body}`}>
              PDV, estoque, fiscal, crediário, financeiro e app próprio em um ERP claro, rápido e personalizável para o varejo e restaurantes.
            </p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <a href="#contato" className="inline-flex items-center justify-center gap-2 rounded-xl bg-octus-600 px-6 py-4 font-bold text-white shadow-xl shadow-octus-600/20 transition hover:bg-octus-700">
                Testar o Octus por 15 dias <ArrowRight size={19} />
              </a>
              <a href="#fundadores" className={`inline-flex items-center justify-center gap-2 rounded-xl border px-6 py-4 font-bold transition ${theme.outline}`}>
                Conhecer Clientes Fundadores
              </a>
            </div>
            <div className={`mt-9 hidden flex-wrap gap-x-6 gap-y-3 text-sm font-semibold sm:flex ${theme.body}`}>
              {['Sem cartão no teste', 'Configuração acompanhada', 'Sua marca em primeiro lugar', 'Fiscal e venda conectados'].map(item => (
                <span key={item} className="inline-flex items-center gap-2"><CheckCircle2 size={17} className="octus-accent" />{item}</span>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className={`border-b ${theme.border} ${theme.surface}`}>
        <div className="mx-auto grid max-w-7xl gap-px px-5 py-7 sm:grid-cols-3 lg:px-8">
          {[['15 dias', 'para conhecer sem mensalidade'], ['1 sistema', 'para conectar toda a operação'], ['100%', 'personalizável para a sua marca']].map(([value, label]) => (
            <div key={value} className="py-4 text-center">
              <p className={`text-3xl font-black ${theme.heading}`}>{value}</p><p className={`mt-1 text-sm ${theme.muted}`}>{label}</p>
            </div>
          ))}
        </div>
      </section>

      <section id="plataforma" className={`scroll-mt-24 border-b px-5 py-24 lg:px-8 ${theme.border} ${theme.surface}`}>
        <div className="mx-auto max-w-7xl">
          <div className="grid gap-8 lg:grid-cols-[.8fr_1.2fr] lg:items-end">
            <div>
              <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">A plataforma por dentro</p>
              <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>
                Mostre para o cliente uma operação pronta, não só uma promessa.
              </h2>
            </div>
            <p className={`text-lg leading-8 ${theme.body}`}>
              A página institucional agora apresenta telas do sistema com contexto: PDV, estoque, relatórios e loja do cliente. Assim quem visita entende rápido o que existe, onde ganha tempo e por que a identidade da empresa continua preservada.
            </p>
          </div>
          <SystemShowcase theme={theme} />
        </div>
      </section>

      <section id="recursos" className="scroll-mt-24 px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">O que fazemos</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Menos troca de tela. Mais controle do negócio.</h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>O Octus conecta a rotina da venda à gestão, sem tirar da sua empresa a identidade que o cliente já conhece.</p>
          </div>
          <div className="mt-12 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
            {RECURSOS.map(({ id, icon: Icon, title, desc }) => (
              <button key={id} type="button" onClick={() => setActiveModuleDemo(id as ModuleDemoId)} className={`group rounded-2xl border p-7 text-left transition hover:-translate-y-1 ${theme.card}`} aria-haspopup="dialog">
                <span className={`inline-flex rounded-xl border p-3 octus-accent ${theme.border} ${theme.soft}`}><Icon size={23} /></span>
                <h3 className={`mt-5 text-lg font-extrabold ${theme.heading}`}>{title}</h3>
                <p className={`mt-2 leading-7 ${theme.body}`}>{desc}</p>
                <span className="mt-5 inline-flex items-center gap-2 text-sm font-extrabold octus-accent">Ver o fluxo funcionando <ArrowRight size={16} /></span>
              </button>
            ))}
          </div>
        </div>
      </section>

      <section id="contador" className={`scroll-mt-24 border-y ${theme.border} ${theme.soft}`}>
        <div className="mx-auto grid max-w-7xl gap-12 px-5 py-20 lg:grid-cols-[1.05fr_.95fr] lg:items-center lg:px-8">
          <div>
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Portal do Contador</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-4xl ${theme.heading}`}>Fiscal organizado para a loja e para quem cuida dela.</h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>Seu contador recebe acesso próprio aos documentos autorizados, acompanha notas e reduz o vai e volta de XML por mensagem.</p>
          </div>
          <div className={`rounded-2xl border p-7 ${theme.card}`}>
            {['XMLs e notas em um só lugar', 'Acesso separado e controlado', 'Cadastro fiscal de produtos', 'Avisos entre contador e lojista'].map(item => (
              <div key={item} className={`flex items-center gap-3 border-b py-4 last:border-0 ${theme.border}`}><FileCheck2 size={20} className="octus-accent" /><span className={`font-semibold ${theme.heading}`}>{item}</span></div>
            ))}
          </div>
        </div>
      </section>

      <section id="fundadores" className="scroll-mt-24 px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl overflow-hidden rounded-[28px] bg-[#071f3d] text-white shadow-2xl shadow-octus-900/20">
          <div className="grid gap-10 p-8 sm:p-12 lg:grid-cols-[1.15fr_.85fr] lg:p-16">
            <div>
              <span className="inline-flex rounded-full border border-amber-300/40 bg-amber-300/10 px-4 py-2 text-xs font-extrabold uppercase tracking-[0.18em] text-amber-300">Programa Clientes Fundadores</span>
              <h2 className="mt-6 text-3xl font-black tracking-[-0.035em] sm:text-5xl">Quem chega no começo cresce com vantagens especiais.</h2>
              <p className="mt-5 max-w-2xl text-lg leading-8 text-slate-300">Para clientes do estado de São Paulo: 15 dias grátis e condições especiais nos quatro primeiros meses pagos.</p>
              <div className="mt-8 grid gap-4 sm:grid-cols-2">
                {[['30%', 'de desconto inicial'], ['+10%', 'por indicação fechada'], ['7 indicações', 'quatro mensalidades grátis'], ['Sem limite', 'de vagas no programa']].map(([value, label]) => (
                  <div key={value} className="rounded-2xl border border-white/10 bg-white/5 p-5"><p className="text-2xl font-black text-amber-300">{value}</p><p className="mt-1 text-sm text-slate-300">{label}</p></div>
                ))}
              </div>
            </div>
            <div className="flex flex-col justify-center rounded-2xl border border-white/10 bg-white/5 p-7">
              <h3 className="text-xl font-extrabold">Como funciona</h3>
              <ol className="mt-6 space-y-5">
                {[
                  'Você usa o Octus gratuitamente por 15 dias.',
                  'Nos quatro meses seguintes, começa com 30% de desconto.',
                  'Cada indicação fechada soma 10% de desconto no mesmo período.',
                  'O desconto chega a 100% com sete indicações fechadas.',
                ].map((item, index) => <li key={item} className="flex gap-4 text-slate-300"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-octus-600 text-xs font-black text-white">{index + 1}</span><span className="leading-7">{item}</span></li>)}
              </ol>
              <p className="mt-6 border-t border-white/10 pt-5 text-xs leading-6 text-slate-400">Visitas técnicas presenciais são destinadas à região metropolitana de São José do Rio Preto. Condições confirmadas com o Marketing.</p>
              <a href={MARKETING_WHATSAPP} target="_blank" rel="noreferrer" onClick={() => trackMarketingEvent('whatsapp_click', { placement: 'founders' })} className="mt-6 inline-flex items-center justify-center gap-2 rounded-xl bg-amber-300 px-5 py-3.5 font-extrabold text-[#071f3d] transition hover:bg-amber-200">Quero ser Cliente Fundador <ArrowRight size={18} /></a>
            </div>
          </div>
        </div>
      </section>

      {/* Ponte para /parceiros. O Programa Clientes Fundadores desconta a
          mensalidade de QUEM USA o sistema; o de afiliados paga comissão a quem
          não é cliente. Sem esta faixa os dois se confundiam, porque a única
          menção a indicação na home era a do desconto. */}
      <section className={`border-y px-5 py-16 lg:px-8 ${theme.border} ${theme.surface}`}>
        <div className={`mx-auto flex max-w-7xl flex-col gap-7 rounded-[28px] border p-8 sm:p-10 lg:flex-row lg:items-center lg:justify-between ${theme.border} ${theme.soft}`}>
          <div className="max-w-2xl">
            <span className="inline-flex items-center gap-2 rounded-full bg-octus-500/10 px-4 py-2 text-xs font-extrabold uppercase tracking-[0.18em] octus-accent">
              <HandCoins size={15} /> Programa de Afiliados
            </span>
            <h2 className={`mt-5 text-3xl font-black tracking-[-0.03em] sm:text-4xl ${theme.heading}`}>
              Não é cliente, mas conhece quem precisa?
            </h2>
            <p className={`mt-4 text-lg leading-8 ${theme.body}`}>
              Contadores, consultores e técnicos de TI indicam o Octus e recebem comissão sobre a implantação
              e sobre cada mensalidade paga, enquanto a indicação seguir ativa.
            </p>
          </div>
          <Link
            href="/parceiros"
            className="inline-flex shrink-0 items-center justify-center gap-2 rounded-xl bg-octus-600 px-6 py-4 font-bold text-white shadow-xl shadow-octus-600/20 transition hover:bg-octus-700"
          >
            Ver como funciona <ArrowRight size={19} />
          </Link>
        </div>
      </section>

      <section id="planos" className={`scroll-mt-24 border-y px-5 py-24 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="mx-auto max-w-3xl text-center">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Planos</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Comece no seu ritmo. Cresça sem trocar de sistema.</h2>
            <p className={`mt-5 text-lg ${theme.body}`}>Todos os planos incluem 15 dias grátis. A primeira mensalidade é cobrada no 16º dia.</p>
          </div>
          <div className="mt-14 grid items-stretch gap-6 lg:grid-cols-3">
            {PLANOS.map(plano => (
              <article key={plano.nome} className={`relative flex flex-col rounded-2xl border p-7 transition ${plano.destaque ? 'border-octus-500 bg-[#071f3d] text-white shadow-2xl shadow-octus-800/15 lg:-translate-y-3' : theme.card}`}>
                {plano.destaque && <span className="absolute -top-3 left-7 rounded-full bg-octus-600 px-3 py-1 text-xs font-extrabold uppercase tracking-wide text-white">Mais escolhido</span>}
                <h3 className={`text-2xl font-black ${plano.destaque ? 'text-white' : theme.heading}`}>{plano.nome}</h3>
                <p className={`mt-2 min-h-12 text-sm leading-6 ${plano.destaque ? 'text-slate-300' : theme.muted}`}>{plano.publico}</p>
                <p className="mt-7"><span className={`text-sm font-bold ${plano.destaque ? 'text-slate-300' : theme.muted}`}>R$ </span><span className={`text-5xl font-black tracking-tight ${plano.destaque ? 'text-white' : theme.heading}`}>{plano.preco}</span><span className={plano.destaque ? 'text-slate-300' : theme.muted}>/mês</span></p>
                {/* Só a existência da taxa, sem o valor: ele passou a ser
                    definido na contratação, e publicá-lo por plano tirava essa
                    margem. Aparece em TODOS os planos — antes o Mar anunciava
                    "Implantação gratuita", o que virou promessa a menos para
                    honrar. */}
                <p className={`mt-2 text-sm font-bold ${plano.destaque ? 'text-slate-300' : theme.muted}`}>
                  + taxa de implantação
                </p>
                <p className={`mt-4 text-sm font-bold ${plano.destaque ? 'text-slate-200' : theme.body}`}>{plano.usuarios}</p>
                <ul className="mt-7 flex-1 space-y-3">
                  {/* No card em destaque o fundo é navy, então o ciano da marca
                      (octus-500) tem folga de contraste; nos demais o fundo é
                      branco e ele cai para 2,5:1 — abaixo dos 3:1 que um ícone
                      informativo precisa. Daí o tom mais fechado no claro. */}
                  {plano.inclui.map(item => <li key={item} className={`flex gap-3 text-sm leading-6 ${plano.destaque ? 'text-slate-300' : theme.body}`}><Check size={18} className={`mt-0.5 shrink-0 ${plano.destaque ? 'text-octus-400' : 'octus-accent'}`} /><span>{item}</span></li>)}
                </ul>
                <a href="#contato" onClick={() => trackMarketingEvent('select_plan', { plan: plano.nome })} className={`mt-8 inline-flex items-center justify-center gap-2 rounded-xl px-4 py-3.5 text-sm font-extrabold transition ${plano.destaque ? 'bg-octus-600 text-white hover:bg-octus-500' : `border ${theme.outline}`}`}>Testar este plano <ArrowRight size={17} /></a>
              </article>
            ))}
          </div>
          <p className={`mx-auto mt-8 max-w-4xl text-center text-sm leading-6 ${theme.muted}`}>Todos os planos têm taxa de implantação, cobrada uma única vez, com valor definido na contratação conforme o porte da operação. O módulo restaurante é opcional e sua ativação é alinhada conforme a operação.</p>
        </div>
      </section>

      <section className="px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl"><p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Por que Octus</p><h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Uma plataforma completa sem apagar a personalidade da sua empresa.</h2></div>
          {/* Tabela de verdade, não uma grade de divs.
              O conteúdo é tabular: cada linha cruza um recurso com três
              colunas. Em `div` o leitor de tela lê "Portal direto para o
              contador, marcado, não marcado, não marcado" sem dizer a que
              coluna cada marca pertence — e os ícones sozinhos não têm texto
              nenhum, daí o <span class="sr-only"> em cada célula. */}
          <div className={`relative mt-12 overflow-x-auto rounded-2xl border ${theme.border}`}>
            <table className="w-full min-w-[560px] border-collapse">
              <caption className="sr-only">
                Comparativo entre o Octus, um ERP genérico e o uso de ferramentas separadas
              </caption>
              <thead>
                <tr className={theme.soft}>
                  <th scope="col" className={`border-b px-4 py-4 text-left text-xs font-extrabold uppercase tracking-wide sm:px-6 ${theme.border} ${theme.muted}`}>Comparativo</th>
                  <th scope="col" className={`border-b px-4 py-4 text-center text-xs font-extrabold uppercase tracking-wide octus-accent sm:px-6 ${theme.border}`}>Octus</th>
                  <th scope="col" className={`border-b px-4 py-4 text-center text-xs font-extrabold uppercase tracking-wide sm:px-6 ${theme.border} ${theme.muted}`}>ERP genérico</th>
                  <th scope="col" className={`border-b px-4 py-4 text-center text-xs font-extrabold uppercase tracking-wide sm:px-6 ${theme.border} ${theme.muted}`}>Ferramentas separadas</th>
                </tr>
              </thead>
              {/* Ao virar <table>, o `last:border-0` da versão em <div> deixou de
                  funcionar: ele passou a morar na primeira célula da linha, que
                  nunca é a última filha do <tr>. Resultado — uma linha sobrando
                  no rodapé da tabela, que não existia antes. Zerar pelo tbody
                  acerta a linha inteira de uma vez. */}
              <tbody className="[&>tr:last-child>*]:border-b-0">
                {COMPARATIVO.map(([label, octus, generico, separadas]) => (
                  <tr key={label}>
                    <th scope="row" className={`border-b px-4 py-4 text-left text-sm font-semibold sm:px-6 ${theme.border} ${theme.heading}`}>{label}</th>
                    {[octus, generico, separadas].map((value, index) => (
                      <td key={index} className={`border-b px-4 py-4 text-center text-sm sm:px-6 ${theme.border}`}>
                        <span className="flex justify-center">
                          {value === true ? <CheckCircle2 size={20} className="text-emerald-500" aria-hidden="true" />
                            : value === false ? <X size={20} className="text-slate-400" aria-hidden="true" />
                            : <span className={`text-xs ${theme.muted}`} aria-hidden="true">Varia</span>}
                          <span className="sr-only">{value === true ? 'Sim' : value === false ? 'Não' : 'Varia'}</span>
                        </span>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section id="clientes" className={`border-y px-5 py-20 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Quem já usa</p><h2 className={`mt-4 text-3xl font-black ${theme.heading}`}>Negócios reais, com identidade própria.</h2></div><a href="#contato" className="inline-flex items-center gap-2 font-bold octus-accent">Quero aparecer aqui <ArrowRight size={17} /></a></div>
          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {tenants.map(tenant => (
              <a key={tenant.slug} href={ROOT_DOMAIN ? `https://${tenant.slug}.${ROOT_DOMAIN}` : '#'} target="_blank" rel="noreferrer" className={`group flex min-h-28 items-center gap-5 rounded-2xl border p-5 transition hover:-translate-y-0.5 hover:shadow-lg sm:p-6 ${theme.card}`}>
                {/* `width`/`height` reservam a caixa antes do download: a
                    vitrine carrega depois do primeiro render, e sem as medidas
                    cada logo que chega empurra o card para baixo. `loading` e
                    `decoding` tiram essas imagens do caminho crítico — elas
                    ficam bem abaixo da dobra. */}
                <span className={`flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-2xl border bg-white p-2 shadow-sm ${theme.border}`}>
                  <img src={tenant.logoUrl!} alt={`Logo ${tenant.displayName}`} width={64} height={64} loading="lazy" decoding="async" className="h-full w-full object-contain" />
                </span>
                <span className="min-w-0"><strong className={`block truncate text-base ${theme.heading}`}>{tenant.displayName}</strong><span className={`mt-1 block truncate text-sm ${theme.muted}`}>{tenant.slug}.{ROOT_DOMAIN}</span></span><span className="ml-auto flex h-9 w-9 shrink-0 items-center justify-center rounded-full octus-accent transition group-hover:bg-octus-600 group-hover:text-white"><ExternalLink size={16} /></span>
              </a>
            ))}
            {tenants.length === 0 && <div className={`rounded-2xl border border-dashed p-7 ${theme.border}`}><Building2 className="octus-accent" /><p className={`mt-4 font-extrabold ${theme.heading}`}>Sua empresa pode ser a próxima</p><p className={`mt-2 text-sm ${theme.muted}`}>A vitrine cresce junto com os clientes do Octus.</p></div>}
          </div>
        </div>
      </section>

      <section id="contato" className="scroll-mt-24 bg-[#071f3d] px-5 py-24 text-white lg:px-8">
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[.9fr_1.1fr]">
          <div>
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] text-octus-400">Vamos conversar</p>
            <h2 className="mt-4 text-4xl font-black tracking-[-0.035em] sm:text-5xl">15 dias para sentir a diferença na rotina.</h2>
            <p className="mt-5 text-lg leading-8 text-slate-300">Conte um pouco do seu negócio. A gente ajuda a escolher o plano e prepara a implantação sem empurrar recurso que você não precisa.</p>
            <div className="mt-8 space-y-3 text-sm text-slate-300">
              <a href={telHref(CONTACTS.supportPhone)} className="flex items-center gap-3 hover:text-white"><Headphones size={18} className="text-octus-400" />Suporte · {CONTACTS.supportPhone}</a>
              <a href={MARKETING_WHATSAPP} target="_blank" rel="noreferrer" onClick={() => trackMarketingEvent('whatsapp_click', { placement: 'contact' })} className="flex items-center gap-3 hover:text-white"><MessageCircle size={18} className="text-octus-400" />Marketing · {CONTACTS.marketingPhone}</a>
              <a href={telHref(CONTACTS.devPhone)} className="flex items-center gap-3 hover:text-white"><Layers3 size={18} className="text-octus-400" />Desenvolvimento · {CONTACTS.devPhone}</a>
              <a href={`mailto:${CONTACTS.email}`} className="flex items-center gap-3 hover:text-white"><Mail size={18} className="text-octus-400" />{CONTACTS.email}</a>
            </div>
          </div>
          <div className="rounded-2xl border border-white/10 bg-white/5 p-6 sm:p-8">
            {leadSubmitted ? (
              <div className="flex min-h-80 flex-col items-center justify-center text-center"><CheckCircle2 size={42} className="text-emerald-400" /><h3 className="mt-5 text-2xl font-black">Recebemos seu contato.</h3><p className="mt-2 text-slate-300">A equipe vai falar com você em breve.</p></div>
            ) : (
              <form onSubmit={handleLeadSubmit} className="grid gap-4 sm:grid-cols-2">
                <label className="text-sm font-bold">Nome<input required maxLength={150} value={leadNome} onChange={event => setLeadNome(event.target.value)} className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400" placeholder="Como podemos te chamar?" /></label>
                <label className="text-sm font-bold">WhatsApp<input required maxLength={30} value={leadTelefone} onChange={event => setLeadTelefone(event.target.value)} className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400" placeholder="(17) 99999-9999" /></label>
                <label className="text-sm font-bold sm:col-span-2">E-mail <span className="font-normal text-slate-400">(opcional)</span><input type="email" maxLength={255} value={leadEmail} onChange={event => setLeadEmail(event.target.value)} className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400" placeholder="voce@empresa.com.br" /></label>
                <label className="text-sm font-bold sm:col-span-2">Sobre seu negócio <span className="font-normal text-slate-400">(opcional)</span><textarea rows={3} maxLength={1000} value={leadMensagem} onChange={event => setLeadMensagem(event.target.value)} className="mt-2 w-full resize-none rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400" placeholder="Varejo, restaurante, tamanho da equipe..." /></label>
                <label className="flex items-start gap-3 text-xs leading-relaxed text-slate-300 sm:col-span-2"><input required type="checkbox" checked={privacyAcknowledged} onChange={event => setPrivacyAcknowledged(event.target.checked)} className="mt-0.5 h-4 w-4 accent-octus-500" /><span>Li e estou ciente da <Link href="/privacidade" target="_blank" className="font-bold text-octus-300 underline">Política de Privacidade</Link>, inclusive sobre o uso dos dados para responder este contato. Esta ciência não autoriza marketing opcional.</span></label>
                {leadError && <p className="text-sm text-red-300 sm:col-span-2">{leadError}</p>}
                <button disabled={leadSubmitting} className="inline-flex items-center justify-center gap-2 rounded-xl bg-octus-600 px-5 py-4 font-extrabold text-white transition hover:bg-octus-500 disabled:opacity-60 sm:col-span-2">{leadSubmitting ? <><Loader2 size={18} className="animate-spin" />Enviando...</> : <>Começar meu teste grátis <ArrowRight size={18} /></>}</button>
              </form>
            )}
          </div>
        </div>
      </section>

      <section className={`px-5 py-20 lg:px-8 ${theme.surface}`}>
        <div className="mx-auto max-w-4xl"><p className="text-center text-sm font-extrabold uppercase tracking-[0.2em] octus-accent">Dúvidas frequentes</p><h2 className={`mt-4 text-center text-3xl font-black ${theme.heading}`}>Antes de começar</h2><div className="mt-9 space-y-3">{FAQS.map(([question, answer]) => <details key={question} className={`group rounded-2xl border p-5 ${theme.card}`}><summary className={`flex cursor-pointer list-none items-center justify-between gap-4 font-extrabold ${theme.heading}`}>{question}<ChevronDown size={18} className="shrink-0 transition group-open:rotate-180" /></summary><p className={`mt-3 max-w-3xl leading-7 ${theme.body}`}>{answer}</p></details>)}</div></div>
      </section>

      <SiteFooter theme={theme} />

      {activeModuleDemo ? <PlatformModuleDemo moduleId={activeModuleDemo} onClose={() => setActiveModuleDemo(null)} theme={theme} /> : null}

      <div className="fixed bottom-5 right-5 z-50">
        {chatOpen && (
          <section aria-label="Assistente Octus" className={`mb-3 flex h-[480px] w-[calc(100vw-40px)] max-w-[380px] flex-col overflow-hidden rounded-2xl border shadow-2xl ${theme.border} ${theme.surface}`}>
            <header className="flex items-center gap-3 bg-[#071f3d] px-4 py-4 text-white"><span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white"><Logo className="h-7 w-7" title="Octus" /></span><span className="min-w-0 flex-1"><strong className="block text-sm">Assistente Octus</strong><span className="block text-xs text-slate-300">Dúvidas rápidas sobre o sistema</span></span><button type="button" onClick={() => setChatOpen(false)} aria-label="Fechar assistente"><X size={18} /></button></header>
            <div className={`flex-1 space-y-3 overflow-y-auto p-4 ${theme.soft}`}>
              {messages.map((message, index) => <p key={`${message.role}-${index}`} className={`max-w-[88%] rounded-2xl px-4 py-3 text-sm leading-6 ${message.role === 'user' ? 'ml-auto bg-octus-600 text-white' : `${theme.surface} ${theme.body}`}`}>{message.text}</p>)}
              {chatLoading && <p className={`inline-flex items-center gap-2 rounded-2xl px-4 py-3 text-sm ${theme.surface} ${theme.body}`}><Loader2 size={15} className="animate-spin" />Pensando...</p>}
              <div ref={chatEndRef} />
            </div>
            <form onSubmit={handleChatSubmit} className={`border-t p-3 ${theme.border}`}><div className="flex gap-2"><input value={chatInput} onChange={event => setChatInput(event.target.value)} maxLength={500} placeholder="Ex.: qual plano atende um restaurante?" className={`min-w-0 flex-1 rounded-xl border px-3 py-2.5 text-sm outline-none focus:border-octus-500 ${theme.input}`} /><button type="submit" disabled={chatLoading || chatInput.trim().length < 2} aria-label="Enviar pergunta" className="rounded-xl bg-octus-600 p-3 text-white disabled:opacity-50"><Send size={18} /></button></div><p className={`mt-2 text-center text-[10px] ${theme.muted}`}>Sem acesso a dados de lojas. Para contratar, fale com o <a href={MARKETING_WHATSAPP} target="_blank" rel="noreferrer" className="font-bold octus-accent">Marketing</a>.</p></form>
          </section>
        )}
        <button type="button" onClick={() => setChatOpen(open => !open)} aria-label={chatOpen ? 'Fechar Assistente Octus' : 'Abrir Assistente Octus'} className="ml-auto flex items-center gap-2 rounded-full bg-octus-600 px-4 py-3 font-extrabold text-white shadow-xl shadow-octus-800/25 transition hover:bg-octus-700"><span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-white"><Logo className="h-5 w-5" title="Octus" /></span><span className="hidden sm:inline">Pergunte ao Octus</span></button>
      </div>
    </main>
  )
}
