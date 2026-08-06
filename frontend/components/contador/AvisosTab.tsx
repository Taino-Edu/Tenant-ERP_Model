'use client'
// =============================================================================
// AvisosTab.tsx — Mural compartilhado entre o contador e o lojista, preso ao
// vínculo (ContadorTenantLink). Não é chat em tempo real: recarrega ao enviar.
// =============================================================================
import { useCallback, useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { MessageSquare, Send } from 'lucide-react'
import clsx from 'clsx'
import { contadorApi, getErrorMessage, type AvisoContadorDto } from '@/lib/api'
import Button from '@/components/admin/ui/Button'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'
import { SecaoHeader } from './contador-shared'

export default function AvisosTab({ tenantId }: { tenantId: string }) {
  const [avisos, setAvisos] = useState<AvisoContadorDto[]>([])
  const [loading, setLoading] = useState(true)
  const [mensagem, setMensagem] = useState('')
  const [enviando, setEnviando] = useState(false)

  const carregar = useCallback(() => {
    setLoading(true)
    contadorApi.listAvisos(tenantId)
      .then(r => setAvisos(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar avisos')))
      .finally(() => setLoading(false))
  }, [tenantId])

  useEffect(() => { carregar() }, [carregar])

  async function enviar(e: React.FormEvent) {
    e.preventDefault()
    if (!mensagem.trim()) return
    setEnviando(true)
    try {
      await contadorApi.postAviso(tenantId, mensagem.trim())
      setMensagem('')
      carregar()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao enviar aviso'))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="card space-y-4">
      <SecaoHeader
        icon={MessageSquare}
        titulo="Avisos"
        descricao="Recado direto para o lojista — ele vê o mesmo mural em /admin/fiscal."
      />

      <div className="flex flex-col gap-2 max-h-[420px] overflow-y-auto">
        {loading ? (
          <Spinner block />
        ) : avisos.length === 0 ? (
          <EmptyState icon={MessageSquare} message="Nenhum aviso ainda." compact />
        ) : (
          avisos.map(aviso => (
            <div key={aviso.id} className={clsx(
              'rounded-xl p-3 text-sm max-w-[85%]',
              aviso.autor === 'Contador'
                ? 'bg-brand-600/10 border border-brand-500/20 self-end text-right'
                : 'bg-surface-800/50 border border-surface-700/50',
            )}>
              <p className="text-white whitespace-pre-wrap">{aviso.mensagem}</p>
              <p className="text-[11px] text-gray-500 mt-1">
                {aviso.autor} · {new Date(aviso.createdAt).toLocaleString('pt-BR')}
              </p>
            </div>
          ))
        )}
      </div>

      <form onSubmit={enviar} className="flex gap-2">
        <input
          className="input flex-1"
          placeholder="Escrever um aviso pro lojista..."
          maxLength={2000}
          value={mensagem}
          onChange={e => setMensagem(e.target.value)}
        />
        <Button type="submit" loading={enviando} className="justify-center">
          {!enviando && <Send className="w-4 h-4" />}
        </Button>
      </form>
    </div>
  )
}
