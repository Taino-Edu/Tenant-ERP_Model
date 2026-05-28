'use client'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { isLoggedIn, isAdmin } from '@/lib/auth'
import { championshipApi, productApi, announcementApi, Championship, Product, AnnouncementDto } from '@/lib/api'
import Link from 'next/link'
import ThemeToggle from '@/components/ThemeToggle'
import {
  Trophy, ShoppingBag, QrCode, Star,
  Calendar, Users, ChevronRight, Zap, Shield,
  X, MessageCircle, CheckCircle, Package,
  ScanLine, CreditCard, Award
} from 'lucide-react'

const MAIKON_WHATSAPP = '5517997633103' // WhatsApp do Maikon

export default function LandingPage() {
  const router = useRouter()
  const [championships, setChampionships] = useState<Championship[]>([])
  const [products,      setProducts]      = useState<Product[]>([])
  const [announcements, setAnnouncements] = useState<AnnouncementDto[]>([])
  const [loading,       setLoading]       = useState(true)
  const [registerModal, setRegisterModal] = useState<Championship | null>(null)
  const [productModal,  setProductModal]  = useState<Product | null>(null)
  const [mobileMenu,    setMobileMenu]    = useState(false)

  useEffect(() => {
    if (isLoggedIn()) {
      router.replace(isAdmin() ? '/admin/dashboard' : '/cliente')
      return
    }
    Promise.allSettled([
      championshipApi.list().then(r =>
        setChampionships(r.data.filter(c => c.status === 'Planejado' || c.status === 'Inscricoes').slice(0, 3))
      ),
      productApi.list().then(r => {
        const active   = r.data.filter(p => p.isActive && p.stockQuantity > 0)
        const featured = active.filter(p => p.isFeatured)
        setProducts(featured.length > 0 ? featured.slice(0, 6) : active.slice(0, 6))
      }),
      announcementApi.visible().then(r => setAnnouncements(r.data)),
    ]).finally(() => setLoading(false))
  }, [router])

  const banners   = announcements.filter(a => a.type === 'Banner')
  const avisos    = announcements.filter(a => a.type === 'Aviso')
  const destaques = announcements.filter(a => a.type === 'Destaque')

  return (
    <div className="min-h-screen" style={{ backgroundColor: 'var(--bg-primary)', color: 'var(--text-primary)' }}>

      {/* ── Navbar ─────────────────────────────────────────────────── */}
      <nav className="fixed top-0 left-0 right-0 z-50 backdrop-blur-md border-b" style={{ backgroundColor: 'color-mix(in srgb, var(--bg-primary) 90%, transparent)', borderColor: 'var(--border-color)' }}>
        <div className="max-w-6xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <img src="/logo-santuario.png" alt="Santuário Nerd" className="h-9 w-9 object-contain" />
            <span className="text-white text-xl" style={{ fontFamily: 'var(--font-modak)' }}>Santuário Nerd</span>
          </div>

          <div className="hidden md:flex items-center gap-6 text-sm text-gray-400">
            <a href="#calendario"    className="hover:text-white transition">Calendário</a>
            <a href="#campeonatos"   className="hover:text-white transition">Campeonatos</a>
            <a href="#produtos"      className="hover:text-white transition">Produtos</a>
            <a href="#como-funciona" className="hover:text-white transition">Como Funciona</a>
            <a href="#pontos"        className="hover:text-white transition">Pontos</a>
          </div>

          <div className="hidden md:flex items-center gap-3">
            <ThemeToggle compact />
            <Link href="/entrar"
              className="text-sm text-gray-400 hover:text-white transition px-4 py-2">
              Minha Conta
            </Link>
            <a href="#campeonatos"
              className="text-sm bg-brand-500 hover:bg-brand-400 text-white font-semibold px-4 py-2 rounded-xl transition shadow-lg shadow-brand-500/20">
              Ver Eventos
            </a>
          </div>

          <button onClick={() => setMobileMenu(!mobileMenu)}
            className="md:hidden text-gray-400 hover:text-white p-1">
            <div className="space-y-1.5">
              <span className={`block w-5 h-0.5 bg-current transition-transform ${mobileMenu ? 'rotate-45 translate-y-2' : ''}`} />
              <span className={`block w-5 h-0.5 bg-current transition-opacity ${mobileMenu ? 'opacity-0' : ''}`} />
              <span className={`block w-5 h-0.5 bg-current transition-transform ${mobileMenu ? '-rotate-45 -translate-y-2' : ''}`} />
            </div>
          </button>
        </div>

        {mobileMenu && (
          <div className="md:hidden border-t px-6 py-4 space-y-3" style={{ backgroundColor: 'var(--bg-card)', borderColor: 'var(--border-color)' }}>
            {['#calendario','#campeonatos','#produtos','#como-funciona','#pontos'].map((href, i) => (
              <a key={href} href={href} onClick={() => setMobileMenu(false)}
                className="block text-sm py-1.5 capitalize" style={{ color: 'var(--text-muted)' }}>
                {['Calendário','Campeonatos','Produtos','Como Funciona','Pontos'][i]}
              </a>
            ))}
            <div className="flex gap-2 pt-2 border-t" style={{ borderColor: 'var(--border-color)' }}>
              <ThemeToggle compact />
              <Link href="/entrar" className="flex-1 text-center py-2 text-sm border rounded-xl" style={{ color: 'var(--text-muted)', borderColor: 'var(--border-color)' }}>Minha Conta</Link>
              <a href="#campeonatos" className="flex-1 text-center py-2 text-sm bg-brand-500 text-white font-semibold rounded-xl">Eventos</a>
            </div>
          </div>
        )}
      </nav>

      {/* ── Hero ───────────────────────────────────────────────────── */}
      <section className="pt-32 pb-24 px-6">
        <div className="max-w-4xl mx-auto text-center">
          <div className="inline-flex items-center gap-2 bg-brand-500/10 border border-brand-500/20 rounded-full px-4 py-1.5 text-brand-400 text-sm font-medium mb-6">
            <Zap className="w-3.5 h-3.5" />
            Loja de Card Games e Campeonatos
          </div>
          <h1 className="text-5xl md:text-7xl font-black text-white mb-6 leading-none tracking-tight">
            Sua loja de
            <span className="text-brand-400"> card games</span>
            <br />favorita
          </h1>
          <p className="text-gray-400 text-lg md:text-xl max-w-2xl mx-auto mb-10 leading-relaxed">
            Produtos, campeonatos e a melhor experiência para jogadores de TCG.
            Sente na mesa, escaneie o QR Code e faça seu pedido direto pelo celular.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <a href="#campeonatos"
              className="inline-flex items-center justify-center gap-2 bg-brand-500 hover:bg-brand-400 text-white font-bold px-8 py-3.5 rounded-xl transition-all active:scale-95 shadow-lg shadow-brand-500/20">
              <Trophy className="w-5 h-5" /> Ver Campeonatos
            </a>
            <a href="#produtos"
              className="inline-flex items-center justify-center gap-2 bg-surface-800 hover:bg-surface-700 border border-surface-500 text-white font-semibold px-8 py-3.5 rounded-xl transition-all active:scale-95">
              <ShoppingBag className="w-5 h-5" /> Ver Produtos
            </a>
          </div>
        </div>
      </section>

      {/* ── Banners (imagem full-width com overlay de texto) ──────── */}
      {banners.length > 0 && (
        <section className="px-6 pb-6 max-w-5xl mx-auto space-y-4">
          {banners.map(a => (
            <a key={a.id}
              href={a.linkUrl ?? '#'}
              target={a.linkUrl ? '_blank' : undefined}
              rel="noreferrer"
              className={`block relative rounded-2xl overflow-hidden border border-surface-600 group ${a.linkUrl ? 'cursor-pointer' : 'cursor-default'}`}>
              {a.imageUrl ? (
                <>
                  <img src={a.imageUrl} alt={a.title}
                    className="w-full object-cover max-h-[280px] group-hover:scale-[1.02] transition-transform duration-500" />
                  {(a.title || a.body) && (
                    <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-transparent to-transparent flex items-end p-6">
                      <div>
                        <p className="text-white font-bold text-base leading-snug drop-shadow">{a.title}</p>
                        {a.body && <p className="text-gray-300 text-sm mt-1 drop-shadow">{a.body}</p>}
                      </div>
                    </div>
                  )}
                </>
              ) : (
                <div className="bg-surface-800 px-6 py-5">
                  <p className="font-bold text-white">{a.title}</p>
                  {a.body && <p className="text-gray-400 text-sm mt-1">{a.body}</p>}
                </div>
              )}
            </a>
          ))}
        </section>
      )}

      {/* ── Destaques (card com imagem lateral) ───────────────────── */}
      {destaques.length > 0 && (
        <section className="px-6 pb-6 max-w-5xl mx-auto space-y-3">
          {destaques.map(a => (
            <a key={a.id}
              href={a.linkUrl ?? '#'}
              target={a.linkUrl ? '_blank' : undefined}
              rel="noreferrer"
              className={`flex items-center gap-0 rounded-2xl overflow-hidden border border-amber-500/25 bg-gradient-to-r from-amber-500/10 to-transparent group ${a.linkUrl ? 'cursor-pointer hover:border-amber-500/50' : 'cursor-default'} transition`}>
              {a.imageUrl && (
                <img src={a.imageUrl} alt=""
                  className="w-28 h-20 object-cover shrink-0 group-hover:brightness-110 transition" />
              )}
              <div className="px-5 py-4 flex items-center gap-3 flex-1 min-w-0">
                <Star className="w-4 h-4 text-amber-400 shrink-0" />
                <div className="min-w-0">
                  <p className="font-bold text-amber-300 text-sm truncate">{a.title}</p>
                  {a.body && <p className="text-gray-400 text-xs mt-0.5 line-clamp-1">{a.body}</p>}
                </div>
              </div>
            </a>
          ))}
        </section>
      )}

      {/* ── Avisos (barra de notificação slim) ────────────────────── */}
      {avisos.length > 0 && (
        <section className="px-6 pb-6 max-w-5xl mx-auto space-y-2">
          {avisos.map(a => (
            <div key={a.id} className="flex items-center gap-3 bg-brand-500/10 border border-brand-500/20 rounded-xl px-4 py-3">
              <Zap className="w-4 h-4 text-brand-400 shrink-0" />
              <div className="flex-1 min-w-0">
                <span className="font-semibold text-brand-300 text-sm">{a.title}</span>
                {a.body && <span className="text-gray-400 text-xs ml-2">{a.body}</span>}
              </div>
            </div>
          ))}
        </section>
      )}

      {/* ── Como funciona ─────────────────────────────────────────── */}
      <section id="como-funciona" className="py-20 px-6 border-y" style={{ borderColor: 'var(--border-color)', backgroundColor: 'color-mix(in srgb, var(--bg-card) 30%, transparent)' }}>
        <div className="max-w-5xl mx-auto">
          <div className="text-center mb-14">
            <p className="text-xs uppercase text-brand-400 font-bold tracking-widest mb-2">Simples assim</p>
            <h2 className="text-3xl font-bold text-white">Como funciona</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-8 relative">
            {/* linha conectora desktop */}
            <div className="hidden md:block absolute top-7 left-[12.5%] right-[12.5%] h-px bg-surface-500" />
            {[
              { icon: ScanLine, step: '01', title: 'Escaneie o QR Code', desc: 'Cada mesa tem um QR único. Escaneie e sua comanda abre automaticamente.' },
              { icon: ShoppingBag, step: '02', title: 'Faça seu pedido', desc: 'Adicione bebidas, salgadinhos e produtos TCG pelo celular.' },
              { icon: Award, step: '03', title: 'Acumule pontos', desc: 'A cada visita você acumula pontos que podem ser trocados por produtos.' },
              { icon: Trophy, step: '04', title: 'Participe de torneios', desc: 'Inscreva-se nos campeonatos e compita com outros jogadores.' },
            ].map(({ icon: Icon, step, title, desc }) => (
              <div key={step} className="text-center relative">
                <div className="w-14 h-14 bg-surface-800 border border-surface-500 rounded-2xl flex items-center justify-center mx-auto mb-4 relative z-10">
                  <Icon className="w-6 h-6 text-brand-400" />
                </div>
                <p className="text-xs font-bold text-brand-500 mb-1">{step}</p>
                <h3 className="font-bold text-white mb-2 text-sm">{title}</h3>
                <p className="text-gray-500 text-sm leading-relaxed">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── Campeonatos ───────────────────────────────────────────── */}
      {/* ── Calendário de Eventos ─────────────────────────────────── */}
      <section id="calendario" className="py-20 px-6 border-t" style={{ borderColor: 'var(--border-color)', backgroundColor: 'color-mix(in srgb, var(--bg-card) 30%, transparent)' }}>
        <div className="max-w-5xl mx-auto">
          <div className="text-center mb-10">
            <p className="text-xs uppercase text-brand-400 font-bold tracking-widest mb-2">Agenda</p>
            <h2 className="text-3xl font-bold text-white">Calendário de Eventos</h2>
            <p className="text-gray-400 mt-2">Veja os próximos campeonatos e marque na agenda</p>
          </div>
          <EventCalendar championships={championships} />
        </div>
      </section>

      {/* ── Campeonatos ────────────────────────────────────────────── */}
      <section id="campeonatos" className="py-20 px-6">
        <div className="max-w-5xl mx-auto">
          <div className="flex items-end justify-between mb-10">
            <div>
              <p className="text-xs uppercase text-brand-400 font-bold tracking-widest mb-2">Torneios</p>
              <h2 className="text-3xl font-bold text-white">Próximos Eventos</h2>
            </div>
          </div>

          {loading ? (
            <div className="flex justify-center py-16">
              <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : championships.length === 0 ? (
            <div className="text-center py-16 border border-surface-500 rounded-2xl bg-surface-800/30">
              <Trophy className="w-10 h-10 text-surface-500 mx-auto mb-3" />
              <p className="text-gray-500 font-medium">Nenhum evento agendado no momento.</p>
              <p className="text-gray-600 text-sm mt-1">Fique atento às novidades.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
              {championships.map(c => (
                <ChampionshipCard key={c.id} championship={c} onRegister={() => setRegisterModal(c)} />
              ))}
            </div>
          )}
        </div>
      </section>

      {/* ── Produtos ──────────────────────────────────────────────── */}
      <section id="produtos" className="py-20 px-6 border-t" style={{ borderColor: 'var(--border-color)', backgroundColor: 'color-mix(in srgb, var(--bg-card) 20%, transparent)' }}>
        <div className="max-w-5xl mx-auto">
          <div className="flex items-end justify-between mb-10">
            <div>
              <p className="text-xs uppercase text-brand-400 font-bold tracking-widest mb-2">Vitrine</p>
              <h2 className="text-3xl font-bold text-white">Produtos em Destaque</h2>
            </div>
          </div>

          {loading ? (
            <div className="flex justify-center py-16">
              <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : products.length === 0 ? (
            <div className="text-center py-16 border border-surface-500 rounded-2xl bg-surface-800/30">
              <Package className="w-10 h-10 text-surface-500 mx-auto mb-3" />
              <p className="text-gray-500 font-medium">Produtos em breve.</p>
            </div>
          ) : (
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              {products.map(p => <ProductCard key={p.id} product={p} onClick={() => setProductModal(p)} />)}
            </div>
          )}
        </div>
      </section>

      {/* ── Pontos ────────────────────────────────────────────────── */}
      <section id="pontos" className="py-20 px-6 border-t" style={{ borderColor: 'var(--border-color)' }}>
        <div className="max-w-5xl mx-auto">
          <div className="text-center mb-12">
            <p className="text-xs uppercase text-brand-400 font-bold tracking-widest mb-2">Programa de Fidelidade</p>
            <h2 className="text-3xl md:text-4xl font-black text-white mb-4">
              Ganhe pontos e troque por prêmios
            </h2>
            <p className="text-gray-400 text-lg max-w-xl mx-auto">
              A cada compra você acumula pontos que podem ser usados como desconto na próxima visita.
              Sem aplicativo, sem complicação.
            </p>
          </div>

          {/* Timeline de como funciona */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-12">
            {[
              { step: '01', icon: QrCode,      color: 'text-brand-400',   bg: 'bg-brand-500/10 border-brand-500/20',   title: 'Cadastre-se',       desc: 'Escaneie o QR Code na loja e informe seu CPF e WhatsApp. Gratuito.' },
              { step: '02', icon: ShoppingBag, color: 'text-amber-400',   bg: 'bg-amber-500/10 border-amber-500/20',   title: 'Compre na loja',     desc: 'A cada compra ou visita, o atendente adiciona pontos à sua conta.' },
              { step: '03', icon: Star,         color: 'text-accent-green',bg: 'bg-accent-green/10 border-accent-green/20', title: 'Acumule pontos',  desc: 'Veja seu saldo de pontos e o histórico a qualquer momento.' },
              { step: '04', icon: Award,        color: 'text-rose-400',   bg: 'bg-rose-500/10 border-rose-500/20',     title: 'Resgate descontos', desc: 'Troque seus pontos por desconto na comanda ou em produtos da loja.' },
            ].map(({ step, icon: Icon, color, bg, title, desc }) => (
              <div key={step} className="bg-surface-800 border border-surface-500 rounded-2xl p-6 relative hover:border-brand-500/30 transition group">
                <span className="absolute top-4 right-4 text-3xl font-black text-surface-500 group-hover:text-brand-500/20 transition">{step}</span>
                <div className={`w-10 h-10 ${bg} border rounded-xl flex items-center justify-center mb-4`}>
                  <Icon className={`w-5 h-5 ${color}`} />
                </div>
                <h3 className="font-bold text-white mb-1 text-sm">{title}</h3>
                <p className="text-gray-500 text-xs leading-relaxed">{desc}</p>
              </div>
            ))}
          </div>

          {/* Destaque de benefícios */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-10">
            {[
              { label: 'Sem senha necessária', desc: 'Login só com CPF' },
              { label: 'Pontos não expiram', desc: 'Válidos enquanto ativo' },
              { label: '1 ponto = R$ 0,01', desc: '100 pontos = R$ 1,00 off' },
            ].map(({ label, desc }) => (
              <div key={label} className="flex items-center gap-3 bg-surface-800 border border-surface-500 rounded-xl px-4 py-3">
                <CheckCircle className="w-5 h-5 text-accent-green shrink-0" />
                <div>
                  <p className="text-sm font-semibold text-white">{label}</p>
                  <p className="text-xs text-gray-500">{desc}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="flex justify-center">
            <div className="inline-flex items-center gap-2 bg-surface-800 border border-surface-500 rounded-xl px-5 py-3 text-sm text-gray-400">
              <Shield className="w-4 h-4 text-brand-400 shrink-0" />
              Dados protegidos — apenas CPF e WhatsApp para identificação
            </div>
          </div>
        </div>
      </section>

      {/* ── CTA final ─────────────────────────────────────────────── */}
      <section className="py-20 px-6 border-t" style={{ borderColor: 'var(--border-color)' }}>
        <div className="max-w-3xl mx-auto border border-brand-500/20 rounded-2xl p-10 md:p-14 text-center" style={{ backgroundColor: 'var(--bg-card)' }}>
          <h2 className="text-3xl md:text-4xl font-black text-white mb-3">Pronto para jogar?</h2>
          <p className="text-gray-400 mb-8 max-w-md mx-auto">
            Escaneie o QR Code na mesa e comece a aproveitar a experiência Santuário Nerd.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <a href="#campeonatos"
              className="inline-flex items-center justify-center gap-2 bg-brand-500 hover:bg-brand-400 text-white font-bold px-8 py-3.5 rounded-xl transition shadow-lg shadow-brand-500/20 active:scale-95">
              <Trophy className="w-5 h-5" /> Ver Eventos
            </a>
            <Link href="/login"
              className="inline-flex items-center justify-center gap-2 bg-surface-700 hover:bg-surface-600 border border-surface-500 text-white font-semibold px-8 py-3.5 rounded-xl transition active:scale-95">
              <CreditCard className="w-5 h-5" /> Área Admin
            </Link>
          </div>
        </div>
      </section>

      {/* O rodapé com links legais (LGPD) é renderizado pelo Footer global em app/layout.tsx */}

      {productModal && (
        <ProductModal product={productModal} onClose={() => setProductModal(null)} />
      )}
      {registerModal && (
        <RegisterModal championship={registerModal} onClose={() => setRegisterModal(null)} />
      )}
    </div>
  )
}

// ── Calendário de eventos ─────────────────────────────────────────────────

function EventCalendar({ championships }: { championships: Championship[] }) {
  const today    = new Date()
  const year     = today.getFullYear()
  const month    = today.getMonth()

  const monthName = today.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })
  const firstDay  = new Date(year, month, 1).getDay() // 0=Dom
  const daysInMonth = new Date(year, month + 1, 0).getDate()

  // Dias com evento
  const eventDays = new Set(
    championships
      .map(c => new Date(c.startDate))
      .filter(d => d.getFullYear() === year && d.getMonth() === month)
      .map(d => d.getDate())
  )

  // Evento do dia clicado
  const getEvent = (day: number) =>
    championships.find(c => {
      const d = new Date(c.startDate)
      return d.getFullYear() === year && d.getMonth() === month && d.getDate() === day
    })

  const cells = []
  for (let i = 0; i < firstDay; i++) cells.push(null)
  for (let d = 1; d <= daysInMonth; d++) cells.push(d)

  const weekDays = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

  if (championships.length === 0) {
    return (
      <div className="text-center py-12 rounded-2xl border" style={{ borderColor: 'var(--border-color)', backgroundColor: 'var(--bg-card)' }}>
        <Calendar className="w-10 h-10 mx-auto mb-3 text-gray-500" />
        <p className="text-gray-500">Nenhum evento agendado para este mês.</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col md:flex-row gap-6 items-start">
      {/* Calendário */}
      <div className="rounded-2xl border p-5 w-full md:w-80 shrink-0" style={{ borderColor: 'var(--border-color)', backgroundColor: 'var(--bg-card)' }}>
        <p className="text-center font-bold capitalize mb-4" style={{ color: 'var(--text-primary)' }}>{monthName}</p>
        <div className="grid grid-cols-7 gap-1 mb-2">
          {weekDays.map(d => (
            <div key={d} className="text-center text-[10px] font-semibold text-gray-500 uppercase">{d}</div>
          ))}
        </div>
        <div className="grid grid-cols-7 gap-1">
          {cells.map((day, i) => {
            if (!day) return <div key={i} />
            const hasEvent = eventDays.has(day)
            const isToday  = day === today.getDate()
            return (
              <div
                key={i}
                className={`aspect-square flex items-center justify-center rounded-lg text-xs font-medium relative transition-all
                  ${hasEvent ? 'bg-brand-500 text-white shadow-lg shadow-brand-500/30 cursor-pointer hover:bg-brand-400' : ''}
                  ${isToday && !hasEvent ? 'border-2 border-brand-500/50' : ''}
                  ${!hasEvent ? 'text-gray-400' : ''}
                `}
                style={!hasEvent ? { color: 'var(--text-muted)' } : {}}
                title={hasEvent ? getEvent(day)?.name : undefined}
              >
                {day}
                {hasEvent && <span className="absolute -bottom-0.5 left-1/2 -translate-x-1/2 w-1 h-1 bg-white rounded-full" />}
              </div>
            )
          })}
        </div>
        <div className="flex items-center gap-2 mt-4 text-xs" style={{ color: 'var(--text-muted)' }}>
          <span className="w-3 h-3 rounded bg-brand-500 shrink-0" />
          <span>Evento marcado</span>
        </div>
      </div>

      {/* Lista de próximos eventos */}
      <div className="flex-1 space-y-3">
        {championships.map(c => {
          const d = new Date(c.startDate)
          const isPast = d < today
          return (
            <div key={c.id} className="flex items-start gap-4 rounded-xl p-4 border transition-all hover:border-brand-500/30"
              style={{ borderColor: 'var(--border-color)', backgroundColor: 'var(--bg-card)' }}>
              {/* Data */}
              <div className={`shrink-0 w-14 text-center rounded-xl py-2 ${isPast ? 'bg-gray-500/10' : 'bg-brand-500/10'}`}>
                <p className={`text-xl font-black leading-none ${isPast ? 'text-gray-500' : 'text-brand-400'}`}>
                  {d.getDate().toString().padStart(2, '0')}
                </p>
                <p className={`text-[10px] uppercase font-bold ${isPast ? 'text-gray-600' : 'text-brand-500/70'}`}>
                  {d.toLocaleDateString('pt-BR', { month: 'short' }).replace('.', '')}
                </p>
              </div>
              {/* Info */}
              <div className="flex-1 min-w-0">
                <p className="font-semibold text-sm truncate" style={{ color: 'var(--text-primary)' }}>{c.name}</p>
                <p className="text-xs mt-0.5" style={{ color: 'var(--text-muted)' }}>
                  {c.game} · {d.toLocaleDateString('pt-BR', { weekday: 'long' })}
                  {c.maxParticipants && ` · Até ${c.maxParticipants} jogadores`}
                </p>
                {c.entryFeeInReais > 0 && (
                  <p className="text-xs text-emerald-500 font-semibold mt-1">Taxa: R$ {c.entryFeeInReais.toFixed(2).replace('.', ',')}</p>
                )}
              </div>
              {/* Status badge */}
              <div className="shrink-0">
                {c.status === 'Inscricoes' && !isPast
                  ? <span className="text-[10px] bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 px-2 py-1 rounded-full font-bold">Inscrições abertas</span>
                  : <span className="text-[10px] bg-gray-500/10 text-gray-500 border border-gray-500/20 px-2 py-1 rounded-full font-bold">{c.status === 'Planejado' ? 'Em breve' : c.status}</span>
                }
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

// ── Sub-componentes ───────────────────────────────────────────────────────

function ChampionshipCard({ championship: c, onRegister }: { championship: Championship; onRegister: () => void }) {
  const gameColor: Record<string, string> = {
    'Pokemon':   'bg-yellow-500/10 text-yellow-400 border-yellow-500/20',
    'Magic':     'bg-blue-500/10   text-blue-400   border-blue-500/20',
    'Yu-Gi-Oh':  'bg-purple-500/10 text-purple-400 border-purple-500/20',
    'One Piece': 'bg-red-500/10    text-red-400    border-red-500/20',
  }
  const statusLabel: Record<string, string> = {
    Planejado:   'Em breve',
    Inscricoes:  'Inscrições abertas',
    EmAndamento: 'Em andamento',
  }

  return (
    <div className="bg-surface-800 border border-surface-500 rounded-2xl p-5 hover:border-brand-500/40 transition group flex flex-col">
      <div className="flex items-start justify-between mb-4">
        <span className={`text-xs font-bold px-2.5 py-1 rounded-full border ${gameColor[c.game] ?? 'bg-surface-700 text-gray-400 border-surface-500'}`}>
          {c.game}
        </span>
        <span className="text-xs text-accent-green bg-accent-green/10 border border-accent-green/20 px-2.5 py-1 rounded-full font-medium">
          {statusLabel[c.status] ?? c.status}
        </span>
      </div>

      <h3 className="font-bold text-white mb-3 leading-snug">{c.name}</h3>

      <div className="space-y-1.5 text-sm text-gray-500 mb-4">
        <div className="flex items-center gap-1.5">
          <Calendar className="w-3.5 h-3.5 shrink-0" />
          {new Date(c.startDate).toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' })}
        </div>
        {c.maxParticipants && (
          <div className="flex items-center gap-1.5">
            <Users className="w-3.5 h-3.5 shrink-0" />
            Até {c.maxParticipants} participantes
          </div>
        )}
        <div className="flex items-center gap-1.5 text-brand-400 font-semibold">
          Inscrição: R$ {(c.entryFeeInCents / 100).toFixed(2).replace('.', ',')}
        </div>
      </div>

      <div className="mt-auto pt-4 border-t border-surface-500">
        <button
          onClick={onRegister}
          className="w-full bg-brand-500 hover:bg-brand-400 text-white font-bold text-sm py-2.5 rounded-xl transition shadow-lg shadow-brand-500/20 active:scale-95"
        >
          Quero me inscrever
        </button>
        <p className="text-xs text-gray-600 text-center mt-2">Pague na chegada · Vagas limitadas</p>
      </div>
    </div>
  )
}

function ProductCard({ product: p, onClick }: { product: Product; onClick: () => void }) {
  return (
    <div
      onClick={onClick}
      className="bg-surface-800 border border-surface-500 rounded-2xl p-4 hover:border-brand-500/50 hover:shadow-lg cursor-pointer transition-all group"
    >
      {p.imageUrl && (
        <div className="w-full aspect-square rounded-xl overflow-hidden mb-3 bg-surface-700">
          <img src={p.imageUrl} alt={p.name} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" />
        </div>
      )}
      <p className="text-xs text-gray-500 mb-1 uppercase tracking-wide">{p.category}</p>
      <p className="text-sm font-semibold leading-snug line-clamp-2 mb-3" style={{ color: 'var(--text-primary)' }}>{p.name}</p>
      <div className="flex items-center justify-between">
        <span className="text-brand-400 font-bold">R$ {p.priceInReais.toFixed(2).replace('.', ',')}</span>
        <span className="text-xs text-gray-500 flex items-center gap-1">
          <Package className="w-3 h-3" /> {p.stockQuantity} un.
        </span>
      </div>
    </div>
  )
}

function ProductModal({ product: p, onClose }: { product: Product; onClose: () => void }) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onClose}>
      <div
        className="relative w-full max-w-md rounded-2xl shadow-2xl overflow-hidden"
        style={{ background: 'var(--card-bg, #ffffff)', border: '1px solid var(--border-color)' }}
        onClick={e => e.stopPropagation()}
      >
        <button onClick={onClose} className="absolute top-3 right-3 z-10 p-1.5 rounded-full bg-surface-700 hover:bg-surface-600 transition">
          <X className="w-4 h-4 text-gray-400" />
        </button>

        {p.imageUrl ? (
          <div className="w-full aspect-video bg-surface-700">
            <img src={p.imageUrl} alt={p.name} className="w-full h-full object-cover" />
          </div>
        ) : (
          <div className="w-full aspect-video bg-surface-700 flex items-center justify-center">
            <Package className="w-12 h-12 text-surface-500" />
          </div>
        )}

        <div className="p-5" style={{ background: 'var(--card-bg, #ffffff)' }}>
          <p className="text-xs uppercase tracking-wide mb-1" style={{ color: 'var(--text-secondary, #6b7280)' }}>{p.category}</p>
          <h3 className="text-lg font-bold mb-2" style={{ color: 'var(--text-primary, #111827)' }}>{p.name}</h3>
          {p.description && (
            <p className="text-sm mb-4 leading-relaxed" style={{ color: 'var(--text-secondary, #6b7280)' }}>{p.description}</p>
          )}
          <div className="flex items-center justify-between pt-3" style={{ borderTop: '1px solid var(--border-color)' }}>
            <span className="text-2xl font-bold text-brand-400">
              R$ {p.priceInReais.toFixed(2).replace('.', ',')}
            </span>
            <span className="text-sm flex items-center gap-1" style={{ color: 'var(--text-secondary, #6b7280)' }}>
              <Package className="w-4 h-4" /> {p.stockQuantity} em estoque
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}

function RegisterModal({ championship, onClose }: { championship: Championship; onClose: () => void }) {
  const [name,  setName]  = useState('')
  const [phone, setPhone] = useState('')
  const [done,  setDone]  = useState(false)

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim() || !phone.trim()) return
    const msg = encodeURIComponent(
      `Olá! Quero me inscrever no *${championship.name}*.\n` +
      `Nome: ${name.trim()}\nWhatsApp: ${phone.trim()}\n` +
      `Confirmo o pagamento de R$ ${(championship.entryFeeInCents / 100).toFixed(2).replace('.', ',')} na chegada.`
    )
    window.open(`https://wa.me/${MAIKON_WHATSAPP}?text=${msg}`, '_blank')
    setDone(true)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md p-6 shadow-2xl">
        <div className="flex items-start justify-between mb-5">
          <div>
            <h3 className="font-bold text-white text-lg">Inscrição</h3>
            <p className="text-gray-500 text-sm mt-0.5">{championship.name}</p>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-white transition p-1">
            <X className="w-5 h-5" />
          </button>
        </div>

        {done ? (
          <div className="text-center py-6">
            <div className="w-14 h-14 bg-accent-green/10 border border-accent-green/20 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <CheckCircle className="w-7 h-7 text-accent-green" />
            </div>
            <p className="font-bold text-white mb-1">Solicitação enviada!</p>
            <p className="text-gray-400 text-sm leading-relaxed">
              O Maikon vai confirmar sua vaga pelo WhatsApp. Pague na chegada.
            </p>
            <button onClick={onClose}
              className="mt-5 w-full btn-secondary justify-center py-2.5">
              Fechar
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="bg-brand-500/10 border border-brand-500/20 rounded-xl px-4 py-3 text-sm text-brand-300">
              Taxa de inscrição:{' '}
              <strong>R$ {(championship.entryFeeInCents / 100).toFixed(2).replace('.', ',')}</strong>
              {' '}— pague na chegada
            </div>
            <div>
              <label className="label">Seu nome</label>
              <input className="input" placeholder="Nome completo"
                value={name} onChange={e => setName(e.target.value)} required />
            </div>
            <div>
              <label className="label">Seu WhatsApp</label>
              <input className="input" placeholder="(11) 99999-9999"
                value={phone} onChange={e => setPhone(e.target.value)} required />
            </div>
            <button type="submit"
              className="w-full btn-primary justify-center py-3">
              <MessageCircle className="w-4 h-4" />
              Confirmar pelo WhatsApp
            </button>
            <p className="text-xs text-gray-600 text-center">
              Você será redirecionado para o WhatsApp para confirmar a vaga.
            </p>
          </form>
        )}
      </div>
    </div>
  )
}
