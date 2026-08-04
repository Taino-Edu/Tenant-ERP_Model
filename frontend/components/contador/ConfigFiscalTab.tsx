'use client'
// =============================================================================
// ConfigFiscalTab.tsx — Onde o contador mantém a configuração fiscal do cliente:
// cadastro do emitente, ambiente/numeração, certificado A1, CSC e os parâmetros
// de apuração (anexo, folha, presunções, ICMS/ISS).
//
// A escrita passa pelas MESMAS validações do /admin/fiscal (FiscalConfigService
// no backend) — inclusive o bloqueio de regime diferente do Simples Nacional,
// que a emissão de NFC-e não sabe montar.
// =============================================================================
import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import {
  Building2, Save, ShieldCheck, Upload, Percent, KeyRound, AlertTriangle, CheckCircle2,
} from 'lucide-react'
import clsx from 'clsx'
import { contadorApi, getErrorMessage, type ContadorConfigDto, type ContadorConfigUpdate } from '@/lib/api'
import Badge from '@/components/admin/ui/Badge'
import Button from '@/components/admin/ui/Button'
import Spinner from '@/components/admin/ui/Spinner'
import { SecaoHeader, Aviso } from './contador-shared'

interface Props {
  tenantId: string
  config: ContadorConfigDto | null
  loading: boolean
  onAtualizado: (config: ContadorConfigDto) => void
}

const REGIMES = [
  { valor: 'SimplesNacional', rotulo: 'Simples Nacional' },
  { valor: 'LucroPresumido',  rotulo: 'Lucro Presumido' },
  { valor: 'LucroReal',       rotulo: 'Lucro Real' },
]

const ANEXOS = [
  { valor: 'I',   rotulo: 'Anexo I — Comércio' },
  { valor: 'II',  rotulo: 'Anexo II — Indústria' },
  { valor: 'III', rotulo: 'Anexo III — Serviços (fator R ≥ 28%)' },
  { valor: 'IV',  rotulo: 'Anexo IV — Construção e serviços do §5º-C' },
  { valor: 'V',   rotulo: 'Anexo V — Serviços do §5º-I (fator R < 28%)' },
]

/** Centavos → string editável ("1234,56"). */
const centavosParaTexto = (centavos: number) => (centavos / 100).toFixed(2).replace('.', ',')
/** "1.234,56" ou "1234.56" → centavos. Vazio vira 0. */
const textoParaCentavos = (texto: string) => {
  const limpo = texto.replace(/\./g, '').replace(',', '.').trim()
  if (!limpo) return 0
  return Math.round(Number(limpo) * 100)
}
const textoParaNumero = (texto: string) => {
  const limpo = texto.replace(',', '.').trim()
  return limpo === '' ? 0 : Number(limpo)
}

export default function ConfigFiscalTab({ tenantId, config, loading, onAtualizado }: Props) {
  if (loading) return <div className="card"><Spinner block size="lg" /></div>
  if (!config) return <div className="card text-sm text-gray-400">Configuração fiscal indisponível.</div>

  return (
    <div className="space-y-5">
      <CadastroEmitente tenantId={tenantId} config={config} onAtualizado={onAtualizado} />
      <ParametrosApuracao tenantId={tenantId} config={config} onAtualizado={onAtualizado} />
      <CertificadoECsc tenantId={tenantId} config={config} onAtualizado={onAtualizado} />
    </div>
  )
}

// ── Cadastro do emitente ────────────────────────────────────────────────────

