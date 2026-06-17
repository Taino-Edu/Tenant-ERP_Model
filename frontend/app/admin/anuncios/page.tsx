'use client'
import { useEffect, useState } from 'react'
import { announcementApi, AnnouncementDto } from '@/lib/api'
import toast from 'react-hot-toast'
import { Plus, Trash2, Eye, EyeOff, Megaphone, Edit2, Loader2, ImageIcon, X } from 'lucide-react'
import clsx from 'clsx'
import ImageUpload from '@/components/admin/ImageUpload'

const TYPE_LABELS: Record<string, string> = {
  Aviso:    '📢 Aviso',
  Destaque: '⭐ Destaque',
}

const TYPE_COLORS: Record<string, string> = {
  Aviso:    'bg-amber-500/20 text-amber-400 border-amber-500/30',
  Destaque: 'bg-brand-500/20 text-brand-300 border-brand-500/30',
}

/* ── Banner do Hero ──────────────────────────────────────────────────────── */
function HeroBannerCard({
  banner, onUpload, onDelete, onToggle,
}: {
  banner:   AnnouncementDto | null
  onUpload: (url: string) => Promise<void>
  onDelete: () => Promise<void>
  onToggle: () => Promise<void>
}) {
  const [uploading, setUploading] = useState(false)
  const [deleting,  setDeleting]  = useState(false)

  async function handleUpload(url: string) {
    setUploading(true)
    try { await onUpload(url) } finally { setUploading(false) }
  }

  async function handleDelete() {
    if (!confirm('Remover o banner do hero?')) return
    setDeleting(true)
    try { await onDelete() } finally { setDeleting(false) }
  }

  return (
    <div className="card space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-bold text-white flex items-center gap-2">
            <ImageIcon className="w-5 h-5 text-blue-400" /> Banner do Hero
          </h2>
          <p className="text-xs text-gray-500 mt-0.5">
            Imagem de fundo da seção principal da landing page. Recomendado: 1920×600px.
          </p>
        </div>
        {banner && (
          <div className="flex items-center gap-2">
            <button onClick={onToggle} className="btn-secondary p-2" title={banner.isActive ? 'Desativar' : 'Ativar'}>
              {banner.isActive ? <Eye className="w-4 h-4" /> : <EyeOff className="w-4 h-4" />}
            </button>
            <button onClick={handleDelete} disabled={deleting} className="btn-danger p-2" title="Remover banner">
              {deleting ? <Loader2 className="w-4 h-4 animate-spin" /> : <X className="w-4 h-4" />}
            </button>
          </div>
        )}
      </div>

      {banner?.imageUrl ? (
        <div className="space-y-3">
          {/* Preview com overlay igual ao site */}
          <div className="relative rounded-xl overflow-hidden" style={{ height: 180 }}>
            <img src={banner.imageUrl} alt="Banner atual" className="w-full h-full object-cover" />
            <div className="absolute inset-0" style={{ background: 'rgba(0,0,0,0.60)' }} />
            <div className="absolute inset-0 flex flex-col items-center justify-center gap-1 pointer-events-none">
              <span className="text-xs text-white/60 uppercase tracking-widest">Pré-visualização</span>
              <span className="text-white font-black text-lg">Santuário Nerd</span>
              <span className="text-white/70 text-xs">Produtos, torneios e a melhor experiência TCG da região.</span>
            </div>
            {!banner.isActive && (
              <div className="absolute top-2 right-2 bg-black/60 text-white text-xs px-2 py-1 rounded-lg">
                Inativo (não aparece no site)
              </div>
            )}
          </div>
          {/* Trocar imagem */}
          <ImageUpload
            label="Trocar imagem do banner"
            hint="Recomendado: 1920×600px"
            currentUrl={banner.imageUrl}
            onUpload={handleUpload}
          />
        </div>
      ) : (
        <div className="border-2 border-dashed border-surface-500 rounded-xl p-8 text-center space-y-3">
          <ImageIcon className="w-10 h-10 text-gray-500 mx-auto" />
          <p className="text-gray-400 text-sm">Nenhum banner ativo. O hero exibe o gradiente padrão.</p>
          <ImageUpload
            label="Enviar banner"
            hint="Recomendado: 1920×600px"
            currentUrl={null}
            onUpload={handleUpload}
          />
        </div>
      )}
    </div>
  )
}

/* ── Formulário de anúncio (Aviso / Destaque) ────────────────────────────── */
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
  const [type,      setType]      = useState(initial?.type && initial.type !== 'Banner' ? initial.type : 'Aviso')
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
            <option value="Aviso">📢 Aviso</option>
            <option value="Destaque">⭐ Destaque</option>
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
            label="Imagem do anúncio (opcional)"
            hint="600×300px"
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

/* ── Página principal ────────────────────────────────────────────────────── */
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

  const banner = items.find(a => a.type === 'Banner') ?? null
  const announcements = items.filter(a => a.type !== 'Banner')

  /* ── Banner actions ── */
  async function handleBannerUpload(url: string) {
    if (banner) {
      await announcementApi.update(banner.id, { imageUrl: url, isActive: true })
      toast.success('Banner atualizado!')
    } else {
      await announcementApi.create({
        title: 'Banner Hero', body: null, imageUrl: url, linkUrl: null,
        type: 'Banner', isActive: true, expiresAt: null,
      } as Parameters<typeof announcementApi.create>[0])
      toast.success('Banner ativado!')
    }
    load()
  }

  async function handleBannerDelete() {
    if (!banner) return
    await announcementApi.delete(banner.id)
    toast.success('Banner removido.')
    load()
  }

  async function handleBannerToggle() {
    if (!banner) return
    await announcementApi.update(banner.id, { isActive: !banner.isActive })
    toast.success(banner.isActive ? 'Banner desativado.' : 'Banner ativado.')
    load()
  }

  /* ── Announcement actions ── */
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
    <div className="p-4 sm:p-6 space-y-6">

      {/* ── Cabeçalho ── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Megaphone className="w-6 h-6 text-brand-400" /> Anúncios
          </h1>
          <p className="text-gray-500 text-sm mt-0.5">Banner do hero e avisos exibidos na landing page</p>
        </div>
      </div>

      {/* ── Banner do Hero (card dedicado) ── */}
      {loading ? (
        <div className="card flex justify-center py-10">
          <div className="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : (
        <HeroBannerCard
          banner={banner}
          onUpload={handleBannerUpload}
          onDelete={handleBannerDelete}
          onToggle={handleBannerToggle}
        />
      )}

      {/* ── Avisos e Destaques ── */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="font-bold text-white flex items-center gap-2">
            <Megaphone className="w-4 h-4 text-brand-400" /> Avisos &amp; Destaques
          </h2>
          {!form && (
            <button onClick={() => setForm('new')} className="btn-primary">
              <Plus className="w-4 h-4" /> Novo anúncio
            </button>
          )}
        </div>

        {form === 'new' && (
          <AnnouncementForm onSave={handleCreate} onCancel={() => setForm(null)} />
        )}

        {!loading && announcements.length === 0 && !form && (
          <div className="card text-center py-10">
            <Megaphone className="w-10 h-10 text-gray-400 mx-auto mb-3" />
            <p className="text-gray-500">Nenhum aviso ou destaque criado ainda.</p>
          </div>
        )}

        <div className="space-y-3">
          {announcements.map(item => (
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
      </div>
    </div>
  )
}
