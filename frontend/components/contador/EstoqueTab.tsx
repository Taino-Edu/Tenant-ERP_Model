'use client'
// =============================================================================
// EstoqueTab.tsx — Classificação fiscal dos produtos (NCM, CEST, tributos).
// A quantidade é somente leitura: estoque é operação da loja, não do contador.
// =============================================================================
import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Package, Save, Search, ChevronDown, AlertTriangle } from 'lucide-react'
import clsx from 'clsx'
import { contadorApi, getErrorMessage, type ContadorProdutoDto } from '@/lib/api'
import Badge from '@/components/admin/ui/Badge'
import Button from '@/components/admin/ui/Button'
import EmptyState from '@/components/admin/ui/EmptyState'
import Spinner from '@/components/admin/ui/Spinner'
import { SecaoHeader } from './contador-shared'

interface Props {
  tenantId: string
  produtos: ContadorProdutoDto[]
  loading: boolean
  onProdutoAtualizado: (produto: ContadorProdutoDto) => void
}

export default function EstoqueTab({ tenantId, produtos, loading, onProdutoAtualizado }: Props) {
  const [busca, setBusca] = useState('')
  const [soPendentes, setSoPendentes] = useState(false)

  const filtrados = useMemo(() => {
    const termo = busca.trim().toLowerCase()
    return produtos.filter(p => {
      if (soPendentes && p.ncm) return false
      if (!termo) return true
      return p.name.toLowerCase().includes(termo)
          || (p.category ?? '').toLowerCase().includes(termo)
          || (p.ncm ?? '').includes(termo)
    })
  }, [produtos, busca, soPendentes])

  const semNcm = produtos.filter(p => !p.ncm).length

  return (
    <div className="space-y-5">
      <div className="card space-y-4">
        <SecaoHeader
          icon={Package}
          titulo="Estoque e classificação fiscal"
          descricao="A quantidade é somente leitura. Você pode atualizar NCM, CEST e tributos."
          acoes={semNcm > 0
            ? <Badge tone="warning"><AlertTriangle className="w-3 h-3 mr-1" />{semNcm} sem NCM</Badge>
            : <Badge tone="success">Todos com NCM</Badge>}
        />

        <div className="flex flex-wrap items-center gap-3">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="w-4 h-4 text-gray-500 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              className="input w-full pl-9"
              placeholder="Buscar por nome, categoria ou NCM"
              value={busca}
              onChange={e => setBusca(e.target.value)}
            />
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-400 cursor-pointer select-none">
            <input type="checkbox" className="accent-brand-500" checked={soPendentes}
                   onChange={e => setSoPendentes(e.target.checked)} />
            Só produtos sem NCM
          </label>
        </div>
      </div>

      <div className="card space-y-2">
        {loading ? (
          <Spinner block />
        ) : filtrados.length === 0 ? (
          <EmptyState icon={Package} message={produtos.length === 0
            ? 'Nenhum produto cadastrado.'
            : 'Nenhum produto corresponde ao filtro.'} compact />
        ) : (
          filtrados.map(produto => (
            <ProdutoFiscalRow key={produto.id} tenantId={tenantId} produto={produto}
                              onAtualizado={onProdutoAtualizado} />
          ))
        )}
      </div>
    </div>
  )
}

function ProdutoFiscalRow({ tenantId, produto, onAtualizado }: {
  tenantId: string
  produto: ContadorProdutoDto
  onAtualizado: (produto: ContadorProdutoDto) => void
}) {
  const [aberto, setAberto] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [form, setForm] = useState({
    ncm: produto.ncm ?? '',
    cest: produto.cest ?? '',
    federal: produto.percentualTributosFederais?.toString() ?? '',
    estadual: produto.percentualTributosEstaduais?.toString() ?? '',
    municipal: produto.percentualTributosMunicipais?.toString() ?? '',
    fonte: produto.fonteTributos ?? '',
  })

  const numeroOuNulo = (valor: string) => valor.trim() === '' ? null : Number(valor.replace(',', '.'))

  async function salvar() {
    setSalvando(true)
    const payload = {
      ncm: form.ncm || null,
      cest: form.cest || null,
      percentualTributosFederais: numeroOuNulo(form.federal),
      percentualTributosEstaduais: numeroOuNulo(form.estadual),
      percentualTributosMunicipais: numeroOuNulo(form.municipal),
      fonteTributos: form.fonte || null,
    }
    try {
      await contadorApi.updateProdutoFiscal(tenantId, produto.id, payload)
      // Atualiza a lista do workspace sem refazer o GET inteiro — o backend só
      // devolve uma mensagem, e o que mudou já está no payload enviado.
      onAtualizado({
        ...produto,
        ncm: payload.ncm ?? undefined,
        cest: payload.cest ?? undefined,
        percentualTributosFederais: payload.percentualTributosFederais ?? undefined,
        percentualTributosEstaduais: payload.percentualTributosEstaduais ?? undefined,
        percentualTributosMunicipais: payload.percentualTributosMunicipais ?? undefined,
        fonteTributos: payload.fonteTributos ?? undefined,
      })
      toast.success(`Classificação fiscal de ${produto.name} atualizada.`)
      setAberto(false)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar classificação fiscal'))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className={clsx(
      'rounded-xl border bg-surface-800/40 transition-colors',
      aberto ? 'border-brand-500/40' : 'border-surface-700 hover:border-surface-500',
    )}>
      <button
        onClick={() => setAberto(v => !v)}
        aria-expanded={aberto}
        className="w-full flex items-center justify-between gap-3 text-left p-3"
      >
        <div className="min-w-0">
          <p className="text-sm font-medium text-white truncate">{produto.name}</p>
          <p className="text-xs text-gray-500">
            {produto.category} · NCM {produto.ncm || <span className="text-amber-400">pendente</span>}
          </p>
        </div>
        <div className="flex items-center gap-3 shrink-0">
          <span className="text-xs text-gray-400">
            Estoque: <strong className="text-white">{produto.stockQuantity}</strong>
          </span>
          <ChevronDown className={clsx('w-4 h-4 text-gray-500 transition-transform', aberto && 'rotate-180')} />
        </div>
      </button>

      {aberto && (
        <div className="grid grid-cols-2 md:grid-cols-6 gap-2 p-3 pt-0">
          <input className="input text-sm" placeholder="NCM (8 dígitos)" maxLength={10}
                 value={form.ncm} onChange={e => setForm({ ...form, ncm: e.target.value })} />
          <input className="input text-sm" placeholder="CEST (7 dígitos)" maxLength={9}
                 value={form.cest} onChange={e => setForm({ ...form, cest: e.target.value })} />
          <input className="input text-sm" inputMode="decimal" placeholder="Federal %"
                 value={form.federal} onChange={e => setForm({ ...form, federal: e.target.value })} />
          <input className="input text-sm" inputMode="decimal" placeholder="Estadual %"
                 value={form.estadual} onChange={e => setForm({ ...form, estadual: e.target.value })} />
          <input className="input text-sm" inputMode="decimal" placeholder="Municipal %"
                 value={form.municipal} onChange={e => setForm({ ...form, municipal: e.target.value })} />
          <input className="input text-sm" placeholder="Fonte (ex.: IBPT)" maxLength={100}
                 value={form.fonte} onChange={e => setForm({ ...form, fonte: e.target.value })} />
          <Button onClick={salvar} loading={salvando} full className="col-span-2 md:col-span-6">
            {!salvando && <Save className="w-4 h-4" />} Salvar dados fiscais
          </Button>
        </div>
      )}
    </div>
  )
}
