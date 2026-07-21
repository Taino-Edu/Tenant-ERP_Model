'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { fiscalApi, FiscalConfigDto, NaturezaOperacaoDto, NotaFiscalDto, SolicitacaoContadorDto, AvisoContadorDto, COMANDA_PAYMENT_METHODS, getErrorMessage } from '@/lib/api'
import toast, { Toaster } from 'react-hot-toast'
import clsx from 'clsx'
import {
  Receipt, Upload, Save, Loader2, AlertTriangle, CheckCircle,
  Plus, Trash2, Download, ShieldCheck, Star, RefreshCw, Ban, ScrollText, Printer,
  Calculator, UserPlus, Check, Clock, CalendarClock, Send, MessageSquare, X,
} from 'lucide-react'

const STATUS_INFO: Record<string, { label: string; color: string }> = {
  PendenteEmissao:         { label: 'Pendente',              color: 'bg-gray-500/15 text-gray-400 border-gray-500/30' },
  Autorizada:              { label: 'Autorizada',            color: 'bg-green-500/15 text-green-400 border-green-500/30' },
  Rejeitada:               { label: 'Rejeitada',             color: 'bg-red-500/15 text-red-400 border-red-500/30' },
  Cancelada:               { label: 'Cancelada',             color: 'bg-amber-500/15 text-amber-400 border-amber-500/30' },
  AutorizadaContingencia:  { label: 'Contingência',          color: 'bg-orange-500/15 text-orange-400 border-orange-500/30' },
}

const REGIMES = [
  { value: 'SimplesNacional', label: 'Simples Nacional' },
  { value: 'LucroPresumido',  label: 'Lucro Presumido' },
  { value: 'LucroReal',       label: 'Lucro Real' },
]

const AMBIENTES = [
  { value: 'Homologacao', label: 'Homologação (testes)' },
  { value: 'Producao',    label: 'Produção' },
]

const CSOSN_OPCOES = [
  { value: '',    label: '— Nenhum —' },
  { value: '102', label: '102 — Tributada sem permissão de crédito (mais comum)' },
  { value: '101', label: '101 — Tributada com permissão de crédito' },
  { value: '103', label: '103 — Isenção por faixa de receita bruta' },
  { value: '201', label: '201 — Com crédito e cobrança de ICMS-ST' },
  { value: '202', label: '202 — Sem crédito e cobrança de ICMS-ST' },
  { value: '203', label: '203 — Isenção e cobrança de ICMS-ST' },
  { value: '300', label: '300 — Imune' },
  { value: '400', label: '400 — Não tributada' },
  { value: '500', label: '500 — ICMS já retido antes (substituição tributária)' },
  { value: '900', label: '900 — Outros' },
]

function fmtDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('pt-BR')
}

