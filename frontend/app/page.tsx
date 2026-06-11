'use client'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { isLoggedIn, isAdmin } from '@/lib/auth'
import { championshipApi, productApi, announcementApi, Championship, Product, AnnouncementDto } from '@/lib/api'
import Link from 'next/link'
import {
  Trophy, ShoppingBag, Star, Calendar, Users,
  X, MessageCircle, CheckCircle, Package,
  CreditCard, Award, QrCode, Shield, ChevronRight
} from 'lucide-react'

const MAIKON_WHATSAPP = '5517997633103'

const C = {
  bg:      '#EBF7FD',
  card:    '#FFFFFF',
  cardAlt: '#F0F9FF',
  border:  'rgba(12,61,90,0.10)',
  blue:    '#3EC2F2',
  yellow:  '#FFE45E',
  text:    '#4D8FAC',
  navy:    '#0C3D5A',
}

export default function LandingPage() {
  const router = useRouter()
  const [championships, setChampionships] = useState<Championship[]>([])
  const [products,      setProducts]      = useState<Product[]>([])
  const [announcements, setAnnouncements] = useState<AnnouncementDto[]>([])
  const [loading,       setLoading]       = useState(true)
  const [registerModal, setRegisterModal] = useState<Championship | null>(null)
  const [productModal,  setProductModal]  = useState<Product | null>(null)
  const [annModal,      setAnnModal]      = useState<AnnouncementDto | null>(null)
  const [mobileMenu,    setMobileMenu]    = useState(false)

  useEffect(() => {
    if (isLoggedIn()) {
      router.replace(isAdmin() ? '/admin/dashboard' : '/cliente')
      return
    }
    Promise.allSettled([
      championshipApi.list().then(r =>
        setChampionships(r.data.filter(c => c.status === 'Planejado' || c.status === 'Inscricoes').slice(0, 4))
      ),
      productApi.list().then(r => {
        const visible  = r.data.filter(p => p.isActive && p.stockQuantity > 0 && p.showOnSite)
        const featured = visible.filter(p => p.isFeatured)
        setProducts(featured.length > 0 ? featured.slice(0, 8) : visible.slice(0, 8))
      }),
      announcementApi.visible().then(r => setAnnouncements(r.data)),
    ]).finally(() => setLoading(false))
  }, [router])

  return (
    <div className="min-h-screen" style={{ backgroundColor: C.bg, color: C.navy }}>

      {/* ── NAVBAR ─────────────────────────────────────────────────────── */}
      <nav className="fixed inset-x-0 top-0 z-50 h-16 flex items-center border-b"
        style={{ backgroundColor: C.navy, backdropFilter: 'blur(16px)', borderColor: 'rgba(255,255,255,0.10)' }}>
        <div className="w-full max-w-6xl mx-auto px-5 flex items-center justify-between">

          {/* Logo */}
          <div className="flex items-center gap-2.5">
            <img src="/logo-maikon.png" alt="Santuário Nerd" className="h-8 w-auto object-contain" />
            <span className="font-black text-lg text-white leading-none">Santuário Nerd</span>
          </div>

          {/* Links desktop */}
          <div className="hidden md:flex items-center gap-7 text-sm font-medium" style={{ color: 'rgba(255,255,255,0.70)' }}>
            <a href="#eventos"  className="hover:text-white transition-colors">Torneios</a>
            <a href="#produtos" className="hover:text-white transition-colors">Produtos</a>
            <a href="#pontos"   className="hover:text-white transition-colors">Pontos</a>
          </div>

          {/* Ações desktop */}
          <div className="hidden md:flex items-center gap-2">
            <Link href="/entrar"
              className="text-sm px-4 py-2 rounded-xl border transition-colors hover:border-white/30 hover:text-white"
              style={{ color: 'rgba(255,255,255,0.70)', borderColor: 'rgba(255,255,255,0.25)' }}>
              Minha Conta
            </Link>
            <a href="#eventos"
              className="text-sm font-black px-5 py-2 rounded-xl transition-all active:scale-95 shadow-lg"
              style={{ backgroundColor: C.blue, color: '#fff', boxShadow: `0 4px 20px rgba(62,194,242,0.3)` }}>
              Ver Eventos
            </a>
          </div>

          {/* Hamburger mobile */}
          <button onClick={() => setMobileMenu(v => !v)} className="md:hidden p-2" style={{ color: 'rgba(255,255,255,0.70)' }}>
            <div className="space-y-1.5">
              <span className={`block w-5 h-0.5 bg-current transition-transform ${mobileMenu ? 'rotate-45 translate-y-2' : ''}`} />
              <span className={`block w-5 h-0.5 bg-current transition-opacity ${mobileMenu ? 'opacity-0' : ''}`} />
              <span className={`block w-5 h-0.5 bg-current transition-transform ${mobileMenu ? '-rotate-45 -translate-y-2' : ''}`} />
            </div>
          </button>
        </div>
      </nav>

      {/* Menu mobile */}
      {mobileMenu && (
        <div className="fixed inset-x-0 top-16 z-40 border-b md:hidden px-5 py-4 space-y-1"
          style={{ backgroundColor: C.navy, borderColor: 'rgba(255,255,255,0.10)' }}>
          {[['#eventos','Torneios'],['#produtos','Produtos'],['#pontos','Pontos']].map(([href, label]) => (
            <a key={href} href={href} onClick={() => setMobileMenu(false)}
              className="block py-2.5 text-sm hover:text-white transition-colors" style={{ color: 'rgba(255,255,255,0.70)' }}>
              {label}
            </a>
          ))}
          <div className="flex gap-2 pt-3 border-t" style={{ borderColor: 'rgba(255,255,255,0.10)' }}>
            <Link href="/entrar" onClick={() => setMobileMenu(false)}
              className="flex-1 text-center py-2.5 text-sm rounded-xl border font-medium hover:text-white transition-colors"
              style={{ color: 'rgba(255,255,255,0.70)', borderColor: 'rgba(255,255,255,0.25)' }}>
              Minha Conta
            </Link>
            <a href="#eventos" onClick={() => setMobileMenu(false)}
              className="flex-1 text-center py-2.5 text-sm rounded-xl font-black"
              style={{ backgroundColor: C.blue, color: '#fff' }}>
              Eventos
            </a>
          </div>
        </div>
      )}

      {/* ── HERO ───────────────────────────────────────────────────────── */}
      <section className="relative pt-16 overflow-hidden" style={{ backgroundColor: C.navy }}>

        <div className="relative max-w-6xl mx-auto px-5 py-20 md:py-24">
          <div className="flex flex-col md:flex-row items-center gap-8 md:gap-12">

            {/* Texto */}
            <div className="flex-1 text-center md:text-left">
              <div className="inline-flex items-center gap-2 text-xs font-black uppercase tracking-widest px-3 py-1.5 rounded-full mb-6 border"
                style={{ color: C.blue, borderColor: `${C.blue}40`, backgroundColor: `${C.blue}15` }}>
                Card Games &amp; Campeonatos · José Bonifácio — SP
              </div>

              <h1 className="text-4xl sm:text-5xl md:text-[3.75rem] font-black leading-[1.05] mb-5 tracking-tight text-white">
                <span style={{ color: C.blue }}>Santuário Nerd</span><br />
                Card Games &amp;<br />
                Campeonatos
              </h1>

              <p className="text-base md:text-lg max-w-md mb-8 leading-relaxed" style={{ color: 'rgba(255,255,255,0.65)' }}>
                Produtos, torneios e a melhor experiência TCG da região.
                Acumule pontos, compre na mesa e participe de campeonatos.
              </p>

              <div className="flex flex-col sm:flex-row gap-3 justify-center md:justify-start">
                <a href="#eventos"
                  className="inline-flex items-center justify-center gap-2 font-black px-7 py-3.5 rounded-xl transition-all active:scale-95"
                  style={{ backgroundColor: C.yellow, color: C.navy, boxShadow: `0 8px 28px rgba(255,228,94,0.22)` }}>
                  <Trophy className="w-5 h-5" /> Ver Torneios
                </a>
                <a href="#produtos"
                  className="inline-flex items-center justify-center gap-2 font-semibold px-7 py-3.5 rounded-xl border transition-all hover:border-white/30 hover:text-white"
                  style={{ borderColor: 'rgba(255,255,255,0.25)', color: 'rgba(255,255,255,0.70)' }}>
                  <ShoppingBag className="w-5 h-5" /> Ver Produtos
                </a>
              </div>
            </div>

            {/* Mascote */}
            <div className="relative shrink-0">
              <img
                src="/logo-maikon.png"
                alt="Mascote Maikon"
                className="w-48 sm:w-56 md:w-64 h-auto object-contain drop-shadow-[0_16px_40px_rgba(0,0,0,0.4)]"
              />
            </div>
          </div>
        </div>
      </section>

      {/* ── ANÚNCIOS (gerenciados pelo admin) ──────────────────────────── */}
      {announcements.length > 0 && (
        <section className="px-5 pb-8 max-w-6xl mx-auto">
          <div className={
            announcements.length === 1
              ? 'grid grid-cols-1'
              : announcements.length === 2
                ? 'grid grid-cols-1 sm:grid-cols-2 gap-4'
                : 'grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4'
          }>
            {announcements.map(a => (
              <button
                key={a.id}
                onClick={() => setAnnModal(a)}
                className="text-left rounded-2xl overflow-hidden border group cursor-pointer transition-all"
                style={{ backgroundColor: C.card, borderColor: C.border }}
              >
                {a.imageUrl ? (
                  <div className={`w-full overflow-hidden ${announcements.length === 1 ? 'h-56 md:h-72' : 'h-44'}`}>
                    <img
                      src={a.imageUrl} alt={a.title}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                    />
                  </div>
                ) : (
                  <div className={`w-full flex items-center justify-center ${announcements.length === 1 ? 'h-36' : 'h-44'}`}
                    style={{ background: `linear-gradient(135deg, ${C.blue}18, ${C.card})` }}>
                    <span className="text-5xl font-black opacity-20" style={{ color: C.blue }}>!</span>
                  </div>
                )}
                <div className="p-4">
                  <p className="font-black text-sm leading-snug" style={{ color: C.navy }}>{a.title}</p>
                  {a.body && <p className="text-xs mt-1.5 line-clamp-2 leading-relaxed" style={{ color: C.text }}>{a.body}</p>}
                  <p className="text-xs mt-2 font-bold" style={{ color: C.blue }}>Ver mais →</p>
                </div>
              </button>
            ))}
          </div>
        </section>
      )}

      {/* ── TORNEIOS ────────────────────────────────────────────────────── */}
      <section id="eventos" className="py-16 px-5 border-t" style={{ borderColor: C.border }}>
        <div className="max-w-6xl mx-auto">
          <div className="flex items-baseline justify-between mb-8">
            <div>
              <p className="text-xs font-black uppercase tracking-widest mb-1.5" style={{ color: C.blue }}>Agenda</p>
              <h2 className="text-2xl md:text-3xl font-black" style={{ color: C.navy }}>Próximos Torneios</h2>
            </div>
          </div>

          {loading ? (
            <div className="flex justify-center py-14">
              <div className="w-7 h-7 border-2 rounded-full animate-spin"
                style={{ borderColor: C.blue, borderTopColor: 'transparent' }} />
            </div>
          ) : championships.length === 0 ? (
            <div className="text-center py-14 rounded-2xl border" style={{ borderColor: C.border, backgroundColor: C.card }}>
              <Trophy className="w-8 h-8 mx-auto mb-3 opacity-20" style={{ color: C.navy }} />
              <p className="font-medium opacity-50" style={{ color: C.navy }}>Nenhum evento agendado no momento.</p>
              <p className="text-sm mt-1 opacity-40" style={{ color: C.text }}>Fique de olho nas novidades.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {championships.map(c => (
                <ChampionshipCard key={c.id} championship={c} onRegister={() => setRegisterModal(c)} />
              ))}
            </div>
          )}
        </div>
      </section>

      {/* ── PRODUTOS ────────────────────────────────────────────────────── */}
      <section id="produtos" className="py-16 px-5 border-t" style={{ borderColor: C.border }}>
        <div className="max-w-6xl mx-auto">
          <div className="flex items-baseline justify-between mb-8">
            <div>
              <p className="text-xs font-black uppercase tracking-widest mb-1.5" style={{ color: C.blue }}>Vitrine</p>
              <h2 className="text-2xl md:text-3xl font-black" style={{ color: C.navy }}>Em Destaque</h2>
            </div>
          </div>

          {loading ? (
            <div className="flex justify-center py-14">
              <div className="w-7 h-7 border-2 rounded-full animate-spin"
                style={{ borderColor: C.blue, borderTopColor: 'transparent' }} />
            </div>
          ) : products.length === 0 ? (
            <div className="text-center py-14 rounded-2xl border" style={{ borderColor: C.border, backgroundColor: C.card }}>
              <Package className="w-8 h-8 mx-auto mb-3 opacity-20" style={{ color: C.navy }} />
              <p className="font-medium opacity-50" style={{ color: C.navy }}>Produtos em breve.</p>
            </div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3 md:gap-4">
              {products.map(p => (
                <ProductCard key={p.id} product={p} onClick={() => setProductModal(p)} />
              ))}
            </div>
          )}
        </div>
      </section>

      {/* ── PONTOS / FIDELIDADE ─────────────────────────────────────────── */}
      <section id="pontos" className="py-16 px-5 border-t" style={{ borderColor: C.border }}>
        <div className="max-w-6xl mx-auto">
          <div className="rounded-2xl overflow-hidden border"
            style={{ backgroundColor: C.card, borderColor: `${C.blue}25` }}>
            <div className="p-8 md:p-12 flex flex-col md:flex-row gap-8 md:gap-16 items-center">

              {/* Texto */}
              <div className="flex-1 text-center md:text-left">
                <p className="text-xs font-black uppercase tracking-widest mb-3"
                  style={{ color: C.yellow }}>Programa de Fidelidade</p>
                <h2 className="text-2xl md:text-3xl font-black mb-4 leading-tight" style={{ color: C.navy }}>
                  Ganhe pontos a cada visita
                </h2>
                <p className="text-sm leading-relaxed max-w-sm mb-6" style={{ color: C.text }}>
                  Acumule pontos nas suas compras e troque por descontos.
                  Só com CPF e WhatsApp — nada de senha ou aplicativo.
                </p>
                <a href="#" className="inline-flex items-center gap-2 text-sm font-bold hover:gap-3 transition-all"
                  style={{ color: C.blue }}>
                  Saiba mais <ChevronRight className="w-4 h-4" />
                </a>
              </div>

              {/* Cards de benefício */}
              <div className="grid grid-cols-3 gap-3 w-full md:w-auto md:min-w-[340px]">
                {[
                  { icon: QrCode, label: 'Sem senha', sub: 'Login só com CPF' },
                  { icon: Star,   label: 'Pontos válidos', sub: 'Enquanto você for ativo' },
                  { icon: Award,  label: '100pts = R$1', sub: 'Desconto na comanda' },
                ].map(({ icon: Icon, label, sub }) => (
                  <div key={label} className="flex flex-col items-center text-center p-4 rounded-xl border"
                    style={{ backgroundColor: `${C.blue}08`, borderColor: `${C.blue}20` }}>
                    <div className="w-10 h-10 rounded-xl flex items-center justify-center mb-3"
                      style={{ backgroundColor: `${C.blue}18` }}>
                      <Icon className="w-5 h-5" style={{ color: C.blue }} />
                    </div>
                    <p className="text-xs font-black leading-snug" style={{ color: C.navy }}>{label}</p>
                    <p className="text-[10px] mt-0.5 leading-snug" style={{ color: C.text }}>{sub}</p>
                  </div>
                ))}
              </div>
            </div>

            {/* Strip de garantia */}
            <div className="border-t px-8 py-4 flex flex-wrap justify-center md:justify-start gap-x-6 gap-y-2"
              style={{ borderColor: C.border }}>
              {[
                { icon: Shield, text: 'Dados protegidos — LGPD' },
                { icon: CheckCircle, text: 'Sem aplicativo necessário' },
                { icon: MessageCircle, text: `Suporte via WhatsApp` },
              ].map(({ icon: Icon, text }) => (
                <span key={text} className="flex items-center gap-2 text-xs font-medium" style={{ color: C.text }}>
                  <Icon className="w-3.5 h-3.5 shrink-0" style={{ color: C.blue }} /> {text}
                </span>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ── FOOTER ──────────────────────────────────────────────────────── */}
      <footer className="border-t py-10 px-5" style={{ borderColor: C.border }}>
        <div className="max-w-6xl mx-auto flex flex-col md:flex-row items-center justify-between gap-5">
          <div className="flex items-center gap-2.5">
            <img src="/logo-maikon.png" alt="Santuário Nerd" className="h-7 w-auto object-contain" />
            <span className="font-black" style={{ color: C.navy }}>Santuário Nerd</span>
          </div>

          <p className="text-xs text-center" style={{ color: `${C.text}` }}>
            José Bonifácio — SP &nbsp;·&nbsp;
            <a href={`https://wa.me/${MAIKON_WHATSAPP}`} target="_blank" rel="noreferrer"
              className="hover:text-white transition-colors">
              WhatsApp
            </a>
            &nbsp;·&nbsp; © {new Date().getFullYear()} Santuário Nerd
          </p>

          <Link href="/login"
            className="flex items-center gap-1.5 text-xs transition-colors hover:text-white"
            style={{ color: C.text }}>
            <CreditCard className="w-3.5 h-3.5" /> Área do Admin
          </Link>
        </div>
      </footer>

      {/* ── MODAIS ──────────────────────────────────────────────────────── */}
      {annModal      && <AnnouncementModal ann={annModal}               onClose={() => setAnnModal(null)} />}
      {productModal  && <ProductModal      product={productModal}       onClose={() => setProductModal(null)} />}
      {registerModal && <RegisterModal     championship={registerModal} onClose={() => setRegisterModal(null)} />}
    </div>
  )
}

// ── Championship Card ─────────────────────────────────────────────────────────

function ChampionshipCard({ championship: c, onRegister }: { championship: Championship; onRegister: () => void }) {
  const gameColors: Record<string, { bg: string; text: string }> = {
    'Pokemon':   { bg: 'rgba(234,179,8,0.15)',  text: '#FBBF24' },
    'Magic':     { bg: 'rgba(99,102,241,0.15)', text: '#818CF8' },
    'Yu-Gi-Oh':  { bg: 'rgba(168,85,247,0.15)', text: '#C084FC' },
    'One Piece': { bg: 'rgba(239,68,68,0.15)',  text: '#F87171' },
  }
  const gc = gameColors[c.game] ?? { bg: 'rgba(62,194,242,0.12)', text: '#3EC2F2' }

  return (
    <div className="rounded-2xl overflow-hidden flex flex-col group transition-all hover:translate-y-[-2px]"
      style={{ backgroundColor: C.card, border: `1px solid ${C.border}` }}>

      {/* Imagem */}
      {c.imageUrl ? (
        <div className="w-full h-36 overflow-hidden">
          <img src={c.imageUrl} alt={c.name}
            className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" />
        </div>
      ) : (
        <div className="w-full h-36 flex items-center justify-center"
          style={{ background: `linear-gradient(135deg, ${gc.bg}, ${C.cardAlt})` }}>
          <Trophy className="w-9 h-9 opacity-30" style={{ color: gc.text }} />
        </div>
      )}

      <div className="p-4 flex flex-col flex-1 gap-3">
        {/* Game badge + status */}
        <div className="flex items-center justify-between gap-2">
          <span className="text-[10px] font-black uppercase px-2.5 py-1 rounded-full"
            style={{ backgroundColor: gc.bg, color: gc.text }}>
            {c.game}
          </span>
          {c.status === 'Inscricoes' && (
            <span className="text-[10px] font-bold px-2 py-1 rounded-full"
              style={{ backgroundColor: 'rgba(34,197,94,0.12)', color: '#4ADE80' }}>
              Inscrições abertas
            </span>
          )}
        </div>

        {/* Nome */}
        <h3 className="font-black text-sm leading-snug line-clamp-2" style={{ color: C.navy }}>{c.name}</h3>

        {/* Data + vagas */}
        <div className="space-y-1.5 text-xs" style={{ color: C.text }}>
          <div className="flex items-center gap-1.5">
            <Calendar className="w-3.5 h-3.5 shrink-0" style={{ color: C.blue }} />
            {new Date(c.startDate).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })}
          </div>
          {c.maxParticipants && (
            <div className="flex items-center gap-1.5">
              <Users className="w-3.5 h-3.5 shrink-0" style={{ color: C.blue }} />
              Até {c.maxParticipants} jogadores
            </div>
          )}
        </div>

        {/* Taxa + botão */}
        <div className="mt-auto pt-3 border-t flex items-center justify-between gap-2"
          style={{ borderColor: C.border }}>
          <span className="text-sm font-black" style={{ color: C.yellow }}>
            R$ {(c.entryFeeInCents / 100).toFixed(2).replace('.', ',')}
          </span>
          <button
            onClick={onRegister}
            className="text-xs font-black px-3.5 py-2 rounded-xl flex items-center gap-1 transition-all active:scale-95"
            style={{ backgroundColor: C.blue, color: '#fff' }}>
            Inscrever <ChevronRight className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Product Card ──────────────────────────────────────────────────────────────

function ProductCard({ product: p, onClick }: { product: Product; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className="text-left rounded-2xl overflow-hidden group transition-all active:scale-95 hover:translate-y-[-2px]"
      style={{ backgroundColor: C.card, border: `1px solid ${C.border}` }}
    >
      <div className="relative w-full aspect-[3/4] overflow-hidden"
        style={{ backgroundColor: C.cardAlt }}>
        {p.imageUrl
          ? <img src={p.imageUrl} alt={p.name}
              className="w-full h-full object-contain group-hover:scale-105 transition-transform duration-300 p-2" />
          : <div className="w-full h-full flex items-center justify-center">
              <Package className="w-10 h-10 opacity-20 text-white" />
            </div>
        }
        {p.isOnPromo && (
          <span className="absolute top-2 left-2 text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded-md"
            style={{ backgroundColor: '#FF3B3B', color: '#fff' }}>
            Promoção
          </span>
        )}
      </div>
      <div className="p-3">
        <p className="text-[10px] uppercase tracking-wide mb-1 font-medium" style={{ color: C.text }}>
          {p.category}
        </p>
        <p className="text-xs font-bold leading-snug line-clamp-2 mb-2" style={{ color: C.navy }}>{p.name}</p>
        <div className="flex items-center justify-between">
          {p.isOnPromo && p.discountPriceInReais != null ? (
            <div className="flex flex-col">
              <span className="text-[10px] line-through" style={{ color: C.text }}>
                R$ {p.priceInReais.toFixed(2).replace('.', ',')}
              </span>
              <span className="text-sm font-black" style={{ color: '#FF3B3B' }}>
                R$ {p.discountPriceInReais.toFixed(2).replace('.', ',')}
              </span>
            </div>
          ) : (
            <span className="text-sm font-black" style={{ color: C.yellow }}>
              R$ {p.priceInReais.toFixed(2).replace('.', ',')}
            </span>
          )}
          <span className="text-[10px] font-medium" style={{ color: C.text }}>
            {p.stockQuantity} un.
          </span>
        </div>
      </div>
    </button>
  )
}

// ── Modais ────────────────────────────────────────────────────────────────────

function AnnouncementModal({ ann, onClose }: { ann: AnnouncementDto; onClose: () => void }) {
  useEffect(() => {
    const h = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm"
      onClick={onClose}>
      <div className="relative w-full max-w-lg rounded-2xl overflow-hidden shadow-2xl border"
        style={{ backgroundColor: C.card, borderColor: C.border }}
        onClick={e => e.stopPropagation()}>
        <button onClick={onClose}
          className="absolute top-3 right-3 z-10 p-1.5 rounded-full bg-black/50 hover:bg-black/80 transition">
          <X className="w-4 h-4 text-white" />
        </button>
        {ann.imageUrl && (
          <img src={ann.imageUrl} alt={ann.title} className="w-full max-h-64 object-cover" />
        )}
        <div className="p-6">
          <h3 className="text-xl font-black text-white leading-snug">{ann.title}</h3>
          {ann.body && (
            <p className="text-sm mt-3 leading-relaxed whitespace-pre-wrap" style={{ color: C.text }}>{ann.body}</p>
          )}
          {ann.linkUrl && (
            <a href={ann.linkUrl} target="_blank" rel="noreferrer"
              className="mt-5 flex items-center justify-center gap-2 w-full font-black py-3 rounded-xl transition active:scale-95"
              style={{ backgroundColor: C.blue, color: '#fff' }}>
              Saiba mais
            </a>
          )}
          <button onClick={onClose}
            className="mt-3 w-full py-2.5 text-sm rounded-xl border transition-colors hover:text-white"
            style={{ color: C.text, borderColor: C.border }}>
            Fechar
          </button>
        </div>
      </div>
    </div>
  )
}

function ProductModal({ product: p, onClose }: { product: Product; onClose: () => void }) {
  useEffect(() => {
    const h = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      onClick={onClose}>
      <div className="relative w-full max-w-sm rounded-2xl overflow-hidden shadow-2xl border"
        style={{ backgroundColor: C.card, borderColor: C.border }}
        onClick={e => e.stopPropagation()}>
        <button onClick={onClose}
          className="absolute top-3 right-3 z-10 p-1.5 rounded-full bg-black/50 hover:bg-black/80 transition">
          <X className="w-4 h-4 text-white" />
        </button>
        {p.imageUrl ? (
          <div className="w-full aspect-square" style={{ backgroundColor: C.cardAlt }}>
            <img src={p.imageUrl} alt={p.name} className="w-full h-full object-contain p-4" />
          </div>
        ) : (
          <div className="w-full aspect-square flex items-center justify-center" style={{ backgroundColor: C.cardAlt }}>
            <Package className="w-12 h-12 opacity-20 text-white" />
          </div>
        )}
        <div className="p-5">
          <p className="text-xs uppercase tracking-wide mb-1 font-medium" style={{ color: C.text }}>{p.category}</p>
          <h3 className="text-lg font-black text-white leading-snug mb-2">{p.name}</h3>
          {p.description && (
            <p className="text-sm mb-4 leading-relaxed" style={{ color: C.text }}>{p.description}</p>
          )}
          <div className="flex items-center justify-between pt-3 border-t" style={{ borderColor: C.border }}>
            {p.isOnPromo && p.discountPriceInReais != null ? (
              <div className="flex flex-col">
                <span className="text-sm line-through" style={{ color: C.text }}>
                  R$ {p.priceInReais.toFixed(2).replace('.', ',')}
                </span>
                <span className="text-2xl font-black" style={{ color: '#FF3B3B' }}>
                  R$ {p.discountPriceInReais.toFixed(2).replace('.', ',')}
                </span>
              </div>
            ) : (
              <span className="text-2xl font-black" style={{ color: C.yellow }}>
                R$ {p.priceInReais.toFixed(2).replace('.', ',')}
              </span>
            )}
            <span className="text-sm flex items-center gap-1" style={{ color: C.text }}>
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
      <div className="w-full max-w-md rounded-2xl shadow-2xl border p-6"
        style={{ backgroundColor: C.card, borderColor: C.border }}>
        <div className="flex items-start justify-between mb-5">
          <div>
            <h3 className="font-black text-white text-lg">Inscrição</h3>
            <p className="text-sm mt-0.5" style={{ color: C.text }}>{championship.name}</p>
          </div>
          <button onClick={onClose} className="p-1 hover:text-white transition-colors" style={{ color: C.text }}>
            <X className="w-5 h-5" />
          </button>
        </div>

        {done ? (
          <div className="text-center py-6">
            <div className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-4"
              style={{ backgroundColor: 'rgba(34,197,94,0.1)', border: '1px solid rgba(34,197,94,0.2)' }}>
              <CheckCircle className="w-7 h-7 text-green-400" />
            </div>
            <p className="font-black text-white mb-1">Solicitação enviada!</p>
            <p className="text-sm leading-relaxed" style={{ color: C.text }}>
              O Maikon vai confirmar sua vaga pelo WhatsApp. Pague na chegada.
            </p>
            <button onClick={onClose}
              className="mt-5 w-full py-2.5 text-sm rounded-xl border transition-colors hover:text-white"
              style={{ color: C.text, borderColor: C.border }}>
              Fechar
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="text-sm px-4 py-3 rounded-xl border"
              style={{ color: C.blue, borderColor: `${C.blue}30`, backgroundColor: `${C.blue}08` }}>
              Taxa: <strong>R$ {(championship.entryFeeInCents / 100).toFixed(2).replace('.', ',')}</strong> — pague na chegada
            </div>
            <div>
              <label className="block text-xs font-bold mb-1.5" style={{ color: C.text }}>Seu nome</label>
              <input
                className="w-full rounded-xl px-4 py-3 text-sm text-white outline-none focus:ring-2 transition-all"
                style={{ backgroundColor: C.cardAlt, border: `1px solid ${C.border}`, ['--tw-ring-color' as any]: C.blue }}
                placeholder="Nome completo"
                value={name} onChange={e => setName(e.target.value)} required
              />
            </div>
            <div>
              <label className="block text-xs font-bold mb-1.5" style={{ color: C.text }}>Seu WhatsApp</label>
              <input
                className="w-full rounded-xl px-4 py-3 text-sm text-white outline-none focus:ring-2 transition-all"
                style={{ backgroundColor: C.cardAlt, border: `1px solid ${C.border}` }}
                placeholder="(17) 99999-9999"
                value={phone} onChange={e => setPhone(e.target.value)} required
              />
            </div>
            <button type="submit"
              className="w-full flex items-center justify-center gap-2 font-black py-3.5 rounded-xl transition-all active:scale-95"
              style={{ backgroundColor: C.blue, color: '#fff' }}>
              <MessageCircle className="w-4 h-4" /> Confirmar pelo WhatsApp
            </button>
            <p className="text-xs text-center" style={{ color: C.text }}>
              Você será redirecionado para o WhatsApp.
            </p>
          </form>
        )}
      </div>
    </div>
  )
}
