'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import {
  Store, ShieldCheck, Layers, Smartphone, Receipt, TrendingUp,
  ArrowRight, CheckCircle2, Sun, Moon, Menu, X, Loader2,
} from 'lucide-react'
import { publicDirectoryApi, PublicTenantDto, leadsApi, getErrorMessage } from '@/lib/api'

const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || ''

const NAV_LINKS = [
  { href: '#quem-somos',    label: 'Quem somos' },
  { href: '#o-que-fazemos', label: 'O que fazemos' },
  { href: '#planos',        label: 'Planos' },
  { href: '#clientes',      label: 'Clientes' },
  { href: '#contato',       label: 'Contato' },
]

// Planos derivados dos módulos que o sistema realmente tem hoje (ver
// Tenant.EnabledModules: fiscal, estoque, pontos, contador, ia, eventos) e do
// limite de usuários por plano (Tenant.MaxUsers, enforçado em
// UserService.AdminCreateUserAsync). Nada aqui promete recurso que não existe.
//
// Os valores são uma PROPOSTA de partida, não pricing fechado — o Tenant já
// trata PlanName como texto livre justamente porque o pricing não estava
// definido. Ajustar aqui e no billing do tenant ao mesmo tempo.
const PLANOS = [
  {
    nome: 'Essencial',
    preco: 120,
    publico: 'Pra loja que quer sair da planilha e do caderno.',
    destaque: false,
    usuarios: '2 usuários no painel',
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
    inclui: [
      'Tudo do Completo',
      'Assistente de IA no painel (pergunte em português sobre sua loja)',
      'Domínio próprio (suamarca.com.br)',
      'Reservas e agendamento',
      'Prioridade no suporte',
    ],
  },
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
  const [tenants,  setTenants]  = useState<PublicTenantDto[]>([])

  // Formulário de lead (CTA "Falar com a gente") — antes disso o link ia
  // direto pra /cadastro, que é a tela de cliente final de uma loja, não de
  // captação de quem quer contratar a plataforma.
  const [leadNome,       setLeadNome]       = useState('')
  const [leadTelefone,   setLeadTelefone]   = useState('')
  const [leadEmail,      setLeadEmail]      = useState('')
  const [leadMensagem,   setLeadMensagem]   = useState('')
  const [leadSubmitting, setLeadSubmitting] = useState(false)
  const [leadSubmitted,  setLeadSubmitted]  = useState(false)
  const [leadError,      setLeadError]      = useState<string | null>(null)

  async function handleLeadSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLeadSubmitting(true)
    setLeadError(null)
    try {
      await leadsApi.create({
        nome: leadNome.trim(),
        telefone: leadTelefone.trim(),
        email: leadEmail.trim() || undefined,
        mensagem: leadMensagem.trim() || undefined,
      })
      setLeadSubmitted(true)
    } catch (err) {
      setLeadError(getErrorMessage(err, 'Não deu pra enviar seu contato agora. Tenta de novo em instantes.'))
    } finally {
      setLeadSubmitting(false)
    }
  }

  useEffect(() => {
    setIsDark(localStorage.getItem('institucional-theme') === 'dark')
  }, [])

  // Diretório de lojas ativas — falha silenciosa de propósito: essa seção é
  // um bônus da página institucional, uma API fora do ar aqui nunca pode
  // quebrar o resto da página (sem loading spinner, sem tela de erro — só
  // fica com a lista vazia e o card de CTA sozinho, ver abaixo).
  useEffect(() => {
    publicDirectoryApi.listTenants()
      .then(r => setTenants(r.data))
      .catch(() => {})
  }, [])

  // Esconde os widgets da loja (footer global da vitrine, botão de instalar
  // PWA) — esta página tem footer próprio e não é o app instalável. Feito por
  // classe no body porque o middleware reescreve "/" pra cá mantendo a URL,
  // então usePathname() nos componentes globais não enxerga "/institucional".
  useEffect(() => {
    document.body.classList.add('institucional-page')
    return () => document.body.classList.remove('institucional-page')
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
    body:    'text-white/70',
    muted:   'text-white/50',
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
            <span className="text-brand-600">3E</span>systen
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
          Somos a 3Esysten: construímos um sistema de gestão completo — PDV, estoque, fiscal,
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
              A 3Esysten começou como o sistema interno de uma loja de card games, construído pra
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

      {/* ── Planos ───────────────────────────────────────────────────────── */}
      <section id="planos" className={`scroll-mt-20 border-y ${C.border} ${C.section}`}>
        <div className="mx-auto max-w-6xl px-6 py-20">
          <h2 className="text-sm font-bold uppercase tracking-widest text-brand-600">
            Planos
          </h2>
          <p className={`mt-3 max-w-2xl text-2xl font-bold sm:text-3xl ${C.heading}`}>
            Escolha pelo tamanho da sua operação, não por uma tabela de recursos confusa.
          </p>
          <p className={`mt-4 max-w-2xl ${C.body}`}>
            Todos os planos incluem seu espaço isolado no banco, sua marca no sistema
            e o primeiro mês de acesso sem mensalidade.
          </p>

          <div className="mt-12 grid items-start gap-6 lg:grid-cols-3">
            {PLANOS.map(({ nome, preco, publico, destaque, usuarios, inclui }) => (
              <div
                key={nome}
                className={`relative flex h-full flex-col rounded-2xl border p-8 transition ${
                  destaque
                    ? 'border-brand-500 shadow-xl shadow-brand-500/10 lg:-mt-4 lg:pb-12'
                    : C.card
                } ${destaque && isDark ? 'bg-[#1A1A1F]' : ''} ${destaque && !isDark ? 'bg-white' : ''}`}
              >
                {destaque && (
                  <span className="absolute -top-3 left-8 rounded-full bg-brand-600 px-3 py-1 text-xs font-bold uppercase tracking-wide text-white">
                    Mais escolhido
                  </span>
                )}

                <h3 className={`text-xl font-extrabold ${C.heading}`}>{nome}</h3>
                <p className={`mt-2 min-h-[2.5rem] text-sm ${C.muted}`}>{publico}</p>

                <div className="mt-6 flex items-baseline gap-1">
                  <span className={`text-sm font-semibold ${C.muted}`}>R$</span>
                  <span className={`text-4xl font-extrabold tabular-nums ${C.heading}`}>{preco}</span>
                  <span className={`text-sm font-medium ${C.muted}`}>/mês</span>
                </div>

                {/* Implantação calculada do próprio preço (2 mensalidades), nunca
                    escrita à mão: valor digitado separado desalinha do plano na
                    primeira vez que o preço muda — e preço de tabela errado numa
                    página de vendas é problema comercial, não bug de UI. */}
                <p className={`mt-1 text-xs ${C.muted}`}>
                  + R$ {preco * 2} de implantação, uma única vez
                </p>

                <p className={`mt-3 text-xs font-semibold ${C.body}`}>{usuarios}</p>

                <ul className="mt-6 flex-1 space-y-3">
                  {inclui.map(item => (
                    <li key={item} className={`flex gap-2.5 text-sm ${C.body}`}>
                      <CheckCircle2 size={17} className="mt-0.5 shrink-0 text-brand-600" />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>

                <a
                  href="#contato"
                  className={`mt-8 inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold transition ${
                    destaque
                      ? 'bg-brand-600 text-white hover:bg-brand-700'
                      : `border ${C.outline}`
                  }`}
                >
                  Falar sobre este plano
                  <ArrowRight size={15} />
                </a>
              </div>
            ))}
          </div>

          {/* Letra pequena — condições comerciais. Fica junto dos planos de
              propósito: taxa de implantação escondida no rodapé ou só no
              contrato é o tipo de surpresa que queima a venda na primeira
              conversa. */}
          <div className={`mt-10 rounded-xl border border-dashed p-6 ${C.border}`}>
            <p className={`text-xs leading-relaxed ${C.muted}`}>
              <strong className={C.body}>Como funciona a cobrança:</strong>{' '}
              na contratação é cobrada uma <strong className={C.body}>taxa de implantação
              equivalente a 2 mensalidades do plano escolhido</strong>, que cobre a
              configuração da loja, a personalização com a sua marca, o cadastro
              inicial e o acompanhamento na virada.{' '}
              <strong className={C.body}>O primeiro mês de acesso não tem mensalidade</strong> —
              a cobrança mensal começa a partir do segundo mês de uso. Sem
              fidelidade e sem multa para cancelar: você avisa e o acesso segue
              até o fim do período já pago. Valores em reais, por loja.
            </p>
          </div>
        </div>
      </section>

      {/* ── Clientes ─────────────────────────────────────────────────────── */}
      {/* Fundo neutro (não C.section) pra alternar com a faixa tingida de Planos
          logo acima — duas seções tingidas coladas viravam um bloco só. */}
      <section id="clientes" className="scroll-mt-20">
        <div className="mx-auto max-w-6xl px-6 py-20">
          <h2 className="text-sm font-bold uppercase tracking-widest text-brand-600">
            Quem já usa
          </h2>
          <p className={`mt-3 max-w-2xl text-2xl font-bold sm:text-3xl ${C.heading}`}>
            {tenants.length > 0
              ? 'Lojas de verdade, rodando na plataforma agora.'
              : 'Primeira loja rodando, muitas outras vindo por aí.'}
          </p>

          <div className="mt-10 grid gap-6 sm:grid-cols-2">
            {tenants.map(t => (
              <a
                key={t.slug}
                href={ROOT_DOMAIN ? `https://${t.slug}.${ROOT_DOMAIN}` : '#'}
                target="_blank"
                rel="noopener noreferrer"
                className={`rounded-xl border p-8 transition ${C.card}`}
              >
                <div className="flex items-center gap-3">
                  {t.logoUrl ? (
                    <img src={t.logoUrl} alt={t.displayName} className="h-12 w-12 rounded-full object-cover" />
                  ) : (
                    <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-600 font-bold text-white">
                      {t.displayName.slice(0, 2).toUpperCase()}
                    </div>
                  )}
                  <div>
                    <p className={`font-bold ${C.heading}`}>{t.displayName}</p>
                    <p className={`text-sm ${C.muted}`}>{t.slug}.{ROOT_DOMAIN}</p>
                  </div>
                </div>
              </a>
            ))}

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
        <div className="mx-auto max-w-lg px-6 text-center">
          <h2 className="text-3xl font-extrabold text-white sm:text-4xl">
            Quer sua loja rodando com a sua cara?
          </h2>
          <p className="mt-4 text-white/75">
            Deixa seu contato que a gente fala com você e monta seu espaço na plataforma —
            subdomínio, identidade visual e módulo fiscal configurados pra vender no mesmo dia.
          </p>

          {leadSubmitted ? (
            <div className="mt-8 flex flex-col items-center gap-3 rounded-xl border border-brand-400/30 bg-white/5 p-8">
              <CheckCircle2 className="text-brand-400" size={32} />
              <p className="font-semibold text-white">Recebemos seu contato!</p>
              <p className="text-sm text-white/70">Vamos falar com você em breve.</p>
            </div>
          ) : (
            <form onSubmit={handleLeadSubmit} className="mt-8 space-y-3 text-left">
              <input
                type="text" required placeholder="Seu nome" value={leadNome}
                onChange={e => setLeadNome(e.target.value)} maxLength={150}
                className="w-full rounded-lg border border-white/20 bg-white/10 px-4 py-3 text-white placeholder-white/50 outline-none focus:border-brand-400"
              />
              <input
                type="text" required placeholder="WhatsApp" value={leadTelefone}
                onChange={e => setLeadTelefone(e.target.value)} maxLength={30}
                className="w-full rounded-lg border border-white/20 bg-white/10 px-4 py-3 text-white placeholder-white/50 outline-none focus:border-brand-400"
              />
              <input
                type="email" placeholder="E-mail (opcional)" value={leadEmail}
                onChange={e => setLeadEmail(e.target.value)} maxLength={255}
                className="w-full rounded-lg border border-white/20 bg-white/10 px-4 py-3 text-white placeholder-white/50 outline-none focus:border-brand-400"
              />
              <textarea
                placeholder="Conta um pouco da sua loja (opcional)" value={leadMensagem}
                onChange={e => setLeadMensagem(e.target.value)} maxLength={1000} rows={3}
                className="w-full resize-none rounded-lg border border-white/20 bg-white/10 px-4 py-3 text-white placeholder-white/50 outline-none focus:border-brand-400"
              />
              {leadError && <p className="text-sm text-red-300">{leadError}</p>}
              <button
                type="submit" disabled={leadSubmitting}
                className="flex w-full items-center justify-center gap-2 rounded-lg bg-brand-500 px-6 py-3 font-semibold text-[#0C3D5A] transition hover:bg-brand-400 disabled:opacity-60"
              >
                {leadSubmitting ? <><Loader2 className="animate-spin" size={18} /> Enviando...</> : <>Falar com a gente <ArrowRight size={18} /></>}
              </button>
            </form>
          )}
        </div>
      </section>

      {/* ── Footer ───────────────────────────────────────────────────────── */}
      <footer className={`border-t px-6 py-8 ${C.border}`}>
        <div className={`mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 text-sm sm:flex-row ${C.muted}`}>
          <p>© {new Date().getFullYear()} 3Esysten — Sistema de gestão para lojas e varejo.</p>
          <div className="flex gap-6">
            <Link href="/termos" className="transition hover:text-brand-600">Termos de uso</Link>
            <Link href="/privacidade" className="transition hover:text-brand-600">Privacidade</Link>
          </div>
        </div>
      </footer>
    </main>
  )
}
