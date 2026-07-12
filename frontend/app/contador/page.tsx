'use client'
import { useEffect, useState, useCallback } from 'react'
import { contadorApi, ContadorNotaDto, ContadorConfigDto } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { Calculator, Download, Loader2, FileText } from 'lucide-react'
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
    contadorApi.listNotas({ inicio, fim, pageSize: 100 })
      .then(r => setNotas(r.data.items))
      .catch(() => toast.error('Erro ao carregar notas fiscais'))
      .finally(() => setLoading(false))
  }, [inicio, fim])

  useEffect(() => { fetchNotas() }, [fetchNotas])

  useEffect(() => {
    contadorApi.getConfig().then(r => setConfig(r.data)).catch(() => {})
  }, [])

  async function exportarXmls() {
    if (!inicio || !fim) { toast.error('Selecione o período (início e fim).'); return }
    setExporting(true)
    try {
      const { data } = await contadorApi.exportarXmls(inicio, fim)
      const url = URL.createObjectURL(data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `xmls-fiscais-${inicio}-a-${fim}.zip`
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
      <PageHeader
        icon={Calculator}
        title="Notas Fiscais"
        description="Acesso somente leitura — notas emitidas e exportação de XMLs"
      />

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
