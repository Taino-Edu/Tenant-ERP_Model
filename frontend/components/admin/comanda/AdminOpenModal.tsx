'use client'
import { useEffect, useState } from 'react'
import { userApi, UserSummary } from '@/lib/api'
import { FolderOpen, XCircle, Search, Loader2, CheckCircle } from 'lucide-react'
import clsx from 'clsx'

// ── Modal: Admin abre comanda por um cliente ──────────────────────────────────

export function AdminOpenModal({
  onConfirm, onCancel,
}: {
  onConfirm: (userId: string, tableIdentifier: string) => Promise<void>
  onCancel:  () => void
}) {
  const [search,   setSearch]   = useState('')
  const [users,    setUsers]    = useState<UserSummary[]>([])
  const [selected, setSelected] = useState<UserSummary | null>(null)
  const [table,    setTable]    = useState('')
  const [loading,  setLoading]  = useState(false)
  const [saving,   setSaving]   = useState(false)

  useEffect(() => {
    setLoading(true)
    userApi.list(search || undefined)
      .then(r => setUsers(r.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [search])

  async function handleConfirm() {
    if (!selected) return
    setSaving(true)
    try {
      await onConfirm(selected.id, table.trim())
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onCancel}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-sm flex flex-col max-h-[80vh]" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-surface-500">
          <h3 className="font-semibold text-white flex items-center gap-2">
            <FolderOpen className="w-4 h-4 text-brand-400" /> Abrir Comanda
          </h3>
          <button onClick={onCancel} className="text-gray-500 hover:text-gray-300"><XCircle className="w-5 h-5" /></button>
        </div>

        <div className="p-3 border-b border-surface-500">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              autoFocus
              className="input pl-9 text-sm"
              placeholder="Buscar cliente..."
              value={search}
              onChange={e => { setSearch(e.target.value); setSelected(null) }}
            />
          </div>
        </div>

        <div className="flex-1 overflow-y-auto">
          {loading ? (
            <div className="flex justify-center py-8"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
          ) : users.length === 0 ? (
            <p className="text-center text-gray-500 text-sm py-8">Nenhum cliente encontrado</p>
          ) : (
            users.map(u => (
              <button
                key={u.id}
                onClick={() => setSelected(u)}
                className={clsx(
                  'w-full flex items-center gap-3 px-4 py-3 text-left transition-colors text-white',
                  selected?.id === u.id
                    ? 'bg-brand-500/25 border-l-2 border-brand-400'
                    : 'hover:bg-surface-700'
                )}
              >
                <div className="w-8 h-8 rounded-full bg-brand-600/20 flex items-center justify-center text-brand-400 font-bold text-sm shrink-0">
                  {u.name.charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-white truncate">{u.name}</p>
                  {u.email && <p className="text-xs text-gray-400 truncate">{u.email}</p>}
                </div>
                {selected?.id === u.id && <CheckCircle className="w-4 h-4 text-brand-400 ml-auto shrink-0" />}
              </button>
            ))
          )}
        </div>

        {selected && (
          <div className="p-3 border-t border-surface-500 space-y-3">
            <div>
              <label className="label text-xs">Mesa / Identificador (opcional)</label>
              <input
                className="input text-sm"
                placeholder="Ex: Mesa 3, Balcão..."
                value={table}
                onChange={e => setTable(e.target.value)}
              />
            </div>
            <button
              onClick={handleConfirm}
              disabled={saving}
              className="btn-primary w-full justify-center"
            >
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <FolderOpen className="w-4 h-4" />}
              Abrir para {selected.name.split(' ')[0]}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
