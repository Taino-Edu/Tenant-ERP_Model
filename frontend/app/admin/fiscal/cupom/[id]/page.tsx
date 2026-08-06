'use client'
// =============================================================================
// admin/fiscal/cupom/[id]/page.tsx — DANFE NFC-e do admin.
//
// O conteúdo vem do XML fiscal desserializado pelo backend (DanfeXmlParser).
// Esta página só escolhe a bobina e manda imprimir; a representação em si vive
// em components/fiscal/DanfeNfce.tsx e é a mesma usada na área do cliente —
// uma fonte, uma representação.
// =============================================================================

import { useEffect, useState } from 'react'
import { useParams } from 'next/navigation'
import { fiscalApi, getErrorMessage, type DanfeFiscalDto } from '@/lib/api'
import DanfeNfce, { type LarguraBobina } from '@/components/fiscal/DanfeNfce'

export default function CupomNfcePage() {
  const params = useParams()
  const id = params?.id as string
  const [danfe, setDanfe] = useState<DanfeFiscalDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [largura, setLargura] = useState<LarguraBobina>(80)

  useEffect(() => {
    if (!id) return
    fiscalApi.obterCupom(id)
      .then(r => setDanfe(r.data))
      .catch(err => setError(getErrorMessage(
        err,
        // 404 aqui não é "nota inexistente": é nota sem XML fiscal — pendente,
        // rejeitada, ou em contingência ainda não persistida. Documento sem
        // autorização não pode ser apresentado como DANFE.
        'Não foi possível carregar o DANFE. A nota pode ainda não ter XML fiscal (pendente, rejeitada ou em contingência), ou sua sessão de administrador expirou.',
      )))
  }, [id])

  if (error) {
    return <div style={{ fontFamily: 'sans-serif', padding: 40, color: '#c00' }}><strong>Erro:</strong> {error}</div>
  }
  if (!danfe) {
    return <div style={{ fontFamily: 'sans-serif', padding: 40, color: '#555' }}>Carregando DANFE...</div>
  }

  return (
    <div style={{ background: '#eee', minHeight: '100vh' }}>
      <div className="danfe-nao-imprime" style={{
        position: 'fixed', top: 16, right: 16, zIndex: 100,
        display: 'flex', gap: 8, alignItems: 'center', fontFamily: 'sans-serif',
      }}>
        <select
          value={largura}
          onChange={e => setLargura(Number(e.target.value) as LarguraBobina)}
          aria-label="Largura da bobina"
          style={{ padding: '9px 10px', borderRadius: 8, border: '1px solid #cbd5e1', fontWeight: 600 }}
        >
          <option value={80}>Bobina 80 mm</option>
          <option value={58}>Bobina 58 mm</option>
        </select>
        <button onClick={() => window.print()} style={{
          background: '#2563eb', color: '#fff', border: 'none', borderRadius: 8,
          padding: '10px 20px', fontWeight: 700, cursor: 'pointer',
        }}>
          Imprimir
        </button>
      </div>

      <DanfeNfce danfe={danfe} largura={largura} />
    </div>
  )
}
