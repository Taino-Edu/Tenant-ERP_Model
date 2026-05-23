'use client'
import { useEffect, useState, useCallback } from 'react'
import { userApi, crediarioApi, CrediariosDto, UserSummary } from '@/lib/api'
import toast from 'react-hot-toast'
import { Users, Search, Star, Plus, Phone, CreditCard, Clock, AlertCircle, Loader2, Wallet, Minus } from 'lucide-react'
import Link from 'next/link'

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

  useEffect(() => { fetchUsers() }, [fetchUsers])

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

  return (
    <div className="p-6 h-full">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-white flex items-center gap-2">
          <Users className="w-6 h-6 text-brand-400" /> Clientes & Cashback
        </h1>
        <p className="text-gray-400 text-sm mt-0.5">Gerencie clientes, pontos Maikon e cashback</p>
      </div>

      <div className="flex gap-6 h-[calc(100vh-180px)]">

        {/* ── Lista de clientes ──────────────────────────────────────────────── */}
        <div className="flex-1 flex flex-col min-w-0">
          <div className="relative mb-4">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              className="input pl-9"
              placeholder="Buscar por nome, CPF ou WhatsApp..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>

          {loading ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : users.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center text-gray-500 gap-3">
              <Users className="w-10 h-10 text-gray-600" />
              <p className="text-sm">Nenhum cliente encontrado</p>
            </div>
          ) : (
            <div className="flex-1 overflow-y-auto space-y-2 pr-1">
              {users.map(u => (
                <button
                  key={u.id}
                  onClick={() => setSelected(u)}
                  className={`w-full card text-left hover:border-brand-500/40 transition-all duration-150 ${
                    selected?.id === u.id ? 'border-brand-500/60 bg-brand-600/5' : ''
                  }`}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <p className="font-medium text-white text-sm">{u.name}</p>
                      <div className="flex flex-wrap gap-3 mt-1">
                        {u.cpf && (
                          <span className="flex items-center gap-1 text-xs text-gray-500">
                            <CreditCard className="w-3 h-3" /> {u.cpf}
                          </span>
                        )}
                        {u.whatsApp && (
                          <span className="flex items-center gap-1 text-xs text-gray-500">
                            <Phone className="w-3 h-3" /> {u.whatsApp}
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
              ))}
            </div>
          )}
        </div>

        {/* ── Painel de pontos ──────────────────────────────────────────────── */}
        <div className="w-80 shrink-0">
          {!selected ? (
            <div className="card h-full flex flex-col items-center justify-center text-gray-600 gap-3">
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
                {selected.whatsApp && <p className="text-xs text-gray-500">WhatsApp: {selected.whatsApp}</p>}
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
                  <p className="text-xs text-gray-600 mt-2">Sem pontos Maikon cadastrados</p>
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
                <p className="text-xs text-gray-600 text-center">
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
    <span className="text-xs text-gray-600">0 pts Maikon</span>
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