function CadastroEmitente({ tenantId, config, onAtualizado }: Omit<Props, 'loading'> & { config: ContadorConfigDto }) {
  const [form, setForm] = useState({
    razaoSocial: config.razaoSocial ?? '',
    cnpj: config.cnpj ?? '',
    inscricaoEstadual: config.inscricaoEstadual ?? '',
    logradouro: config.logradouro ?? '',
    numero: config.numero ?? '',
    complemento: config.complemento ?? '',
    bairro: config.bairro ?? '',
    municipio: config.municipio ?? '',
    codigoMunicipioIbge: config.codigoMunicipioIbge ?? '',
    uf: config.uf ?? '',
    cep: config.cep ?? '',
    emailContador: config.emailContador ?? '',
    serieNfce: String(config.serieNfce ?? 1),
    ambiente: config.ambiente,
    regimeTributario: config.regimeTributario,
  })
  const [salvando, setSalvando] = useState(false)

  useEffect(() => {
    setForm({
      razaoSocial: config.razaoSocial ?? '',
      cnpj: config.cnpj ?? '',
      inscricaoEstadual: config.inscricaoEstadual ?? '',
      logradouro: config.logradouro ?? '',
      numero: config.numero ?? '',
      complemento: config.complemento ?? '',
      bairro: config.bairro ?? '',
      municipio: config.municipio ?? '',
      codigoMunicipioIbge: config.codigoMunicipioIbge ?? '',
      uf: config.uf ?? '',
      cep: config.cep ?? '',
      emailContador: config.emailContador ?? '',
      serieNfce: String(config.serieNfce ?? 1),
      ambiente: config.ambiente,
      regimeTributario: config.regimeTributario,
    })
  }, [config])

  async function salvar(e: React.FormEvent) {
    e.preventDefault()
    setSalvando(true)
    try {
      const { data } = await contadorApi.updateConfig(tenantId, {
        razaoSocial: form.razaoSocial,
        cnpj: form.cnpj,
        inscricaoEstadual: form.inscricaoEstadual,
        logradouro: form.logradouro,
        numero: form.numero,
        complemento: form.complemento,
        bairro: form.bairro,
        municipio: form.municipio,
        codigoMunicipioIbge: form.codigoMunicipioIbge,
        uf: form.uf,
        cep: form.cep,
        emailContador: form.emailContador,
        serieNfce: Number(form.serieNfce) || 1,
        ambiente: form.ambiente,
        regimeTributario: form.regimeTributario,
      })
      onAtualizado(data)
      toast.success('Cadastro do emitente atualizado.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar o cadastro'))
    } finally {
      setSalvando(false)
    }
  }

  const producao = form.ambiente === 'Producao'

  return (
    <form onSubmit={salvar} className="card space-y-4">
      <SecaoHeader
        icon={Building2}
        titulo="Cadastro do emitente"
        descricao="Estes campos vão direto para o XML da NFC-e — um município ou UF errado derruba a autorização."
        acoes={<Badge tone={producao ? 'success' : 'warning'}>{producao ? 'Produção' : 'Homologação'}</Badge>}
      />

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Campo label="Razão social" value={form.razaoSocial} onChange={v => setForm({ ...form, razaoSocial: v })} maxLength={150} />
        <Campo label="CNPJ" value={form.cnpj} onChange={v => setForm({ ...form, cnpj: v })} maxLength={18} />
        <Campo label="Inscrição estadual" value={form.inscricaoEstadual} onChange={v => setForm({ ...form, inscricaoEstadual: v })} maxLength={20} />
        <Campo label="E-mail do contador" value={form.emailContador} onChange={v => setForm({ ...form, emailContador: v })} type="email" maxLength={200}
               ajuda="Destino do ZIP mensal de XMLs enviado automaticamente." />
        <Campo label="Logradouro" value={form.logradouro} onChange={v => setForm({ ...form, logradouro: v })} maxLength={150} />
        <div className="grid grid-cols-2 gap-3">
          <Campo label="Número" value={form.numero} onChange={v => setForm({ ...form, numero: v })} maxLength={20} />
          <Campo label="Complemento" value={form.complemento} onChange={v => setForm({ ...form, complemento: v })} maxLength={100} />
        </div>
        <Campo label="Bairro" value={form.bairro} onChange={v => setForm({ ...form, bairro: v })} maxLength={100} />
        <Campo label="CEP" value={form.cep} onChange={v => setForm({ ...form, cep: v })} maxLength={9} />
        <Campo label="Município" value={form.municipio} onChange={v => setForm({ ...form, municipio: v })} maxLength={100} />
        <div className="grid grid-cols-2 gap-3">
          <Campo label="Código IBGE" value={form.codigoMunicipioIbge} onChange={v => setForm({ ...form, codigoMunicipioIbge: v })} maxLength={7}
                 ajuda="7 dígitos" />
          <Campo label="UF" value={form.uf} onChange={v => setForm({ ...form, uf: v.toUpperCase() })} maxLength={2} />
        </div>
        <div>
          <label className="label" htmlFor="regime">Regime tributário</label>
          <select id="regime" className="input w-full" value={form.regimeTributario}
                  onChange={e => setForm({ ...form, regimeTributario: e.target.value })}>
            {REGIMES.map(r => <option key={r.valor} value={r.valor}>{r.rotulo}</option>)}
          </select>
        </div>
        <Campo label="Série da NFC-e" value={form.serieNfce} onChange={v => setForm({ ...form, serieNfce: v })} type="number"
               ajuda={`Próximo número a emitir: ${config.proximoNumeroNfce}`} />
        <div className="sm:col-span-2">
          <label className="label" htmlFor="ambiente">Ambiente</label>
          <select id="ambiente" className="input w-full" value={form.ambiente}
                  onChange={e => setForm({ ...form, ambiente: e.target.value })}>
            <option value="Homologacao">Homologação (nota sem valor fiscal)</option>
            <option value="Producao">Produção (nota com valor fiscal)</option>
          </select>
        </div>
      </div>

      {form.regimeTributario !== 'SimplesNacional' && (
        <Aviso tone="warning">
          <p>
            Fora do Simples a NFC-e é montada com <strong>CST</strong> no lugar do CSOSN. Cada natureza de
            operação precisa ter o CST cadastrado (e a alíquota de ICMS da operação própria) em
            Admin &gt; Fiscal &gt; Naturezas de operação — sem isso a emissão para no pré-voo, antes de
            consumir numeração. PIS e COFINS passam a ser destacados por item, com as alíquotas
            do regime.
          </p>
        </Aviso>
      )}

      <Aviso tone="info">
        <p>
          Ligar a Produção exige certificado A1 instalado e do mesmo CNPJ da loja — o sistema recusa
          a mudança caso contrário.
        </p>
      </Aviso>

      <div className="flex justify-end">
        <Button type="submit" loading={salvando}>
          {!salvando && <Save className="w-4 h-4" />} Salvar cadastro
        </Button>
      </div>
    </form>
  )
}

