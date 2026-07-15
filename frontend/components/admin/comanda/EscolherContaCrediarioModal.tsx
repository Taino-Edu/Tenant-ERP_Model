'use client'
import { useEffect, useState } from 'react'
import { CrediariosDto } from '@/lib/api'
import { CreditCard, X, CheckCircle } from 'lucide-react'
import clsx from 'clsx'
import { fmt } from './shared'

// ── Modal: escolher conta de crediário ───────────────────────────────────────

export function EscolherContaCrediarioModal({
  userName,
  contasAbertas,
  valorNovo,
  onEscolher,
  onNova,
  onCancel,
}: {
  userName:      string
  contasAbertas: CrediariosDto[]
  valorNovo:     number
  onEscolher:    (crediarioId: string) => void
  onNova:        () => void
  onCancel:      () => void
}) {
  const [escolhido, setEscolhido] = useState<string | null>(null)

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onCancel()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onCancel])

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center bg-black/75 backdrop-blur-sm p-4"
      onClick={e => { if (e.target === e.currentTarget) onCancel() }}
    >
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md max-h-[85vh] overflow-y-auto shadow-2xl">
        {/* Header */}
        <div className="flex items-start justify-between px-6 py-4 border-b border-surface-600 sticky top-0 bg-surface-800">
          <div>
            <h2 className="font-bold text-white text-lg flex items-center gap-2">
              <CreditCard className="w-5 h-5 text-amber-400" /> Conta de Crediário
            </h2>
            <p className="text-sm text-gray-400 mt-0.5">
              {userName} já tem {contasAbertas.length} conta{contasAbertas.length > 1 ? 's' : ''} em aberto
            </p>
          </div>
          <button onClick={onCancel} className="text-gray-500 hover:text-white">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="px-6 py-4 space-y-3">
          <p className="text-xs text-gray-500 uppercase tracking-widest font-semibold">Adicionar a uma conta existente</p>

          {contasAbertas.map(c => {
            const sel = escolhido === c.id
            return (
              <button
                key={c.id}
                onClick={() => setEscolhido(sel ? null : c.id)}
                className={clsx(
                  'w-full text-left rounded-xl border px-4 py-3 transition-all',
                  sel
                    ? 'border-amber-400 bg-amber-400/10'
                    : 'border-surface-500 bg-surface-700 hover:border-surface-400'
                )}
              >
                <div className="flex justify-between items-center">
                  <span className="text-sm font-medium text-white">
                    Saldo em aberto: <span className="text-accent-gold">{fmt(c.saldoRestanteEmReais)}</span>
                    {c.vencido && <span className="ml-2 text-[10px] text-red-400 font-semibold">[VENCIDO]</span>}
                  </span>
                  <span className={clsx('w-4 h-4 rounded-full border-2 shrink-0',
                    sel ? 'border-amber-400 bg-amber-400' : 'border-gray-500'
                  )} />
                </div>
                <p className="text-xs text-gray-500 mt-0.5">
                  Vence {new Date(c.dataVencimento).toLocaleDateString('pt-BR')} ·{' '}
                  {c.observacao ?? 'Sem observação'}
                </p>
                {sel && (
                  <p className="text-xs text-amber-300 mt-1">
                    Novo total: {fmt(c.saldoRestanteEmReais + valorNovo / 100)}
                  </p>
                )}
              </button>
            )
          })}

          <div className="border-t border-surface-600 pt-3">
            <p className="text-xs text-gray-500 uppercase tracking-widest font-semibold mb-2">Ou criar conta nova</p>
            <button
              onClick={() => setEscolhido('__nova__')}
              className={clsx(
                'w-full text-left rounded-xl border px-4 py-3 transition-all',
                escolhido === '__nova__'
                  ? 'border-brand-400 bg-brand-400/10'
                  : 'border-surface-500 bg-surface-700 hover:border-surface-400'
              )}
            >
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium text-white">Nova conta — prazo 30 dias</span>
                <span className={clsx('w-4 h-4 rounded-full border-2 shrink-0',
                  escolhido === '__nova__' ? 'border-brand-400 bg-brand-400' : 'border-gray-500'
                )} />
              </div>
              <p className="text-xs text-gray-500 mt-0.5">Dívida independente com vencimento próprio</p>
            </button>
          </div>
        </div>

        <div className="flex gap-3 px-6 pb-5">
          <button onClick={onCancel} className="btn-secondary flex-1 justify-center">
            Cancelar
          </button>
          <button
            onClick={() => {
              if (!escolhido) return
              if (escolhido === '__nova__') onNova()
              else onEscolher(escolhido)
            }}
            disabled={!escolhido}
            className="btn-primary flex-1 justify-center"
          >
            <CheckCircle className="w-4 h-4" /> Confirmar
          </button>
        </div>
      </div>
    </div>
  )
}
