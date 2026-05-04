'use client'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { isLoggedIn, isAdmin } from '@/lib/auth'
import { championshipApi, productApi, Championship, Product } from '@/lib/api'
import Link from 'next/link'
import {
  Sword, Trophy, ShoppingBag, QrCode, Star,
  Calendar, Users, ChevronRight, Zap, Shield, Heart,
  X, MessageCircle, CheckCircle
} from 'lucide-react'

// WhatsApp do Maikon para confirmação de inscrições
const MAIKON_WHATSAPP = '5511999999999' // TODO: substituir pelo número real

export default function LandingPage() {
  const router = useRouter()
  const [championships, setChampionships]   = useState<Championship[]>([])
  const [products, setProducts]             = useState<Product[]>([])
  const [loadingData, setLoadingData]       = useState(true)
  const [registerModal, setRegisterModal]   = useState<Championship | null>(null)

  useEffect(() => {
    // Redireciona usuários já logados
    if (isLoggedIn()) {
      router.replace(isAdmin() ? '/admin/dashboard' : '/cliente')
      return
    }

    // Busca dados públicos para exibir na landing
    Promise.allSettled([
      championshipApi.list().then(r => setChampionships(
        r.data.filter(c => c.status === 'Planejado' || c.status === 'InscricoesAbertas').slice(0, 3)
      )),
      productApi.list().then(r => setProducts(
        r.data.filter(p => p.isActive && p.stockQuantity > 0).slice(0, 6)
      )),
    ]).finally(() => setLoadingData(false))
  }, [router])

  return (
    <div className="min-h-screen bg-[#0a0a10] text-white">

      {/* ── Navbar ─────────────────────────────────────────────────────────── */}
      <nav className="fixed top-0 left-0 right-0 z-50 border-b border-white/5 bg-[#0a0a10]/90 backdrop-blur-md">
        <div className="max-w-6xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 bg-amber-500/20 border border-amber-500/40 rounded-lg flex items-center justify-center">
              <Sword className="w-4 h-4 text-amber-400" />
            </div>
            <span className="font-bold text-white text-lg">softNerd</span>
          </div>
          <div className="flex items-center gap-3">
            <Link
              href="/login"
              className="text-sm text-gray-400 hover:text-white transition-colors px-4 py-2"
            >
              Área Admin
            </Link>
            <a
              href="#campeonatos"
              className="text-sm bg-amber-500 hover:bg-amber-400 text-black font-semibold px-4 py-2 rounded-lg transition-colors"
            >
              Ver Eventos
            </a>
          </div>
        </div>
      </nav>

      {/* ── Hero ───────────────────────────────────────────────────────────── */}
      <section className="pt-32 pb-24 px-6">
        <div className="max-w-4xl mx-auto text-center">
          <div className="inline-flex items-center gap-2 bg-blue-500/10 border border-blue-500/20 rounded-full px-4 py-1.5 text-blue-400 text-sm font-medium mb-6">
            <Zap className="w-3.5 h-3.5" />
            Loja de Card Games & Campeonatos
          </div>
          <h1 className="text-5xl md:text-7xl font-black text-white mb-6 leading-none tracking-tight">
            Sua loja de
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-amber-400 to-amber-600"> card games</span>
            <br />favorita
          </h1>
          <p className="text-gray-400 text-lg md:text-xl max-w-2xl mx-auto mb-10 leading-relaxed">
            Produtos, campeonatos e a melhor experiência para jogadores de TCG.
            Sente na sua mesa, escaneie o QR Code e faça seu pedido direto pelo celular.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <a
              href="#campeonatos"
              className="inline-flex items-center gap-2 bg-amber-500 hover:bg-amber-400 text-black font-bold px-8 py-3.5 rounded-xl transition-all duration-150 active:scale-95"
            >
              <Trophy className="w-5 h-5" /> Ver Campeonatos
            </a>
            <a
              href="#produtos"
              className="inline-flex items-center gap-2 bg-white/5 hover:bg-white/10 border border-white/10 text-white font-semibold px-8 py-3.5 rounded-xl transition-all duration-150 active:scale-95"
            >
              <ShoppingBag className="w-5 h-5" /> Ver Produtos
            </a>
          </div>
        </div>
      </section>

      {/* ── Como funciona ──────────────────────────────────────────────────── */}
      <section className="py-20 px-6 border-y border-white/5 bg-white/[0.02]">
        <div className="max-w-5xl mx-auto">
          <h2 className="text-2xl font-bold text-center mb-12 text-white">Como funciona</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            {[
              {
                icon: QrCode,
                color: 'text-blue-400',
                bg:    'bg-blue-500/10 border-blue-500/20',
                step:  '1',
                title: 'Escaneie o QR Code',
                desc:  'Cada mesa tem um QR Code único. Escaneie com seu celular para abrir sua comanda automaticamente.',
              },
              {
                icon: ShoppingBag,
                color: 'text-amber-400',
                bg:    'bg-amber-500/10 border-amber-500/20',
                step:  '2',
                title: 'Faça seu pedido',
                desc:  'Adicione bebidas, salgadinhos e produtos direto pelo celular, sem precisar chamar o atendente.',
              },
              {
                icon: Heart,
                color: 'text-emerald-400',
                bg:    'bg-emerald-500/10 border-emerald-500/20',
                step:  '3',
                title: 'Jogue e aproveite',
                desc:  'O atendente fecha sua comanda quando você sair. Simples assim.',
              },
            ].map(({ icon: Icon, color, bg, step, title, desc }) => (
              <div key={step} className="text-center">
                <div className={`w-14 h-14 rounded-2xl border flex items-center justify-center mx-auto mb-4 ${bg}`}>
                  <Icon className={`w-7 h-7 ${color}`} />
                </div>
                <div className="text-xs font-bold text-gray-600 mb-1">PASSO {step}</div>
                <h3 className="font-bold text-white mb-2">{title}</h3>
                <p className="text-gray-500 text-sm leading-relaxed">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── Campeonatos ────────────────────────────────────────────────────── */}
      <section id="campeonatos" className="py-20 px-6">
        <div className="max-w-5xl mx-auto">
          <div className="flex items-center justify-between mb-10">
            <div>
              <h2 className="text-3xl font-bold text-white">Próximos Eventos</h2>
              <p className="text-gray-500 mt-1">Campeonatos e torneios de TCG</p>
            </div>
          </div>

          {loadingData ? (
            <div className="flex justify-center py-16">
              <div className="w-8 h-8 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : championships.length === 0 ? (
            <div className="text-center py-16 border border-white/5 rounded-2xl">
              <Trophy className="w-10 h-10 text-gray-700 mx-auto mb-3" />
              <p className="text-gray-500">Nenhum campeonato agendado no momento.</p>
              <p className="text-gray-700 text-sm mt-1">Fique atento às novidades!</p>
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

      {/* ── Produtos em destaque ───────────────────────────────────────────── */}
      <section id="produtos" className="py-20 px-6 bg-white/[0.02] border-t border-white/5">
        <div className="max-w-5xl mx-auto">
          <div className="mb-10">
            <h2 className="text-3xl font-bold text-white">Produtos</h2>
            <p className="text-gray-500 mt-1">Disponíveis na loja</p>
          </div>

          {loadingData ? (
            <div className="flex justify-center py-16">
              <div className="w-8 h-8 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : products.length === 0 ? (
            <div className="text-center py-16 border border-white/5 rounded-2xl">
              <ShoppingBag className="w-10 h-10 text-gray-700 mx-auto mb-3" />
              <p className="text-gray-500">Produtos em breve.</p>
            </div>
          ) : (
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              {products.map(p => (
                <ProductCard key={p.id} product={p} />
              ))}
            </div>
          )}
        </div>
      </section>

      {/* ── Banner Pontos ──────────────────────────────────────────────────── */}
      <section className="py-20 px-6 border-t border-white/5">
        <div className="max-w-3xl mx-auto text-center">
          <div className="inline-flex items-center gap-2 bg-amber-500/10 border border-amber-500/20 rounded-full px-4 py-1.5 text-amber-400 text-sm font-medium mb-6">
            <Star className="w-3.5 h-3.5" />
            Programa de Pontos
          </div>
          <h2 className="text-4xl font-black text-white mb-4">
            Ganhe pontos,<br />
            <span className="text-amber-400">troque por produtos</span>
          </h2>
          <p className="text-gray-400 text-lg mb-8">
            O Maikon adiciona pontos na sua conta a cada visita. Junte pontos e troque por itens na loja. Os pontos são válidos por 30 dias.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <div className="bg-white/5 border border-white/10 rounded-xl px-6 py-4 text-left">
              <div className="flex items-center gap-2 mb-1">
                <Shield className="w-4 h-4 text-blue-400" />
                <span className="text-sm font-semibold text-white">Cadastro grátis</span>
              </div>
              <p className="text-xs text-gray-500">Apenas CPF + WhatsApp no QR Code</p>
            </div>
            <div className="bg-white/5 border border-white/10 rounded-xl px-6 py-4 text-left">
              <div className="flex items-center gap-2 mb-1">
                <Star className="w-4 h-4 text-amber-400" />
                <span className="text-sm font-semibold text-white">Pontos por visita</span>
              </div>
              <p className="text-xs text-gray-500">Maikon adiciona pontos manualmente</p>
            </div>
            <div className="bg-white/5 border border-white/10 rounded-xl px-6 py-4 text-left">
              <div className="flex items-center gap-2 mb-1">
                <ShoppingBag className="w-4 h-4 text-emerald-400" />
                <span className="text-sm font-semibold text-white">Troque por produtos</span>
              </div>
              <p className="text-xs text-gray-500">Use na comanda para abater o valor</p>
            </div>
          </div>
        </div>
      </section>

      {/* ── Modal de inscrição ────────────────────────────────────────────── */}
      {registerModal && (
        <RegisterModal
          championship={registerModal}
          onClose={() => setRegisterModal(null)}
        />
      )}

      {/* ── Footer ─────────────────────────────────────────────────────────── */}
      <footer className="border-t border-white/5 py-10 px-6">
        <div className="max-w-5xl mx-auto flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2.5">
            <div className="w-7 h-7 bg-amber-500/20 border border-amber-500/40 rounded-lg flex items-center justify-center">
              <Sword className="w-3.5 h-3.5 text-amber-400" />
            </div>
            <span className="font-bold text-white">softNerd</span>
          </div>
          <p className="text-gray-600 text-sm">
            © {new Date().getFullYear()} softNerd. Todos os direitos reservados.
          </p>
          <Link href="/login" className="text-gray-600 hover:text-gray-400 text-sm transition-colors flex items-center gap-1">
            Área Admin <ChevronRight className="w-3 h-3" />
          </Link>
        </div>
      </footer>
    </div>
  )
}

// ── Sub-componentes ───────────────────────────────────────────────────────────

function ChampionshipCard({ championship: c, onRegister }: { championship: Championship; onRegister: () => void }) {
  const gameColors: Record<string, string> = {
    'Pokemon':    'text-yellow-400 bg-yellow-500/10 border-yellow-500/20',
    'Magic':      'text-blue-400   bg-blue-500/10   border-blue-500/20',
    'Yu-Gi-Oh':   'text-purple-400 bg-purple-500/10 border-purple-500/20',
    'One Piece':  'text-red-400    bg-red-500/10    border-red-500/20',
  }
  const colorClass = gameColors[c.game] ?? 'text-gray-400 bg-gray-500/10 border-gray-500/20'

  const statusLabel: Record<string, string> = {
    Planejado:         'Em breve',
    InscricoesAbertas: 'Inscrições Abertas',
  }

  return (
    <div className="bg-white/[0.03] border border-white/8 rounded-2xl p-5 hover:border-amber-500/30 transition-all duration-200 group">
      <div className="flex items-start justify-between mb-4">
        <span className={`text-xs font-bold px-2.5 py-1 rounded-full border ${colorClass}`}>
          {c.game}
        </span>
        <span className="text-xs text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 px-2.5 py-1 rounded-full font-medium">
          {statusLabel[c.status] ?? c.status}
        </span>
      </div>
      <h3 className="font-bold text-white mb-3 leading-tight">{c.name}</h3>
      <div className="space-y-1.5 text-sm text-gray-500">
        <div className="flex items-center gap-1.5">
          <Calendar className="w-3.5 h-3.5" />
          {new Date(c.startDate).toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' })}
        </div>
        {c.maxParticipants && (
          <div className="flex items-center gap-1.5">
            <Users className="w-3.5 h-3.5" />
            Até {c.maxParticipants} participantes
          </div>
        )}
        <div className="flex items-center gap-1.5 text-amber-400 font-semibold">
          Inscrição: R$ {(c.entryFeeInCents / 100).toFixed(2).replace('.', ',')}
        </div>
      </div>
      <div className="mt-4 pt-4 border-t border-white/5">
        <button
          onClick={onRegister}
          className="w-full bg-amber-500 hover:bg-amber-400 text-black font-bold text-sm py-2 rounded-lg transition-colors"
        >
          Quero me inscrever
        </button>
        <p className="text-xs text-gray-600 text-center mt-2">Pague na chegada · Vagas limitadas</p>
      </div>
    </div>
  )
}

function RegisterModal({ championship, onClose }: { championship: Championship; onClose: () => void }) {
  const [name, setName]       = useState('')
  const [phone, setPhone]     = useState('')
  const [done, setDone]       = useState(false)

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim() || !phone.trim()) return

    const msg = encodeURIComponent(
      `Olá! Quero me inscrever no campeonato *${championship.name}*.\n` +
      `Nome: ${name.trim()}\n` +
      `WhatsApp: ${phone.trim()}\n` +
      `Confirmo que pagarei na chegada (R$ ${(championship.entryFeeInCents / 100).toFixed(2).replace('.', ',')}).`
    )
    window.open(`https://wa.me/${MAIKON_WHATSAPP}?text=${msg}`, '_blank')
    setDone(true)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="bg-[#16161d] border border-white/10 rounded-2xl w-full max-w-md p-6">
        <div className="flex items-start justify-between mb-5">
          <div>
            <h3 className="font-bold text-white text-lg">Inscrição no Campeonato</h3>
            <p className="text-gray-500 text-sm mt-0.5">{championship.name}</p>
          </div>
          <button onClick={onClose} className="text-gray-600 hover:text-white transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        {done ? (
          <div className="text-center py-6">
            <CheckCircle className="w-12 h-12 text-emerald-400 mx-auto mb-3" />
            <p className="font-bold text-white mb-1">Solicitação enviada!</p>
            <p className="text-gray-400 text-sm">O Maikon vai confirmar sua vaga pelo WhatsApp. Pague na chegada.</p>
            <button onClick={onClose} className="mt-5 w-full bg-white/5 hover:bg-white/10 border border-white/10 text-white font-medium py-2.5 rounded-xl transition-colors">
              Fechar
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="bg-amber-500/10 border border-amber-500/20 rounded-xl px-4 py-3 text-sm text-amber-400">
              Taxa de inscrição: <strong>R$ {(championship.entryFeeInCents / 100).toFixed(2).replace('.', ',')}</strong> — pague na chegada
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-300 mb-1.5">Seu nome</label>
              <input
                className="w-full px-3 py-2.5 bg-white/5 border border-white/10 rounded-lg text-white placeholder-gray-600 text-sm focus:outline-none focus:ring-2 focus:ring-amber-500/50"
                placeholder="Nome completo"
                value={name}
                onChange={e => setName(e.target.value)}
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-1.5">Seu WhatsApp</label>
              <input
                className="w-full px-3 py-2.5 bg-white/5 border border-white/10 rounded-lg text-white placeholder-gray-600 text-sm focus:outline-none focus:ring-2 focus:ring-amber-500/50"
                placeholder="(11) 99999-9999"
                value={phone}
                onChange={e => setPhone(e.target.value)}
                required
              />
            </div>

            <button
              type="submit"
              className="w-full flex items-center justify-center gap-2 bg-amber-500 hover:bg-amber-400 text-black font-bold py-3 rounded-xl transition-colors"
            >
              <MessageCircle className="w-4 h-4" />
              Confirmar pelo WhatsApp
            </button>
            <p className="text-xs text-gray-600 text-center">
              Você será redirecionado para o WhatsApp do Maikon para confirmar sua vaga.
            </p>
          </form>
        )}
      </div>
    </div>
  )
}

function ProductCard({ product: p }: { product: Product }) {
  return (
    <div className="bg-white/[0.03] border border-white/8 rounded-2xl p-4 hover:border-amber-500/20 transition-all duration-200">
      <p className="text-xs text-gray-600 mb-1">{p.category}</p>
      <p className="text-sm font-semibold text-white leading-snug line-clamp-2 mb-3">{p.name}</p>
      <div className="flex items-center justify-between">
        <span className="text-amber-400 font-bold">
          R$ {p.priceInReais.toFixed(2).replace('.', ',')}
        </span>
        <span className="text-xs text-gray-600">{p.stockQuantity} un.</span>
      </div>
    </div>
  )
}
