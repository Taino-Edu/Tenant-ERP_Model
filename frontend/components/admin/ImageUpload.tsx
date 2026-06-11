'use client'
// =============================================================================
// ImageUpload.tsx — Componente reutilizável de upload de imagem
// Faz upload via POST /api/upload/image e retorna a URL pública ao pai.
// =============================================================================

import { useRef, useState, DragEvent, ChangeEvent } from 'react'
import { UploadCloud, X, Image as ImageIcon, Loader2, RefreshCw } from 'lucide-react'
import { uploadApi } from '@/lib/api'

interface ImageUploadProps {
  currentUrl?: string | null
  onUpload: (url: string) => void
  label?: string
  /** Dica de tamanho/formato exibida abaixo do label. Ex: "800×450px recomendado" */
  hint?: string
}

export default function ImageUpload({ currentUrl, onUpload, label = 'Imagem', hint }: ImageUploadProps) {
  const inputRef           = useRef<HTMLInputElement>(null)
  const replaceRef         = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError]  = useState<string | null>(null)
  const [preview, setPreview] = useState<string | null>(currentUrl ?? null)

  // Sincroniza preview quando currentUrl mudar (ex: abrir modal de produto diferente)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  if (preview === null && currentUrl) setPreview(currentUrl)

  async function handleFile(file: File) {
    setError(null)

    // Validação client-side rápida
    const allowed = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp']
    if (!allowed.includes(file.type)) {
      setError('Apenas JPEG, PNG ou WebP são permitidos.')
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('O arquivo deve ter no máximo 5 MB.')
      return
    }

    setUploading(true)
    // Preview imediato (blob URL)
    const objectUrl = URL.createObjectURL(file)
    setPreview(objectUrl)

    try {
      const { data } = await uploadApi.image(file)
      onUpload(data.url)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setError(msg ?? 'Erro ao fazer upload. Tente novamente.')
      setPreview(currentUrl ?? null) // reverte preview
    } finally {
      setUploading(false)
    }
  }

  function handleChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) handleFile(file)
  }

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setDragging(false)
    const file = e.dataTransfer.files?.[0]
    if (file) handleFile(file)
  }

  function handleDragOver(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setDragging(true)
  }

  function clearImage() {
    setPreview(null)
    setError(null)
    onUpload('')
    if (inputRef.current) inputRef.current.value = ''
  }

  return (
    <div className="space-y-2">
      <div className="flex items-baseline gap-2">
        <label className="label">{label}</label>
        {hint && <span className="text-[10px] text-[var(--text-muted)]">{hint}</span>}
      </div>

      {preview ? (
        // ── Preview da imagem selecionada ─────────────────────────────────────
        <div className="relative rounded-xl overflow-hidden border border-[var(--border-color)] bg-[var(--bg-input)] group">
          <img
            src={preview}
            alt="Preview"
            className="w-full max-h-48 object-cover"
          />
          {uploading && (
            <div className="absolute inset-0 bg-black/50 flex items-center justify-center">
              <Loader2 className="w-8 h-8 text-white animate-spin" />
            </div>
          )}
          {!uploading && (
            <div className="absolute top-2 right-2 flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
              {/* Trocar imagem sem precisar apagar primeiro */}
              <button
                type="button"
                onClick={() => replaceRef.current?.click()}
                className="w-7 h-7 rounded-full bg-black/60 hover:bg-brand-600/80 flex items-center justify-center text-white"
                title="Trocar imagem"
              >
                <RefreshCw className="w-3.5 h-3.5" />
              </button>
              <button
                type="button"
                onClick={clearImage}
                className="w-7 h-7 rounded-full bg-black/60 hover:bg-red-600/80 flex items-center justify-center text-white"
                title="Remover imagem"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          )}
          {/* Input oculto para trocar imagem diretamente */}
          <input
            ref={replaceRef}
            type="file"
            accept="image/jpeg,image/jpg,image/png,image/webp"
            className="hidden"
            onChange={handleChange}
          />
        </div>
      ) : (
        // ── Área de drop ──────────────────────────────────────────────────────
        <div
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onDragLeave={() => setDragging(false)}
          onClick={() => inputRef.current?.click()}
          className={[
            'flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed',
            'px-4 py-8 cursor-pointer transition-colors',
            dragging
              ? 'border-brand-500 bg-brand-500/10'
              : 'border-[var(--border-color)] bg-[var(--bg-input)] hover:border-brand-500/50 hover:bg-brand-500/5',
          ].join(' ')}
        >
          {uploading ? (
            <Loader2 className="w-8 h-8 text-brand-400 animate-spin" />
          ) : (
            <UploadCloud className="w-8 h-8 text-gray-500" />
          )}
          <div className="text-center">
            <p className="text-sm font-medium text-[var(--text-primary)]">
              {uploading ? 'Enviando...' : 'Clique ou arraste uma imagem'}
            </p>
            <p className="text-xs text-[var(--text-muted)] mt-0.5">
              JPEG, PNG, WebP · máx. 5 MB
            </p>
          </div>

          {/* Ou digitar URL manualmente */}
          {!uploading && (
            <div className="flex items-center gap-2 mt-1 text-gray-500">
              <ImageIcon className="w-3.5 h-3.5" />
              <span className="text-xs">ou cole uma URL abaixo</span>
            </div>
          )}
        </div>
      )}

      {/* Alternativa: URL manual */}
      {!uploading && (
        <input
          type="text"
          className="input text-xs"
          placeholder="https://exemplo.com/imagem.jpg  (alternativa ao upload)"
          value={preview && !preview.startsWith('blob:') ? preview : ''}
          onChange={e => {
            const v = e.target.value.trim()
            setPreview(v || null)
            onUpload(v)
          }}
        />
      )}

      {error && (
        <p className="text-xs text-red-400 flex items-center gap-1.5">
          <X className="w-3.5 h-3.5 shrink-0" /> {error}
        </p>
      )}

      {/* Input file oculto */}
      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/jpg,image/png,image/webp"
        className="hidden"
        onChange={handleChange}
      />
    </div>
  )
}
