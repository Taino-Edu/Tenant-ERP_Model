'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import {
  Store, ShieldCheck, Layers, Smartphone, Receipt, TrendingUp,
  ArrowRight, CheckCircle2, Sun, Moon, Menu, X,
} from 'lucide-react'

const NAV_LINKS = [
  { href: '#quem-somos',    label: 'Quem somos' },
  { href: '#o-que-fazemos', label: 'O que fazemos' },
  { href: '#clientes',      label: 'Clientes' },
  { href: '#contato',       label: 'Contato' },
]

const PILARES = [
  {
    icon: Store,
    title: 'Sua loja, sua identidade',
    desc: 'Cada cliente recebe um espaço próprio, com subdomínio, cores e logo dele — como se fosse um sistema exclusivo, feito sob medida.',
  },
  {
    icon: Receipt,
    title: 'Fiscal sem dor de cabeça',
    desc: 'Emissão de NFC-e, controle de impostos e integração com o contador rodando por baixo, sem o lojista precisar entender de SEFAZ.',
  },
  {
    icon: Layers,
    title: 'Tudo em um só lugar',
    desc: 'PDV, estoque, crediário, financeiro e relatórios conversando entre si — sem planilha solta, sem sistema remendado.',
  },
  {
    icon: Smartphone,
    title: 'App próprio, sem custo de loja',
    desc: 'Instala na tela inicial do celular do cliente como um app de verdade, com a marca do lojista — sem passar pela Apple Store ou Google Play.',
  },
  {
    icon: ShieldCheck,
    title: 'Dados isolados e seguros',
    desc: 'Cada loja opera em um espaço isolado no banco de dados — o que é de um cliente nunca se mistura com o de outro.',
  },
  {
    icon: TrendingUp,
    title: 'Feito pra crescer junto',
    desc: 'Arquitetura pensada para atender de uma loja só a uma rede inteira, sem trocar de sistema no meio do caminho.',
  },
]

