'use client'
// =============================================================================
// Integrações da plataforma — credenciais dos serviços externos que NÓS usamos
// pra operar o negócio (hoje: Banco Inter, pra cobrar as mensalidades).
//
// Não confundir com /admin/integracoes, que é do lojista e guarda as
// credenciais DELE. Aqui é a nossa conta bancária: por isso nenhum segredo é
// exibido, nem depois de salvo — o servidor só devolve "tem/não tem".
// =============================================================================

import { useCallback, useEffect, useState } from 'react'
import {
  platformApi, PlatformIntegrationDto, SalvarPlatformIntegrationRequest, getErrorMessage,
} from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import Badge from '@/components/admin/ui/Badge'
import EmptyState from '@/components/admin/ui/EmptyState'
import Modal from '@/components/admin/ui/Modal'
import Spinner from '@/components/admin/ui/Spinner'
import toast from 'react-hot-toast'
import { Plug, Landmark, Pencil, CheckCircle2, AlertTriangle, Power, PowerOff, Trash2 } from 'lucide-react'

function fmtQuando(iso: string | null) {
  if (!iso) return null
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// ── Modal de credenciais ─────────────────────────────────────────────────────
function CredenciaisModal({ integracao, onClose, onSaved }: {
  integracao: PlatformIntegrationDto
  onClose: () => void
  onSaved: (i: PlatformIntegrationDto) => void
}) {
  const [form, setForm]   = useState<SalvarPlatformIntegrationRequest>({
    clientId:      integracao.clientId ?? '',
    contaCorrente: integracao.contaCorrente ?? '',
    pixKey:        integracao.pixKey ?? '',
  })
  const [saving, setSaving] = useState(false)
  const set = (k: keyof SalvarPlatformIntegrationRequest, v: string) => setForm(f => ({ ...f, [k]: v }))

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    try {
      // Segredos vazios são omitidos do payload: no servidor, ausente significa
      // "mantém o que está salvo". Mandar "" seria pedir pra apagar.
      const payload: SalvarPlatformIntegrationRequest = { ...form }
      for (const campo of ['clientSecret', 'certificateCrt', 'certificateKey'] as const)
        if (!payload[campo]?.trim()) delete payload[campo]

      const { data } = await platformApi.salvarIntegracao(integracao.provider, payload)
      toast.success('Credenciais salvas.')
      onSaved(data)
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar credenciais.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal onClose={onClose} maxWidth="lg" title={integracao.nome} icon={Landmark} closeOnBackdrop={false}>
      <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
        <div>
          <label className="label">Client ID</label>
          <input className="input" value={form.clientId ?? ''} onChange={e => set('clientId', e.target.value)}
                 placeholder="identificador da aplicação no Inter" />
        </div>

        <div>
          <label className="label">
            Client Secret {integracao.temClientSecret && <span className="text-emerald-400 font-normal">· já salvo</span>}
          </label>
          <input className="input" type="password" autoComplete="new-password"
                 value={form.clientSecret ?? ''} onChange={e => set('clientSecret', e.target.value)}
                 placeholder={integracao.temClientSecret ? 'deixe em branco para manter' : 'cole o secret'} />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Conta corrente</label>
            <input className="input" value={form.contaCorrente ?? ''} onChange={e => set('contaCorrente', e.target.value)}
                   placeholder="somente números" />
          </div>
          <div>
            <label className="label">Chave Pix</label>
            <input className="input" value={form.pixKey ?? ''} onChange={e => set('pixKey', e.target.value)}
                   placeholder="chave da conta" />
            <p className="text-xs text-gray-500 mt-1">Sem ela o boleto sai sem QR Code.</p>
          </div>
        </div>

        <details className="rounded-lg bg-surface-700/60 border border-surface-600 px-4 py-3" open={!integracao.temCertificado}>
          <summary className="cursor-pointer text-sm font-semibold text-gray-300">
            Certificado mTLS {integracao.temCertificado && <span className="text-emerald-400 font-normal">· já salvo</span>}
          </summary>
          <div className="mt-3 space-y-3">
            <p className="text-xs text-gray-400">
              A API do Inter exige certificado de cliente. Baixe o par no Internet Banking
              (Aplicações → sua aplicação) e cole o conteúdo dos arquivos abaixo.
            </p>
            <div>
              <label className="label">Certificado (.crt)</label>
              <textarea className="input min-h-[90px] font-mono text-xs resize-y"
                        value={form.certificateCrt ?? ''} onChange={e => set('certificateCrt', e.target.value)}
                        placeholder={integracao.temCertificado ? 'deixe em branco para manter' : '-----BEGIN CERTIFICATE-----'} />
            </div>
            <div>
              <label className="label">Chave privada (.key)</label>
              <textarea className="input min-h-[90px] font-mono text-xs resize-y"
                        value={form.certificateKey ?? ''} onChange={e => set('certificateKey', e.target.value)}
                        placeholder={integracao.temCertificado ? 'deixe em branco para manter' : '-----BEGIN PRIVATE KEY-----'} />
            </div>
          </div>
        </details>

        <div className="flex gap-3 pt-1">
          <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
          <button type="submit" disabled={saving} className="btn-primary flex-1 justify-center">
            {saving && <Spinner size="sm" className="text-white" />} Salvar
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── Card de uma integração ───────────────────────────────────────────────────
function IntegracaoCard({ integracao, onChanged }: {
  integracao: PlatformIntegrationDto
  onChanged: (i: PlatformIntegrationDto) => void
}) {
  const [editando, setEditando] = useState(false)
  const [alternando, setAlternando] = useState(false)

  async function alternarAtivo() {
    setAlternando(true)
    try {
      const { data } = await platformApi.salvarIntegracao(integracao.provider, { isActive: !integracao.isActive })
      onChanged(data)
      toast.success(data.isActive ? 'Integração ligada.' : 'Integração desligada.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao alterar a integração.'))
    } finally {
      setAlternando(false)
    }
  }

  async function remover() {
    if (!confirm(`Apagar as credenciais do ${integracao.nome}? A cobrança automática para de funcionar até você configurar de novo.`)) return
    try {
      await platformApi.removerIntegracao(integracao.provider)
      toast.success('Credenciais removidas.')
      onChanged({ ...integracao, configurado: false, operacional: false, isActive: false, clientId: null, temClientSecret: false, temCertificado: false, contaCorrente: null, pixKey: null, pendencias: ['Integração ainda não configurada.'], lastError: null, updatedAt: null })
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao remover credenciais.'))
    }
  }

  return (
    <div className="card space-y-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div className="w-10 h-10 rounded-xl bg-brand-600/15 flex items-center justify-center shrink-0">
            <Landmark className="w-5 h-5 text-brand-400" />
          </div>
          <div className="min-w-0">
            <p className="font-bold text-white truncate">{integracao.nome}</p>
            <p className="text-xs text-gray-500">Cobrança das mensalidades das lojas</p>
          </div>
        </div>
        <Badge tone={integracao.operacional ? 'success' : integracao.configurado ? 'warning' : 'neutral'}>
          {integracao.operacional ? 'Operacional' : integracao.configurado ? 'Incompleta' : 'Não configurada'}
        </Badge>
      </div>

      {integracao.pendencias.length > 0 ? (
        <ul className="space-y-1">
          {integracao.pendencias.map(p => (
            <li key={p} className="flex items-start gap-2 text-xs text-amber-400">
              <AlertTriangle className="w-3.5 h-3.5 shrink-0 mt-0.5" /> {p}
            </li>
          ))}
        </ul>
      ) : (
        <p className="flex items-center gap-2 text-xs text-emerald-400">
          <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> Pronta para emitir cobranças.
        </p>
      )}

      {integracao.lastError && (
        <p className="text-xs text-red-400 bg-red-500/10 border border-red-500/30 rounded-lg px-3 py-2">
          Último erro: {integracao.lastError}
        </p>
      )}

      {(integracao.clientId || integracao.lastSyncAt) && (
        <dl className="grid grid-cols-2 gap-2 text-xs">
          {integracao.clientId && <><dt className="text-gray-500">Client ID</dt><dd className="text-gray-300 font-mono truncate">{integracao.clientId}</dd></>}
          {integracao.contaCorrente && <><dt className="text-gray-500">Conta</dt><dd className="text-gray-300 font-mono">{integracao.contaCorrente}</dd></>}
          {integracao.lastSyncAt && <><dt className="text-gray-500">Última sincronização</dt><dd className="text-gray-300">{fmtQuando(integracao.lastSyncAt)}</dd></>}
        </dl>
      )}

      <div className="flex items-center gap-2 pt-1">
        <button onClick={() => setEditando(true)} className="btn-primary text-sm py-1.5">
          <Pencil className="w-4 h-4" /> {integracao.configurado ? 'Editar credenciais' : 'Configurar'}
        </button>
        {integracao.configurado && (
          <>
            <button onClick={alternarAtivo} disabled={alternando} className="btn-secondary text-sm py-1.5"
                    title={integracao.isActive ? 'Desligar sem apagar as credenciais' : 'Ligar de novo'}>
              {alternando ? <Spinner size="sm" /> : integracao.isActive ? <PowerOff className="w-4 h-4" /> : <Power className="w-4 h-4" />}
              {integracao.isActive ? 'Desligar' : 'Ligar'}
            </button>
            <button onClick={remover} title="Apagar credenciais"
                    className="ml-auto w-8 h-8 rounded-lg flex items-center justify-center border border-red-500/30 text-red-400 hover:bg-red-500/10 transition-colors shrink-0">
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </>
        )}
      </div>

      {editando && (
        <CredenciaisModal integracao={integracao} onClose={() => setEditando(false)} onSaved={onChanged} />
      )}
    </div>
  )
}

export default function PlataformaIntegracoesPage() {
  const [integracoes, setIntegracoes] = useState<PlatformIntegrationDto[]>([])
  const [loading, setLoading] = useState(true)

  const fetchIntegracoes = useCallback(async () => {
    try {
      const { data } = await platformApi.listIntegracoes()
      setIntegracoes(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar integrações.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { fetchIntegracoes() }, [fetchIntegracoes])

  function aplicar(atualizada: PlatformIntegrationDto) {
    setIntegracoes(atuais => atuais.map(i => i.provider === atualizada.provider ? atualizada : i))
  }

  return (
    <div className="space-y-5">
      <PageHeader
        icon={Plug}
        title="Integrações"
        description="Serviços externos que a plataforma usa para operar"
      />

      <p className="text-xs text-gray-500 bg-surface-700/40 border border-surface-600 rounded-lg px-3 py-2">
        Estas são as credenciais <strong className="text-gray-300">da plataforma</strong> — não as das lojas.
        Nenhum segredo é exibido depois de salvo: o campo em branco mantém o que já está guardado.
      </p>

      {loading ? (
        <Spinner size="lg" block />
      ) : integracoes.length === 0 ? (
        <EmptyState icon={Plug} message="Nenhuma integração disponível." />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {integracoes.map(i => (
            <IntegracaoCard key={i.provider} integracao={i} onChanged={aplicar} />
          ))}
        </div>
      )}
    </div>
  )
}
