'use client'
import { useEffect } from 'react'
import { ComandaDto, COMANDA_PAYMENT_METHODS } from '@/lib/api'
import { CheckCircle, X, Receipt } from 'lucide-react'
import { fmt, printComandaReceiptPDF } from './shared'
import Modal from '@/components/admin/ui/Modal'

/** Resumo da comanda recém-fechada + botão de imprimir comprovante — some
 * sozinho, não bloqueia nada; só existe pra sempre sobrar algum papel/PDF
 * da venda mesmo quando não emite NFC-e. */
export function ComandaReceiptModal({ comanda, siteName, onClose }: {
  comanda: ComandaDto
  siteName: string
  onClose: () => void
}) {
  const payLabel = COMANDA_PAYMENT_METHODS.find(m => m.value === comanda.paymentMethod)?.label ?? comanda.paymentMethod ?? '—'

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  return (
    <Modal onClose={onClose} maxWidth="sm">
        {/* Ícone verde (sucesso) não cabe no cabeçalho padrão do Modal, que é
            sempre na cor de marca — mantido como cabeçalho próprio. */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-surface-600 sticky top-0 bg-surface-800">
          <h3 className="font-semibold text-white flex items-center gap-2">
            <CheckCircle className="w-5 h-5 text-emerald-400" /> Comanda fechada
          </h3>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-5 space-y-4">
          <div className="bg-surface-900 rounded-xl divide-y divide-surface-600 text-left">
            {comanda.items.map((item, idx) => (
              <div key={idx} className="flex justify-between items-center px-4 py-2.5 text-sm">
                <span className="text-gray-300">{item.quantity}× {item.itemNameSnapshot}</span>
                <span className="text-gray-400 font-mono">{fmt(item.subtotalInReais)}</span>
              </div>
            ))}
            <div className="flex justify-between items-center px-4 py-2.5 font-bold">
              <span className="text-white">Total</span>
              <span className="text-accent-gold text-lg">{fmt(comanda.totalInReais)}</span>
            </div>
          </div>
          <p className="text-xs text-gray-400">Pagamento: {payLabel}</p>
          {comanda.cashReceivedInCents != null && (
            <div className="grid grid-cols-2 gap-2 text-xs">
              <div className="rounded-lg bg-surface-900 px-3 py-2 text-gray-400">
                Recebido <strong className="block text-sm text-white">{fmt(comanda.cashReceivedInCents / 100)}</strong>
              </div>
              <div className="rounded-lg bg-surface-900 px-3 py-2 text-gray-400">
                Troco <strong className="block text-sm text-emerald-300">{fmt(comanda.changeInCents / 100)}</strong>
              </div>
            </div>
          )}
          <div className="flex gap-2">
            <button
              onClick={() => printComandaReceiptPDF(comanda, payLabel, siteName)}
              className="btn-secondary flex-1 justify-center"
            >
              <Receipt className="w-4 h-4" /> Imprimir / PDF
            </button>
            <button onClick={onClose} className="btn-primary flex-1 justify-center">
              Fechar
            </button>
          </div>
        </div>
    </Modal>
  )
}
