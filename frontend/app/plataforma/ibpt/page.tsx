'use client'

import { useEffect, useRef, useState } from 'react'
import { AlertTriangle, CheckCircle2, FileSpreadsheet, Loader2, RefreshCw, Upload } from 'lucide-react'
import toast from 'react-hot-toast'
import { getErrorMessage, platformIbptApi, type PlatformIbptStatusDto } from '@/lib/api'
import { usePlatformPermissions } from '@/hooks/usePlatformPermissions'

function fmtDate(value: string | null) {
  if (!value) return 'não informada'
  return new Intl.DateTimeFormat('pt-BR', { timeZone: 'UTC' }).format(new Date(value))
}

export default function PlataformaIbptPage() {
  const pode = usePlatformPermissions()
  const inputRef = useRef<HTMLInputElement>(null)
  const [status, setStatus] = useState<PlatformIbptStatusDto[]>([])
  const [loading, setLoading] = useState(true)
  const [uploading, setUploading] = useState(false)

  async function load() {
    setLoading(true)
    try {
      const { data } = await platformIbptApi.list()
      setStatus(data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível consultar as tabelas do IBPT.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  async function upload(file: File) {
    setUploading(true)
    try {
      const { data } = await platformIbptApi.importar(file)
      toast.success(`${data.ncmsImportados.toLocaleString('pt-BR')} NCMs de ${data.uf} publicados na versão ${data.versao ?? 'não informada'}.`)
      if (data.linhasIgnoradas > 0)
        toast(`${data.linhasIgnoradas.toLocaleString('pt-BR')} linha(s) não aplicáveis foram ignoradas.`)
      await load()
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível importar a tabela do IBPT.'))
    } finally {
      setUploading(false)
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <div className="flex items-center gap-2 text-brand-400"><FileSpreadsheet className="h-5 w-5" /><span className="text-xs font-black uppercase tracking-wider">Fiscal global</span></div>
          <h1 className="mt-2 text-2xl font-bold text-white">Tabela IBPT</h1>
          <p className="mt-1 max-w-3xl text-sm text-gray-400">Publique o CSV oficial por UF. A carga substitui atomicamente a versão anterior e passa a atender todas as lojas daquele estado.</p>
        </div>
        <div className="flex gap-2">
          <button type="button" onClick={load} disabled={loading || uploading} className="btn-secondary px-3" title="Atualizar situação"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /></button>
          {pode('platform.tenants.manage') ? <>
            <button type="button" onClick={() => inputRef.current?.click()} disabled={uploading} className="btn-primary justify-center">
              {uploading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
              {uploading ? 'Publicando...' : 'Enviar tabela CSV'}
            </button>
            <input ref={inputRef} type="file" accept=".csv,text/csv" className="hidden" disabled={uploading} onChange={event => { const file = event.target.files?.[0]; event.target.value = ''; if (file) upload(file) }} />
          </> : null}
        </div>
      </header>

      <section className="rounded-xl border border-brand-500/25 bg-brand-500/5 p-4 text-sm text-gray-300">
        Use o arquivo sem renomear: <strong className="text-white">TabelaIBPTaxSP26.1.L.csv</strong>, por exemplo. A UF é lida do nome para impedir que uma tabela estadual seja publicada no estado errado.
      </section>

      {loading ? <div className="card flex min-h-44 items-center justify-center"><Loader2 className="h-6 w-6 animate-spin text-brand-400" /></div> : status.length === 0 ? (
        <div className="card py-14 text-center"><AlertTriangle className="mx-auto h-7 w-7 text-amber-400" /><h2 className="mt-3 font-semibold text-white">Nenhuma tabela publicada</h2><p className="mt-1 text-sm text-gray-500">Envie o primeiro CSV oficial do IBPT para disponibilizar os tributos por NCM.</p></div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {status.map(item => {
            const vencida = item.vigenciaFim ? new Date(item.vigenciaFim).getTime() < Date.now() : false
            return <article key={item.uf} className="card p-5">
              <div className="flex items-start justify-between gap-3"><div><p className="text-2xl font-black text-white">{item.uf}</p><p className="mt-1 text-xs text-gray-500">Versão {item.versao ?? 'não informada'}</p></div><span className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-[11px] font-bold ${vencida ? 'bg-amber-500/10 text-amber-300' : 'bg-emerald-500/10 text-emerald-400'}`}>{vencida ? <AlertTriangle className="h-3 w-3" /> : <CheckCircle2 className="h-3 w-3" />}{vencida ? 'Vencida' : 'Disponível'}</span></div>
              <dl className="mt-5 grid grid-cols-2 gap-3 text-xs"><div><dt className="text-gray-500">NCMs</dt><dd className="mt-1 font-bold text-white">{item.ncms.toLocaleString('pt-BR')}</dd></div><div><dt className="text-gray-500">Vigência</dt><dd className="mt-1 font-bold text-white">até {fmtDate(item.vigenciaFim)}</dd></div><div className="col-span-2"><dt className="text-gray-500">Última publicação</dt><dd className="mt-1 text-gray-300">{fmtDate(item.atualizadoEm)}</dd></div></dl>
            </article>
          })}
        </div>
      )}
    </div>
  )
}