export default function FiscalPage() {
  const [config,   setConfig]   = useState<FiscalConfigDto | null>(null)
  const [loading,  setLoading]  = useState(true)
  const [saving,   setSaving]   = useState(false)

  // Formulário de dados da empresa
  const [cnpj, setCnpj]                   = useState('')
  const [razaoSocial, setRazaoSocial]     = useState('')
  const [ie, setIe]                       = useState('')
  const [regime, setRegime]               = useState('SimplesNacional')
  const [ambiente, setAmbiente]           = useState('Homologacao')
  const [serieNfce, setSerieNfce]         = useState(1)
  const [emailContador, setEmailContador] = useState('')

  // Endereço do estabelecimento (obrigatório no XML da NFC-e)
  const [logradouro, setLogradouro]               = useState('')
  const [numero, setNumero]                       = useState('')
  const [complemento, setComplemento]             = useState('')
  const [bairro, setBairro]                       = useState('')
  const [codigoMunicipioIbge, setCodigoMunicipio] = useState('')
  const [municipio, setMunicipio]                 = useState('')
  const [uf, setUf]                               = useState('')
  const [cep, setCep]                             = useState('')

  // CSC (Código de Segurança do Contribuinte) — usado pro QR Code do cupom
  const [cscId, setCscId]       = useState('')
  const [cscToken, setCscToken] = useState('')

  // Formas de pagamento que emitem NFC-e sozinhas ao fechar a venda, sem perguntar.
  // Vazio por padrão — o admin decide a cada fechamento via checkbox (ver /admin/comanda
  // e /admin/venda-avulsa). Marcar aqui só muda o valor pré-marcado desse checkbox.
  const [autoEmit, setAutoEmit] = useState<string[]>([])

  // Upload de certificado
  const [certFile, setCertFile] = useState<File | null>(null)
  const [certSenha, setCertSenha] = useState('')
  const [uploadingCert, setUploadingCert] = useState(false)

  // Naturezas de operação
  const [naturezas, setNaturezas] = useState<NaturezaOperacaoDto[]>([])
  const [novaDescricao, setNovaDescricao] = useState('')
  const [novoCfop, setNovoCfop]           = useState('')
  const [novoCsosn, setNovoCsosn]         = useState('')
  const [novoPercentualCredito, setNovoPercentualCredito] = useState('')
  const [novaOrigemMercadoria, setNovaOrigemMercadoria] = useState('0')
  const [novaModalidadeBcSt, setNovaModalidadeBcSt] = useState('4')
  const [novoMvaSt, setNovoMvaSt] = useState('')
  const [novaReducaoBcSt, setNovaReducaoBcSt] = useState('0')
  const [novaAliquotaIcmsSt, setNovaAliquotaIcmsSt] = useState('')
  const [novaAliquotaIcmsProprio, setNovaAliquotaIcmsProprio] = useState('')
  const [novaAliquotaFcpSt, setNovaAliquotaFcpSt] = useState('0')
  const [novaBaseStFixa, setNovaBaseStFixa] = useState('')
  const [novoIbsCbsCst, setNovoIbsCbsCst] = useState('000')
  const [novoIbsCbsClassTrib, setNovoIbsCbsClassTrib] = useState('000001')
  const [novoPadrao, setNovoPadrao]       = useState(false)
  const [savingNatureza, setSavingNatureza] = useState(false)

  // Exportação de XMLs
  const [inicio, setInicio] = useState('')
  const [fim, setFim]       = useState('')
  const [exporting, setExporting] = useState(false)

  // Inutilização explícita de lacunas de numeração
  const [inutAno, setInutAno] = useState(new Date().getFullYear())
  const [inutSerie, setInutSerie] = useState(1)
  const [inutInicio, setInutInicio] = useState(1)
  const [inutFim, setInutFim] = useState(1)
  const [inutJustificativa, setInutJustificativa] = useState('')
  const [inutilizando, setInutilizando] = useState(false)

  // Histórico de notas
  const [notas, setNotas]               = useState<NotaFiscalDto[]>([])
  const [notasLoading, setNotasLoading] = useState(true)
  const [pendentesCount, setPendentesCount] = useState(0)
  const [pendenteMaisAntiga, setPendenteMaisAntiga] = useState<string | undefined>()
  const [reprocessingId, setReprocessingId] = useState<string | null>(null)
  const [cancelModalId, setCancelModalId]   = useState<string | null>(null)
  const [cancelJustificativa, setCancelJustificativa] = useState('')
  const [cancelling, setCancelling] = useState(false)

  // Contador — convite e aprovação de solicitações de acesso
  const [convidarEmail, setConvidarEmail] = useState('')
  const [convidando, setConvidando] = useState(false)
  const [solicitacoes, setSolicitacoes] = useState<SolicitacaoContadorDto[]>([])
  const [loadingSolicitacoes, setLoadingSolicitacoes] = useState(true)
  const [aprovandoId, setAprovandoId] = useState<string | null>(null)
  const [recusandoId, setRecusandoId] = useState<string | null>(null)

  // Contador — avisos (mural de recados entre lojista e contador)
  const [avisos, setAvisos] = useState<AvisoContadorDto[]>([])
  const [loadingAvisos, setLoadingAvisos] = useState(true)
  const [novoAviso, setNovoAviso] = useState('')
  const [enviandoAviso, setEnviandoAviso] = useState(false)
  const [avisoLinkId, setAvisoLinkId] = useState('')

  // Só entra na hora de escrever o aviso se houver MAIS de um contador
  // aprovado — no caso comum (0 ou 1) o formulário fica igual a antes.
  const contadoresAprovados = solicitacoes.filter(s => s.status === 'Approved')

  async function loadNotas(showLoading = true) {
    if (showLoading) setNotasLoading(true)
    try {
      const { data } = await fiscalApi.listNotas({ pageSize: 30 })
      setNotas(data.items)
      setPendentesCount(data.pendentesCount)
      setPendenteMaisAntiga(data.pendenteMaisAntiga)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar notas emitidas'))
    } finally {
      if (showLoading) setNotasLoading(false)
    }
  }

  useEffect(() => { loadNotas() }, [])

  async function loadSolicitacoes() {
    setLoadingSolicitacoes(true)
    try {
      const { data } = await fiscalApi.listSolicitacoesContador()
      setSolicitacoes(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar solicitações de contador'))
    } finally {
      setLoadingSolicitacoes(false)
    }
  }

  useEffect(() => {
    loadSolicitacoes()
    const interval = setInterval(loadSolicitacoes, 20000)
    return () => clearInterval(interval)
  }, [])

  async function convidarContador(e: React.FormEvent) {
    e.preventDefault()
    if (!convidarEmail.trim()) return
    setConvidando(true)
    try {
      const { data } = await fiscalApi.convidarContador(convidarEmail.trim())
      toast.success(data.message)
      setConvidarEmail('')
      loadSolicitacoes()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao convidar contador'))
    } finally {
      setConvidando(false)
    }
  }

  async function aprovarSolicitacao(linkId: string) {
    setAprovandoId(linkId)
    try {
      await fiscalApi.aprovarSolicitacaoContador(linkId)
      toast.success('Solicitação aprovada!')
      loadSolicitacoes()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao aprovar solicitação'))
    } finally {
      setAprovandoId(null)
    }
  }

  async function recusarSolicitacao(linkId: string) {
    setRecusandoId(linkId)
    try {
      await fiscalApi.recusarSolicitacaoContador(linkId)
      toast.success('Solicitação recusada.')
      loadSolicitacoes()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao recusar solicitação'))
    } finally {
      setRecusandoId(null)
    }
  }

  async function loadAvisos() {
    setLoadingAvisos(true)
    try {
      const { data } = await fiscalApi.listAvisosContador()
      setAvisos(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar avisos do contador'))
    } finally {
      setLoadingAvisos(false)
    }
  }

  useEffect(() => { loadAvisos() }, [])

  async function enviarAviso(e: React.FormEvent) {
    e.preventDefault()
    if (!novoAviso.trim()) return
    if (contadoresAprovados.length > 1 && !avisoLinkId) {
      toast.error('Selecione pra qual contador é o aviso.')
      return
    }
    setEnviandoAviso(true)
    try {
      await fiscalApi.postAvisoContador(novoAviso.trim(), avisoLinkId || undefined)
      setNovoAviso('')
      loadAvisos()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao enviar aviso'))
    } finally {
      setEnviandoAviso(false)
    }
  }

  async function reprocessarNota(id: string) {
    setReprocessingId(id)
    try {
      const { data } = await fiscalApi.reprocessarNota(id)
      setNotas(prev => prev.map(n => n.id === id
        ? { ...n, status: data.status, motivoRejeicao: data.motivoRejeicao }
        : n))
      toast[data.status === 'Autorizada' ? 'success' : 'error'](
        data.status === 'Autorizada' ? 'Nota autorizada!' : `Ainda não autorizou: ${data.status}${data.motivoRejeicao ? ' — ' + data.motivoRejeicao : ''}`,
        { duration: data.status === 'Autorizada' ? 4000 : 10000 })
      await loadNotas(false)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao reprocessar'))
    } finally {
      setReprocessingId(null)
    }
  }

  async function confirmarCancelamento() {
    if (!cancelModalId) return
    if (cancelJustificativa.trim().length < 15) {
      toast.error('Justificativa precisa ter pelo menos 15 caracteres.')
      return
    }
    setCancelling(true)
    try {
      const { data } = await fiscalApi.cancelarNota(cancelModalId, cancelJustificativa.trim())
      if (data.erpEstornoErro)
        toast.error(`Nota cancelada na SEFAZ, mas o estorno ERP exige ação: ${data.erpEstornoErro}`, { duration: 10000 })
      else
        toast.success('Nota cancelada e ERP estornado!')
      setCancelModalId(null)
      setCancelJustificativa('')
      loadNotas()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao cancelar'))
    } finally {
      setCancelling(false)
    }
  }

  async function reprocessarEstornoErp(id: string) {
    setReprocessingId(id)
    try {
      const { data } = await fiscalApi.reprocessarEstornoErp(id)
      if (data.erpEstornadoEm) toast.success('Estorno ERP concluído!')
      else toast.error(`Estorno ainda pendente: ${data.erpEstornoErro ?? 'verifique os dados da venda'}`)
      loadNotas()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao reprocessar estorno'))
    } finally {
      setReprocessingId(null)
    }
  }

  async function inutilizarFaixa() {
    if (inutJustificativa.trim().length < 15) {
      toast.error('A justificativa precisa ter pelo menos 15 caracteres.')
      return
    }
    if (!window.confirm(
      `Inutilizar definitivamente a faixa ${inutSerie}/${inutInicio}-${inutFim} de ${inutAno}? Esta operação não pode ser desfeita.`
    )) return

    setInutilizando(true)
    try {
      const { data } = await fiscalApi.inutilizarFaixa({
        ano: inutAno, serie: inutSerie, numeroInicial: inutInicio,
        numeroFinal: inutFim, justificativa: inutJustificativa.trim(),
      })
      toast.success(`Faixa inutilizada. Protocolo ${data.protocolo}`)
      setInutJustificativa('')
      loadNotas()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao inutilizar faixa'))
    } finally {
      setInutilizando(false)
    }
  }

  async function load() {
    setLoading(true)
    try {
      const [{ data: cfg }, { data: nats }] = await Promise.all([
        fiscalApi.getConfig(),
        fiscalApi.listNaturezas(),
      ])
      setConfig(cfg)
      setCnpj(cfg.cnpj ?? '')
      setRazaoSocial(cfg.razaoSocial ?? '')
      setIe(cfg.inscricaoEstadual ?? '')
      setRegime(cfg.regimeTributario ?? 'SimplesNacional')
      setAmbiente(cfg.ambiente ?? 'Homologacao')
      setSerieNfce(cfg.serieNfce ?? 1)
      setEmailContador(cfg.emailContador ?? '')
      setLogradouro(cfg.logradouro ?? '')
      setNumero(cfg.numero ?? '')
      setComplemento(cfg.complemento ?? '')
      setBairro(cfg.bairro ?? '')
      setCodigoMunicipio(cfg.codigoMunicipioIbge ?? '')
      setMunicipio(cfg.municipio ?? '')
      setUf(cfg.uf ?? '')
      setCep(cfg.cep ?? '')
      setCscId(cfg.cscId ?? '')
      setAutoEmit(cfg.formasPagamentoAutoEmissao ?? [])
      setNaturezas(nats)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar dados fiscais'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  async function saveConfig() {
    setSaving(true)
    try {
      const { data } = await fiscalApi.saveConfig({
        cnpj, razaoSocial, inscricaoEstadual: ie, regimeTributario: regime,
        ambiente, serieNfce, emailContador,
        logradouro, numero, complemento, bairro,
        codigoMunicipioIbge, municipio, uf, cep,
        cscId, ...(cscToken ? { cscToken } : {}),
        formasPagamentoAutoEmissao: autoEmit,
      })
      setConfig(data)
      toast.success('Configuração fiscal salva!')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar configuração fiscal'))
    } finally {
      setSaving(false)
    }
  }

  function toggleAutoEmit(method: string) {
    setAutoEmit(prev => prev.includes(method) ? prev.filter(m => m !== method) : [...prev, method])
  }

  async function uploadCertificado() {
    if (!certFile || !certSenha) {
      toast.error('Selecione o arquivo .pfx e informe a senha.')
      return
    }
    setUploadingCert(true)
    try {
      await fiscalApi.uploadCertificado(certFile, certSenha)
      toast.success('Certificado validado e salvo com sucesso!')
      setCertFile(null)
      setCertSenha('')
      load()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao validar certificado'))
    } finally {
      setUploadingCert(false)
    }
  }

  async function addNatureza() {
    if (!novaDescricao || !novoCfop) {
      toast.error('Descrição e CFOP são obrigatórios.')
      return
    }
    setSavingNatureza(true)
    try {
      const numero = (valor: string) => valor === '' ? undefined : Number(valor)
      await fiscalApi.createNatureza({
        descricao: novaDescricao, cfop: novoCfop,
        csosn: novoCsosn || undefined,
        percentualCreditoSn: ['101', '201'].includes(novoCsosn) && novoPercentualCredito ? Number(novoPercentualCredito) : undefined,
        origemMercadoria: Number(novaOrigemMercadoria),
        modalidadeBcSt: ['201', '202', '203'].includes(novoCsosn) ? Number(novaModalidadeBcSt) : undefined,
        percentualMvaSt: novaModalidadeBcSt === '4' ? numero(novoMvaSt) : undefined,
        percentualReducaoBcSt: numero(novaReducaoBcSt),
        aliquotaIcmsSt: numero(novaAliquotaIcmsSt),
        aliquotaIcmsProprio: numero(novaAliquotaIcmsProprio),
        aliquotaFcpSt: numero(novaAliquotaFcpSt),
        baseStFixaEmCentavos: !['4', '6'].includes(novaModalidadeBcSt) && novaBaseStFixa
          ? Math.round(Number(novaBaseStFixa) * 100) : undefined,
        ibsCbsCst: novoIbsCbsCst,
        ibsCbsClassTrib: novoIbsCbsClassTrib,
        isPadrao: novoPadrao,
      })
      setNovaDescricao(''); setNovoCfop(''); setNovoCsosn(''); setNovoPercentualCredito(''); setNovoPadrao(false)
      setNovaOrigemMercadoria('0'); setNovaModalidadeBcSt('4'); setNovoMvaSt('')
      setNovaReducaoBcSt('0'); setNovaAliquotaIcmsSt(''); setNovaAliquotaIcmsProprio('')
      setNovaAliquotaFcpSt('0'); setNovaBaseStFixa(''); setNovoIbsCbsCst('000'); setNovoIbsCbsClassTrib('000001')
      toast.success('Natureza de operação criada!')
      load()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao criar natureza de operação'))
    } finally {
      setSavingNatureza(false)
    }
  }

  async function removeNatureza(id: string) {
    if (!confirm('Remover esta natureza de operação?')) return
    try {
      await fiscalApi.removeNatureza(id)
      setNaturezas(prev => prev.filter(n => n.id !== id))
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao remover'))
    }
  }

  async function exportarXmls() {
    if (!inicio || !fim) {
      toast.error('Selecione o período (início e fim).')
      return
    }
    setExporting(true)
    try {
      const { data } = await fiscalApi.exportarXmls(inicio, fim)
      const url = URL.createObjectURL(data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `xmls-fiscais-${inicio}-a-${fim}.zip`
      a.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar ZIP de XMLs'))
    } finally {
      setExporting(false)
    }
  }

  const hojeDia = new Date().getDate()
  const dasProximoAoVencimento = hojeDia <= 20 && (20 - hojeDia) <= 5

  const diasParaVencer = config?.diasParaVencer
  const certStatusColor = !config?.certificadoConfigurado
    ? 'gray'
    : diasParaVencer !== undefined && diasParaVencer !== null && diasParaVencer <= 7
      ? 'red'
      : diasParaVencer !== undefined && diasParaVencer !== null && diasParaVencer <= 30
        ? 'amber'
        : 'green'

  if (loading) {
    return <div className="flex justify-center py-16"><Loader2 className="w-8 h-8 animate-spin text-brand-400" /></div>
  }

  return (
    <div className="p-4 md:p-6 max-w-3xl mx-auto flex flex-col gap-6">
      <Toaster />

      <div className="flex items-center gap-3">
        <div className="p-2 rounded-xl bg-brand-500/10">
          <Receipt className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">Fiscal — NFC-e</h1>
          <p className="text-sm text-gray-400">Certificado digital, dados da empresa e naturezas de operação</p>
        </div>
      </div>

      {/* Certificado */}
      <div className={clsx('card p-5', certStatusColor === 'red' && 'border-red-500/30', certStatusColor === 'green' && 'border-green-500/20')}>
        <h3 className="font-bold text-white flex items-center gap-2 mb-3">
          <ShieldCheck className="w-4 h-4 text-brand-400" /> Certificado Digital A1
        </h3>

        {config?.certificadoConfigurado ? (
          <div className={clsx(
            'flex items-center gap-2 text-sm rounded-lg p-3 mb-4',
            certStatusColor === 'red'   && 'bg-red-500/10 text-red-400',
            certStatusColor === 'amber' && 'bg-amber-500/10 text-amber-400',
            certStatusColor === 'green' && 'bg-green-500/10 text-green-400',
          )}>
            {certStatusColor === 'green' ? <CheckCircle className="w-4 h-4 shrink-0" /> : <AlertTriangle className="w-4 h-4 shrink-0" />}
            Válido até <strong>{fmtDate(config.certificadoValidade)}</strong>
            {diasParaVencer !== undefined && diasParaVencer !== null && (
              <span>— {diasParaVencer >= 0 ? `${diasParaVencer} dia(s) restantes` : 'VENCIDO'}</span>
            )}
          </div>
        ) : (
          <div className="flex items-center gap-2 text-sm text-gray-400 bg-surface-800/50 rounded-lg p-3 mb-4">
            <AlertTriangle className="w-4 h-4 shrink-0" /> Nenhum certificado configurado ainda.
          </div>
        )}

        <div className="flex flex-col sm:flex-row gap-3">
          <label className="flex-1 flex items-center gap-2 px-3 py-2.5 rounded-xl bg-surface-700 border border-surface-600 text-sm text-gray-300 cursor-pointer hover:bg-surface-500">
            <Upload className="w-4 h-4 shrink-0" />
            <span className="truncate">{certFile ? certFile.name : 'Selecionar arquivo .pfx'}</span>
            <input type="file" accept=".pfx,.p12" className="hidden"
                   onChange={e => setCertFile(e.target.files?.[0] ?? null)} />
          </label>
          <input
            type="password" placeholder="Senha do certificado" value={certSenha}
            onChange={e => setCertSenha(e.target.value)}
            className="input sm:w-48" />
          <button onClick={uploadCertificado} disabled={uploadingCert} className="btn-primary justify-center">
            {uploadingCert ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
            Enviar
          </button>
        </div>
      </div>

      {/* Dados da empresa */}
      <div className="card p-5">
        <h3 className="font-bold text-white mb-3">Dados da Empresa Emitente</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">CNPJ</label>
            <input value={cnpj} onChange={e => setCnpj(e.target.value)} placeholder="00.000.000/0001-00" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Razão Social</label>
            <input value={razaoSocial} onChange={e => setRazaoSocial(e.target.value)} placeholder="Nome da empresa na Receita" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Inscrição Estadual</label>
            <input value={ie} onChange={e => setIe(e.target.value)} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Regime Tributário</label>
            <select value={regime} onChange={e => setRegime(e.target.value)} className="input w-full">
              {REGIMES.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Ambiente SEFAZ</label>
            <select value={ambiente} onChange={e => setAmbiente(e.target.value)} className="input w-full">
              {AMBIENTES.map(a => <option key={a.value} value={a.value}>{a.label}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Série NFC-e</label>
            <input type="number" min={1} value={serieNfce} onChange={e => setSerieNfce(Number(e.target.value))} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Email do Contador</label>
            <input type="email" value={emailContador} onChange={e => setEmailContador(e.target.value)} placeholder="contador@email.com" className="input w-full" />
          </div>
        </div>

        <h4 className="text-xs uppercase tracking-wider text-gray-500 font-bold mt-5 mb-3">Endereço do Estabelecimento</h4>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="sm:col-span-2">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Logradouro</label>
            <input value={logradouro} onChange={e => setLogradouro(e.target.value)} placeholder="Rua/Av." className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Número</label>
            <input value={numero} onChange={e => setNumero(e.target.value)} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Complemento</label>
            <input value={complemento} onChange={e => setComplemento(e.target.value)} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Bairro</label>
            <input value={bairro} onChange={e => setBairro(e.target.value)} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">CEP</label>
            <input value={cep} onChange={e => setCep(e.target.value)} placeholder="00000-000" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Município</label>
            <input value={municipio} onChange={e => setMunicipio(e.target.value)} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Código IBGE do Município</label>
            <input value={codigoMunicipioIbge} onChange={e => setCodigoMunicipio(e.target.value)} placeholder="7 dígitos" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">UF</label>
            <input value={uf} onChange={e => setUf(e.target.value.toUpperCase())} maxLength={2} placeholder="SP" className="input w-full" />
          </div>
        </div>

        <h4 className="text-xs uppercase tracking-wider text-gray-500 font-bold mt-5 mb-3">
          CSC (QR Code do cupom) {config?.cscConfigurado && <span className="text-green-400 normal-case">· configurado</span>}
        </h4>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">ID do CSC</label>
            <input value={cscId} onChange={e => setCscId(e.target.value)} placeholder="000001" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Token do CSC</label>
            <input type="password" value={cscToken} onChange={e => setCscToken(e.target.value)}
                   placeholder={config?.cscConfigurado ? '••••••••' : 'Obtido na SEFAZ'} className="input w-full" />
          </div>
        </div>
        <p className="text-xs text-gray-400 mt-1">
          Sem o CSC, o cupom funciona mas o QR Code fica sem o hash de segurança oficial.
        </p>

        <button onClick={saveConfig} disabled={saving} className="btn-primary mt-4">
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          Salvar
        </button>
      </div>

      {/* Emissão automática por forma de pagamento */}
      <div className="card p-5">
        <h3 className="font-bold text-white mb-1">Emissão automática de nota fiscal</h3>
        <p className="text-xs text-gray-400 mb-4">
          Por padrão, nenhuma forma de pagamento emite nota sozinha — ao fechar uma venda, sempre
          aparece a opção de escolher "Emitir cupom fiscal agora". Marque aqui só as formas de
          pagamento em que essa opção deve vir <strong>pré-marcada</strong> (ainda é possível
          desmarcar na hora do fechamento). Vendas sem nota emitida podem receber a nota depois,
          pelo botão "Emitir nota fiscal" no histórico.
        </p>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
          {COMANDA_PAYMENT_METHODS.map(m => (
            <button
              key={m.value}
              type="button"
              onClick={() => toggleAutoEmit(m.value)}
              className={clsx(
                'flex items-center justify-between gap-2 px-3 py-2.5 rounded-xl border text-sm transition-all text-left',
                autoEmit.includes(m.value)
                  ? 'bg-brand-600/10 border-brand-500/40 text-brand-300'
                  : 'border-surface-500 text-gray-400 hover:border-surface-400 hover:text-gray-200'
              )}
            >
              <span>{m.label}</span>
              <span className={clsx('w-4 h-4 rounded border flex items-center justify-center text-xs shrink-0',
                autoEmit.includes(m.value) ? 'bg-brand-500 border-brand-500 text-white' : 'border-surface-400'
              )}>
                {autoEmit.includes(m.value) && '✓'}
              </span>
            </button>
          ))}
        </div>
        <button onClick={saveConfig} disabled={saving} className="btn-primary mt-4">
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          Salvar
        </button>
      </div>

      {/* Naturezas de operação */}
      <div className="card p-5">
        <h3 className="font-bold text-white mb-3">Naturezas de Operação</h3>
        <p className="text-xs text-gray-400 mb-4">
          Cadastre CFOP/CSOSN uma vez e vincule aos produtos — em vez de configurar item a item.
        </p>

        <div className="flex flex-col gap-2 mb-4">
          {naturezas.map(n => (
            <div key={n.id} className="flex items-center gap-3 bg-surface-800/50 rounded-xl p-3 border border-surface-700/50">
              {n.isPadrao && <Star className="w-3.5 h-3.5 text-accent-gold shrink-0" fill="currentColor" />}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-white truncate">{n.descricao}</p>
                <p className="text-xs text-gray-500">
                  CFOP {n.cfop}{n.csosn ? ` · CSOSN ${n.csosn}` : ''}
                  {n.csosn === '101' && n.percentualCreditoIcmsSn != null && ` (${n.percentualCreditoIcmsSn}% crédito)`}
                </p>
              </div>
              <button onClick={() => removeNatureza(n.id)} className="text-gray-500 hover:text-red-400 p-1">
                <Trash2 className="w-4 h-4" />
              </button>
            </div>
          ))}
          {naturezas.length === 0 && (
            <p className="text-sm text-gray-500 text-center py-2">Nenhuma natureza cadastrada ainda.</p>
          )}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-[1fr_100px_1fr_auto] gap-2 items-end">
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Descrição</label>
            <input value={novaDescricao} onChange={e => setNovaDescricao(e.target.value)}
                   placeholder="Venda de mercadoria dentro do estado" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">CFOP</label>
            <input value={novoCfop} onChange={e => setNovoCfop(e.target.value)} placeholder="5102" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">CSOSN</label>
            <select value={novoCsosn} onChange={e => setNovoCsosn(e.target.value)} className="input w-full">
              {CSOSN_OPCOES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <button onClick={addNatureza} disabled={savingNatureza} className="btn-primary justify-center">
            {savingNatureza ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
          </button>
        </div>
        {['101', '201'].includes(novoCsosn) && (
          <div className="mt-2 max-w-[200px]">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">% de crédito de ICMS</label>
            <input type="number" min={0} max={100} step={0.01} value={novoPercentualCredito}
                   onChange={e => setNovoPercentualCredito(e.target.value)} placeholder="Ex: 2.5" className="input w-full" />
          </div>
        )}
        <details className="mt-3 rounded-xl border border-surface-600 bg-surface-800/40 p-3">
          <summary className="cursor-pointer text-sm font-semibold text-gray-300">
            Parâmetros tributários avançados
          </summary>
          <p className="text-xs text-amber-300/90 mt-2">
            Preencha conforme orientação do contador. Os valores pertencem somente a esta loja e natureza.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-3">
            <div>
              <label className="text-xs text-gray-400 font-semibold mb-1 block">Origem da mercadoria</label>
              <select value={novaOrigemMercadoria} onChange={e => setNovaOrigemMercadoria(e.target.value)} className="input w-full">
                <option value="0">0 — Nacional</option><option value="1">1 — Importação direta</option>
                <option value="2">2 — Estrangeira, mercado interno</option><option value="3">3 — Nacional, importação &gt; 40%</option>
                <option value="4">4 — Nacional, processo básico</option><option value="5">5 — Nacional, importação ≤ 40%</option>
                <option value="6">6 — Importação sem similar</option><option value="7">7 — Estrangeira sem similar</option>
                <option value="8">8 — Nacional, importação &gt; 70%</option>
              </select>
            </div>
            <div>
              <label className="text-xs text-gray-400 font-semibold mb-1 block">CST IBS/CBS</label>
              <input value={novoIbsCbsCst} maxLength={3} onChange={e => setNovoIbsCbsCst(e.target.value.replace(/\D/g, ''))} className="input w-full" />
            </div>
            <div>
              <label className="text-xs text-gray-400 font-semibold mb-1 block">cClassTrib</label>
              <input value={novoIbsCbsClassTrib} maxLength={6} onChange={e => setNovoIbsCbsClassTrib(e.target.value.replace(/\D/g, ''))} className="input w-full" />
            </div>
          </div>

          {['201', '202', '203'].includes(novoCsosn) && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mt-3 border-t border-surface-600 pt-3">
              <div>
                <label className="text-xs text-gray-400 font-semibold mb-1 block">Modalidade BC-ST</label>
                <select value={novaModalidadeBcSt} onChange={e => setNovaModalidadeBcSt(e.target.value)} className="input w-full">
                  <option value="4">4 — MVA (%)</option><option value="6">6 — Valor da operação</option>
                  <option value="0">0 — Preço tabelado</option><option value="1">1 — Lista negativa</option>
                  <option value="2">2 — Lista positiva</option><option value="3">3 — Lista neutra</option>
                  <option value="5">5 — Pauta</option>
                </select>
              </div>
              {novaModalidadeBcSt === '4' ? <div>
                <label className="text-xs text-gray-400 font-semibold mb-1 block">MVA-ST (%)</label>
                <input type="number" min={0} step={0.01} value={novoMvaSt} onChange={e => setNovoMvaSt(e.target.value)} className="input w-full" />
              </div> : !['6'].includes(novaModalidadeBcSt) ? <div>
                <label className="text-xs text-gray-400 font-semibold mb-1 block">Base/pauta por un. (R$)</label>
                <input type="number" min={0.01} step={0.01} value={novaBaseStFixa} onChange={e => setNovaBaseStFixa(e.target.value)} className="input w-full" />
              </div> : null}
              <div><label className="text-xs text-gray-400 font-semibold mb-1 block">Redução BC-ST (%)</label><input type="number" min={0} max={100} step={0.01} value={novaReducaoBcSt} onChange={e => setNovaReducaoBcSt(e.target.value)} className="input w-full" /></div>
              <div><label className="text-xs text-gray-400 font-semibold mb-1 block">Alíquota ICMS-ST (%)</label><input type="number" min={0} max={100} step={0.01} value={novaAliquotaIcmsSt} onChange={e => setNovaAliquotaIcmsSt(e.target.value)} className="input w-full" /></div>
              <div><label className="text-xs text-gray-400 font-semibold mb-1 block">ICMS próprio (%)</label><input type="number" min={0} max={100} step={0.01} value={novaAliquotaIcmsProprio} onChange={e => setNovaAliquotaIcmsProprio(e.target.value)} className="input w-full" /></div>
              <div><label className="text-xs text-gray-400 font-semibold mb-1 block">FCP-ST (%)</label><input type="number" min={0} max={100} step={0.01} value={novaAliquotaFcpSt} onChange={e => setNovaAliquotaFcpSt(e.target.value)} className="input w-full" /></div>
            </div>
          )}
        </details>
        <label className="flex items-center gap-2 mt-2 text-xs text-gray-400 cursor-pointer">
          <input type="checkbox" checked={novoPadrao} onChange={e => setNovoPadrao(e.target.checked)} />
          Definir como padrão
        </label>
      </div>

      {/* Exportação de XMLs */}
      <div className="card p-5">
        <h3 className="font-bold text-white mb-3">Exportar XMLs para o Contador</h3>
        <p className="text-xs text-gray-400 mb-4">
          Gera um .zip com as NFC-e autorizadas e canceladas no período selecionado.
          Um envio automático também acontece todo dia 1 do mês para o email do contador, se configurado.
        </p>
        <div className="flex flex-col sm:flex-row gap-3 items-end">
          <div className="flex-1">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Início</label>
            <input type="date" value={inicio} onChange={e => setInicio(e.target.value)} className="input w-full" />
          </div>
          <div className="flex-1">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Fim</label>
            <input type="date" value={fim} onChange={e => setFim(e.target.value)} className="input w-full" />
          </div>
          <button onClick={exportarXmls} disabled={exporting} className="btn-primary justify-center">
            {exporting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
            Baixar ZIP
          </button>
        </div>
      </div>

      {/* Inutilização explícita de numeração */}
      <div className="card p-5 border border-red-500/20">
        <h3 className="font-bold text-white mb-2 flex items-center gap-2">
          <Ban className="w-4 h-4 text-red-400" /> Inutilizar faixa de numeração
        </h3>
        <p className="text-xs text-gray-400 mb-4">
          Use somente para números abandonados que nunca foram autorizados. A SEFAZ não permite desfazer esta operação.
        </p>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-3">
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Ano</label>
            <input type="number" value={inutAno} onChange={e => setInutAno(Number(e.target.value))} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Série</label>
            <input type="number" min={1} max={999} value={inutSerie} onChange={e => setInutSerie(Number(e.target.value))} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Número inicial</label>
            <input type="number" min={1} value={inutInicio} onChange={e => setInutInicio(Number(e.target.value))} className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Número final</label>
            <input type="number" min={1} value={inutFim} onChange={e => setInutFim(Number(e.target.value))} className="input w-full" />
          </div>
        </div>
        <div className="flex flex-col sm:flex-row gap-3 items-end">
          <div className="flex-1 w-full">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Justificativa (mínimo 15 caracteres)</label>
            <input value={inutJustificativa} onChange={e => setInutJustificativa(e.target.value)}
                   maxLength={255} placeholder="Motivo pelo qual a faixa não será utilizada" className="input w-full" />
          </div>
          <button onClick={inutilizarFaixa} disabled={inutilizando}
                  className="px-4 py-3 rounded-xl bg-red-500/10 hover:bg-red-500/20 border border-red-500/30 text-sm font-bold text-red-400 flex items-center justify-center gap-2">
            {inutilizando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Ban className="w-4 h-4" />}
            Inutilizar definitivamente
          </button>
        </div>
      </div>

      {/* Contador — convite e aprovação de acesso */}
      <div className="card p-5">
        <h3 className="font-bold text-white flex items-center gap-2 mb-1">
          <Calculator className="w-4 h-4 text-brand-400" /> Contador
        </h3>
        <p className="text-xs text-gray-400 mb-4">
          O contador cria a própria conta em <strong>/contador/cadastro</strong> e escolhe o slug da
          sua loja. Se ele já tiver conta, convide pelo e-mail abaixo (acesso liberado na hora). Se
          ele solicitou acesso por conta própria, aprove a solicitação na lista.
        </p>

        {regime === 'SimplesNacional' && (
          <div className={clsx(
            'flex items-center gap-2 text-sm rounded-lg p-3 mb-4',
            dasProximoAoVencimento ? 'bg-amber-500/10 text-amber-400' : 'bg-surface-800/50 text-gray-400'
          )}>
            <CalendarClock className="w-4 h-4 shrink-0" />
            Lembrete: DAS vence todo dia 20.
          </div>
        )}

        <form onSubmit={convidarContador} className="flex flex-col sm:flex-row gap-3 mb-4">
          <input
            type="email" className="input flex-1" placeholder="contador@escritorio.com"
            value={convidarEmail} onChange={e => setConvidarEmail(e.target.value)} required
          />
          <button type="submit" disabled={convidando} className="btn-primary justify-center">
            {convidando ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
            Convidar
          </button>
        </form>

        {loadingSolicitacoes ? (
          <div className="flex justify-center py-6"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
        ) : solicitacoes.length === 0 ? (
          <p className="text-sm text-gray-500 text-center py-2">Nenhum contador vinculado ainda.</p>
        ) : (
          <div className="flex flex-col gap-2">
            {solicitacoes.map(s => (
              <div key={s.linkId} className="flex items-center gap-3 bg-surface-800/50 rounded-xl p-3 border border-surface-700/50">
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-white truncate">{s.name}</p>
                  <p className="text-xs text-gray-500 truncate">{s.email}</p>
                </div>
                {s.status === 'Approved' ? (
                  <span className="text-xs font-medium px-2 py-0.5 rounded-full border bg-accent-green/10 text-accent-green border-accent-green/30 flex items-center gap-1 shrink-0">
                    <Check className="w-3 h-3" /> Aprovado
                  </span>
                ) : (
                  <div className="flex items-center gap-2 shrink-0">
                    <button
                      onClick={() => recusarSolicitacao(s.linkId)}
                      disabled={recusandoId === s.linkId || aprovandoId === s.linkId}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-red-600/10 hover:bg-red-600/20 border border-red-500/30 text-sm text-red-300"
                    >
                      {recusandoId === s.linkId ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <X className="w-3.5 h-3.5" />}
                      Recusar
                    </button>
                    <button
                      onClick={() => aprovarSolicitacao(s.linkId)}
                      disabled={aprovandoId === s.linkId || recusandoId === s.linkId}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-brand-600/10 hover:bg-brand-600/20 border border-brand-500/30 text-sm text-brand-300"
                    >
                      {aprovandoId === s.linkId ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Clock className="w-3.5 h-3.5" />}
                      Aprovar
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}

        <h4 className="text-xs uppercase tracking-wider text-gray-500 font-bold mt-5 mb-3 flex items-center gap-1.5">
          <MessageSquare className="w-3.5 h-3.5" /> Avisos
        </h4>
        <div className="flex flex-col gap-2 max-h-64 overflow-y-auto mb-3">
          {loadingAvisos ? (
            <div className="flex justify-center py-4"><Loader2 className="w-4 h-4 animate-spin text-brand-400" /></div>
          ) : avisos.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-2">Nenhum aviso ainda.</p>
          ) : (
            avisos.map(a => (
              <div key={a.id} className={clsx(
                'rounded-xl p-3 text-sm max-w-[85%]',
                a.autor === 'Lojista' ? 'bg-brand-600/10 border border-brand-500/20 self-end text-right' : 'bg-surface-800/50 border border-surface-700/50'
              )}>
                <p className="text-white">{a.mensagem}</p>
                <p className="text-[11px] text-gray-500 mt-1">{a.autor} · {new Date(a.createdAt).toLocaleString('pt-BR')}</p>
              </div>
            ))
          )}
        </div>
        {contadoresAprovados.length > 1 && (
          <select
            className="input w-full mb-2"
            value={avisoLinkId}
            onChange={e => setAvisoLinkId(e.target.value)}
          >
            <option value="">Pra qual contador?</option>
            {contadoresAprovados.map(c => (
              <option key={c.linkId} value={c.linkId}>{c.name} — {c.email}</option>
            ))}
          </select>
        )}
        <form onSubmit={enviarAviso} className="flex gap-2">
          <input
            className="input flex-1" placeholder="Escrever um aviso pro contador..."
            value={novoAviso} onChange={e => setNovoAviso(e.target.value)}
          />
          <button type="submit" disabled={enviandoAviso} className="btn-primary justify-center">
            {enviandoAviso ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
          </button>
        </form>
      </div>

      {/* Histórico de notas emitidas */}
      <div className="card p-5">
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-bold text-white flex items-center gap-2">
            <ScrollText className="w-4 h-4 text-brand-400" /> Notas Emitidas
          </h3>
          <button onClick={() => loadNotas()} className="p-2 rounded-lg bg-surface-700 hover:bg-surface-500 text-gray-400">
            <RefreshCw className={clsx('w-4 h-4', notasLoading && 'animate-spin')} />
          </button>
        </div>

        {pendentesCount > 0 && (
          <div className="flex items-center gap-2 text-sm bg-amber-500/10 text-amber-400 rounded-lg p-3 mb-3">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            {pendentesCount} nota(s) pendente(s) ou em contingência aguardando emissão/retransmissão
            {pendenteMaisAntiga && ` — a mais antiga é de ${fmtDate(pendenteMaisAntiga)}`}.
            {' '}O retry automático tenta a cada 15 min; use &quot;Reprocessar&quot; pra forçar agora.
          </div>
        )}

        {notasLoading ? (
          <div className="flex justify-center py-8"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
        ) : notas.length === 0 ? (
          <p className="text-sm text-gray-500 text-center py-4">Nenhuma nota emitida ainda.</p>
        ) : (
          <div className="flex flex-col gap-2">
            {notas.map(n => {
              const info = STATUS_INFO[n.status] ?? STATUS_INFO.PendenteEmissao
              return (
                <div key={n.id} className="flex items-center gap-3 bg-surface-800/50 rounded-xl p-3 border border-surface-700/50">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className={clsx('text-[10px] font-bold px-2 py-0.5 rounded-full border uppercase', info.color)}>
                        {info.label}
                      </span>
                      <span className="text-sm text-white font-semibold">
                        R$ {(n.valorTotalEmCentavos / 100).toFixed(2)}
                      </span>
                      {n.serie != null && n.numero != null && (
                        <span className="text-xs text-gray-500">Série {n.serie} · Nº {n.numero}</span>
                      )}
                      <span className="text-xs text-gray-500">{fmtDate(n.createdAt)}</span>
                    </div>
                    {n.chaveAcesso && <p className="text-[11px] font-mono text-gray-500 mt-1 truncate">{n.chaveAcesso}</p>}
                    {n.motivoRejeicao && <p className="text-xs text-red-400 mt-1">{n.motivoRejeicao}</p>}
                    {n.erpEstornoErro && <p className="text-xs text-amber-400 mt-1">Estorno ERP pendente: {n.erpEstornoErro}</p>}
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    {n.chaveAcesso && (
                      <Link href={`/admin/fiscal/cupom/${n.id}`} target="_blank"
                            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-surface-700 hover:bg-surface-500 border border-surface-600 text-sm text-gray-300">
                        <Printer className="w-3.5 h-3.5" /> Cupom
                      </Link>
                    )}
                    {(n.status === 'PendenteEmissao' || n.status === 'Rejeitada' || n.status === 'AutorizadaContingencia') && (
                      <button onClick={() => reprocessarNota(n.id)} disabled={reprocessingId === n.id}
                              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-surface-700 hover:bg-surface-500 border border-surface-600 text-sm text-gray-300">
                        {reprocessingId === n.id ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RefreshCw className="w-3.5 h-3.5" />}
                        Reprocessar
                      </button>
                    )}
                    {n.status === 'Autorizada' && (
                      <button onClick={() => setCancelModalId(n.id)}
                              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-red-500/10 hover:bg-red-500/20 border border-red-500/30 text-sm text-red-400">
                        <Ban className="w-3.5 h-3.5" /> Cancelar
                      </button>
                    )}
                    {n.status === 'Cancelada' && n.erpEstornoErro && !n.erpEstornadoEm && (
                      <button onClick={() => reprocessarEstornoErp(n.id)} disabled={reprocessingId === n.id}
                              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-sm text-amber-400">
                        {reprocessingId === n.id ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RefreshCw className="w-3.5 h-3.5" />}
                        Reprocessar estorno
                      </button>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>

      {/* Modal de cancelamento */}
      {cancelModalId && (
        <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4">
          <div className="bg-surface-800 rounded-2xl w-full max-w-md p-6 flex flex-col gap-4">
            <h2 className="font-black text-white">Cancelar NFC-e</h2>
            <p className="text-sm text-gray-400">
              Só é possível cancelar dentro de 30 minutos após a autorização. Estoque, pontos, cashback e crediário não pago serão estornados; dinheiro, Pix e cartão exigem confirmação manual do reembolso.
            </p>
            <textarea
              className="input w-full min-h-[80px]"
              placeholder="Motivo do cancelamento..."
              value={cancelJustificativa}
              onChange={e => setCancelJustificativa(e.target.value)}
            />
            <div className="flex gap-3">
              <button onClick={() => { setCancelModalId(null); setCancelJustificativa('') }} disabled={cancelling}
                      className="flex-1 py-3 rounded-xl bg-surface-700 text-gray-300 text-sm font-semibold">
                Voltar
              </button>
              <button onClick={confirmarCancelamento} disabled={cancelling}
                      className="flex-1 py-3 rounded-xl bg-red-500 hover:bg-red-400 disabled:opacity-50 text-white text-sm font-bold flex items-center justify-center gap-2">
                {cancelling ? <Loader2 className="w-4 h-4 animate-spin" /> : <Ban className="w-4 h-4" />}
                Confirmar cancelamento
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
