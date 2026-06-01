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
  const hasPoints = profile && profile.pointsBalance > 0 && !isExpired

  return (
    <div className="min-h-screen bg-[#0f0f13] pb-20 text-white">
      
      {/* ── HEADER ────────────────────────────────────────────────── */}
      <header className="bg-[#16161d] border-b border-[#252530] px-6 py-5 sticky top-0 z-20 backdrop-blur-md">
        <div className="max-w-lg mx-auto flex items-center justify-between">
          <Link href="/cliente" className="p-2 -ml-2 text-gray-400 hover:text-white transition-colors">
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <h1 className="text-sm font-black uppercase tracking-[0.2em]" style={{ fontFamily: 'var(--font-cinzel)' }}>
            Minha Conta
          </h1>
          <button onClick={handleLogout} className="p-2 -mr-2 text-gray-400 hover:text-red-400 transition-colors">
            <LogOut className="w-5 h-5" />
          </button>
        </div>
      </header>

      <main className="max-w-lg mx-auto px-4 py-8 space-y-6">
        
        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <Loader2 className="w-8 h-8 animate-spin text-[#7839F3]" />
            <p className="text-sm text-gray-500 font-medium">Consultando registros...</p>
          </div>
        ) : (
          <>
            {/* ── PERFIL / AVATAR ── */}
            <section className="bg-gradient-to-b from-[#1e1e28] to-[#16161d] border border-[#32323f] rounded-3xl p-8 text-center shadow-xl">
              <div className="w-20 h-20 bg-[#7839F3]/10 border-2 border-[#7839F3]/30 rounded-full flex items-center justify-center mx-auto mb-4 shadow-[0_0_20px_rgba(120,57,243,0.15)]">
                <User className="w-10 h-10 text-[#7839F3]" />
              </div>
              <h2 className="text-xl font-bold text-white">{profile?.name ?? getUserName() ?? 'Visitante'}</h2>
              <div className="flex flex-col gap-1 mt-2">
                {profile?.email && (
                  <div className="flex items-center justify-center gap-1.5 text-xs text-gray-500">
                    <Mail className="w-3 h-3" /> {profile.email}
                  </div>
                )}
                {profile?.whatsapp && (
                  <div className="flex items-center justify-center gap-1.5 text-xs text-gray-500">
                    <Phone className="w-3 h-3" /> {profile.whatsapp}
                  </div>
                )}
              </div>
            </section>

            {/* ── TABS NAVEGAÇÃO ── */}
            <nav className="flex bg-[#16161d] p-1 rounded-2xl border border-[#252530] gap-1">
              {[
                { id: 'pontos', icon: Star, label: 'Pontos' },
                { id: 'historico', icon: Receipt, label: 'Logs' },
                { id: 'torneios', icon: Trophy, label: 'Torneios' },
                { id: 'crediario', icon: Wallet, label: 'Dívida' },
              ].map((t) => (
                <button
                  key={t.id}
                  onClick={() => setTab(t.id as any)}
                  className={clsx(
                    "flex-1 flex flex-col items-center py-2.5 rounded-xl transition-all",
                    tab === t.id ? "bg-[#7839F3] text-white shadow-lg" : "text-gray-500 hover:text-gray-300"
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
                  <div className="bg-[#1e1e28] border border-[#32323f] rounded-2xl p-6 relative overflow-hidden">
                    <Star className="absolute -right-4 -bottom-4 w-24 h-24 text-white opacity-[0.03]" />
                    <p className="text-xs font-bold text-[#7839F3] uppercase tracking-widest mb-1">Saldo de Experiência</p>
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
                    <div className="bg-[#1e1e28] border border-[#32323f] rounded-2xl p-4">
                      <p className="text-[10px] font-bold text-gray-500 uppercase mb-1">Gasto no Mês</p>
                      <p className="text-lg font-bold">R$ {consumoMensal.toFixed(2).replace('.', ',')}</p>
                    </div>
                    <div className="bg-[#1e1e28] border border-[#32323f] rounded-2xl p-4">
                      <p className="text-[10px] font-bold text-gray-500 uppercase mb-1">Status</p>
                      <p className="text-lg font-bold flex items-center gap-1.5 text-emerald-400">
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
                      <p className="text-sm italic">Nenhum registro encontrado...</p>
                    </div>
                  ) : (
                    history.map(c => (
                      <div key={c.id} className="bg-[#1e1e28] border border-[#32323f] rounded-2xl p-4 flex items-center justify-between group">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-xl bg-[#16161d] flex items-center justify-center border border-[#32323f] group-hover:border-[#7839F3]/30 transition-colors">
                            <Receipt className="w-5 h-5 text-gray-500" />
                          </div>
                          <div>
                            <p className="text-sm font-bold text-white">Comanda {c.closedAt ? new Date(c.closedAt).toLocaleDateString('pt-BR') : 'Ativa'}</p>
                            <p className="text-[10px] text-gray-500 uppercase font-bold tracking-tighter">{c.items.length} itens • {c.status}</p>
                          </div>
                        </div>
                        <span className="font-black text-[#00F0A8]">R$ {c.totalInReais.toFixed(2).replace('.', ',')}</span>
                      </div>
                    ))
                  )}
                </div>
              )}

              {/* TAB: TORNEIOS */}
              {tab === 'torneios' && (
                <div className="space-y-3 animate-in fade-in slide-in-from-bottom-2 duration-300">
                  {participations.length === 0 ? (
                    <div className="text-center py-12 text-gray-600">
                      <Trophy className="w-10 h-10 mx-auto mb-2 opacity-20" />
                      <p className="text-sm italic">Você ainda não entrou em batalhas...</p>
                    </div>
                  ) : (
                    participations.map(p => (
                      <div key={p.championshipId} className="bg-[#1e1e28] border border-[#32323f] rounded-2xl p-5 space-y-3">
                        <div className="flex justify-between items-start">
                          <div>
                            <p className="text-xs font-bold text-[#7839F3] uppercase tracking-widest">{p.tcg}</p>
                            <h3 className="text-lg font-bold text-white leading-tight">{p.name}</h3>
                          </div>
                          <div className="px-2 py-1 rounded bg-emerald-500/10 border border-emerald-500/20 text-[10px] font-black text-emerald-400 uppercase">
                            Inscrito
                          </div>
                        </div>
                        <div className="flex items-center gap-4 pt-2 border-t border-[#32323f]">
                          <div className="flex items-center gap-1.5 text-xs text-gray-400">
                            <CalendarClock className="w-3.5 h-3.5" />
                            {new Date(p.date).toLocaleDateString('pt-BR')}
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
                        <p className="text-4xl font-black text-white">R$ {crediario.valorTotalInReais.toFixed(2).replace('.', ',')}</p>
                      </div>
                      <div className="p-4 bg-black/20 rounded-2xl flex flex-col gap-2">
                        <div className="flex justify-between text-xs">
                          <span className="text-gray-500 uppercase font-bold">Vencimento</span>
                          <span className="text-red-400 font-bold">{new Date(crediario.dataVencimento).toLocaleDateString('pt-BR')}</span>
                        </div>
                      </div>
                      <p className="text-[10px] text-gray-600 italic">
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

      {/* Carregamento flutuante de reconexão do SignalR (se houver) */}
      <Toaster position="bottom-center" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />
    </div>
  )
}

// Helper para Spinner
function Loader2({ className }: { className?: string }) {
  return (
    <svg className={clsx("animate-spin", className)} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
    </svg>
  )
}

                <button
                  key={key}
                  onClick={() => setTab(key)}
                  className={`relative flex flex-col items-center justify-center gap-0.5 py-2 rounded-lg text-[10px] font-semibold transition-all ${
                    tab === key
                      ? 'bg-brand-500 text-white shadow'
                      : 'text-gray-400 hover:text-white'
                  }`}
                >
                  <Icon className="w-3.5 h-3.5" />
                  {label}
                  {key === 'crediario' && crediario && (
                    <span className="w-1.5 h-1.5 bg-red-400 rounded-full absolute top-1.5 right-1.5" />
                  )}
                </button>
              ))}
            </div>

            {/* ── Tab: Pontos ───────────────────────────────────── */}
            {tab === 'pontos' && (
              <div className="card">
                <div className="text-center py-4">
                  <p className={`text-6xl font-black mb-1 ${isExpired ? 'text-gray-600' : 'text-accent-gold'}`}>
                    {isExpired ? '0' : (profile?.pointsBalance ?? 0)}
                  </p>
                  <p className="text-gray-500 text-sm">pontos disponíveis</p>

                  {isExpired && (
                    <div className="flex items-center justify-center gap-1.5 mt-3 text-sm text-red-400 bg-red-500/10 border border-red-500/20 rounded-lg px-4 py-2">
                      <AlertCircle className="w-4 h-4 shrink-0" />
                      Seus pontos expiraram. Fale com o Maikon!
                    </div>
                  )}

                  {hasPoints && profile?.pointsExpiresAt && (
                    <div className="flex items-center justify-center gap-1.5 mt-3 text-sm text-gray-500">
                      <Clock className="w-3.5 h-3.5" />
                      Válido até {new Date(profile.pointsExpiresAt).toLocaleDateString('pt-BR', {
                        day: '2-digit', month: 'long', year: 'numeric',
                      })}
                    </div>
                  )}

                  {!profile?.pointsBalance && !isExpired && (
                    <p className="text-gray-600 text-sm mt-3">Você ainda não tem pontos. Fale com o Maikon!</p>
                  )}
                </div>

                {/* Cashback / Crédito na loja */}
                {(profile?.balanceInCents ?? 0) > 0 && (
                  <div className="bg-emerald-500/10 border border-emerald-500/20 rounded-xl px-4 py-3 flex items-center gap-3">
                    <Coins className="w-5 h-5 text-emerald-400 shrink-0" />
                    <div>
                      <p className="text-xs text-gray-400">Cashback / Crédito na loja</p>
                      <p className="font-bold text-emerald-400">
                        R$ {((profile?.balanceInCents ?? 0) / 100).toFixed(2).replace('.', ',')}
                      </p>
                    </div>
                  </div>
                )}

                {/* Consumo mensal */}
                <div className="bg-surface-700 rounded-xl px-4 py-3 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ShoppingBag className="w-4 h-4 text-brand-400" />
                    <span className="text-sm text-gray-400">Consumo este mês</span>
                  </div>
                  <span className="font-bold text-white">
                    R$ {consumoMensal.toFixed(2).replace('.', ',')}
                  </span>
                </div>

                <div className="border-t border-surface-500 pt-4 mt-2">
                  <div className="flex items-start gap-2.5 text-xs text-gray-500 leading-relaxed">
                    <CheckCircle className="w-3.5 h-3.5 text-accent-green shrink-0 mt-0.5" />
                    <span>100 pontos = R$ 1,00 de desconto na comanda. Válidos por 30 dias.</span>
                  </div>
                </div>

                {/* Dados pessoais */}
                <div className="border-t border-surface-500 pt-4 mt-4 space-y-3">
                  {profile?.whatsApp && (
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 bg-surface-600 rounded-lg flex items-center justify-center shrink-0">
                        <Phone className="w-4 h-4 text-gray-400" />
                      </div>
                      <div>
                        <p className="text-xs text-gray-500">WhatsApp</p>
                        <p className="text-sm text-white">{profile.whatsApp}</p>
                      </div>
                    </div>
                  )}
                  {profile?.createdAt && (
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 bg-surface-600 rounded-lg flex items-center justify-center shrink-0">
                        <CreditCard className="w-4 h-4 text-gray-400" />
                      </div>
                      <div>
                        <p className="text-xs text-gray-500">Cliente desde</p>
                        <p className="text-sm text-white">
                          {new Date(profile.createdAt).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })}
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* ── Tab: Histórico ────────────────────────────────── */}
            {tab === 'historico' && (
              <div className="space-y-3">
                {history.length === 0 ? (
                  <div className="card text-center py-10">
                    <ShoppingBag className="w-10 h-10 text-gray-600 mx-auto mb-3" />
                    <p className="text-gray-500 text-sm">Nenhuma comanda anterior.</p>
                    <p className="text-gray-600 text-xs mt-1">Seu histórico de visitas aparece aqui.</p>
                  </div>
                ) : (
                  history.map(c => {
                    const open = expanded === c.id
                    const isFechada = c.status === 'Fechada'
                    return (
                      <div key={c.id} className="card p-0 overflow-hidden">
                        {/* Header da comanda */}
                        <button
                          className="w-full flex items-center gap-3 px-4 py-3.5 text-left hover:bg-surface-700/30 transition-colors"
                          onClick={() => setExpanded(open ? null : c.id)}
                        >
                          <div className={`w-8 h-8 rounded-lg flex items-center justify-center shrink-0 ${
                            isFechada ? 'bg-emerald-500/10' : 'bg-red-500/10'
                          }`}>
                            {isFechada
                              ? <CheckCircle className="w-4 h-4 text-emerald-400" />
                              : <XCircle className="w-4 h-4 text-red-400" />
                            }
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between gap-2">
                              <span className="font-semibold text-white text-sm">
                                {c.closedAt
                                  ? new Date(c.closedAt).toLocaleDateString('pt-BR', {
                                      day: '2-digit', month: 'short', year: 'numeric',
                                    })
                                  : '—'
                                }
                              </span>
                              <span className="font-bold text-accent-gold text-sm shrink-0">
                                R$ {c.totalInReais.toFixed(2).replace('.', ',')}
                              </span>
                            </div>
                            <div className="flex items-center gap-3 mt-0.5">
                              <span className={`text-xs font-medium ${isFechada ? 'text-emerald-400' : 'text-red-400'}`}>
                                {c.status}
                              </span>
                              {c.paymentMethod && isFechada && (
                                <span className="text-xs text-gray-500">· {c.paymentMethod}</span>
                              )}
                              {c.tableIdentifier && (
                                <span className="text-xs text-gray-600">· Mesa {c.tableIdentifier}</span>
                              )}
                              <span className="text-xs text-gray-600">· {c.items.length} iten{c.items.length !== 1 ? 's' : ''}</span>
                            </div>
                          </div>
                          {open
                            ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" />
                            : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />
                          }
                        </button>

                        {/* Itens da comanda (expandível) */}
                        {open && (
                          <div className="border-t border-surface-600 divide-y divide-surface-600/50">
                            {c.items.length === 0 ? (
                              <p className="px-4 py-3 text-xs text-gray-500">Sem itens registrados.</p>
                            ) : (
                              c.items.map(item => (
                                <div key={item.id} className="flex items-center justify-between px-4 py-2.5">
                                  <div className="min-w-0">
                                    <p className="text-sm text-white truncate">{item.itemNameSnapshot}</p>
                                    <p className="text-xs text-gray-500">{item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}</p>
                                  </div>
                                  <span className="text-sm font-semibold text-accent-gold shrink-0 ml-3">
                                    R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                                  </span>
                                </div>
                              ))
                            )}
                            <div className="flex justify-between items-center px-4 py-2.5 bg-surface-800/60">
                              <span className="text-xs text-gray-400">Total pago</span>
                              <span className="font-bold text-accent-gold text-sm">
                                R$ {c.totalInReais.toFixed(2).replace('.', ',')}
                              </span>
                            </div>
                          </div>
                        )}
                      </div>
                    )
                  })
                )}
              </div>
            )}

            {/* ── Tab: Torneios ────────────────────────────────── */}
            {tab === 'torneios' && (
              <div className="space-y-3">
                {participations.length === 0 ? (
                  <div className="card text-center py-10">
                    <Trophy className="w-10 h-10 text-gray-600 mx-auto mb-3" />
                    <p className="text-gray-500 text-sm">Você ainda não participou de nenhum torneio.</p>
                  </div>
                ) : (
                  participations.map(p => {
                    const statusColor: Record<string, string> = {
                      Planejado:   'text-gray-400',
                      Inscricoes:  'text-brand-400',
                      EmAndamento: 'text-amber-400',
                      Finalizado:  'text-emerald-400',
                      Cancelado:   'text-red-400',
                    }
                    const statusLabel: Record<string, string> = {
                      Planejado:   'Em breve',
                      Inscricoes:  'Inscrições abertas',
                      EmAndamento: 'Em andamento',
                      Finalizado:  'Finalizado',
                      Cancelado:   'Cancelado',
                    }
                    return (
                      <div key={p.participationId} className="card">
                        <div className="flex items-start justify-between gap-3">
                          <div className="flex-1 min-w-0">
                            <p className="font-bold text-white text-sm leading-snug">{p.championshipName}</p>
                            <p className="text-xs text-gray-500 mt-0.5">{p.game} · {new Date(p.startDate).toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' })}</p>
                          </div>
                          <span className={`text-xs font-semibold shrink-0 ${statusColor[p.status] ?? 'text-gray-400'}`}>
                            {statusLabel[p.status] ?? p.status}
                          </span>
                        </div>

                        <div className="flex items-center gap-4 mt-3 pt-3 border-t border-surface-600">
                          <div className="text-center">
                            <p className="text-xs text-gray-500">Nº</p>
                            <p className="font-bold text-white text-sm">#{p.playerNumber}</p>
                          </div>
                          {p.deckName && (
                            <div>
                              <p className="text-xs text-gray-500">Deck</p>
                              <p className="text-sm text-white">{p.deckName}</p>
                            </div>
                          )}
                          {p.placement && (
                            <div className="ml-auto text-center">
                              <p className="text-xs text-gray-500">Colocação</p>
                              <p className={`font-black text-lg ${p.placement === 1 ? 'text-yellow-400' : p.placement === 2 ? 'text-gray-300' : p.placement === 3 ? 'text-amber-600' : 'text-white'}`}>
                                {p.placement === 1 ? '🥇' : p.placement === 2 ? '🥈' : p.placement === 3 ? '🥉' : `${p.placement}º`}
                              </p>
                            </div>
                          )}
                          {p.entryFeeInReais > 0 && (
                            <div className="ml-auto text-right">
                              <p className="text-xs text-gray-500">Taxa</p>
                              <p className="text-sm text-accent-gold font-semibold">R$ {p.entryFeeInReais.toFixed(2).replace('.', ',')}</p>
                            </div>
                          )}
                        </div>
                      </div>
                    )
                  })
                )}
              </div>
            )}

            {/* ── Tab: Crediário / Dívida ───────────────────────── */}
            {tab === 'crediario' && (
              <div className="space-y-3">
                <div className="card">
                {crediario ? (
                  <div className="space-y-4">
                    {/* Saldo destaque */}
                    <div className="text-center py-4">
                      <p className="text-xs text-gray-500 mb-1">Você deve ao Maikon</p>
                      <p className={`text-5xl font-black ${crediario.vencido ? 'text-red-400' : 'text-orange-400'}`}>
                        R$ {crediario.saldoRestanteEmReais.toFixed(2).replace('.', ',')}
                      </p>
                      {crediario.vencido && (
                        <div className="flex items-center justify-center gap-1.5 mt-3 text-sm text-red-400 bg-red-500/10 border border-red-500/20 rounded-lg px-4 py-2">
                          <AlertCircle className="w-4 h-4 shrink-0" />
                          Em atraso! Fale com o Maikon.
                        </div>
                      )}
                    </div>

                    {/* Resumo */}
                    <div className="border-t border-surface-500 pt-3 space-y-2 text-sm">
                      <div className="flex justify-between text-gray-400">
                        <span>Total da dívida</span>
                        <span className="text-white">R$ {crediario.valorEmReais.toFixed(2).replace('.', ',')}</span>
                      </div>
                      <div className="flex justify-between text-gray-400">
                        <span>Já pago</span>
                        <span className="text-emerald-400">R$ {crediario.valorPagoEmReais.toFixed(2).replace('.', ',')}</span>
                      </div>
                      <div className="flex items-center gap-1.5 text-gray-500 text-xs mt-1">
                        <CalendarClock className="w-3.5 h-3.5 shrink-0" />
                        Vencimento: {new Date(crediario.dataVencimento).toLocaleDateString('pt-BR', {
                          day: '2-digit', month: 'long', year: 'numeric',
                        })}
                      </div>
                    </div>

                    {/* Itens da dívida */}
                    {crediario.itensComanda.length > 0 && (
                      <div className="border-t border-surface-500 pt-3">
                        <p className="text-xs text-gray-500 mb-2 font-medium">O que gerou a dívida</p>
                        <div className="space-y-2">
                          {crediario.itensComanda.map((item, i) => (
                            <div key={i} className="flex items-center justify-between">
                              <div className="min-w-0">
                                <p className="text-sm text-white truncate">{item.itemName}</p>
                                <p className="text-xs text-gray-500">{item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}</p>
                              </div>
                              <span className="text-sm font-semibold text-orange-400 shrink-0 ml-3">
                                R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="text-center py-8">
                    <CheckCircle className="w-12 h-12 text-emerald-400 mx-auto mb-3" />
                    <p className="text-emerald-400 font-bold">Sem dívidas em aberto</p>
                    <p className="text-gray-600 text-sm mt-1">Você está em dia com o Maikon! 🎉</p>
                  </div>
                )}
                </div>

              {/* Histórico de crediários quitados */}
              {crediariospagos.length > 0 && (
                <div className="space-y-2">
                  <p className="text-xs text-gray-500 uppercase font-semibold tracking-wider px-1">
                    Parcelas quitadas
                  </p>
                  {crediariospagos.map(c => (
                    <div key={c.id} className="card py-3">
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <CheckCircle className="w-4 h-4 text-emerald-400 shrink-0" />
                          <div>
                            <p className="text-sm font-semibold text-white">
                              R$ {c.valorEmReais.toFixed(2).replace('.', ',')}
                            </p>
                            <p className="text-xs text-gray-500">
                              Quitado em {c.dataPagamento
                                ? new Date(c.dataPagamento).toLocaleDateString('pt-BR')
                                : '—'}
                            </p>
                          </div>
                        </div>
                        {c.observacao && (
                          <p className="text-xs text-gray-600 text-right max-w-[120px] truncate">{c.observacao}</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
