'use client'
import { useEffect, useState, useCallback } from 'react'
import { contadorApi, ContadorClienteDto, ContadorNotaDto, ContadorConfigDto } from '@/lib/api'
import toast from 'react-hot-toast'
import { Calculator, Download, Loader2, FileText, Building2, ChevronLeft, Clock, Plus } from 'lucide-react'
import clsx from 'clsx'

const fmt = (cents: number) => `R$ ${(cents / 100).toFixed(2).replace('.', ',')}`
const brToday = () => new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())

const STATUS_STYLES: Record<string, string> = {
  Autorizada:              'bg-accent-green/10 text-accent-green border-accent-green/30',
  Cancelada:               'bg-red-500/10 text-red-400 border-red-500/30',
  PendenteEmissao:         'bg-amber-500/10 text-amber-400 border-amber-500/30',
  AutorizadaContingencia:  'bg-amber-500/10 text-amber-400 border-amber-500/30',
  Rejeitada:               'bg-red-500/10 text-red-400 border-red-500/30',
}

export default function ContadorPage() {
  const [clientes, setClientes] = useState<ContadorClienteDto[]>([])
  const [loadingClientes, setLoadingClientes] = useState(true)
  const [selected, setSelected] = useState<ContadorClienteDto | null>(null)

  const [novoSlug, setNovoSlug] = useState('')
  const [solicitando, setSolicitando] = useState(false)

  const fetchClientes = useCallback(() => {
    setLoadingClientes(true)
    contadorApi.listClientes()
      .then(r => setClientes(r.data))
      .catch(() => toast.error('Erro ao carregar lista de clientes'))
      .finally(() => setLoadingClientes(false))
  }, [])

  useEffect(() => { fetchClientes() }, [fetchClientes])

  async function solicitarAcesso(e: React.FormEvent) {
    e.preventDefault()
    if (!novoSlug.trim()) return
    setSolicitando(true)
    try {
      const { data } = await contadorApi.solicitarAcesso(novoSlug.trim().toLowerCase())
      toast.success(data.message)
      setNovoSlug('')
      fetchClientes()
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Erro ao solicitar acesso')
    } finally {
      setSolicitando(false)
    }
  }

  if (selected) {
    return (
      <ClienteDetalhe
        cliente={selected}
        onVoltar={() => setSelected(null)}
      />
    )
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <div className="p-2 rounded-xl bg-brand-500/10">
          <Calculator className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">Meus Clientes</h1>
          <p className="text-sm text-gray-400">Lojas vinculadas à sua conta de contador</p>
        </div>
      </div>

      <form onSubmit={solicitarAcesso} className="card p-4 flex flex-col sm:flex-row gap-3 sm:items-end">
        <div className="flex-1">
          <label className="label">Solicitar acesso a mais uma loja</label>
          <input
            className="input w-full"
            placeholder="slug-da-loja"
            value={novoSlug}
            onChange={e => setNovoSlug(e.target.value)}
          />
        </div>
        <button type="submit" disabled={solicitando} className="btn-primary justify-center">
          {solicitando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
          Solicitar acesso
        </button>
      </form>

      <div className="card">
        {loadingClientes ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : clientes.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <Building2 className="w-10 h-10 text-gray-600 mb-2" />
            <p className="text-gray-400">Você ainda não tem nenhuma loja vinculada.</p>
            <p className="text-sm text-gray-500 mt-1">Peça o slug ao lojista e solicite acesso acima.</p>
          </div>
        ) : (
          <div className="divide-y divide-surface-700">
            {clientes.map(c => (
              <button
                key={c.tenantId}
                disabled={c.status !== 'Approved'}
                onClick={() => setSelected(c)}
                className={clsx(
                  'w-full flex items-center justify-between gap-3 px-4 py-4 text-left transition-colors',
                  c.status === 'Approved' ? 'hover:bg-surface-800/60 cursor-pointer' : 'cursor-not-allowed opacity-70'
                )}
              >
                <div className="flex items-center gap-3">
                  <Building2 className="w-4 h-4 text-gray-500" />
                  <span className="text-white font-medium">{c.slug}</span>
                </div>
                {c.status === 'Approved' ? (
                  <span className="text-xs font-medium px-2 py-0.5 rounded-full border bg-accent-green/10 text-accent-green border-accent-green/30">
                    Aprovado
                  </span>
                ) : (
                  <span className="text-xs font-medium px-2 py-0.5 rounded-full border bg-amber-500/10 text-amber-400 border-amber-500/30 flex items-center gap-1">
                    <Clock className="w-3 h-3" /> Aguardando aprovação
                  </span>
                )}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

// ── Drill-down: notas fiscais + config de UM cliente aprovado ───────────────
function ClienteDetalhe({ cliente, onVoltar }: { cliente: ContadorClienteDto; onVoltar: () => void }) {
  const [config, setConfig]     = useState<ContadorConfigDto | null>(null)
  const [notas, setNotas]       = useState<ContadorNotaDto[]>([])
  const [loading, setLoading]   = useState(true)
  const [exporting, setExporting] = useState(false)

  const [inicio, setInicio] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() - 30)
    return new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(d)
  })
  const [fim, setFim] = useState(brToday)

  const fetchNotas = useCallback(() => {
    setLoading(true)
    contadorApi.listNotas(cliente.tenantId, { inicio, fim, pageSize: 100 })
      .then(r => setNotas(r.data.items))
      .catch(() => toast.error('Erro ao carregar notas fiscais'))
      .finally(() => setLoading(false))
  }, [cliente.tenantId, inicio, fim])

  useEffect(() => { fetchNotas() }, [fetchNotas])

  useEffect(() => {
    contadorApi.getConfig(cliente.tenantId).then(r => setConfig(r.data)).catch(() => {})
  }, [cliente.tenantId])

  async function exportarXmls() {
    if (!inicio || !fim) { toast.error('Selecione o período (início e fim).'); return }
    setExporting(true)
    try {
      const { data } = await contadorApi.exportarXmls(cliente.tenantId, inicio, fim)
      const url = URL.createObjectURL(data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `xmls-fiscais-${cliente.slug}-${inicio}-a-${fim}.zip`
      a.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Erro ao gerar ZIP de XMLs')
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <button onClick={onVoltar} className="btn-secondary py-1.5 px-2.5">
          <ChevronLeft className="w-4 h-4" />
        </button>
        <div>
          <h1 className="text-xl font-black text-white">{cliente.slug}</h1>
          <p className="text-sm text-gray-400">Acesso somente leitura — notas emitidas e exportação de XMLs</p>
        </div>
      </div>

      {config && (
        <div className="card grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
          <div><span className="text-gray-500">Razão Social: </span><span className="text-white">{config.razaoSocial || '—'}</span></div>
          <div><span className="text-gray-500">CNPJ: </span><span className="text-white">{config.cnpj || '—'}</span></div>
          <div><span className="text-gray-500">Inscrição Estadual: </span><span className="text-white">{config.inscricaoEstadual || '—'}</span></div>
          <div><span className="text-gray-500">Regime Tributário: </span><span className="text-white">{config.regimeTributario}</span></div>
          <div className="sm:col-span-2">
            <span className="text-gray-500">Endereço: </span>
            <span className="text-white">
              {[config.logradouro, config.numero, config.bairro, config.municipio, config.uf].filter(Boolean).join(', ') || '—'}
            </span>
          </div>
        </div>
      )}

      <div className="card space-y-4">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="label">De</label>
            <input type="date" className="input" value={inicio} max={fim} onChange={e => setInicio(e.target.value)} />
          </div>
          <div>
            <label className="label">Até</label>
            <input type="date" className="input" value={fim} max={brToday()} min={inicio} onChange={e => setFim(e.target.value)} />
          </div>
          <button onClick={exportarXmls} disabled={exporting} className="btn-primary">
            {exporting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
            Exportar XMLs do período
          </button>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : notas.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <FileText className="w-10 h-10 text-gray-600 mb-2" />
            <p className="text-gray-400">Nenhuma nota fiscal no período selecionado.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[600px] text-sm">
              <thead>
                <tr className="text-left text-gray-500 border-b border-surface-600">
                  <th className="py-2 font-medium">Data</th>
                  <th className="py-2 font-medium">Número</th>
                  <th className="py-2 font-medium">Origem</th>
                  <th className="py-2 font-medium">Status</th>
                  <th className="py-2 font-medium text-right">Valor</th>
                </tr>
              </thead>
              <tbody>
                {notas.map(n => (
                  <tr key={n.id} className="border-b border-surface-700 last:border-0">
                    <td className="py-3 text-gray-400">
                      {new Date(n.createdAt).toLocaleDateString('pt-BR')}
                    </td>
                    <td className="py-3 text-white">{n.serie && n.numero ? `${n.serie}/${n.numero}` : '—'}</td>
                    <td className="py-3 text-gray-400">{n.origem}</td>
                    <td className="py-3">
                      <span className={clsx('text-xs font-medium px-2 py-0.5 rounded-full border', STATUS_STYLES[n.status] || 'bg-gray-500/10 text-gray-400 border-gray-500/30')}>
                        {n.status}
                      </span>
                    </td>
                    <td className="py-3 text-right text-white font-mono">{fmt(n.valorTotalEmCentavos)}</td>
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