export default function InstitucionalPage() {
  // Tema claro (branco + azul) é o padrão da identidade — o escuro é opcional
  // e fica salvo no navegador do visitante.
  const [isDark,   setIsDark]   = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)

  useEffect(() => {
    setIsDark(localStorage.getItem('institucional-theme') === 'dark')
  }, [])

  const toggleTheme = () => {
    const next = !isDark
    setIsDark(next)
    localStorage.setItem('institucional-theme', next ? 'dark' : 'light')
  }

  const C = isDark ? {
    bg:      'bg-[#121215]',
    header:  'bg-[#121215]/95 border-white/10',
    card:    'bg-[#1A1A1F] border-white/10 hover:border-brand-500/40',
    heading: 'text-white',
    body:    'text-white/65',
    muted:   'text-white/45',
    section: 'bg-[#17171B]',
    border:  'border-white/10',
    chip:    'bg-brand-500/15 text-brand-300',
    navLink: 'text-white/70 hover:text-white',
    outline: 'border-white/25 text-white hover:bg-white/5',
  } : {
    bg:      'bg-white',
    header:  'bg-white/95 border-[#0C3D5A]/10',
    card:    'bg-white border-[#0C3D5A]/10 hover:border-brand-500/60',
    heading: 'text-[#0C3D5A]',
    body:    'text-[#3E5A6E]',
    muted:   'text-[#6B8598]',
    section: 'bg-brand-50',
    border:  'border-[#0C3D5A]/10',
    chip:    'bg-brand-100 text-brand-700',
    navLink: 'text-[#3E5A6E] hover:text-[#0C3D5A]',
    outline: 'border-[#0C3D5A]/25 text-[#0C3D5A] hover:bg-brand-50',
  }

  return (
    <main className={`min-h-screen ${C.bg}`}>
      {/* ── Navbar ───────────────────────────────────────────────────────── */}
      <header className={`sticky top-0 z-40 border-b backdrop-blur ${C.header}`}>
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
          <Link href="/" className={`text-xl font-extrabold tracking-tight ${C.heading}`}>
            <span className="text-brand-600">2E</span>systen
          </Link>

          <nav className="hidden items-center gap-8 md:flex">
            {NAV_LINKS.map(({ href, label }) => (
              <a key={href} href={href} className={`text-sm font-semibold transition ${C.navLink}`}>
                {label}
              </a>
            ))}
          </nav>

          <div className="flex items-center gap-3">
            <button
              onClick={toggleTheme}
              aria-label={isDark ? 'Tema claro' : 'Tema escuro'}
              className={`rounded-lg border p-2 transition ${C.outline}`}
            >
              {isDark ? <Sun size={16} /> : <Moon size={16} />}
            </button>
            <Link
              href="/login"
              className="hidden rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 sm:inline-flex"
            >
              Entrar
            </Link>
            <button
              onClick={() => setMenuOpen(v => !v)}
              aria-label="Menu"
              className={`rounded-lg border p-2 md:hidden ${C.outline}`}
            >
              {menuOpen ? <X size={16} /> : <Menu size={16} />}
            </button>
          </div>
        </div>

        {menuOpen && (
          <nav className={`border-t px-6 py-4 md:hidden ${C.border}`}>
            <div className="flex flex-col gap-4">
              {NAV_LINKS.map(({ href, label }) => (
                <a
                  key={href}
                  href={href}
                  onClick={() => setMenuOpen(false)}
                  className={`text-sm font-semibold ${C.navLink}`}
                >
                  {label}
                </a>
              ))}
              <Link href="/login" className="text-sm font-semibold text-brand-600">
                Entrar
              </Link>
            </div>
          </nav>
        )}
      </header>

      {/* ── Hero ─────────────────────────────────────────────────────────── */}
      <section className="mx-auto max-w-6xl px-6 py-20 sm:py-28">
        <p className="text-sm font-bold uppercase tracking-widest text-brand-600">
          Plataforma de gestão para lojistas
        </p>
        <h1 className={`mt-4 max-w-3xl text-4xl font-extrabold leading-tight sm:text-5xl ${C.heading}`}>
          O ERP completo, <span className="text-brand-600">com a cara da sua loja</span>
        </h1>
        <p className={`mt-6 max-w-2xl text-lg ${C.body}`}>
          Somos a 2Esysten: construímos um sistema de gestão completo — PDV, estoque, fiscal,
          crediário e app próprio — que cada lojista pode chamar de seu, sem abrir mão da
          praticidade de uma plataforma pronta.
        </p>
        <div className="mt-10 flex flex-wrap gap-4">
          <a
            href="#contato"
            className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-6 py-3 font-semibold text-white transition hover:bg-brand-700"
          >
            Quero minha loja no sistema <ArrowRight size={18} />
          </a>
          <a
            href="#clientes"
            className={`inline-flex items-center gap-2 rounded-lg border px-6 py-3 font-semibold transition ${C.outline}`}
          >
            Ver quem já usa
          </a>
        </div>
      </section>

      {/* ── Quem somos ───────────────────────────────────────────────────── */}
      <section id="quem-somos" className={`scroll-mt-20 border-y ${C.border} ${C.section}`}>
        <div className="mx-auto grid max-w-6xl gap-12 px-6 py-20 md:grid-cols-2 md:items-center">
          <div>
            <h2 className="text-sm font-bold uppercase tracking-widest text-brand-600">
              Quem somos
            </h2>
            <p className={`mt-4 text-2xl font-bold leading-snug sm:text-3xl ${C.heading}`}>
              Nascemos dentro de uma loja de verdade, resolvendo problema de verdade.
            </p>
          </div>
          <div className={`space-y-4 ${C.body}`}>
            <p>
              A 2Esysten começou como o sistema interno de uma loja de card games, construído pra
              resolver o dia a dia de quem vende, emite nota, controla estoque e fecha caixa —
              tudo ao mesmo tempo.
            </p>
            <p>
              Percebemos que o problema não era só nosso: todo lojista de médio porte lida com o
              mesmo emaranhado de planilha, sistema fiscal separado e falta de identidade digital
              própria. Transformamos aquele sistema interno em uma plataforma multi-loja, onde
              cada cliente ganha o próprio espaço — isolado, com a cara dele, sem perder a
              praticidade de uma solução pronta.
            </p>
          </div>
        </div>
      </section>

      {/* ── O que fazemos ────────────────────────────────────────────────── */}
      <section id="o-que-fazemos" className="mx-auto max-w-6xl scroll-mt-20 px-6 py-20">
        <h2 className="text-sm font-bold uppercase tracking-widest text-brand-600">
          O que fazemos
        </h2>
        <p className={`mt-3 max-w-2xl text-2xl font-bold sm:text-3xl ${C.heading}`}>
          Um sistema só, cobrindo o que hoje toma cinco ferramentas diferentes.
        </p>

        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {PILARES.map(({ icon: Icon, title, desc }) => (
            <div key={title} className={`rounded-xl border p-6 transition ${C.card}`}>
              <div className={`mb-4 inline-flex rounded-lg p-3 ${C.chip}`}>
                <Icon size={22} />
              </div>
              <h3 className={`font-bold ${C.heading}`}>{title}</h3>
              <p className={`mt-2 text-sm ${C.body}`}>{desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ── Clientes ─────────────────────────────────────────────────────── */}
      <section id="clientes" className={`scroll-mt-20 border-y ${C.border} ${C.section}`}>
        <div className="mx-auto max-w-6xl px-6 py-20">
          <h2 className="text-sm font-bold uppercase tracking-widest text-brand-600">
            Quem já usa
          </h2>
          <p className={`mt-3 max-w-2xl text-2xl font-bold sm:text-3xl ${C.heading}`}>
            Primeira loja rodando, muitas outras vindo por aí.
          </p>

          <div className="mt-10 grid gap-6 sm:grid-cols-2">
            <div className={`rounded-xl border p-8 ${C.card}`}>
              <div className="flex items-center gap-3">
                <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-600 font-bold text-white">
                  SN
                </div>
                <div>
                  <p className={`font-bold ${C.heading}`}>Santuário Nerd</p>
                  <p className={`text-sm ${C.muted}`}>Loja de card games</p>
                </div>
              </div>
              <p className={`mt-4 text-sm ${C.body}`}>
                Primeira loja a rodar a plataforma — PDV, comandas, crediário e emissão fiscal no
                dia a dia, com a marca própria do Santuário do início ao fim.
              </p>
            </div>

            <div className={`flex flex-col items-start justify-center rounded-xl border border-dashed p-8 ${C.border}`}>
              <CheckCircle2 className="mb-3 text-brand-600" size={24} />
              <p className={`font-semibold ${C.heading}`}>A próxima pode ser a sua loja</p>
              <p className={`mt-2 text-sm ${C.muted}`}>
                Estamos abrindo espaço para novos lojistas conforme a plataforma cresce.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── CTA final ────────────────────────────────────────────────────── */}
      <section id="contato" className="scroll-mt-20 bg-[#0C3D5A] py-20">
        <div className="mx-auto max-w-3xl px-6 text-center">
          <h2 className="text-3xl font-extrabold text-white sm:text-4xl">
            Quer sua loja rodando com a sua cara?
          </h2>
          <p className="mt-4 text-white/75">
            Fala com a gente e a gente monta seu espaço na plataforma — subdomínio, identidade
            visual e módulo fiscal configurados pra você vender no mesmo dia.
          </p>
          <div className="mt-8 flex flex-wrap justify-center gap-4">
            <Link
              href="/cadastro"
              className="inline-flex items-center gap-2 rounded-lg bg-brand-500 px-6 py-3 font-semibold text-[#0C3D5A] transition hover:bg-brand-400"
            >
              Falar com a gente <ArrowRight size={18} />
            </Link>
          </div>
        </div>
      </section>

      {/* ── Footer ───────────────────────────────────────────────────────── */}
      <footer className={`border-t px-6 py-8 ${C.border}`}>
        <div className={`mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 text-sm sm:flex-row ${C.muted}`}>
          <p>© {new Date().getFullYear()} 2Esysten — Sistema de gestão para lojas e varejo.</p>
          <div className="flex gap-6">
            <Link href="/termos" className="transition hover:text-brand-600">Termos de uso</Link>
            <Link href="/privacidade" className="transition hover:text-brand-600">Privacidade</Link>
          </div>
        </div>
      </footer>
    </main>
  )
}
