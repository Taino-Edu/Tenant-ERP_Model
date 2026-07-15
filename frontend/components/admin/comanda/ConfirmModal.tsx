'use client'

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
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onCancel}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-sm p-6" onClick={e => e.stopPropagation()}>
        <h3 className="font-semibold text-white text-lg mb-2">{title}</h3>
        <p className="text-gray-400 text-sm mb-6">{message}</p>
        <div className="flex gap-3">
          <button onClick={onCancel} className="btn-secondary flex-1 justify-center">Voltar</button>
          <button onClick={onConfirm} className={`${confirmClass} flex-1 justify-center`}>{confirmLabel}</button>
        </div>
      </div>
    </div>
  )
}
