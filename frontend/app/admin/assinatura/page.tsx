'use client'

// =============================================================================
// Assinatura — a loja vendo e mantendo os próprios dados de cobrança.
//
// Antes disto, CNPJ e e-mail de faturamento só existiam via SQL no catálogo: o
// lojista não tinha onde informar, não via fatura e não sabia se estava em dia.
// =============================================================================

import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, ExternalLink, Loader2, ReceiptText, ShieldCheck } from 'lucide-react'
import { assinaturaApi, getErrorMessage, type AssinaturaDto } from '@/lib/api'
import toast from 'react-hot-toast'

const dinheiro = (v: number) =>
  v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

const data = (iso: string) => new Date(iso).toLocaleDateString('pt-BR')

const competencia = (iso: string) =>
  new Date(iso).toLocaleDateString('pt-BR', { month: '2-digit', year: 'numeric' })

export default function AssinaturaPage() {
  const [dados, setDados]       = useState<AssinaturaDto | null>(null)
  const [loading, setLoading]   = useState(true)
  const [salvando, setSalvando] = useState(false)
  const [documento, setDocumento] = useState('')
  const [email, setEmail]         = useState('')

  const carregar = useCallback(async () => {
    try {
      const { data: res } = await assinaturaApi.obter()
      setDados(res)
      setDocumento(res.cnpj ?? '')
      setEmail(res.emailDeFaturamento ?? '')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar a assinatura'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { carregar() }, [carregar])

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault()
    setSalvando(true)
    try {
      const { data: res } = await assinaturaApi.salvarFaturamento({ documento, email })
      setDados(res)
      toast.success('Dados de cobrança salvos')
    } catch (err) {
      // O backend valida o dígito verificador do CPF/CNPJ e devolve a mensagem
      // pronta — é aqui que o lojista descobre o erro, e não dias depois numa
      // cobrança que nunca chegou.
      toast.error(getErrorMessage(err, 'Não foi possível salvar'))
    } finally {
      setSalvando(false)
    }
  }

  if (loading) {
    return <div className="p-10 flex justify-center"><Loader2 className="w-7 h-7 animate-spin text-brand-400" /></div>
  }

  if (!dados) return null

  const suspensa = dados.situacao === 'Suspensa'

  return (
    <div className="p-4 sm:p-6 space-y-5 max-w-3xl">
      <div>
        <h1 className="text-xl font-bold text-white">Assinatura</h1>
        <p className="text-sm text-gray-400 mt-1">
          Seu plano, suas faturas e para onde a cobrança é enviada.
        </p>
      </div>

      {/* ── Plano ── */}
      <div className="card p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-wide text-gray-500">Plano</p>
            <p className="text-lg font-bold text-white">{dados.plano}</p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-gray-500">Mensalidade</p>
            <p className="text-lg font-bold text-white">{dinheiro(dados.mensalidade)}</p>
          </div>
          <span
            className={`rounded-full px-3 py-1 text-xs font-bold ${
              suspensa ? 'bg-red-500/15 text-red-300' : 'bg-emerald-500/15 text-emerald-300'
            }`}
          >
            {dados.situacao}
          </span>
        </div>

        {suspensa && (
          <p className="mt-4 flex gap-2 rounded-lg bg-red-500/10 p-3 text-sm text-red-200">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
            <span>
              Sua loja está suspensa por falta de pagamento. Quite a fatura em aberto abaixo
              para reativar — a liberação é automática assim que o pagamento é confirmado.
            </span>
          </p>
        )}
      </div>

      {/* ── Dados de cobrança ── */}
      <form onSubmit={salvar} className="card p-5 space-y-4">
        <div className="flex items-center gap-2">
          <ShieldCheck className="w-4 h-4 text-brand-400" />
          <h2 className="font-bold text-white">Dados de cobrança</h2>
        </div>

        {!dados.dadosCompletos && (
          <p className="flex gap-2 rounded-lg bg-amber-500/10 p-3 text-sm text-amber-200">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
            <span>
              Preencha o CPF/CNPJ e o e-mail para começar a receber as faturas.
              Enquanto estiver incompleto, nenhuma cobrança é enviada.
            </span>
          </p>
        )}

        <label className="block">
          <span className="text-sm text-gray-400">CPF ou CNPJ</span>
          <input
            value={documento}
            onChange={e => setDocumento(e.target.value)}
            placeholder="00.000.000/0000-00"
            className="input w-full mt-1"
            required
          />
        </label>

        <label className="block">
          <span className="text-sm text-gray-400">E-mail de cobrança</span>
          <input
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            placeholder="financeiro@sualoja.com.br"
            className="input w-full mt-1"
            required
          />
          <span className="mt-1 block text-xs text-gray-500">
            É para cá que a fatura vai. Pode ser diferente do e-mail de quem usa o sistema.
          </span>
        </label>

        <button type="submit" disabled={salvando} className="btn-primary disabled:opacity-50">
          {salvando ? 'Salvando...' : 'Salvar'}
        </button>
      </form>

      {/* ── Faturas ── */}
      <div className="card p-5">
        <div className="flex items-center gap-2 mb-4">
          <ReceiptText className="w-4 h-4 text-brand-400" />
          <h2 className="font-bold text-white">Faturas</h2>
        </div>

        {dados.faturas.length === 0 ? (
          <p className="text-sm text-gray-400">Nenhuma fatura emitida ainda.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs uppercase tracking-wide text-gray-500">
                  <th className="pb-2">Referência</th>
                  <th className="pb-2">Valor</th>
                  <th className="pb-2">Vencimento</th>
                  <th className="pb-2">Situação</th>
                  <th className="pb-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {dados.faturas.map(f => (
                  <tr key={f.id}>
                    <td className="py-3 text-gray-300">
                      {f.tipo === 'Implantacao' ? 'Implantação' : competencia(f.competencia)}
                    </td>
                    <td className="py-3 text-white font-medium">{dinheiro(f.valor)}</td>
                    <td className="py-3 text-gray-400">{data(f.vencimento)}</td>
                    <td className="py-3">
                      {f.pagoEm ? (
                        <span className="text-emerald-300">Paga em {data(f.pagoEm)}</span>
                      ) : f.vencida ? (
                        <span className="text-red-300">Vencida</span>
                      ) : (
                        <span className="text-gray-400">Em aberto</span>
                      )}
                    </td>
                    <td className="py-3 text-right">
                      {!f.pagoEm && f.linkDePagamento && (
                        <a
                          href={f.linkDePagamento}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-1 font-bold text-brand-400 hover:underline"
                        >
                          Pagar <ExternalLink className="w-3 h-3" />
                        </a>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
