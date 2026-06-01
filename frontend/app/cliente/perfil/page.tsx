'use client'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { userApi, UserProfile, crediarioApi, CrediariosDto, comandaApi, ComandaDto, championshipApi, MyParticipation } from '@/lib/api'
import { getUserName, clearAuth } from '@/lib/auth'
import { authApi } from '@/lib/api'
import {
  Star, User, Phone, CreditCard, Clock, AlertCircle, ArrowLeft, LogOut,
  CheckCircle, Wallet, CalendarClock, Receipt, ChevronDown, ChevronUp,
  ShoppingBag, XCircle, Trophy, Coins, ShieldCheck, Mail
} from 'lucide-react'
import Link from 'next/link'
import clsx from 'clsx'
import { Toaster } from 'react-hot-toast'

export default function PerfilPage() {
  const router = useRouter()
  const [profile,        setProfile]        = useState<UserProfile | null>(null)
  const [crediarios,     setCrediarios]     = useState<CrediariosDto[]>([])
  const [history,        setHistory]        = useState<ComandaDto[]>([])
  const [participations, setParticipations] = useState<MyParticipation[]>([])
  const [loading,        setLoading]        = useState(true)
  const [expanded,       setExpanded]       = useState<string | null>(null)
  const [tab,            setTab]            = useState<'pontos' | 'historico' | 'torneios' | 'crediario'>('pontos')

  useEffect(() => {
    Promise.all([
      userApi.me().then(r => setProfile(r.data)).catch(() => {}),
      crediarioApi.meuHistorico().then(r => setCrediarios(r.data)).catch(() => {}),
      comandaApi.myHistory().then(r => setHistory(r.data)).catch(() => {}),
      championshipApi.myParticipations().then(r => setParticipations(r.data)).catch(() => {}),
    ]).finally(() => setLoading(false))
  }, [])

  // Crediário aberto (se houver)
  const crediario = crediarios.find(c => c.status === 'Aberto' || c.status === 'Vencido') ?? null

  const agora = new Date()
  const consumoMensal = history
    .filter(c => {
      if (!c.closedAt || c.status !== 'Fechada') return false
      const d = new Date(c.closedAt)
      return d.getMonth() === agora.getMonth() && d.getFullYear() === agora.getFullYear()
    })
    .reduce((s, c) => s + c.totalInReais, 0)

  async function handleLogout() {
    try { await authApi.logout() } catch {}
    clearAuth()
    router.push('/')
  }

  const isExpired = profile?.pointsExpired
  const [isUploading, setIsUploading] = useState(false)

  async function handleAvatarClick() {
    document.getElementById('avatar-upload')?.click()
  }

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return

    if (file.size > 5 * 1024 * 1024) {
      toast.error('A imagem deve ter no máximo 5MB.')
      return
    }

    setIsUploading(true)
    try {
      const { data } = await authApi.uploadProfileImage(file)
      setProfile(prev => prev ? { ...prev, profileImageUrl: data.url } : prev)
      toast.success('Avatar atualizado!')
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Erro ao enviar imagem.')
    } finally {
      setIsUploading(false)
      // Limpa o input para permitir upload da mesma foto novamente se necessário
      e.target.value = ''
    }
  }

  return (
    <div className="min-h-screen bg-surface-900 pb-20 text-white font-sans">
      
      {/* ── HEADER ────────────────────────────────────────────────── */}
      <header className="bg-surface-800 border-b border-surface-700 px-6 py-5 sticky top-0 z-20 backdrop-blur-md">
        <div className="max-w-lg mx-auto flex items-center justify-between">
          <Link href="/cliente" className="p-2 -ml-2 text-gray-400 hover:text-white transition-colors">
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <h1 className="text-sm font-black uppercase tracking-[0.2em]" style={{ fontFamily: 'var(--font-cinzel)' }}>
            Minha Conta
          </h1>
          <button onClick={handleLogout} className="p-2 -mr-2 text-gray-400 hover:text-accent-red transition-colors">
            <LogOut className="w-5 h-5" />
          </button>
        </div>
      </header>

      <main className="max-w-lg mx-auto px-4 py-8 space-y-6">
        
        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <Loader2 className="w-8 h-8 animate-spin text-brand-500" />
            <p className="text-sm text-gray-500 font-medium text-center">Consultando registros...</p>
          </div>
        ) : (
          <>
            {/* ── PERFIL / AVATAR ── */}
            <section className="bg-gradient-to-b from-surface-800 to-surface-900 border border-surface-600 rounded-3xl p-8 text-center shadow-xl">
              <input 
                type="file" 
                id="avatar-upload" 
                accept="image/jpeg, image/png, image/webp" 
                className="hidden" 
                onChange={handleFileChange}
              />
              <button 
                onClick={handleAvatarClick}
                disabled={isUploading}
                className="relative w-20 h-20 bg-brand-500/10 border-2 border-brand-500/30 rounded-full flex items-center justify-center mx-auto mb-4 shadow-[0_0_20px_rgba(66,182,238,0.15)] hover:scale-105 hover:border-brand-500 transition-all cursor-pointer group overflow-hidden"
              >
                {isUploading ? (
                  <Loader2 className="w-8 h-8 animate-spin text-brand-500" />
                ) : profile?.profileImageUrl ? (
                  <>
                    <img 
                      src={`${process.env.NEXT_PUBLIC_API_URL || 'https://santuarionerd.tech'}${profile.profileImageUrl}`} 
                      alt="Avatar" 
                      className="w-full h-full object-cover" 
                      crossOrigin="anonymous"
                    />
                    <div className="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                      <span className="text-[10px] font-bold uppercase tracking-wider text-white">Editar</span>
                    </div>
                  </>
                ) : (
                  <>
                    <User className="w-10 h-10 text-brand-500" />
                    <div className="absolute inset-0 bg-black/60 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity rounded-full">
                      <span className="text-[10px] font-bold uppercase tracking-wider text-white">Editar</span>
                    </div>
                  </>
                )}
              </button>
              <h2 className="text-xl font-bold text-white">{profile?.name ?? getUserName() ?? 'Visitante'}</h2>
              <div className="flex flex-col gap-1 mt-2">
                {profile?.email && (
                  <div className="flex items-center justify-center gap-1.5 text-xs text-gray-500">
                    <Mail className="w-3 h-3" /> {profile.email}
                  </div>
                )}
                {profile?.whatsApp && (
                  <div className="flex items-center justify-center gap-1.5 text-xs text-gray-500">
                    <Phone className="w-3 h-3" /> {profile.whatsApp}
                  </div>
                )}
              </div>
            </section>

            {/* ── TABS NAVEGAÇÃO ── */}
            <nav className="flex bg-surface-800 p-1 rounded-2xl border border-surface-600 gap-1">
              {[
                { id: 'pontos', icon: Star, label: 'Pontos' },
                { id: 'historico', icon: Receipt, label: 'Histórico' },
                { id: 'torneios', icon: Trophy, label: 'Torneios' },
                { id: 'crediario', icon: Wallet, label: 'Dívida' },
              ].map((t) => (
                <button
                  key={t.id}
                  onClick={() => setTab(t.id as any)}
                  className={clsx(
                    "flex-1 flex flex-col items-center py-2.5 rounded-xl transition-all",
                    tab === t.id ? "bg-brand-500 text-white shadow-lg" : "text-gray-500 hover:text-gray-300"
                  )}
                >
                  <t.icon className={clsx("w-5 h-5 mb-1", tab === t.id ? "text-white" : "text-gray-600")} />
                  <span className="text-[10px] font-bold uppercase tracking-tighter">{t.label}</span>
                </button>
              ))}
            </nav>

            {/* ── CONTEÚDO DAS TABS ── */}
            <div className="space-y-4">
              
              {/* TAB: PONTOS */}
              {tab === 'pontos' && (
                <div className="space-y-4 animate-in fade-in slide-in-from-bottom-2 duration-300">
                  <div className="bg-surface-800 border border-surface-600 rounded-2xl p-6 relative overflow-hidden">
                    <Star className="absolute -right-4 -bottom-4 w-24 h-24 text-white opacity-[0.03]" />
                    <p className="text-xs font-bold text-brand-500 uppercase tracking-widest mb-1">Saldo de Experiência</p>
                    <div className="flex items-baseline gap-2">
                      <span className="text-4xl font-black">{profile?.pointsBalance ?? 0}</span>
                      <span className="text-gray-500 font-bold uppercase text-xs tracking-widest">Pontos</span>
                    </div>
                    {isExpired && (
                      <div className="mt-4 p-3 bg-red-500/10 border border-red-500/20 rounded-xl flex items-center gap-3 text-red-400 text-xs">
                        <AlertCircle className="w-4 h-4 shrink-0" />
                        Seus pontos expiraram. Continue frequentando para ganhar novos!
                      </div>
                    )}
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div className="bg-surface-800 border border-surface-600 rounded-2xl p-4">
                      <p className="text-[10px] font-bold text-gray-500 uppercase mb-1">Gasto no Mês</p>
                      <p className="text-lg font-bold">R$ {consumoMensal.toFixed(2).replace('.', ',')}</p>
                    </div>
                    <div className="bg-surface-800 border border-surface-600 rounded-2xl p-4">
                      <p className="text-[10px] font-bold text-gray-500 uppercase mb-1">Status</p>
                      <p className="text-lg font-bold flex items-center gap-1.5 text-accent-green">
                        <ShieldCheck className="w-4 h-4" /> Ativo
                      </p>
                    </div>
                  </div>
                </div>
              )}

              {/* TAB: HISTÓRICO */}
              {tab === 'historico' && (
                <div className="space-y-3 animate-in fade-in slide-in-from-bottom-2 duration-300">
                  {history.length === 0 ? (
                    <div className="text-center py-12 text-gray-600">
                      <Clock className="w-10 h-10 mx-auto mb-2 opacity-20" />
                      <p className="text-sm italic text-center">Nenhum registro encontrado...</p>
                    </div>
                  ) : (
                    history.map(c => {
                      const open = expanded === c.id
                      return (
                        <div key={c.id} className="bg-surface-800 border border-surface-600 rounded-2xl overflow-hidden group">
                          <button 
                            onClick={() => setExpanded(open ? null : c.id)}
                            className="w-full p-4 flex items-center justify-between hover:bg-surface-700/50 transition-colors"
                          >
                            <div className="flex items-center gap-3 text-left">
                              <div className="w-10 h-10 rounded-xl bg-surface-900 flex items-center justify-center border border-surface-600 group-hover:border-brand-500/30 transition-colors">
                                <Receipt className="w-5 h-5 text-gray-500" />
                              </div>
                              <div>
                                <p className="text-sm font-bold text-white">Comanda {c.closedAt ? new Date(c.closedAt).toLocaleDateString('pt-BR') : 'Ativa'}</p>
                                <p className="text-[10px] text-gray-500 uppercase font-bold tracking-tighter">{c.items.length} itens • {c.status}</p>
                              </div>
                            </div>
                            <div className="flex items-center gap-3">
                              <span className="font-black text-accent-green">R$ {c.totalInReais.toFixed(2).replace('.', ',')}</span>
                              {open ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                            </div>
                          </button>
                          {open && (
                            <div className="px-4 pb-4 border-t border-surface-700 bg-surface-900/50 space-y-2 pt-3">
                              {c.items.map((item, idx) => (
                                <div key={idx} className="flex justify-between items-center text-xs">
                                  <span className="text-gray-400 font-medium">{item.quantity}x {item.itemNameSnapshot}</span>
                                  <span className="text-gray-300 font-bold">R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}</span>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      )
                    })
                  )}
                </div>
              )}

              {/* TAB: TORNEIOS */}
              {tab === 'torneios' && (
                <div className="space-y-3 animate-in fade-in slide-in-from-bottom-2 duration-300">
                  {participations.length === 0 ? (
                    <div className="text-center py-12 text-gray-600">
                      <Trophy className="w-10 h-10 mx-auto mb-2 opacity-20" />
                      <p className="text-sm italic text-center">Você ainda não entrou em batalhas...</p>
                    </div>
                  ) : (
                    participations.map(p => (
                      <div key={p.participationId} className="bg-surface-800 border border-surface-600 rounded-2xl p-5 space-y-3">
                        <div className="flex justify-between items-start">
                          <div>
                            <p className="text-xs font-bold text-brand-500 uppercase tracking-widest">{p.game}</p>
                            <h3 className="text-lg font-bold text-white leading-tight">{p.championshipName}</h3>
                          </div>
                          <div className="px-2 py-1 rounded bg-emerald-500/10 border border-emerald-500/20 text-[10px] font-black text-emerald-400 uppercase">
                            Inscrito
                          </div>
                        </div>
                        <div className="flex items-center gap-4 pt-2 border-t border-surface-600">
                          <div className="flex items-center gap-1.5 text-xs text-gray-400">
                            <CalendarClock className="w-3.5 h-3.5" />
                            {new Date(p.startDate).toLocaleDateString('pt-BR')}
                          </div>
                          <div className="flex items-center gap-1.5 text-xs text-gray-400">
                            <Coins className="w-3.5 h-3.5" />
                            R$ {p.entryFeeInReais.toFixed(2)}
                          </div>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              )}

              {/* TAB: CREDIÁRIO */}
              {tab === 'crediario' && (
                <div className="animate-in fade-in slide-in-from-bottom-2 duration-300">
                  {!crediario ? (
                    <div className="bg-emerald-500/5 border border-emerald-500/10 rounded-3xl p-8 text-center space-y-4">
                      <div className="w-16 h-16 bg-emerald-500/10 rounded-full flex items-center justify-center mx-auto">
                        <ShieldCheck className="w-8 h-8 text-emerald-400" />
                      </div>
                      <div className="space-y-1">
                        <p className="font-bold text-white text-lg">Tudo limpo!</p>
                        <p className="text-gray-500 text-sm">Você não possui dívidas ativas no santuário.</p>
                      </div>
                    </div>
                  ) : (
                    <div className="bg-red-500/5 border border-red-500/10 rounded-3xl p-8 text-center space-y-6">
                      <div className="w-16 h-16 bg-red-500/10 rounded-full flex items-center justify-center mx-auto animate-pulse">
                        <Wallet className="w-8 h-8 text-red-400" />
                      </div>
                      <div className="space-y-1">
                        <p className="text-xs font-black text-red-500 uppercase tracking-widest">Dívida Pendente</p>
                        <p className="text-4xl font-black text-white">R$ {crediario.saldoRestanteEmReais.toFixed(2).replace('.', ',')}</p>
                      </div>
                      <div className="p-4 bg-black/20 rounded-2xl flex flex-col gap-2">
                        <div className="flex justify-between text-xs">
                          <span className="text-gray-500 uppercase font-bold">Vencimento</span>
                          <span className="text-red-400 font-bold">{new Date(crediario.dataVencimento).toLocaleDateString('pt-BR')}</span>
                        </div>
                      </div>
                      <p className="text-[10px] text-gray-600 italic text-center">
                        * Compareça ao balcão para quitar sua dívida com o Maikon.
                      </p>
                    </div>
                  )}
                </div>
              )}

            </div>
          </>
        )}
      </main>

      <Toaster position="bottom-center" />
    </div>
  )
}

function Loader2({ className }: { className?: string }) {
  return (
    <svg className={clsx("animate-spin", className)} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
    </svg>
  )
}
