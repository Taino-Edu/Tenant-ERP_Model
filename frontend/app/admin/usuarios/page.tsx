'use client'
import { useEffect, useState, useCallback } from 'react'
import { userApi, crediarioApi, analyticsApi, CrediariosDto, UserSummary, ClienteInsightDto } from '@/lib/api'
import toast from 'react-hot-toast'
import { Users, Search, Star, Plus, Phone, CreditCard, Clock, AlertCircle, Loader2, Wallet, Minus, UserPlus, KeyRound, X, UserX } from 'lucide-react'
import Link from 'next/link'

// ── Modal: Novo Cliente ───────────────────────────────────────────────────────
function NovoClienteModal({ onClose, onSuccess }: { onClose: () => void; onSuccess: (u: UserSummary) => void }) {
  const [nome, setNome]       = useState('')
  const [cpf, setCpf]         = useState('')
  const [whats, setWhats]     = useState('')
  const [email, setEmail]     = useState('')
  const [senha, setSenha]     = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!nome.trim()) { toast.error('Nome é obrigatório'); return }
    setLoading(true)
    try {
      const { data } = await userApi.adminCreate({
        name: nome.trim(),
        cpf: cpf.trim() || undefined,
        whatsApp: whats.trim() || undefined,
        email: email.trim() || undefined,
        password: senha || undefined,
      })
      toast.success(`Cliente ${data.name} criado com sucesso!`)
      onSuccess(data)
      onClose()
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Erro ao criar cliente')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <UserPlus className="w-5 h-5 text-brand-400" /> Novo Cliente
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <div>
            <label className="label">Nome completo *</label>
            <input className="input" placeholder="João da Silva" value={nome} onChange={e => setNome(e.target.value)} required />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">CPF</label>
              <input className="input" placeholder="12345678901" value={cpf} onChange={e => setCpf(e.target.value)} maxLength={11} />
            </div>
            <div>
              <label className="label">WhatsApp</label>
              <input className="input" placeholder="5517999999999" value={whats} onChange={e => setWhats(e.target.value)} maxLength={20} />
            </div>
          </div>
          <div>
            <label className="label">E-mail</label>
            <input type="email" className="input" placeholder="joao@email.com" value={email} onChange={e => setEmail(e.target.value)} />
          </div>
          <div>
            <label className="label">Senha inicial (opcional)</label>
            <input type="password" className="input" placeholder="Mínimo 8 caracteres" value={senha} onChange={e => setSenha(e.target.value)} minLength={8} />
            <p className="text-xs text-gray-400 mt-1">Se não informada, o cliente precisará redefinir via e-mail.</p>
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={loading} className="btn-primary flex-1 justify-center">
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Criando...</> : <><UserPlus className="w-4 h-4" /> Criar Cliente</>}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal: Redefinir Senha ────────────────────────────────────────────────────
function RedefinirSenhaModal({ user, onClose }: { user: UserSummary; onClose: () => void }) {
  const [senha, setSenha]     = useState('')
  const [confirma, setConfirma] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (senha.length < 8) { toast.error('Mínimo 8 caracteres'); return }
    if (senha !== confirma) { toast.error('As senhas não coincidem'); return }
    setLoading(true)
    try {
      await userApi.adminResetPassword(user.id, senha)
      toast.success(`Senha de ${user.name} redefinida!`)
      onClose()
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Erro ao redefinir senha')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-sm shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <KeyRound className="w-5 h-5 text-brand-400" /> Redefinir Senha
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <p className="text-sm text-gray-400">Definindo nova senha para <strong className="text-white">{user.name}</strong>.</p>
          <div>
            <label className="label">Nova senha</label>
            <input type="password" className="input" placeholder="Mínimo 8 caracteres" value={senha} onChange={e => setSenha(e.target.value)} required minLength={8} />
          </div>
          <div>
            <label className="label">Confirmar senha</label>
            <input type="password" className="input" placeholder="Repita a senha" value={confirma} onChange={e => setConfirma(e.target.value)} required minLength={8} />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={loading} className="btn-primary flex-1 justify-center">
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : <><KeyRound className="w-4 h-4" /> Redefinir</>}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default function UsuariosPage() {
  const [users, setUsers]           = useState<UserSummary[]>([])
  const [crediarios, setCrediarios] = useState<Record<string, CrediariosDto>>({})
  const [search, setSearch]         = useState('')
  const [loading, setLoading]       = useState(true)
  const [selected, setSelected]     = useState<UserSummary | null>(null)
  const [points, setPoints]         = useState('')
  const [reason, setReason]         = useState('')
  const [adding, setAdding]         = useState(false)
  const [balanceAmount, setBalanceAmount] = useState('')
  const [balanceReason, setBalanceReason] = useState('')
  const [adjustingBalance, setAdjustingBalance] = useState(false)
  const [showNovoCliente, setShowNovoCliente] = useState(false)
  const [showRedefinirSenha, setShowRedefinirSenha] = useState(false)
  const [tabUsuarios, setTabUsuarios]         = useState<'todos' | 'inativos'>('todos')
  const [insights, setInsights]               = useState<ClienteInsightDto[]>([])

  const fetchUsers = useCallback(async (q?: string) => {
    setLoading(true)
    try {
      const [usersRes, credRes] = await Promise.all([
        userApi.list(q),
        crediarioApi.list('Aberto'),
      ])
      setUsers(usersRes.data)
      // Mapeia userId → crediário aberto
      const map: Record<string, CrediariosDto> = {}
      for (const c of credRes.data) map[c.userId] = c
      setCrediarios(map)
    } catch {
      toast.error('Erro ao carregar usuários')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchUsers()
    analyticsApi.clientes().then(r => setInsights(r.data)).catch(() => {})
  }, [fetchUsers])

  // Busca ao digitar (debounce simples)
  useEffect(() => {
    const t = setTimeout(() => fetchUsers(search || undefined), 400)
    return () => clearTimeout(t)
  }, [search, fetchUsers])

  async function handleAddPoints() {
    if (!selected || !points || Number(points) <= 0) return
    setAdding(true)
    try {
      const { data } = await userApi.addPoints(selected.id, Number(points), reason || undefined)
      toast.success(`${points} pontos Maikon adicionados para ${selected.name}!`)
      setUsers(prev => prev.map(u => u.id === data.id ? data : u))
      setSelected(data)
      setPoints('')
      setReason('')
    } catch {
      toast.error('Erro ao adicionar pontos')
    } finally {
      setAdding(false)
    }
  }

  async function handleAdjustBalance(isCredit: boolean) {
    if (!selected || !balanceAmount || Number(balanceAmount) <= 0) return
    setAdjustingBalance(true)
    try {
      const cents = Math.round(Number(balanceAmount) * 100) * (isCredit ? 1 : -1)
      const { data } = await userApi.adjustBalance(selected.id, cents, balanceReason || undefined)
      toast.success(isCredit
        ? `R$ ${Number(balanceAmount).toFixed(2)} creditado para ${selected.name}!`
        : `R$ ${Number(balanceAmount).toFixed(2)} debitado de ${selected.name}!`)
      setUsers(prev => prev.map(u => u.id === data.id ? data : u))
      setSelected(data)
      setBalanceAmount('')
      setBalanceReason('')
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao ajustar saldo')
    } finally {
      setAdjustingBalance(false)
    }
  }

  // ── Derivados para aba inativos ───────────────────────────────────────────────
  const inativoIds = new Set(insights.filter(i => i.inativo30).map(i => i.userId))
  const insightMap = new Map(insights.map(i => [i.userId, i]))
  const listaFiltrada = tabUsuarios === 'inativos'
    ? users.filter(u => inativoIds.has(u.id))
    : users

  return (
    <div className="p-4 sm:p-6 h-full">
      {/* Modals */}
      {showNovoCliente && (
        <NovoClienteModal
          onClose={() => setShowNovoCliente(false)}
          onSuccess={u => setUsers(prev => [u, ...prev])}
        />
      )}
      {showRedefinirSenha && selected && (
        <RedefinirSenhaModal
          user={selected}
          onClose={() => setShowRedefinirSenha(false)}
        />
      )}

      {/* Header */}
      <div className="mb-4 sm:mb-6 flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Users className="w-6 h-6 text-brand-400" /> Clientes & Cashback
          </h1>
          <p className="text-gray-400 text-sm mt-0.5">Gerencie clientes, pontos Maikon e cashback</p>
        </div>
        <button
          onClick={() => setShowNovoCliente(true)}
          className="btn-primary whitespace-nowrap"
        >
          <UserPlus className="w-4 h-4" /> Novo Cliente
        </button>
      </div>

      <div className="flex flex-col md:flex-row gap-4 sm:gap-6 md:h-[calc(100vh-180px)]">

        {/* ── Lista de clientes ──────────────────────────────────────────────── */}
        <div className="flex-1 flex flex-col min-w-0">

          {/* Tabs + Busca */}
          <div className="flex items-center gap-3 mb-4">
            <div className="flex gap-1 bg-surface-800 p-1 rounded-lg shrink-0">
              <button
                onClick={() => setTabUsuarios('todos')}
                className={`px-3 py-1.5 rounded-md text-xs font-medium transition-all ${tabUsuarios === 'todos' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200'}`}
              >
                <Users className="w-3.5 h-3.5 inline mr-1" />Todos
              </button>
              <button
                onClick={() => setTabUsuarios('inativos')}
                className={`px-3 py-1.5 rounded-md text-xs font-medium transition-all flex items-center gap-1 ${tabUsuarios === 'inativos' ? 'bg-amber-600 text-white' : 'text-gray-400 hover:text-gray-200'}`}
              >
                <UserX className="w-3.5 h-3.5" />
                Inativos
                {insights.filter(i => i.inativo30).length > 0 && (
                  <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-bold ${tabUsuarios === 'inativos' ? 'bg-white/20' : 'bg-amber-500/20 text-amber-400'}`}>
                    {insights.filter(i => i.inativo30).length}
                  </span>
                )}
              </button>
            </div>
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                className="input pl-9 w-full"
                placeholder="Buscar por nome, CPF ou WhatsApp..."
                value={search}
                onChange={e => setSearch(e.target.value)}
              />
            </div>
          </div>

          {loading ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : listaFiltrada.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center text-gray-500 gap-3">
              {tabUsuarios === 'inativos'
                ? <><UserX className="w-10 h-10 text-amber-600/40" /><p className="text-sm">Nenhum cliente inativo 🎉</p></>
                : <><Users className="w-10 h-10 text-gray-400" /><p className="text-sm">Nenhum cliente encontrado</p></>
              }
            </div>
          ) : (
            <div className="flex-1 overflow-y-auto space-y-2 pr-1">
              {listaFiltrada.map(u => {
                const insight = insightMap.get(u.id)
                return (
                <button
                  key={u.id}
                  onClick={() => setSelected(u)}
                  className={`w-full card text-left hover:border-brand-500/40 transition-all duration-150 ${
                    selected?.id === u.id ? 'border-brand-500/60 bg-brand-600/5' : ''
                  } ${insight?.inativo30 ? 'border-amber-500/20' : ''}`}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="font-medium text-white text-sm">{u.name}</p>
                        {insight?.inativo30 && (
                          <span className="text-[10px] font-medium px-1.5 py-0.5 rounded-full bg-amber-500/15 text-amber-400 border border-amber-500/20 shrink-0">
                            inativo
                          </span>
                        )}
                      </div>
                      <div className="flex flex-wrap gap-3 mt-1">
                        {u.cpf && (
                          <span className="flex items-center gap-1 text-xs text-gray-500">
                            <CreditCard className="w-3 h-3" /> {u.cpf}
                          </span>
                        )}
                        {u.whatsApp && (
                          <a
                            href={`https://wa.me/${u.whatsApp.replace(/\D/g, '')}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            onClick={e => e.stopPropagation()}
                            className="flex items-center gap-1 text-xs text-emerald-400 hover:text-emerald-300 transition-colors"
                          >
                            <svg viewBox="0 0 24 24" className="w-3 h-3 fill-current shrink-0">
                              <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z"/>
                            </svg>
                            {u.whatsApp}
                          </a>
                        )}
                        {insight && (
                          <span className="flex items-center gap-1 text-xs text-gray-400">
                            <Clock className="w-3 h-3" />
                            {insight.numVisitas} visita{insight.numVisitas !== 1 ? 's' : ''}
                            {insight.ultimaVisita && ` · última: ${new Date(insight.ultimaVisita).toLocaleDateString('pt-BR')}`}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <PointsBadge user={u} />
                      {u.balanceInCents > 0 && (
                        <span className="flex items-center gap-1 text-xs text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 px-2 py-0.5 rounded-full shrink-0 font-bold">
                          <Wallet className="w-3 h-3" /> R$ {(u.balanceInCents / 100).toFixed(2).replace('.', ',')}
                        </span>
                      )}
                      {crediarios[u.id] && <CreditBadge crediario={crediarios[u.id]} />}
                    </div>
                  </div>
                </button>
              )
            })}
            </div>
          )}
        </div>

        {/* ── Painel de pontos ──────────────────────────────────────────────── */}
        <div className="w-full md:w-80 md:shrink-0">
          {!selected ? (
            <div className="card h-full flex flex-col items-center justify-center text-gray-400 gap-3">
              <Star className="w-10 h-10" />
              <p className="text-sm text-center">Selecione um cliente<br />para gerenciar pontos Maikon e cashback</p>
            </div>
          ) : (
            <div className="card space-y-5">
              {/* Dados do cliente */}
              <div>
                <p className="text-xs text-gray-500 mb-1">Cliente selecionado</p>
                <p className="font-bold text-white text-lg">{selected.name}</p>
                {selected.cpf && <p className="text-xs text-gray-500">CPF: {selected.cpf}</p>}
                {selected.whatsApp && (
                  <a
                    href={`https://wa.me/${selected.whatsApp.replace(/\D/g, '')}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center gap-1.5 text-xs text-emerald-400 hover:text-emerald-300 transition-colors mt-0.5"
                  >
                    <svg viewBox="0 0 24 24" className="w-3 h-3 fill-current shrink-0">
                      <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z"/>
                    </svg>
                    {selected.whatsApp}
                  </a>
                )}
                {selected.email && <p className="text-xs text-gray-500 mt-0.5">{selected.email}</p>}
                <button
                  onClick={() => setShowRedefinirSenha(true)}
                  className="btn-secondary mt-2 text-xs py-1.5 px-3 w-full justify-center"
                >
                  <KeyRound className="w-3.5 h-3.5" /> Redefinir Senha
                </button>
              </div>

              {/* Pontos Maikon */}
              <div className="bg-surface-800 rounded-xl p-4 text-center">
                <p className="text-xs text-gray-500 mb-1">Pontos Maikon</p>
                <p className="text-4xl font-black text-accent-gold">{selected.pointsBalance}</p>
                <p className="text-xs text-gray-500 mt-0.5">pontos</p>

                {selected.pointsExpired && (
                  <div className="flex items-center justify-center gap-1.5 mt-2 text-xs text-red-400">
                    <AlertCircle className="w-3 h-3" />
                    Pontos expirados
                  </div>
                )}
                {!selected.pointsExpired && selected.pointsExpiresAt && (
                  <div className="flex items-center justify-center gap-1.5 mt-2 text-xs text-gray-500">
                    <Clock className="w-3 h-3" />
                    Expira {new Date(selected.pointsExpiresAt).toLocaleDateString('pt-BR')}
                  </div>
                )}
                {selected.pointsBalance === 0 && !selected.pointsExpiresAt && (
                  <p className="text-xs text-gray-400 mt-2">Sem pontos Maikon cadastrados</p>
                )}
              </div>

              {/* Adicionar Pontos Maikon */}
              <div className="space-y-3">
                <p className="text-sm font-semibold text-white">Adicionar Pontos Maikon</p>
                <div>
                  <label className="label text-xs">Quantidade de pontos</label>
                  <input
                    type="number"
                    className="input"
                    placeholder="Ex: 100"
                    min={1}
                    value={points}
                    onChange={e => setPoints(e.target.value)}
                  />
                </div>
                <div>
                  <label className="label text-xs">Motivo (opcional)</label>
                  <input
                    className="input"
                    placeholder="Ex: Campeonato de Pokémon"
                    value={reason}
                    onChange={e => setReason(e.target.value)}
                    maxLength={255}
                  />
                </div>
                <button
                  onClick={handleAddPoints}
                  disabled={!points || Number(points) <= 0 || adding}
                  className="btn-primary w-full justify-center disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {adding
                    ? <><Loader2 className="w-4 h-4 animate-spin" /> Adicionando...</>
                    : <><Plus className="w-4 h-4" /> Adicionar Pontos Maikon</>
                  }
                </button>
                <p className="text-xs text-gray-400 text-center">
                  Validade renovada para 30 dias após cada adição
                </p>
              </div>

              {/* Cashback */}
              <div className="space-y-3 border-t border-surface-500 pt-4">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-semibold text-white flex items-center gap-1.5">
                    <Wallet className="w-4 h-4 text-emerald-400" /> Cashback
                  </p>
                  <span className="text-lg font-bold text-emerald-400">
                    R$ {((selected.balanceInCents ?? 0) / 100).toFixed(2).replace('.', ',')}
                  </span>
                </div>
                <div>
                  <label className="label text-xs">Valor (R$)</label>
                  <input
                    type="number"
                    className="input"
                    placeholder="Ex: 20,00"
                    min={0.01}
                    step={0.01}
                    value={balanceAmount}
                    onChange={e => setBalanceAmount(e.target.value)}
                  />
                </div>
                <div>
                  <label className="label text-xs">Motivo (opcional)</label>
                  <input
                    className="input"
                    placeholder="Ex: Recarga de saldo"
                    value={balanceReason}
                    onChange={e => setBalanceReason(e.target.value)}
                    maxLength={255}
                  />
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    onClick={() => handleAdjustBalance(true)}
                    disabled={!balanceAmount || Number(balanceAmount) <= 0 || adjustingBalance}
                    className="btn-primary justify-center text-sm py-2 disabled:opacity-50"
                  >
                    {adjustingBalance ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                    Creditar
                  </button>
                  <button
                    onClick={() => handleAdjustBalance(false)}
                    disabled={!balanceAmount || Number(balanceAmount) <= 0 || adjustingBalance || (selected.balanceInCents ?? 0) <= 0}
                    className="btn-secondary justify-center text-sm py-2 disabled:opacity-50 hover:border-red-500/40 hover:text-red-400"
                  >
                    {adjustingBalance ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Minus className="w-3.5 h-3.5" />}
                    Debitar
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function PointsBadge({ user }: { user: UserSummary }) {
  if (user.pointsExpired)
    return (
      <span className="flex items-center gap-1 text-xs text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full shrink-0">
        <AlertCircle className="w-3 h-3" /> Expirado
      </span>
    )
  if (user.pointsBalance > 0)
    return (
      <span className="flex items-center gap-1 text-xs text-amber-400 bg-amber-500/10 border border-amber-500/20 px-2 py-0.5 rounded-full shrink-0 font-bold">
        <Star className="w-3 h-3" /> {user.pointsBalance} pts Maikon
      </span>
    )
  return (
    <span className="text-xs text-gray-400">0 pts Maikon</span>
  )
}

function CreditBadge({ crediario }: { crediario: CrediariosDto }) {
  const label = crediario.vencido ? 'Crediário Vencido' : 'Crediário Aberto'
  const color = crediario.vencido
    ? 'text-red-400 bg-red-500/10 border-red-500/20'
    : 'text-orange-400 bg-orange-500/10 border-orange-500/20'

  return (
    <Link href="/admin/crediario">
      <span className={`flex items-center gap-1 text-xs border px-2 py-0.5 rounded-full shrink-0 ${color}`}>
        <CreditCard className="w-3 h-3" /> {label}
      </span>
    </Link>
  )
}
