'use client'
// =============================================================================
// /contador — Portal do contador. Duas telas: a lista de clientes e o workspace
// de um cliente (em abas). Tudo que é conteúdo vive em components/contador/*;
// aqui fica só a busca da lista e a navegação entre as duas.
// =============================================================================
import { useCallback, useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Calculator } from 'lucide-react'
import { contadorApi, getErrorMessage, type ContadorClienteDto } from '@/lib/api'
import ClientesList from '@/components/contador/ClientesList'
import ClienteWorkspace from '@/components/contador/ClienteWorkspace'

export default function ContadorPage() {
  const [clientes, setClientes] = useState<ContadorClienteDto[]>([])
  const [loading, setLoading] = useState(true)
  const [selecionado, setSelecionado] = useState<ContadorClienteDto | null>(null)

  const carregarClientes = useCallback(() => {
    setLoading(true)
    contadorApi.listClientes()
      .then(r => setClientes(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar lista de clientes')))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { carregarClientes() }, [carregarClientes])

  async function solicitarAcesso(slug: string) {
    try {
      const { data } = await contadorApi.solicitarAcesso(slug)
      toast.success(data.message)
      carregarClientes()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao solicitar acesso'))
    }
  }

  if (selecionado) {
    return <ClienteWorkspace cliente={selecionado} onVoltar={() => setSelecionado(null)} />
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <div className="p-2 rounded-xl bg-brand-500/10">
          <Calculator className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">Meus clientes</h1>
          <p className="text-sm text-gray-400">Lojas vinculadas à sua conta de contador</p>
        </div>
      </div>

      <ClientesList
        clientes={clientes}
        loading={loading}
        onSelecionar={setSelecionado}
        onSolicitarAcesso={solicitarAcesso}
      />
    </div>
  )
}
