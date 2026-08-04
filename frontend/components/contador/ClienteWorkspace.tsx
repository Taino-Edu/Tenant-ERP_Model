'use client'
// =============================================================================
// ClienteWorkspace.tsx — Um cliente aberto: cabeçalho, abas e os dados que mais
// de uma aba consome (config, DRE, notas, produtos, apuração).
//
// O período é estado daqui, não de cada aba: mudar o mês no fechamento e voltar
// pra visão geral tem que mostrar o mesmo recorte, senão o contador compara
// números de janelas diferentes sem perceber.
// =============================================================================
import { useCallback, useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import {
  ChevronLeft, LayoutDashboard, CalendarCheck, Scale, Settings2, Package, MessageSquare, Download,
} from 'lucide-react'
import clsx from 'clsx'
import {
  contadorApi, getErrorMessage,
  type ApuracaoTributariaDto, type ContadorClienteDto, type ContadorConfigDto,
  type ContadorNotaDto, type ContadorNotaRecebidaDto, type ContadorProdutoDto, type FinanceiroDto,
} from '@/lib/api'
import Badge from '@/components/admin/ui/Badge'
import Button from '@/components/admin/ui/Button'
import { baixarBlob, brToday, diasAte } from './contador-shared'
import VisaoGeralTab from './VisaoGeralTab'
import FechamentoTab from './FechamentoTab'
import ImpostosTab from './ImpostosTab'
import ConfigFiscalTab from './ConfigFiscalTab'
import EstoqueTab from './EstoqueTab'
import AvisosTab from './AvisosTab'

type AbaId = 'visao' | 'fechamento' | 'impostos' | 'fiscal' | 'estoque' | 'avisos'

const ABAS: Array<{ id: AbaId; label: string; icon: typeof LayoutDashboard }> = [
  { id: 'visao',      label: 'Visão geral',       icon: LayoutDashboard },
  { id: 'fechamento', label: 'Fechamento do mês', icon: CalendarCheck },
  { id: 'impostos',   label: 'Impostos',          icon: Scale },
  { id: 'fiscal',     label: 'Config. fiscal',    icon: Settings2 },
  { id: 'estoque',    label: 'Estoque e NCM',     icon: Package },
  { id: 'avisos',     label: 'Avisos',            icon: MessageSquare },
]

/** Primeiro dia do mês corrente em Brasília, em "yyyy-MM-dd". */
function primeiroDiaDoMes(): string {
  const [ano, mes] = brToday().split('-')
  return `${ano}-${mes}-01`
}

export default function ClienteWorkspace({ cliente, onVoltar }: {
  cliente: ContadorClienteDto
  onVoltar: () => void
}) {
  const [aba, setAba] = useState<AbaId>('visao')

  const [inicio, setInicio] = useState(primeiroDiaDoMes)
  const [fim, setFim] = useState(brToday)

  const [config, setConfig] = useState<ContadorConfigDto | null>(null)
  const [loadingConfig, setLoadingConfig] = useState(true)

  const [dre, setDre] = useState<FinanceiroDto | null>(null)
  const [notas, setNotas] = useState<ContadorNotaDto[]>([])
  const [notasRecebidas, setNotasRecebidas] = useState<ContadorNotaRecebidaDto[]>([])
  const [apuracao, setApuracao] = useState<ApuracaoTributariaDto | null>(null)
  const [loadingPeriodo, setLoadingPeriodo] = useState(true)

  const [produtos, setProdutos] = useState<ContadorProdutoDto[]>([])
  const [loadingProdutos, setLoadingProdutos] = useState(true)

  const [exportando, setExportando] = useState(false)

  // ── Carregamento ──────────────────────────────────────────────────────────

  useEffect(() => {
    setLoadingConfig(true)
    contadorApi.getConfig(cliente.tenantId)
      .then(r => setConfig(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar a configuração fiscal')))
      .finally(() => setLoadingConfig(false))
  }, [cliente.tenantId])

  useEffect(() => {
    setLoadingProdutos(true)
    contadorApi.listProdutos(cliente.tenantId)
      .then(r => setProdutos(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar estoque')))
      .finally(() => setLoadingProdutos(false))
  }, [cliente.tenantId])

  const carregarPeriodo = useCallback(() => {
    setLoadingPeriodo(true)
    Promise.all([
      contadorApi.listNotas(cliente.tenantId, { inicio, fim, pageSize: 100 }),
      contadorApi.listNotasRecebidas(cliente.tenantId, { inicio, fim }),
      contadorApi.getDre(cliente.tenantId, inicio, fim),
      contadorApi.getApuracao(cliente.tenantId, inicio, fim),
    ])
      .then(([saidas, entradas, resultado, tributos]) => {
        setNotas(saidas.data.items)
        setNotasRecebidas(entradas.data)
        setDre(resultado.data)
        setApuracao(tributos.data)
      })
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar o fechamento fiscal')))
      .finally(() => setLoadingPeriodo(false))
  }, [cliente.tenantId, inicio, fim])

  useEffect(() => { carregarPeriodo() }, [carregarPeriodo])

  // ── Competência derivada do período (usada pela aba de fechamento) ────────

  const competencia = useMemo(() => {
    const [ano, mes] = inicio.split('-').map(Number)
    return { ano, mes }
  }, [inicio])

  /** Trocar a competência move o período inteiro pro mês escolhido. */
  const definirCompetencia = useCallback((ano: number, mes: number) => {
    const primeiro = new Date(ano, mes - 1, 1)
    const ultimo = new Date(ano, mes, 0)
    const iso = (d: Date) =>
      `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

    setInicio(iso(primeiro))
    // Mês corrente ainda não terminou: usa hoje como fim, senão a DRE somaria
    // dias no futuro e a apuração ficaria com base incompleta sem avisar.
    const hoje = brToday()
    const fimDoMes = iso(ultimo)
    setFim(fimDoMes > hoje ? hoje : fimDoMes)
  }, [])

  async function exportarXmls() {
    if (!inicio || !fim) { toast.error('Selecione o período (início e fim).'); return }
    setExportando(true)
    try {
      const { data } = await contadorApi.exportarXmls(cliente.tenantId, inicio, fim)
      baixarBlob(data as Blob, `xmls-fiscais-${cliente.slug}-${inicio}-a-${fim}.zip`)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar ZIP de XMLs'))
    } finally {
      setExportando(false)
    }
  }

  const diasCert = diasAte(cliente.certificadoValidade ?? config?.certificadoValidade)

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3 flex-wrap print:hidden">
        <div className="flex items-start gap-3">
          <button onClick={onVoltar} aria-label="Voltar para a lista de clientes"
                  className="btn-secondary py-1.5 px-2.5 mt-0.5">
            <ChevronLeft className="w-4 h-4" />
          </button>
          <div>
            <h1 className="text-xl font-black text-white">{config?.razaoSocial || cliente.slug}</h1>
            <p className="text-sm text-gray-400">
              {cliente.slug}{config?.cnpj ? ` · CNPJ ${config.cnpj}` : ''}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          {config && (
            <Badge tone={config.ambiente === 'Producao' ? 'success' : 'warning'}>
              {config.ambiente === 'Producao' ? 'Produção' : 'Homologação'}
            </Badge>
          )}
          {/* O regime muda a montagem da NFC-e (CST em vez de CSOSN) e a base da
              apuração — vale ficar visível em todas as abas, não só na config. */}
          {config && config.regimeTributario !== 'SimplesNacional' && (
            <Badge tone="neutral">
              {config.regimeTributario === 'LucroPresumido' ? 'Lucro Presumido' : 'Lucro Real'}
            </Badge>
          )}
          {diasCert !== null && diasCert <= 30 && (
            <Badge tone={diasCert <= 7 ? 'danger' : 'warning'}>
              {diasCert < 0 ? 'Certificado vencido' : `Certificado vence em ${diasCert}d`}
            </Badge>
          )}
          <Button variant="secondary" size="sm" onClick={exportarXmls} loading={exportando}>
            {!exportando && <Download className="w-3.5 h-3.5" />} XMLs do período
          </Button>
        </div>
      </div>

      <nav className="flex gap-1 overflow-x-auto border-b border-surface-600 print:hidden" aria-label="Seções do cliente">
        {ABAS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            onClick={() => setAba(id)}
            aria-current={aba === id ? 'page' : undefined}
            className={clsx(
              'flex items-center gap-2 px-3 py-2.5 text-sm font-medium whitespace-nowrap border-b-2 -mb-px transition-colors',
              aba === id
                ? 'border-brand-500 text-brand-400'
                : 'border-transparent text-gray-400 hover:text-white hover:border-surface-400',
            )}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
      </nav>

      {aba === 'visao' && (
        <VisaoGeralTab
          slug={cliente.slug}
          config={config}
          dre={dre}
          notas={notas}
          notasRecebidas={notasRecebidas}
          loading={loadingPeriodo}
          inicio={inicio} fim={fim} onInicio={setInicio} onFim={setFim}
          exportando={exportando}
          onExportarXmls={exportarXmls}
        />
      )}

      {aba === 'fechamento' && (
        <FechamentoTab
          tenantId={cliente.tenantId}
          slug={cliente.slug}
          ano={competencia.ano}
          mes={competencia.mes}
          onCompetencia={definirCompetencia}
          dre={dre}
          notas={notas}
          notasRecebidas={notasRecebidas}
          produtos={produtos}
          apuracao={apuracao}
          loading={loadingPeriodo}
        />
      )}

      {aba === 'impostos' && (
        <ImpostosTab
          apuracao={apuracao}
          loading={loadingPeriodo}
          inicio={inicio} fim={fim} onInicio={setInicio} onFim={setFim}
        />
      )}

      {aba === 'fiscal' && (
        <ConfigFiscalTab
          tenantId={cliente.tenantId}
          config={config}
          loading={loadingConfig}
          onAtualizado={atualizado => {
            setConfig(atualizado)
            // Anexo, folha e presunções mudam a apuração — recarrega pra aba de
            // impostos não continuar mostrando o cálculo antigo.
            carregarPeriodo()
          }}
        />
      )}

      {aba === 'estoque' && (
        <EstoqueTab
          tenantId={cliente.tenantId}
          produtos={produtos}
          loading={loadingProdutos}
          onProdutoAtualizado={atualizado =>
            setProdutos(lista => lista.map(p => p.id === atualizado.id ? atualizado : p))}
        />
      )}

      {aba === 'avisos' && <AvisosTab tenantId={cliente.tenantId} />}
    </div>
  )
}