// ── Parâmetros de apuração ──────────────────────────────────────────────────

function ParametrosApuracao({ tenantId, config, onAtualizado }: Omit<Props, 'loading'> & { config: ContadorConfigDto }) {
  const [form, setForm] = useState({
    anexoSimples: config.anexoSimples,
    folha12m: centavosParaTexto(config.folhaPagamento12mEmCentavos),
    folhaMensal: centavosParaTexto(config.folhaPagamentoMensalEmCentavos),
    presuncaoIrpj: String(config.percentualPresuncaoIrpj).replace('.', ','),
    presuncaoCsll: String(config.percentualPresuncaoCsll).replace('.', ','),
    icms: String(config.aliquotaIcmsPercentual).replace('.', ','),
    iss: String(config.aliquotaIssPercentual).replace('.', ','),
  })
  const [salvando, setSalvando] = useState(false)

  async function salvar(e: React.FormEvent) {
    e.preventDefault()
    setSalvando(true)
    try {
      const payload: ContadorConfigUpdate = {
        anexoSimples: form.anexoSimples,
        folhaPagamento12mEmCentavos: textoParaCentavos(form.folha12m),
        folhaPagamentoMensalEmCentavos: textoParaCentavos(form.folhaMensal),
        percentualPresuncaoIrpj: textoParaNumero(form.presuncaoIrpj),
        percentualPresuncaoCsll: textoParaNumero(form.presuncaoCsll),
        aliquotaIcmsPercentual: textoParaNumero(form.icms),
        aliquotaIssPercentual: textoParaNumero(form.iss),
      }
      const { data } = await contadorApi.updateConfig(tenantId, payload)
      onAtualizado(data)
      toast.success('Parâmetros de apuração atualizados.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar os parâmetros'))
    } finally {
      setSalvando(false)
    }
  }

  const usaFatorR = form.anexoSimples === 'III' || form.anexoSimples === 'V'

  return (
    <form onSubmit={salvar} className="card space-y-4">
      <SecaoHeader
        icon={Percent}
        titulo="Parâmetros de apuração"
        descricao="Não entram no XML. Alimentam o comparativo Simples x Presumido da aba Impostos."
      />

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="sm:col-span-2">
          <label className="label" htmlFor="anexo">Anexo do Simples Nacional</label>
          <select id="anexo" className="input w-full" value={form.anexoSimples}
                  onChange={e => setForm({ ...form, anexoSimples: e.target.value })}>
            {ANEXOS.map(a => <option key={a.valor} value={a.valor}>{a.rotulo}</option>)}
          </select>
        </div>

        <Campo label="Folha de 12 meses (R$)" value={form.folha12m} onChange={v => setForm({ ...form, folha12m: v })}
               ajuda={usaFatorR
                 ? 'Numerador do fator R — sem ela o cálculo cai no Anexo V, o mais caro.'
                 : 'Usada só nos anexos III e V (fator R).'} />
        <Campo label="Folha mensal (R$)" value={form.folhaMensal} onChange={v => setForm({ ...form, folhaMensal: v })}
               ajuda="Base do INSS patronal (20%) no Lucro Presumido." />
        <Campo label="Presunção IRPJ (%)" value={form.presuncaoIrpj} onChange={v => setForm({ ...form, presuncaoIrpj: v })}
               ajuda="8 para comércio/indústria, 32 para serviços." />
        <Campo label="Presunção CSLL (%)" value={form.presuncaoCsll} onChange={v => setForm({ ...form, presuncaoCsll: v })}
               ajuda="12 para comércio/indústria, 32 para serviços." />
        <Campo label="ICMS médio (%)" value={form.icms} onChange={v => setForm({ ...form, icms: v })}
               ajuda="Fora do Simples o ICMS é apurado à parte — informe a alíquota média da UF." />
        <Campo label="ISS (%)" value={form.iss} onChange={v => setForm({ ...form, iss: v })}
               ajuda="Alíquota do município, 2% a 5%. Deixe 0 se a loja não presta serviço." />
      </div>

      <div className="flex justify-end">
        <Button type="submit" loading={salvando}>
          {!salvando && <Save className="w-4 h-4" />} Salvar parâmetros
        </Button>
      </div>
    </form>
  )
}

