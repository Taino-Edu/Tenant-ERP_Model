'use client'

import Image from 'next/image'
import Link from 'next/link'
import { FormEvent, useEffect, useRef, useState } from 'react'
import {
  ArrowRight, BarChart3, Bot, Boxes, Building2, Calculator, Check,
  CheckCircle2, ChevronDown, ExternalLink, FileCheck2, Headphones,
  Instagram, Layers3, Linkedin, Loader2, Mail, Menu, MessageCircle,
  Moon, Palette, ReceiptText, Send, ShieldCheck, Smartphone, Sun,
  UtensilsCrossed, X,
} from 'lucide-react'
import {
  getErrorMessage, leadsApi, publicAssistantApi, publicDirectoryApi,
  type PublicTenantDto,
} from '@/lib/api'
import { PLANOS, formatarReais, taxaImplantacao } from '@/lib/planos'

const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || ''
const MARKETING_WHATSAPP = 'https://wa.me/5517997455482'

const NAV_LINKS = [
  { href: '#recursos', label: 'Recursos' },
  { href: '#planos', label: 'Planos' },
  { href: '#fundadores', label: 'Clientes Fundadores' },
  { href: '#contador', label: 'Portal do Contador' },
]

const RECURSOS = [
  { icon: ReceiptText, title: 'PDV e fiscal', desc: 'Venda, caixa e emissão de NFC-e no mesmo fluxo, sem redigitar informações.' },
  { icon: Boxes, title: 'Estoque organizado', desc: 'Produtos, variantes, movimentações, alertas e cadastro fiscal em um só lugar.' },
  { icon: Calculator, title: 'Financeiro claro', desc: 'Crediário, contas a receber, fechamento de caixa e visão real da operação.' },
  { icon: BarChart3, title: 'Decisões com dados', desc: 'Relatórios e indicadores que mostram o que vende, o que gira e o que precisa de atenção.' },
  { icon: Smartphone, title: 'Experiência própria', desc: 'Site e app instalável com nome, cores, logo e domínio da sua empresa.' },
  { icon: UtensilsCrossed, title: 'Módulo restaurante', desc: 'Comandas e operação de restaurante como adicional opcional, ativado apenas para quem precisa.' },
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
  const [isDark, setIsDark] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const [tenants, setTenants] = useState<PublicTenantDto[]>([])
  const [leadNome, setLeadNome] = useState('')
  const [leadTelefone, setLeadTelefone] = useState('')
  const [leadEmail, setLeadEmail] = useState('')
  const [leadMensagem, setLeadMensagem] = useState('')
  const [leadSubmitting, setLeadSubmitting] = useState(false)
  const [leadSubmitted, setLeadSubmitted] = useState(false)
  const [leadError, setLeadError] = useState<string | null>(null)
  const [chatOpen, setChatOpen] = useState(false)
  const [chatInput, setChatInput] = useState('')
  const [chatLoading, setChatLoading] = useState(false)
  const [messages, setMessages] = useState<ChatMessage[]>([
    { role: 'assistant', text: 'Oi! Eu sou o Assistente Octus. Posso explicar os planos, recursos e o Programa Clientes Fundadores.' },
  ])
  const chatEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    setIsDark(localStorage.getItem('institucional-theme') === 'dark')
    document.body.classList.add('institucional-page')
    publicDirectoryApi.listTenants().then(response => setTenants(response.data)).catch(() => {})
    return () => document.body.classList.remove('institucional-page')
  }, [])

  useEffect(() => {
    // Não devolver o resultado de scrollIntoView: o React trata qualquer retorno
    // não vazio de um efeito como função de limpeza na próxima renderização.
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages.length, chatLoading])

  const toggleTheme = () => {
    const next = !isDark
    setIsDark(next)
    localStorage.setItem('institucional-theme', next ? 'dark' : 'light')
  }

  async function handleLeadSubmit(event: FormEvent) {
    event.preventDefault()
    setLeadSubmitting(true)
    setLeadError(null)
    try {
      await leadsApi.create({
        nome: leadNome.trim(), telefone: leadTelefone.trim(),
        email: leadEmail.trim() || undefined, mensagem: leadMensagem.trim() || undefined,
      })
      setLeadSubmitted(true)
    } catch (error) {
      setLeadError(getErrorMessage(error, 'Não foi possível enviar agora. Fale com o Marketing pelo WhatsApp.'))
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

  const theme = isDark ? {
    page: 'bg-[#08192d] text-white', surface: 'bg-[#0d223b]', soft: 'bg-[#102a48]',
    border: 'border-white/10', heading: 'text-white', body: 'text-slate-300', muted: 'text-slate-400',
    card: 'bg-[#0d223b] border-white/10 hover:border-blue-400/50', header: 'bg-[#08192d]/95 border-white/10',
    outline: 'border-white/20 text-white hover:bg-white/5', input: 'bg-white/5 border-white/15 text-white placeholder:text-slate-500',
  } : {
    page: 'bg-white text-[#071f3d]', surface: 'bg-white', soft: 'bg-[#f3f7ff]',
    border: 'border-[#0b3261]/10', heading: 'text-[#071f3d]', body: 'text-[#38516d]', muted: 'text-[#657b93]',
    card: 'bg-white border-[#0b3261]/10 hover:border-blue-500/50', header: 'bg-white/95 border-[#0b3261]/10',
    outline: 'border-[#0b3261]/20 text-[#0b3261] hover:bg-[#f3f7ff]', input: 'bg-white border-[#0b3261]/15 text-[#071f3d] placeholder:text-[#7d8ea1]',
  }

  return (
    <main className={`min-h-screen overflow-x-hidden ${theme.page}`}>
      <header className={`sticky top-0 z-40 border-b backdrop-blur-xl ${theme.header}`}>
        <div className="mx-auto flex h-[72px] max-w-7xl items-center justify-between px-5 lg:px-8">
          <Link href="/" aria-label="3E Systen — início" className={`text-2xl font-black tracking-[-0.04em] ${theme.heading}`}>
            <span className="text-blue-600">3E</span> Systen
          </Link>
          <nav className="hidden items-center gap-7 lg:flex" aria-label="Navegação principal">
            {NAV_LINKS.map(link => <a key={link.href} href={link.href} className={`text-sm font-semibold transition hover:text-blue-600 ${theme.body}`}>{link.label}</a>)}
          </nav>
          <div className="flex items-center gap-2">
            <button type="button" onClick={toggleTheme} aria-label={isDark ? 'Ativar tema claro' : 'Ativar tema escuro'} className={`rounded-xl border p-2.5 transition ${theme.outline}`}>
              {isDark ? <Sun size={18} /> : <Moon size={18} />}
            </button>
            <Link href="/login" className={`hidden rounded-xl border px-4 py-2.5 text-sm font-bold sm:block ${theme.outline}`}>Entrar</Link>
            <a href="#contato" className="hidden rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-bold text-white transition hover:bg-blue-700 md:block">Teste grátis</a>
            <button type="button" onClick={() => setMenuOpen(open => !open)} aria-expanded={menuOpen} aria-label="Abrir menu" className={`rounded-xl border p-2.5 lg:hidden ${theme.outline}`}>
              {menuOpen ? <X size={18} /> : <Menu size={18} />}
            </button>
          </div>
        </div>
        {menuOpen && (
          <nav className={`border-t px-5 py-5 lg:hidden ${theme.border}`}>
            <div className="mx-auto flex max-w-7xl flex-col gap-4">
              {NAV_LINKS.map(link => <a key={link.href} href={link.href} onClick={() => setMenuOpen(false)} className={`font-semibold ${theme.body}`}>{link.label}</a>)}
              <Link href="/login" className="font-semibold text-blue-600">Entrar</Link>
              <a href="#contato" onClick={() => setMenuOpen(false)} className="font-semibold text-blue-600">Começar teste grátis</a>
            </div>
          </nav>
        )}
      </header>

      <section className={`relative isolate min-h-[690px] overflow-hidden border-b ${theme.border}`}>
        <Image
          src="/institutional/octus-hero-waves.png"
          alt=""
          fill
          priority
          sizes="100vw"
          className={`-z-10 object-cover object-right transition duration-500 ${
            isDark
              ? 'opacity-40 invert hue-rotate-180 brightness-50 saturate-150'
              : 'opacity-[0.55] sm:opacity-100'
          }`}
        />
        <div className="mx-auto flex min-h-[690px] max-w-7xl items-center px-5 py-20 lg:px-8">
          <div className="max-w-3xl">
            <p className="mb-5 text-sm font-extrabold uppercase tracking-[0.22em] text-blue-600">Octus · gestão que veste a sua marca</p>
            <h1 className={`text-5xl font-black leading-[1.02] tracking-[-0.045em] sm:text-6xl lg:text-7xl ${theme.heading}`}>
              Tudo o que seu negócio precisa, <span className="text-blue-600">numa tela só.</span>
            </h1>
            <p className={`mt-7 max-w-2xl text-lg leading-8 sm:text-xl ${theme.body}`}>
              PDV, estoque, fiscal, crediário, financeiro e app próprio em um ERP claro, rápido e personalizável para o varejo e restaurantes.
            </p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <a href="#contato" className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-6 py-4 font-bold text-white shadow-xl shadow-blue-600/20 transition hover:bg-blue-700">
                Testar o Octus por 15 dias <ArrowRight size={19} />
              </a>
              <a href="#fundadores" className={`inline-flex items-center justify-center gap-2 rounded-xl border px-6 py-4 font-bold transition ${theme.outline}`}>
                Conhecer Clientes Fundadores
              </a>
            </div>
            <div className={`mt-9 hidden flex-wrap gap-x-6 gap-y-3 text-sm font-semibold sm:flex ${theme.body}`}>
              {['Sem cartão no teste', 'Configuração acompanhada', 'Sua marca em primeiro lugar'].map(item => (
                <span key={item} className="inline-flex items-center gap-2"><CheckCircle2 size={17} className="text-blue-600" />{item}</span>
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

      <section id="recursos" className="scroll-mt-24 px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] text-blue-600">O que fazemos</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Menos troca de tela. Mais controle do negócio.</h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>O Octus conecta a rotina da venda à gestão, sem tirar da sua empresa a identidade que o cliente já conhece.</p>
          </div>
          <div className="mt-12 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
            {RECURSOS.map(({ icon: Icon, title, desc }) => (
              <article key={title} className={`rounded-2xl border p-7 transition ${theme.card}`}>
                <span className={`inline-flex rounded-xl border p-3 text-blue-600 ${theme.border} ${theme.soft}`}><Icon size={23} /></span>
                <h3 className={`mt-5 text-lg font-extrabold ${theme.heading}`}>{title}</h3>
                <p className={`mt-2 leading-7 ${theme.body}`}>{desc}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section id="contador" className={`scroll-mt-24 border-y ${theme.border} ${theme.soft}`}>
        <div className="mx-auto grid max-w-7xl gap-12 px-5 py-20 lg:grid-cols-[1.05fr_.95fr] lg:items-center lg:px-8">
          <div>
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] text-blue-600">Portal do Contador</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-4xl ${theme.heading}`}>Fiscal organizado para a loja e para quem cuida dela.</h2>
            <p className={`mt-5 text-lg leading-8 ${theme.body}`}>Seu contador recebe acesso próprio aos documentos autorizados, acompanha notas e reduz o vai e volta de XML por mensagem.</p>
          </div>
          <div className={`rounded-2xl border p-7 ${theme.card}`}>
            {['XMLs e notas em um só lugar', 'Acesso separado e controlado', 'Cadastro fiscal de produtos', 'Avisos entre contador e lojista'].map(item => (
              <div key={item} className={`flex items-center gap-3 border-b py-4 last:border-0 ${theme.border}`}><FileCheck2 size={20} className="text-blue-600" /><span className={`font-semibold ${theme.heading}`}>{item}</span></div>
            ))}
          </div>
        </div>
      </section>

      <section id="fundadores" className="scroll-mt-24 px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl overflow-hidden rounded-[28px] bg-[#071f3d] text-white shadow-2xl shadow-blue-950/20">
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
                ].map((item, index) => <li key={item} className="flex gap-4 text-slate-300"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-blue-600 text-xs font-black text-white">{index + 1}</span><span className="leading-7">{item}</span></li>)}
              </ol>
              <p className="mt-6 border-t border-white/10 pt-5 text-xs leading-6 text-slate-400">Visitas técnicas presenciais são destinadas à região metropolitana de São José do Rio Preto. Condições confirmadas com o Marketing.</p>
              <a href={MARKETING_WHATSAPP} target="_blank" rel="noreferrer" className="mt-6 inline-flex items-center justify-center gap-2 rounded-xl bg-amber-300 px-5 py-3.5 font-extrabold text-[#071f3d] transition hover:bg-amber-200">Quero ser Cliente Fundador <ArrowRight size={18} /></a>
            </div>
          </div>
        </div>
      </section>

      <section id="planos" className={`scroll-mt-24 border-y px-5 py-24 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="mx-auto max-w-3xl text-center">
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] text-blue-600">Planos</p>
            <h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Comece no seu ritmo. Cresça sem trocar de sistema.</h2>
            <p className={`mt-5 text-lg ${theme.body}`}>Todos os planos incluem 15 dias grátis. A primeira mensalidade é cobrada no 16º dia.</p>
          </div>
          <div className="mt-14 grid items-stretch gap-6 lg:grid-cols-3">
            {PLANOS.map(plano => (
              <article key={plano.nome} className={`relative flex flex-col rounded-2xl border p-7 transition ${plano.destaque ? 'border-blue-600 bg-[#071f3d] text-white shadow-2xl shadow-blue-800/15 lg:-translate-y-3' : theme.card}`}>
                {plano.destaque && <span className="absolute -top-3 left-7 rounded-full bg-blue-600 px-3 py-1 text-xs font-extrabold uppercase tracking-wide text-white">Mais escolhido</span>}
                <h3 className={`text-2xl font-black ${plano.destaque ? 'text-white' : theme.heading}`}>{plano.nome}</h3>
                <p className={`mt-2 min-h-12 text-sm leading-6 ${plano.destaque ? 'text-slate-300' : theme.muted}`}>{plano.publico}</p>
                <p className="mt-7"><span className={`text-sm font-bold ${plano.destaque ? 'text-slate-300' : theme.muted}`}>R$ </span><span className={`text-5xl font-black tracking-tight ${plano.destaque ? 'text-white' : theme.heading}`}>{plano.preco}</span><span className={plano.destaque ? 'text-slate-300' : theme.muted}>/mês</span></p>
                <p className={`mt-2 text-sm font-bold ${plano.taxaImplantacao === 0 ? 'text-emerald-400' : plano.destaque ? 'text-slate-300' : theme.muted}`}>
                  {plano.taxaImplantacao === 0 ? 'Implantação gratuita' : `${formatarReais(taxaImplantacao(plano))} de implantação`}
                </p>
                <p className={`mt-4 text-sm font-bold ${plano.destaque ? 'text-slate-200' : theme.body}`}>{plano.usuarios}</p>
                <ul className="mt-7 flex-1 space-y-3">
                  {plano.inclui.map(item => <li key={item} className={`flex gap-3 text-sm leading-6 ${plano.destaque ? 'text-slate-300' : theme.body}`}><Check size={18} className="mt-0.5 shrink-0 text-blue-500" /><span>{item}</span></li>)}
                </ul>
                <a href="#contato" className={`mt-8 inline-flex items-center justify-center gap-2 rounded-xl px-4 py-3.5 text-sm font-extrabold transition ${plano.destaque ? 'bg-blue-600 text-white hover:bg-blue-500' : `border ${theme.outline}`}`}>Testar este plano <ArrowRight size={17} /></a>
              </article>
            ))}
          </div>
          <p className={`mx-auto mt-8 max-w-4xl text-center text-sm leading-6 ${theme.muted}`}>Lagoa e Rio têm implantação equivalente a duas mensalidades. O plano Mar tem implantação gratuita. O módulo restaurante é opcional e sua ativação é alinhada conforme a operação.</p>
        </div>
      </section>

      <section className="px-5 py-24 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="max-w-3xl"><p className="text-sm font-extrabold uppercase tracking-[0.2em] text-blue-600">Por que Octus</p><h2 className={`mt-4 text-3xl font-black tracking-[-0.03em] sm:text-5xl ${theme.heading}`}>Uma plataforma completa sem apagar a personalidade da sua empresa.</h2></div>
          <div className={`mt-12 overflow-hidden rounded-2xl border ${theme.border}`}>
            <div className={`grid grid-cols-[1.7fr_repeat(3,.75fr)] border-b px-4 py-4 text-center text-xs font-extrabold uppercase tracking-wide sm:px-6 ${theme.border} ${theme.soft}`}><span className="text-left">Comparativo</span><span className="text-blue-600">Octus</span><span>ERP genérico</span><span>Ferramentas separadas</span></div>
            {COMPARATIVO.map(([label, octus, generico, separadas]) => (
              <div key={label} className={`grid grid-cols-[1.7fr_repeat(3,.75fr)] items-center border-b px-4 py-4 text-center text-sm last:border-0 sm:px-6 ${theme.border}`}>
                <span className={`pr-3 text-left font-semibold ${theme.heading}`}>{label}</span>
                {[octus, generico, separadas].map((value, index) => <span key={index} className="flex justify-center">{value === true ? <CheckCircle2 size={20} className="text-emerald-500" /> : value === false ? <X size={20} className="text-slate-400" /> : <span className={`text-xs ${theme.muted}`}>Varia</span>}</span>)}
              </div>
            ))}
          </div>
        </div>
      </section>

      <section id="clientes" className={`border-y px-5 py-20 lg:px-8 ${theme.border} ${theme.soft}`}>
        <div className="mx-auto max-w-7xl">
          <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="text-sm font-extrabold uppercase tracking-[0.2em] text-blue-600">Quem já usa</p><h2 className={`mt-4 text-3xl font-black ${theme.heading}`}>Negócios reais, com identidade própria.</h2></div><a href="#contato" className="inline-flex items-center gap-2 font-bold text-blue-600">Quero aparecer aqui <ArrowRight size={17} /></a></div>
          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {tenants.map(tenant => (
              <a key={tenant.slug} href={ROOT_DOMAIN ? `https://${tenant.slug}.${ROOT_DOMAIN}` : '#'} target="_blank" rel="noreferrer" className={`flex items-center gap-4 rounded-2xl border p-6 transition ${theme.card}`}>
                {tenant.logoUrl ? <img src={tenant.logoUrl} alt={`Logo ${tenant.displayName}`} className="h-14 w-14 rounded-xl object-cover" /> : <span className="flex h-14 w-14 items-center justify-center rounded-xl bg-blue-600 font-black text-white">{tenant.displayName.slice(0, 2).toUpperCase()}</span>}
                <span><strong className={`block ${theme.heading}`}>{tenant.displayName}</strong><span className={`mt-1 block text-sm ${theme.muted}`}>{tenant.slug}.{ROOT_DOMAIN}</span></span><ExternalLink size={16} className="ml-auto text-blue-600" />
              </a>
            ))}
            {tenants.length === 0 && <div className={`rounded-2xl border border-dashed p-7 ${theme.border}`}><Building2 className="text-blue-600" /><p className={`mt-4 font-extrabold ${theme.heading}`}>Sua empresa pode ser a próxima</p><p className={`mt-2 text-sm ${theme.muted}`}>A vitrine cresce junto com os clientes do Octus.</p></div>}
          </div>
        </div>
      </section>

      <section id="contato" className="scroll-mt-24 bg-[#071f3d] px-5 py-24 text-white lg:px-8">
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[.9fr_1.1fr]">
          <div>
            <p className="text-sm font-extrabold uppercase tracking-[0.2em] text-blue-400">Vamos conversar</p>
            <h2 className="mt-4 text-4xl font-black tracking-[-0.035em] sm:text-5xl">15 dias para sentir a diferença na rotina.</h2>
            <p className="mt-5 text-lg leading-8 text-slate-300">Conte um pouco do seu negócio. A gente ajuda a escolher o plano e prepara a implantação sem empurrar recurso que você não precisa.</p>
            <div className="mt-8 space-y-3 text-sm text-slate-300">
              <a href="tel:+5517997563555" className="flex items-center gap-3 hover:text-white"><Headphones size={18} className="text-blue-400" />Suporte · +55 17 99756-3555</a>
              <a href={MARKETING_WHATSAPP} target="_blank" rel="noreferrer" className="flex items-center gap-3 hover:text-white"><MessageCircle size={18} className="text-blue-400" />Marketing · +55 17 99745-5482</a>
              <a href="tel:+5517997455282" className="flex items-center gap-3 hover:text-white"><Layers3 size={18} className="text-blue-400" />Desenvolvimento · +55 17 99745-5282</a>
              <a href="mailto:3esysten@gmail.com" className="flex items-center gap-3 hover:text-white"><Mail size={18} className="text-blue-400" />3esysten@gmail.com</a>
            </div>
          </div>
          <div className="rounded-2xl border border-white/10 bg-white/5 p-6 sm:p-8">
            {leadSubmitted ? (
              <div className="flex min-h-80 flex-col items-center justify-center text-center"><CheckCircle2 size={42} className="text-emerald-400" /><h3 className="mt-5 text-2xl font-black">Recebemos seu contato.</h3><p className="mt-2 text-slate-300">A equipe vai falar com você em breve.</p></div>
            ) : (
              <form onSubmit={handleLeadSubmit} className="grid gap-4 sm:grid-cols-2">
                <label className="text-sm font-bold">Nome<input required maxLength={150} value={leadNome} onChange={event => setLeadNome(event.target.value)} className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-blue-400" placeholder="Como podemos te chamar?" /></label>
                <label className="text-sm font-bold">WhatsApp<input required maxLength={30} value={leadTelefone} onChange={event => setLeadTelefone(event.target.value)} className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-blue-400" placeholder="(17) 99999-9999" /></label>
                <label className="text-sm font-bold sm:col-span-2">E-mail <span className="font-normal text-slate-400">(opcional)</span><input type="email" maxLength={255} value={leadEmail} onChange={event => setLeadEmail(event.target.value)} className="mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-blue-400" placeholder="voce@empresa.com.br" /></label>
                <label className="text-sm font-bold sm:col-span-2">Sobre seu negócio <span className="font-normal text-slate-400">(opcional)</span><textarea rows={3} maxLength={1000} value={leadMensagem} onChange={event => setLeadMensagem(event.target.value)} className="mt-2 w-full resize-none rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-blue-400" placeholder="Varejo, restaurante, tamanho da equipe..." /></label>
                {leadError && <p className="text-sm text-red-300 sm:col-span-2">{leadError}</p>}
                <button disabled={leadSubmitting} className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-4 font-extrabold text-white transition hover:bg-blue-500 disabled:opacity-60 sm:col-span-2">{leadSubmitting ? <><Loader2 size={18} className="animate-spin" />Enviando...</> : <>Começar meu teste grátis <ArrowRight size={18} /></>}</button>
              </form>
            )}
          </div>
        </div>
      </section>

      <section className={`px-5 py-20 lg:px-8 ${theme.surface}`}>
        <div className="mx-auto max-w-4xl"><p className="text-center text-sm font-extrabold uppercase tracking-[0.2em] text-blue-600">Dúvidas frequentes</p><h2 className={`mt-4 text-center text-3xl font-black ${theme.heading}`}>Antes de começar</h2><div className="mt-9 space-y-3">{FAQS.map(([question, answer]) => <details key={question} className={`group rounded-2xl border p-5 ${theme.card}`}><summary className={`flex cursor-pointer list-none items-center justify-between gap-4 font-extrabold ${theme.heading}`}>{question}<ChevronDown size={18} className="shrink-0 transition group-open:rotate-180" /></summary><p className={`mt-3 max-w-3xl leading-7 ${theme.body}`}>{answer}</p></details>)}</div></div>
      </section>

      <footer className={`border-t px-5 py-9 lg:px-8 ${theme.border}`}>
        <div className={`mx-auto flex max-w-7xl flex-col gap-6 text-sm sm:flex-row sm:items-center sm:justify-between ${theme.muted}`}>
          <div><p className={`text-lg font-black ${theme.heading}`}><span className="text-blue-600">3E</span> Systen</p><p className="mt-1">Octus · gestão completa com a identidade da sua empresa.</p></div>
          <div className="flex flex-wrap items-center gap-5"><a href="https://www.instagram.com/3e.systen/" target="_blank" rel="noreferrer" aria-label="Instagram" className="hover:text-blue-600"><Instagram size={19} /></a><a href="https://www.linkedin.com/company/3e-systen/posts/?feedView=all" target="_blank" rel="noreferrer" aria-label="LinkedIn" className="hover:text-blue-600"><Linkedin size={19} /></a><Link href="/termos" className="hover:text-blue-600">Termos</Link><Link href="/privacidade" className="hover:text-blue-600">Privacidade</Link></div>
        </div>
      </footer>

      <div className="fixed bottom-5 right-5 z-50">
        {chatOpen && (
          <section aria-label="Assistente Octus" className={`mb-3 flex h-[480px] w-[calc(100vw-40px)] max-w-[380px] flex-col overflow-hidden rounded-2xl border shadow-2xl ${theme.border} ${theme.surface}`}>
            <header className="flex items-center gap-3 bg-[#071f3d] px-4 py-4 text-white"><span className="rounded-xl bg-blue-600 p-2"><Bot size={20} /></span><span className="min-w-0 flex-1"><strong className="block text-sm">Assistente Octus</strong><span className="block text-xs text-slate-300">Dúvidas rápidas sobre o sistema</span></span><button type="button" onClick={() => setChatOpen(false)} aria-label="Fechar assistente"><X size={18} /></button></header>
            <div className={`flex-1 space-y-3 overflow-y-auto p-4 ${theme.soft}`}>
              {messages.map((message, index) => <p key={`${message.role}-${index}`} className={`max-w-[88%] rounded-2xl px-4 py-3 text-sm leading-6 ${message.role === 'user' ? 'ml-auto bg-blue-600 text-white' : `${theme.surface} ${theme.body}`}`}>{message.text}</p>)}
              {chatLoading && <p className={`inline-flex items-center gap-2 rounded-2xl px-4 py-3 text-sm ${theme.surface} ${theme.body}`}><Loader2 size={15} className="animate-spin" />Pensando...</p>}
              <div ref={chatEndRef} />
            </div>
            <form onSubmit={handleChatSubmit} className={`border-t p-3 ${theme.border}`}><div className="flex gap-2"><input value={chatInput} onChange={event => setChatInput(event.target.value)} maxLength={500} placeholder="Ex.: qual plano tem implantação grátis?" className={`min-w-0 flex-1 rounded-xl border px-3 py-2.5 text-sm outline-none focus:border-blue-500 ${theme.input}`} /><button type="submit" disabled={chatLoading || chatInput.trim().length < 2} aria-label="Enviar pergunta" className="rounded-xl bg-blue-600 p-3 text-white disabled:opacity-50"><Send size={18} /></button></div><p className={`mt-2 text-center text-[10px] ${theme.muted}`}>Sem acesso a dados de lojas. Para contratar, fale com o <a href={MARKETING_WHATSAPP} target="_blank" rel="noreferrer" className="font-bold text-blue-600">Marketing</a>.</p></form>
          </section>
        )}
        <button type="button" onClick={() => setChatOpen(open => !open)} aria-label={chatOpen ? 'Fechar Assistente Octus' : 'Abrir Assistente Octus'} className="ml-auto flex items-center gap-2 rounded-full bg-blue-600 px-5 py-3.5 font-extrabold text-white shadow-xl shadow-blue-800/25 transition hover:bg-blue-700"><Bot size={20} /><span className="hidden sm:inline">Pergunte ao Octus</span></button>
      </div>
    </main>
  )
}
