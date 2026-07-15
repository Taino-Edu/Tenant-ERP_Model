'use client'
import { useEffect, useState } from 'react'
import { ComandaDto, COMANDA_PAYMENT_METHODS } from '@/lib/api'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { CreditCard, QrCode, CheckCircle } from 'lucide-react'
import clsx from 'clsx'
import { SECOND_PAYMENT_METHODS } from './shared'

// ── Modal: selecionar pagamento ao fechar comanda ────────────────────────────

export function CloseComandaModal({
  comanda, onConfirm, onCancel, onGerarPix, autoEmitMethods, fiscalEnabled,
}: {
  comanda:   ComandaDto
  onConfirm: (paymentMethod: string, secondMethod?: string, secondAmountInCents?: number, discountInCents?: number, emitirNotaFiscal?: boolean) => void
  onCancel:  () => void
  onGerarPix: () => void
  autoEmitMethods: string[]
  fiscalEnabled: boolean
}) {
  const { site } = useSiteConfig()
  const paymentMethods = site.pontosFidelidadeAtivo ? COMANDA_PAYMENT_METHODS : COMANDA_PAYMENT_METHODS.filter(m => m.value !== 'Pontos')
  const secondPaymentMethods = site.pontosFidelidadeAtivo ? SECOND_PAYMENT_METHODS : SECOND_PAYMENT_METHODS.filter(m => m.value !== 'Pontos')
  const [method,        setMethod]        = useState('Dinheiro')
  const [splitEnabled,  setSplitEnabled]  = useState(false)
  const [secondMethod,  setSecondMethod]  = useState('Cashback')
  const [secondAmtStr,  setSecondAmtStr]  = useState('')
  const [descontoStr,   setDescontoStr]   = useState('')
  const [emitirNota,    setEmitirNota]    = useState(() => autoEmitMethods.includes('Dinheiro'))
  const [notaTouched,   setNotaTouched]   = useState(false)

  useEffect(() => {
    if (!notaTouched) setEmitirNota(autoEmitMethods.includes(method))
  }, [method, autoEmitMethods, notaTouched])

  const totalAntesDesconto = comanda.totalInReais - comanda.pointsApplied / 100
  const descontoCents  = Math.min(
    Math.round(parseFloat(descontoStr.replace(',', '.') || '0') * 100),
    Math.round(totalAntesDesconto * 100),
  )
  const totalRestante  = totalAntesDesconto - descontoCents / 100
  const saldoCashback  = comanda.userBalanceInCents / 100
  const saldoPontos    = comanda.userPointsBalance

  const secondAmtCents = splitEnabled ? Math.round(parseFloat(secondAmtStr || '0') * 100) : 0
  const primaryAmtCents = Math.round(totalRestante * 100) - secondAmtCents
  const primaryAmtReais = primaryAmtCents / 100

  // Validações
  const semSaldoCashback = method === 'Cashback' && !splitEnabled && saldoCashback < totalRestante
  const semSaldoPontos   = method === 'Pontos'   && !splitEnabled && saldoPontos < Math.round(totalRestante * 100)
  const splitInvalido    = splitEnabled && (secondAmtCents <= 0 || secondAmtCents >= Math.round(totalRestante * 100))
  const splitSemCashback = splitEnabled && secondMethod === 'Cashback' && secondAmtCents > comanda.userBalanceInCents
  const splitSemPontos   = splitEnabled && secondMethod === 'Pontos'   && secondAmtCents > saldoPontos
  const bloqueado = semSaldoCashback || semSaldoPontos || splitInvalido || splitSemCashback || splitSemPontos

  function handleConfirm() {
    const emitir = fiscalEnabled && emitirNota
    if (splitEnabled && secondAmtCents > 0)
      onConfirm(method, secondMethod, secondAmtCents, descontoCents, emitir)
    else
      onConfirm(method, undefined, undefined, descontoCents, emitir)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onCancel}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-sm p-6 space-y-4 max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
        <div>
          <h3 className="font-semibold text-white text-lg">Fechar comanda</h3>
          <p className="text-gray-400 text-sm mt-1">
            {comanda.userName} · <span className="text-accent-gold font-bold">{`R$ ${totalRestante.toFixed(2).replace('.', ',')}`}</span>
          </p>
          {((site.pontosFidelidadeAtivo && saldoPontos > 0) || saldoCashback > 0) && (
            <div className="flex gap-3 mt-2">
              {site.pontosFidelidadeAtivo && saldoPontos > 0 && (
                <span className="text-xs text-amber-400 bg-amber-500/10 border border-amber-500/20 rounded-lg px-2 py-1">
                  {saldoPontos} pts
                </span>
              )}
              {saldoCashback > 0 && (
                <span className="text-xs text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 rounded-lg px-2 py-1">
                  R$ {saldoCashback.toFixed(2).replace('.', ',')} cashback
                </span>
              )}
            </div>
          )}
        </div>

        <div>
          <p className="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wider">Desconto (R$)</p>
          <input
            type="text"
            inputMode="decimal"
            placeholder="0,00"
            value={descontoStr}
            onChange={e => setDescontoStr(e.target.value)}
            className="input text-sm w-full font-mono"
          />
          {descontoCents > 0 && (
            <p className="text-xs text-accent-green mt-1">
              Total após desconto: R$ {totalRestante.toFixed(2).replace('.', ',')}
            </p>
          )}
        </div>

        <div>
          <p className="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wider">
            {splitEnabled ? 'Pagamento principal (restante)' : 'Forma de pagamento'}
          </p>
          <div className="grid grid-cols-1 gap-2">
            {paymentMethods.map(pm => (
              <button
                key={pm.value}
                onClick={() => setMethod(pm.value)}
                className={clsx(
                  'flex items-center gap-3 px-4 py-3 rounded-xl border text-sm font-medium transition-all text-left',
                  method === pm.value
                    ? pm.value === 'Crediario'
                      ? 'bg-amber-500/10 border-amber-500/50 text-amber-300'
                      : 'bg-brand-600/20 border-brand-500/50 text-brand-300'
                    : 'border-surface-500 text-gray-400 hover:border-surface-400 hover:text-gray-200'
                )}
              >
                <CreditCard className="w-4 h-4 shrink-0" />
                <span className="flex-1">{pm.label}</span>
                {splitEnabled && method === pm.value && (
                  <span className="text-xs font-mono text-white">
                    R$ {primaryAmtReais > 0 ? primaryAmtReais.toFixed(2).replace('.', ',') : '—'}
                  </span>
                )}
                {!splitEnabled && pm.value === 'Crediario' && (
                  <span className="text-xs text-amber-400/70 font-normal">acumula no saldo</span>
                )}
                {!splitEnabled && pm.value === 'Cashback' && saldoCashback > 0 && (
                  <span className="text-xs text-emerald-400/70 font-normal">
                    R$ {saldoCashback.toFixed(2).replace('.', ',')} disp.
                  </span>
                )}
                {!splitEnabled && pm.value === 'Pontos' && saldoPontos > 0 && (
                  <span className="text-xs text-amber-400/70 font-normal">
                    {saldoPontos} pts disp.
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>

        {/* Toggle split */}
        <button
          onClick={() => { setSplitEnabled(v => !v); setSecondAmtStr('') }}
          className={clsx(
            'w-full flex items-center justify-between px-3 py-2.5 rounded-xl border text-sm transition-all',
            splitEnabled
              ? 'bg-brand-600/10 border-brand-500/40 text-brand-300'
              : 'border-surface-500 text-gray-500 hover:border-surface-400 hover:text-gray-300'
          )}
        >
          <span className="flex items-center gap-2">
            <CreditCard className="w-3.5 h-3.5" />
            Dividir em dois métodos de pagamento
          </span>
          <span className={clsx('w-4 h-4 rounded border flex items-center justify-center text-xs shrink-0',
            splitEnabled ? 'bg-brand-500 border-brand-500 text-white' : 'border-surface-400'
          )}>
            {splitEnabled && '✓'}
          </span>
        </button>

        {/* Segundo pagamento */}
        {splitEnabled && (
          <div className="bg-surface-800 rounded-xl p-3 space-y-3">
            <p className="text-xs text-gray-400 font-medium">Segundo pagamento</p>
            <select
              value={secondMethod}
              onChange={e => setSecondMethod(e.target.value)}
              className="input text-sm w-full"
            >
              {secondPaymentMethods.map(m => (
                <option key={m.value} value={m.value}>{m.label}</option>
              ))}
            </select>
            <div>
              <label className="text-xs text-gray-500 mb-1 block">Valor (R$)</label>
              <input
                type="number"
                min="0.01"
                step="0.01"
                max={(totalRestante - 0.01).toFixed(2)}
                placeholder="0,00"
                value={secondAmtStr}
                onChange={e => setSecondAmtStr(e.target.value)}
                className="input text-sm w-full font-mono"
              />
              {secondMethod === 'Cashback' && saldoCashback > 0 && (
                <button
                  type="button"
                  onClick={() => setSecondAmtStr(Math.min(saldoCashback, totalRestante - 0.01).toFixed(2))}
                  className="mt-1 text-xs text-emerald-400 hover:text-emerald-300 transition-colors"
                >
                  Usar tudo (R$ {saldoCashback.toFixed(2).replace('.', ',')})
                </button>
              )}
              {secondMethod === 'Pontos' && saldoPontos > 0 && (
                <button
                  type="button"
                  onClick={() => setSecondAmtStr((Math.min(saldoPontos, Math.round(totalRestante * 100) - 1) / 100).toFixed(2))}
                  className="mt-1 text-xs text-amber-400 hover:text-amber-300 transition-colors"
                >
                  Usar tudo ({saldoPontos} pts = R$ {(saldoPontos / 100).toFixed(2).replace('.', ',')})
                </button>
              )}
            </div>
            {secondAmtCents > 0 && primaryAmtCents > 0 && !splitInvalido && (
              <div className="text-xs text-gray-400 pt-1 border-t border-surface-600 space-y-1">
                <div className="flex justify-between">
                  <span>{SECOND_PAYMENT_METHODS.find(m => m.value === secondMethod)?.label}:</span>
                  <span className="text-white font-mono">R$ {(secondAmtCents / 100).toFixed(2).replace('.', ',')}</span>
                </div>
                <div className="flex justify-between">
                  <span>{COMANDA_PAYMENT_METHODS.find(m => m.value === method)?.label ?? method}:</span>
                  <span className="text-white font-mono">R$ {(primaryAmtCents / 100).toFixed(2).replace('.', ',')}</span>
                </div>
              </div>
            )}
          </div>
        )}

        {method === 'Crediario' && !splitEnabled && (
          <div className="bg-amber-500/5 border border-amber-500/20 rounded-xl p-3 text-xs text-amber-300">
            O valor será acumulado no saldo devedor do cliente. Novas comandas podem ser abertas normalmente.
          </div>
        )}
        {method === 'Crediario' && splitEnabled && secondAmtCents > 0 && (
          <div className="bg-amber-500/5 border border-amber-500/20 rounded-xl p-3 text-xs text-amber-300">
            R$ {(primaryAmtCents / 100).toFixed(2).replace('.', ',')} irá para o crediário. O restante já foi quitado.
          </div>
        )}
        {semSaldoCashback && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            Saldo insuficiente. Cliente tem R$ {saldoCashback.toFixed(2).replace('.', ',')} mas a comanda custa R$ {totalRestante.toFixed(2).replace('.', ',')}.
          </div>
        )}
        {semSaldoPontos && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            Pontos insuficientes. Cliente tem {saldoPontos} pts mas a comanda requer {Math.round(totalRestante * 100)} pts.
          </div>
        )}
        {splitSemCashback && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            Cashback insuficiente. Cliente tem R$ {saldoCashback.toFixed(2).replace('.', ',')} mas foi solicitado R$ {(secondAmtCents / 100).toFixed(2).replace('.', ',')}.
          </div>
        )}
        {splitSemPontos && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            Pontos insuficientes para o segundo pagamento. Cliente tem {saldoPontos} pts, solicitado {secondAmtCents} pts.
          </div>
        )}
        {splitEnabled && splitInvalido && secondAmtStr !== '' && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            O segundo valor deve ser maior que zero e menor que o total (R$ {totalRestante.toFixed(2).replace('.', ',')}).
          </div>
        )}

        {method === 'Pix' && !splitEnabled && (
          <p className="text-xs text-gray-500 -mt-1">
            Cliente já pagou por fora? Use "Confirmar" direto. Pra gerar um QR Code de cobrança, use "Gerar QR Pix".
          </p>
        )}

        {fiscalEnabled && (
          <>
            <button
              type="button"
              onClick={() => { setEmitirNota(v => !v); setNotaTouched(true) }}
              className={clsx(
                'w-full flex items-center justify-between px-3 py-2.5 rounded-xl border text-sm transition-all',
                emitirNota
                  ? 'bg-brand-600/10 border-brand-500/40 text-brand-300'
                  : 'border-surface-500 text-gray-500 hover:border-surface-400 hover:text-gray-300'
              )}
            >
              <span>Emitir cupom fiscal (NFC-e) agora</span>
              <span className={clsx('w-4 h-4 rounded border flex items-center justify-center text-xs shrink-0',
                emitirNota ? 'bg-brand-500 border-brand-500 text-white' : 'border-surface-400'
              )}>
                {emitirNota && '✓'}
              </span>
            </button>
            {!emitirNota && (
              <p className="text-xs text-gray-500 -mt-1">
                Sem nota agora. Depois é possível emitir pelo histórico da comanda.
              </p>
            )}
          </>
        )}

        <div className="flex gap-3 pt-1">
          <button onClick={onCancel} className="btn-secondary flex-1 justify-center">Voltar</button>
          {method === 'Pix' && !splitEnabled ? (
            <>
              <button
                onClick={handleConfirm}
                disabled={bloqueado}
                className="btn-secondary flex-1 justify-center disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <CheckCircle className="w-4 h-4" /> Confirmar
              </button>
              <button
                onClick={onGerarPix}
                className="btn-success flex-1 justify-center"
              >
                <QrCode className="w-4 h-4" /> Gerar QR Pix
              </button>
            </>
          ) : (
            <button
              onClick={handleConfirm}
              disabled={bloqueado}
              className="btn-success flex-1 justify-center disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <CheckCircle className="w-4 h-4" /> Confirmar
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