// ── Certificado A1 e CSC ────────────────────────────────────────────────────

function CertificadoECsc({ tenantId, config, onAtualizado }: Omit<Props, 'loading'> & { config: ContadorConfigDto }) {
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [senha, setSenha] = useState('')
  const [enviando, setEnviando] = useState(false)

  const [cscId, setCscId] = useState(config.cscId ?? '')
  const [cscToken, setCscToken] = useState('')
  const [salvandoCsc, setSalvandoCsc] = useState(false)

  const dias = config.diasParaVencer

  async function enviarCertificado(e: React.FormEvent) {
    e.preventDefault()
    if (!arquivo) { toast.error('Selecione o arquivo .pfx do certificado.'); return }
    setEnviando(true)
    try {
      const { data } = await contadorApi.uploadCertificado(tenantId, arquivo, senha)
      toast.success(`${data.message} Vence em ${data.diasRestantes} dia(s).`)
      setArquivo(null)
      setSenha('')
      const atualizado = await contadorApi.getConfig(tenantId)
      onAtualizado(atualizado.data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao enviar o certificado'))
    } finally {
      setEnviando(false)
    }
  }

  async function salvarCsc(e: React.FormEvent) {
    e.preventDefault()
    setSalvandoCsc(true)
    try {
      const { data } = await contadorApi.updateConfig(tenantId, {
        cscId,
        // Token vazio não é enviado: o backend trata null como "manter o atual",
        // e o valor guardado nunca volta pra tela.
        ...(cscToken.trim() ? { cscToken: cscToken.trim() } : {}),
      })
      onAtualizado(data)
      setCscToken('')
      toast.success('CSC atualizado.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar o CSC'))
    } finally {
      setSalvandoCsc(false)
    }
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
      <form onSubmit={enviarCertificado} className="card space-y-4">
        <SecaoHeader
          icon={ShieldCheck}
          titulo="Certificado digital A1"
          descricao="Assina e transmite a NFC-e. Precisa ser do mesmo CNPJ do emitente."
          acoes={
            config.certificadoConfigurado ? (
              <Badge tone={dias != null && dias <= 30 ? (dias <= 7 ? 'danger' : 'warning') : 'success'}>
                {dias != null && dias < 0 ? 'Vencido' : `Vence em ${dias}d`}
              </Badge>
            ) : (
              <Badge tone="danger">Não configurado</Badge>
            )
          }
        />

        {config.certificadoConfigurado && config.certificadoValidade && (
          <p className="text-xs text-gray-500 flex items-center gap-1.5">
            {dias != null && dias <= 30
              ? <AlertTriangle className="w-3.5 h-3.5 text-amber-400" />
              : <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />}
            Válido até {new Date(config.certificadoValidade).toLocaleDateString('pt-BR')}
          </p>
        )}

        <div>
          <label className="label" htmlFor="pfx">Arquivo .pfx</label>
          <input
            id="pfx" type="file" accept=".pfx,.p12"
            onChange={e => setArquivo(e.target.files?.[0] ?? null)}
            className="input w-full file:mr-3 file:rounded-lg file:border-0 file:bg-surface-600 file:px-3 file:py-1 file:text-sm file:text-gray-200"
          />
        </div>
        <div>
          <label className="label" htmlFor="senha-pfx">Senha do certificado</label>
          <input id="senha-pfx" type="password" className="input w-full" value={senha}
                 onChange={e => setSenha(e.target.value)} autoComplete="off" />
        </div>

        <div className="flex justify-end">
          <Button type="submit" loading={enviando}>
            {!enviando && <Upload className="w-4 h-4" />} Enviar certificado
          </Button>
        </div>
      </form>

      <form onSubmit={salvarCsc} className="card space-y-4">
        <SecaoHeader
          icon={KeyRound}
          titulo="CSC (Código de Segurança do Contribuinte)"
          descricao="Cadastrado na SEFAZ; usado para gerar o QR Code da NFC-e."
          acoes={<Badge tone={config.cscConfigurado ? 'success' : 'warning'}>
            {config.cscConfigurado ? 'Configurado' : 'Pendente'}
          </Badge>}
        />

        <div>
          <label className="label" htmlFor="csc-id">ID do CSC</label>
          <input id="csc-id" className="input w-full" maxLength={10} value={cscId}
                 onChange={e => setCscId(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="csc-token">Token do CSC</label>
          <input
            id="csc-token" type="password" className="input w-full" autoComplete="off"
            placeholder={config.cscConfigurado ? '•••••••• (deixe em branco para manter)' : ''}
            value={cscToken} onChange={e => setCscToken(e.target.value)}
          />
          <p className={clsx('text-[11px] mt-1', 'text-gray-500')}>
            O token guardado é criptografado e nunca volta para a tela.
          </p>
        </div>

        <div className="flex justify-end">
          <Button type="submit" loading={salvandoCsc}>
            {!salvandoCsc && <Save className="w-4 h-4" />} Salvar CSC
          </Button>
        </div>
      </form>
    </div>
  )
}

// ── Campo de formulário ─────────────────────────────────────────────────────

function Campo({ label, value, onChange, ajuda, type = 'text', maxLength }: {
  label: string
  value: string
  onChange: (v: string) => void
  ajuda?: string
  type?: string
  maxLength?: number
}) {
  const id = `campo-${label.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`
  return (
    <div>
      <label className="label" htmlFor={id}>{label}</label>
      <input id={id} type={type} className="input w-full" value={value} maxLength={maxLength}
             onChange={e => onChange(e.target.value)} />
      {ajuda && <p className="text-[11px] text-gray-500 mt-1">{ajuda}</p>}
    </div>
  )
}
