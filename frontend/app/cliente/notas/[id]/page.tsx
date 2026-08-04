'use client'
// =============================================================================
// cliente/notas/[id]/page.tsx — DANFE NFC-e do próprio cliente, pra imprimir ou
// guardar. Mesma representação do admin (components/fiscal/DanfeNfce.tsx), mas
// só acessa a nota se ela for dele — validado no backend via
// /api/minhas-notas/{id}/cupom.
// =============================================================================

import { useEffect, useState } from 'react'
import { useParams } from 'next/navigation'
import { minhasNotasApi, type DanfeFiscalDto } from '@/lib/api'
import DanfeNfce from '@/components/fiscal/DanfeNfce'

export default function MeuCupomNfcePage() {
  const params = useParams()
  const id = params?.id as string
  const [danfe, setDanfe] = useState<DanfeFiscalDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    minhasNotasApi.obterCupom(id)
      .then(r => setDanfe(r.data))
      .catch(() => setError(
        'Não foi possível carregar a nota. Verifique se você está logado e se essa nota é sua. ' +
        'Notas ainda sem autorização da SEFAZ não têm documento para exibir.'))
  }, [id])

  if (error) {
    return <div style={{ fontFamily: 'sans-serif', padding: 40, color: '#c00' }}><strong>Erro:</strong> {error}</div>
  }
  if (!danfe) {
    return <div style={{ fontFamily: 'sans-serif', padding: 40, color: '#555' }}>Carregando nota...</div>
  }

  return (
    <div style={{ background: '#eee', minHeight: '100vh' }}>
      <div className="danfe-nao-imprime" style={{ position: 'fixed', top: 16, right: 16, zIndex: 100 }}>
        <button onClick={() => window.print()} style={{
          background: '#2563eb', color: '#fff', border: 'none', borderRadius: 8,
          padding: '10px 20px', fontWeight: 700, cursor: 'pointer', fontFamily: 'sans-serif',
        }}>
          Imprimir
        </button>
      </div>

      <DanfeNfce danfe={danfe} />
    </div>
  )
}
