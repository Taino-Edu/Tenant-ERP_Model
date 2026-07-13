'use client'
// =============================================================================
// AttachImageButton.tsx — Anexo de imagem compacto pra formulário de resposta
// de chamado (paperclip + miniatura), ao lado do textarea. Pro formulário de
// abrir chamado (com mais espaço vertical) usa o ImageUpload padrão em vez
// deste.
// =============================================================================

import { useRef, useState, ChangeEvent } from 'react'
import { Paperclip, X, Loader2 } from 'lucide-react'
import { uploadApi } from '@/lib/api'

export default function AttachImageButton({ value, onChange }: { value: string | null; onChange: (url: string | null) => void }) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)

  async function handleChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    try {
      const { data } = await uploadApi.image(file)
      onChange(data.url)
    } catch {
      // Falha de upload aqui não impede enviar a mensagem só com texto.
    } finally {
      setUploading(false)
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  if (value) {
    return (
      <div className="relative shrink-0">
        <img src={value} alt="Anexo" className="w-11 h-11 rounded-lg object-cover border border-surface-500" />
        <button
          type="button"
          onClick={() => onChange(null)}
          className="absolute -top-1.5 -right-1.5 w-4 h-4 rounded-full bg-red-500 flex items-center justify-center text-white"
          title="Remover anexo"
        >
          <X className="w-2.5 h-2.5" />
        </button>
      </div>
    )
  }

  return (
    <>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={uploading}
        className="btn-secondary shrink-0 w-11 h-11 justify-center px-0"
        title="Anexar imagem"
      >
        {uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Paperclip className="w-4 h-4" />}
      </button>
      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/jpg,image/png,image/webp"
        className="hidden"
        onChange={handleChange}
      />
    </>
  )
}
