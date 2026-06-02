'use client'
import { useEffect, useState } from 'react'
import { announcementApi, AnnouncementDto, ANNOUNCEMENT_TYPES } from '@/lib/api'
import toast from 'react-hot-toast'
import { Plus, Trash2, Eye, EyeOff, Megaphone, Edit2, Loader2 } from 'lucide-react'
import clsx from 'clsx'
import ImageUpload from '@/components/admin/ImageUpload'

const TYPE_LABELS: Record<string, string> = {
  Banner:   '🖼 Banner',
  Aviso:    '📢 Aviso',
  Destaque: '⭐ Destaque',
}

const TYPE_COLORS: Record<string, string> = {
  Banner:   'bg-blue-500/20 text-blue-400 border-blue-500/30',
  Aviso:    'bg-amber-500/20 text-amber-400 border-amber-500/30',
  Destaque: 'bg-brand-500/20 text-brand-300 border-brand-500/30',
}

function AnnouncementForm({
  initial, onSave, onCancel,
}: {
  initial?: Partial<AnnouncementDto>
  onSave:   (data: Omit<AnnouncementDto, 'id' | 'createdAt'>) => Promise<void>
  onCancel: () => void
}) {
  const [title,     setTitle]     = useState(initial?.title     ?? '')
  const [body,      setBody]      = useState(initial?.body      ?? '')
  const [imageUrl,  setImageUrl]  = useState(initial?.imageUrl  ?? '')
  const [linkUrl,   setLinkUrl]   = useState(initial?.linkUrl   ?? '')
  const [type,      setType]      = useState(initial?.type      ?? 'Aviso')
  const [expiresAt, setExpiresAt] = useState(
    initial?.expiresAt ? new Date(initial.expiresAt).toISOString().slice(0, 16) : ''
  )
  const [saving, setSaving] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!title.trim()) { toast.error('Título obrigatório.'); return }
    setSaving(true)
    try {
      await onSave({
        title:    title.trim(),
        body:     body.trim() || null,
        imageUrl: imageUrl.trim() || null,
        linkUrl:  linkUrl.trim() || null,
        type,
        isActive:  true,
        expiresAt: expiresAt ? new Date(expiresAt).toISOString() : null,
      } as Omit<AnnouncementDto, 'id' | 'createdAt'>)
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="card space-y-4">
      <h3 className="font-semibold text-white">{initial ? 'Editar anúncio' : 'Novo anúncio'}</h3>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label className="label">Tipo</label>
          <select className="input" value={type} onChange={e => setType(e.target.value)}>
            {ANNOUNCEMENT_TYPES.map(t => (
              <option key={t} value={t}>{TYPE_LABELS[t]}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="label">Expira em (opcional)</label>
          <input type="datetime-local" className="input" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} />
        </div>
      </div>

      <div>
        <label className="label">Título *</label>
        <input className="input" placeholder="Ex: Torneio Pokémon sábado!" value={title} onChange={e => setTitle(e.target.value)} required />
      </div>

      <div>
        <label className="label">Texto / Descrição</label>
        <textarea className="input min-h-[80px] resize-y" placeholder="Detalhes do anúncio..." value={body} onChange={e => setBody(e.target.value)} />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <ImageUpload
            label="Imagem (Banner: 1200×400px · Aviso: 600×300px)"
            currentUrl={imageUrl || null}
            onUpload={url => setImageUrl(url)}
          />
        </div>
        <div>
          <label className="label">Link de destino (opcional)</label>
          <input className="input" placeholder="https://..." value={linkUrl} onChange={e => setLinkUrl(e.target.value)} />
        </div>
      </div>

      <div className="flex gap-3 justify-end pt-2">
        <button type="button" onClick={onCancel} className="btn-secondary">Cancelar</button>
        <button type="submit" disabled={saving} className="btn-primary">
          {saving ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : 'Salvar'}
        </button>
      </div>
    </form>
  )
}

export default function AnunciosPage() {
  const [items,   setItems]   = useState<AnnouncementDto[]>([])
  const [loading, setLoading] = useState(true)
  const [form,    setForm]    = useState<'new' | string | null>(null)

  async function load() {
    try {
      const { data } = await announcementApi.all()
      setItems(data)
    } catch {
      toast.error('Erro ao carregar anúncios')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  async function handleCreate(data: Omit<AnnouncementDto, 'id' | 'createdAt'>) {
    await announcementApi.create(data as Parameters<typeof announcementApi.create>[0])
    toast.success('Anúncio criado!')
    setForm(null)
    load()
  }

  async function handleUpdate(id: string, data: Omit<AnnouncementDto, 'id' | 'createdAt'>) {
    await announcementApi.update(id, data as Parameters<typeof announcementApi.update>[1])
    toast.success('Anúncio atualizado!')
    setForm(null)
    load()
  }

  async function handleToggle(item: AnnouncementDto) {
    await announcementApi.update(item.id, { isActive: !item.isActive })
    setItems(prev => prev.map(a => a.id === item.id ? { ...a, isActive: !a.isActive } : a))
  }

  async function handleDelete(id: string) {
    if (!confirm('Remover este anúncio permanentemente?')) return
    await announcementApi.delete(id)
    toast.success('Removido.')
    setItems(prev => prev.filter(a => a.id !== id))
  }

  const editing = typeof form === 'string' ? items.find(a => a.id === form) : undefined

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Megaphone className="w-6 h-6 text-brand-400" /> Anúncios
          </h1>
          <p className="text-gray-500 text-sm mt-0.5">Banners, avisos e destaques exibidos na landing page</p>
        </div>
        {!form && (
          <button onClick={() => setForm('new')} className="btn-primary">
            <Plus className="w-4 h-4" /> Novo anúncio
          </button>
        )}
      </div>

      {form === 'new' && (
        <AnnouncementForm onSave={handleCreate} onCancel={() => setForm(null)} />
      )}

      {loading ? (
        <div className="flex justify-center py-16">
          <div className="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : items.length === 0 ? (
        <div className="card text-center py-12">
          <Megaphone className="w-10 h-10 text-gray-400 mx-auto mb-3" />
          <p className="text-gray-500">Nenhum anúncio criado ainda.</p>
          <p className="text-gray-400 text-sm mt-1">Crie banners, avisos e destaques para a landing page.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {items.map(item => (
            form === item.id ? (
              <AnnouncementForm
                key={item.id}
                initial={item}
                onSave={data => handleUpdate(item.id, data)}
                onCancel={() => setForm(null)}
              />
            ) : (
              <div key={item.id} className={clsx('card flex items-start gap-4', !item.isActive && 'opacity-50')}>
                {item.imageUrl && (
                  <img src={item.imageUrl} alt="" className="w-20 h-14 object-cover rounded-lg shrink-0 bg-surface-600" />
                )}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className={clsx('badge border text-xs', TYPE_COLORS[item.type] ?? 'bg-gray-500/20 text-gray-400 border-gray-500/30')}>
                      {TYPE_LABELS[item.type] ?? item.type}
                    </span>
                    {item.expiresAt && (
                      <span className="text-xs text-gray-500">
                        Expira: {new Date(item.expiresAt).toLocaleDateString('pt-BR')}
                      </span>
                    )}
                  </div>
                  <p className="font-semibold text-white truncate">{item.title}</p>
                  {item.body && <p className="text-sm text-gray-400 line-clamp-1 mt-0.5">{item.body}</p>}
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <button onClick={() => handleToggle(item)} className="btn-secondary p-2" title={item.isActive ? 'Desativar' : 'Ativar'}>
                    {item.isActive ? <Eye className="w-4 h-4" /> : <EyeOff className="w-4 h-4" />}
                  </button>
                  <button onClick={() => setForm(item.id)} className="btn-secondary p-2" title="Editar">
                    <Edit2 className="w-4 h-4" />
                  </button>
                  <button onClick={() => handleDelete(item.id)} className="btn-danger p-2" title="Remover">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )
          ))}
        </div>
      )}
    </div>
  )
}
