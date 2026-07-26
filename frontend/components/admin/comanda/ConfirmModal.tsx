'use client'
import Modal from '@/components/admin/ui/Modal'

// ── Modal de confirmação genérico (cancelar) ──────────────────────────────────

export function ConfirmModal({
  title, message, confirmLabel, confirmClass, onConfirm, onCancel,
}: {
  title:        string
  message:      string
  confirmLabel: string
  confirmClass: string
  onConfirm:    () => void
  onCancel:     () => void
}) {
  return (
    <Modal onClose={onCancel} maxWidth="sm" surface="surface-700" scrollable={false} className="p-6">
      <h3 className="font-semibold text-white text-lg mb-2">{title}</h3>
      <p className="text-gray-400 text-sm mb-6">{message}</p>
      <div className="flex gap-3">
        <button onClick={onCancel} className="btn-secondary flex-1 justify-center">Voltar</button>
        <button onClick={onConfirm} className={`${confirmClass} flex-1 justify-center`}>{confirmLabel}</button>
      </div>
    </Modal>
  )
}
