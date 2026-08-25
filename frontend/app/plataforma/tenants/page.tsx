'use client'
import { useEffect, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import Link from 'next/link'
import { platformApi, TenantSummary, TenantStatus, TenantPaymentStatus, PlatformOverviewDto, getErrorMessage, TENANT_MODULES } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import StatCard from '@/components/admin/StatCard'
import StatusPillSelect from '@/components/admin/StatusPillSelect'
import Badge from '@/components/admin/ui/Badge'
import EmptyState from '@/components/admin/ui/EmptyState'
import Modal from '@/components/admin/ui/Modal'
import Spinner from '@/components/admin/ui/Spinner'
import CreateTenantModal from '@/components/plataforma/CreateTenantModal'
import { usePlatformPermissions } from '@/hooks/usePlatformPermissions'
import toast from 'react-hot-toast'
import { Building2, Plus, Power, PowerOff, Check, LogIn, ChevronRight, Download, Trash2, AlertTriangle, Search, CheckCircle2, PauseCircle, AlertCircle } from 'lucide-react'
import clsx from 'clsx'
import { PLANOS, PLANO_PERSONALIZADO, acharPlano, taxaImplantacao } from '@/lib/planos'

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/** "há X min/h/dias" — null = nunca teve atividade registrada. */
function fmtRelative(iso: string | null): { text: string; tone: 'success' | 'warning' | 'danger' } {
  if (!iso) return { text: 'sem atividade', tone: 'danger' }
  const diffMs   = Date.now() - new Date(iso).getTime()
  const diffDays = diffMs / 86_400_000
  const tone: 'success' | 'warning' | 'danger' = diffDays <= 3 ? 'success' : diffDays <= 14 ? 'warning' : 'danger'

  if (diffMs < 0)          return { text: 'agora', tone: 'success' }
  const diffMin = diffMs / 60_000
  if (diffMin < 60)        return { text: `há ${Math.max(1, Math.round(diffMin))} min`, tone }
  const diffH = diffMin / 60
  if (diffH < 24)          return { text: `há ${Math.round(diffH)}h`, tone }
  return { text: `há ${Math.round(diffDays)} dia${diffDays >= 2 ? 's' : ''}`, tone }
}

const ACTIVITY_TONE: Record<'success' | 'warning' | 'danger', string> = {
  success: 'text-emerald-400',
  warning: 'text-amber-400',
  danger:  'text-gray-500',
}

/** Estilos do StatusPillSelect de pagamento — o select JÁ é a pill, então não
 * existe mais um badge redundante repetindo o mesmo texto ao lado. */
const PAYMENT_STYLES: Record<TenantPaymentStatus, string> = {
  Pago:     'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
  Atrasado: 'bg-red-500/20 text-red-400 border-red-500/30',
  Isento:   'bg-surface-600/40 text-gray-300 border-surface-400',
}
const PAYMENT_OPTIONS = ['Pago', 'Atrasado', 'Isento'] as const

type TenantFiltro = 'todos' | 'ativos' | 'suspensos' | 'atrasados'

/** O que o perfil de quem está logado libera nesta tela.
 *
 * Cada campo espelha a permissão que o endpoint correspondente exige — repare
 * que editar billing NÃO é `tenants.manage`, e sim `finance.manage`: um perfil
 * Comercial administra a loja mas não mexe no que ela paga. Sem esse recorte a
 * tela oferecia as quatro ações a todo mundo e o backend devolvia 403 depois do
 * clique. */
interface TenantAcoes {
  /** POST /tenants, PATCH status, GET backup */
  gerenciar: boolean
  /** PATCH billing — plano, mensalidade, status de pagamento e módulos */
  billing: boolean
  /** DELETE /tenants/{id} */
  apagar: boolean
  /** POST /tenants/{id}/impersonate */
  simular: boolean
}

// ── Modal: Apagar Tenant (irreversível — exige digitar o slug de volta) ───────
function DeleteTenantModal({ tenant, onClose, onDeleted }: { tenant: TenantSummary; onClose: () => void; onDeleted: () => void }) {
  const [confirmSlug, setConfirmSlug] = useState('')
  const [loading, setLoading]         = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      await platformApi.deleteTenant(tenant.id, confirmSlug)
      toast.success(`Tenant "${tenant.slug}" apagado.`)
      onDeleted()
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao apagar tenant'))
    } finally {
      setLoading(false)
    }
  }

  return (
    // closeOnBackdrop desligado: clique sem querer no fundo não pode fechar um
    // formulário de exclusão irreversível já preenchido.
    <Modal onClose={onClose} maxWidth="sm" title="Apagar Tenant" icon={AlertTriangle} closeOnBackdrop={false}>
      <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
        <p className="text-sm text-gray-400">
          Isso apaga <strong className="text-white">{tenant.slug}</strong>{tenant.kind === 'Native'
            ? ' e todos os dados locais (produtos, vendas e clientes)'
            : ' e todas as credenciais técnicas associadas'}{' '}
          <strong className="text-red-400">permanentemente</strong>. Não dá pra desfazer.
        </p>
        <div>
          <label className="label">Digite <code className="text-red-400">{tenant.slug}</code> pra confirmar</label>
          <input
            className="input" value={confirmSlug} onChange={e => setConfirmSlug(e.target.value)}
            placeholder={tenant.slug} required
          />
        </div>
        <div className="flex gap-3 pt-2">
          <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
          <button
            type="submit" disabled={loading || confirmSlug !== tenant.slug}
            className="flex-1 justify-center inline-flex items-center gap-2 rounded-xl bg-red-600 hover:bg-red-500 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium py-2 transition-colors"
          >
            {loading ? <Spinner size="sm" className="text-white" /> : <Trash2 className="w-4 h-4" />}
            Apagar Definitivamente
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── Modal: módulos contratados ───────────────────────────────────────────────
// Cada módulo é pago, então a descrição do que ele libera precisa caber em
// algum lugar — dentro de uma célula de tabela ela só cabia como `title`.
function ModulesModal({ tenant, saving, onToggle, onClose }: {
  tenant: TenantSummary; saving: boolean; onToggle: (module: string) => void; onClose: () => void
}) {
  return (
    <Modal onClose={onClose} maxWidth="md" title={`Módulos — ${tenant.slug}`} icon={Building2}>
      <div className="px-6 py-4 space-y-2">
        {TENANT_MODULES.map(({ value: module, label, description }) => {
          const ativo = tenant.enabledModules.includes(module)
          return (
            <button
              key={module}
              type="button"
              onClick={() => onToggle(module)}
              disabled={saving}
              className={clsx('w-full flex items-start gap-3 text-left px-3 py-2.5 rounded-xl border transition-colors disabled:opacity-60',
                ativo ? 'bg-brand-600/10 border-brand-500/40' : 'border-surface-600 hover:border-surface-400')}
            >
              <span className={clsx('w-4 h-4 mt-0.5 rounded border flex items-center justify-center shrink-0',
                ativo ? 'bg-brand-500 border-brand-500' : 'border-surface-400')}>
                {ativo && <Check className="w-3 h-3 text-white" />}
              </span>
              <span className="min-w-0">
                <span className={clsx('block text-sm font-medium', ativo ? 'text-brand-300' : 'text-gray-300')}>{label}</span>
                <span className="block text-xs text-gray-500">{description}</span>
              </span>
            </button>
          )
        })}
      </div>
    </Modal>
  )
}

/** Base dos quatro botões-ícone de ação do tenant (acessar / suspender /
 *  backup / apagar). 32px é uma densidade adequada à tabela do desktop, mas no
 *  celular fica abaixo do alvo confortável de toque — e um desses botões APAGA
 *  A LOJA permanentemente. `max-md:` sobe pra 44px só no celular, sem alterar a
 *  tabela. Extraído numa constante porque os quatro precisam concordar: se um
 *  crescer e o vizinho não, a fileira desalinha. */
const BTN_ACAO = 'w-8 h-8 max-md:w-11 max-md:h-11 rounded-lg flex items-center justify-center border transition-colors shrink-0'

/** Linha de tenant — `<tr>` no desktop, card no celular.
 *
 * Como o LeadRow, esta linha edita em vez de só exibir (plano, mensalidade,
 * status de pagamento, módulos), então o estado precisa morar num componente
 * por linha e não dá pra usar o DataTable. Os blocos são montados uma vez só e
 * as duas molduras consomem os mesmos — nenhuma ação existe num layout e falta
 * no outro. */
function TenantRow({ tenant, lastActivityAt, onChanged, acoesPermitidas, layout = 'row' }: { tenant: TenantSummary; lastActivityAt: string | null | undefined; onChanged: () => void; acoesPermitidas: TenantAcoes; layout?: 'row' | 'card' }) {
  const [planName, setPlanName]   = useState(tenant.planName)
  const [savingBilling, setSavingBilling] = useState(false)
  const [updatingStatus, setUpdatingStatus] = useState(false)
  const [impersonating, setImpersonating] = useState(false)
  const [backingUp, setBackingUp] = useState(false)
  const [showDelete, setShowDelete] = useState(false)
  const [showModules, setShowModules] = useState(false)
  const [mensalidade, setMensalidade] = useState(String(tenant.monthlyPrice ?? 0))
  const [implantacao, setImplantacao] = useState(String(tenant.setupFee ?? 0))

  /** Trocar de plano aplica o preço de tabela junto — era exatamente isso que
   *  faltava: o nome mudava e o valor ficava para trás. Personalizado preserva
   *  o valor atual, porque ali quem manda é o negociado. */
  function aplicarPlano(nome: string) {
    const plano = acharPlano(nome)
    if (!plano) { saveBilling({ planName: PLANO_PERSONALIZADO }); return }
    setMensalidade(String(plano.preco))
    saveBilling({
      planName:       plano.nome,
      monthlyPrice:   plano.preco,
      setupFee:       taxaImplantacao(plano),
      enabledModules: plano.modules,
    })
  }

  function salvarMensalidade() {
    const valor = Number(mensalidade)
    if (!Number.isFinite(valor) || valor < 0) { setMensalidade(String(tenant.monthlyPrice)); return }
    if (valor === tenant.monthlyPrice) return
    // Mexer no valor à mão descola da tabela — o plano vira Personalizado pra
    // não ficar escrito "Rio" numa loja que paga outro preço.
    //
    // A implantação NÃO é recalculada aqui, de propósito. Ela é moeda de
    // negociação e tem campo próprio: recalcular como o dobro da mensalidade
    // apagaria, sem aviso, um desconto que alguém acabou de fechar com o
    // cliente. Trocar de PLANO ainda aplica o valor de tabela, porque ali a
    // escolha é do pacote inteiro.
    saveBilling({
      planName:     acharPlano(planName) && valor !== acharPlano(planName)!.preco ? PLANO_PERSONALIZADO : planName,
      monthlyPrice: valor,
    })
  }

  function salvarImplantacao() {
    const valor = Number(implantacao)
    if (!Number.isFinite(valor) || valor < 0) { setImplantacao(String(tenant.setupFee)); return }
    if (valor === tenant.setupFee) return
    saveBilling({ setupFee: valor })
  }

  useEffect(() => { setPlanName(tenant.planName) }, [tenant.planName])
  useEffect(() => { setMensalidade(String(tenant.monthlyPrice ?? 0)) }, [tenant.monthlyPrice])
  useEffect(() => { setImplantacao(String(tenant.setupFee ?? 0)) }, [tenant.setupFee])

  // Plano que não está na tabela (cortesia, piloto, legado como "Mar"/"Lagoa")
  // aparece como Personalizado em vez de sumir do select.
  const planoSelecionado = acharPlano(planName)?.nome ?? PLANO_PERSONALIZADO

  async function saveBilling(next: Partial<{ planName: string; paymentStatus: TenantPaymentStatus; enabledModules: string[]; monthlyPrice: number; setupFee: number }>) {
    setSavingBilling(true)
    try {
      await platformApi.updateTenantBilling(tenant.id, {
        planName:       next.planName       ?? planName,
        paymentStatus:  next.paymentStatus  ?? tenant.paymentStatus,
        enabledModules: next.enabledModules ?? tenant.enabledModules,
        monthlyPrice:   next.monthlyPrice,
        setupFee:       next.setupFee,
      })
      toast.success('Billing atualizado.')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar billing do tenant.'))
    } finally {
      setSavingBilling(false)
    }
  }

  async function toggleStatus() {
    const next: TenantStatus = tenant.status === 'Active' ? 'Suspended' : 'Active'
    setUpdatingStatus(true)
    try {
      await platformApi.updateTenantStatus(tenant.id, next)
      toast.success(next === 'Active' ? 'Tenant reativado.' : 'Tenant suspenso.')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar status do tenant.'))
    } finally {
      setUpdatingStatus(false)
    }
  }

  function toggleModule(module: string) {
    const has = tenant.enabledModules.includes(module)
    const nextModules = has
      ? tenant.enabledModules.filter(m => m !== module)
      : [...tenant.enabledModules, module]
    saveBilling({ enabledModules: nextModules })
  }

  async function baixarBackup() {
    setBackingUp(true)
    try {
      const { data } = await platformApi.downloadTenantBackup(tenant.id)
      const url = URL.createObjectURL(data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `backup-${tenant.slug}-${new Date().toISOString().slice(0, 10)}.sql`
      a.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar backup'))
    } finally {
      setBackingUp(false)
    }
  }

  async function acessarAdmin() {
    setImpersonating(true)
    try {
      const { data } = await platformApi.impersonate(tenant.id)
      const rootDomain = process.env.NEXT_PUBLIC_ROOT_DOMAIN
      const url = `${window.location.protocol}//${tenant.slug}.${rootDomain}/api/auth/impersonate?ticket=${encodeURIComponent(data.ticket)}`
      window.open(url, '_blank', 'noopener')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar acesso de simulação.'))
    } finally {
      setImpersonating(false)
    }
  }

  const card = layout === 'card'
  const activity = fmtRelative(lastActivityAt ?? null)

  // Campos de cobrança viram somente leitura em vez de sumir: o plano e a
  // mensalidade são justamente o que um perfil de leitura vem conferir.
  const billingTravado = !acoesPermitidas.billing
  const tituloBilling  = billingTravado ? 'Seu perfil não permite alterar a cobrança desta loja.' : undefined

  const slugLink = (
    // `py-1.5` no celular: é o link que abre o detalhe da loja, o destino mais
    // usado do card, e como texto puro tinha 24px de alvo.
    <Link href={`/plataforma/tenants/${tenant.id}`} className="text-white font-medium hover:text-brand-400 flex items-center gap-1 max-md:py-1.5">
      {tenant.slug}
      {tenant.kind === 'ExternalIntegrated' && <Badge tone="brand">Externo</Badge>}
      <ChevronRight className="w-3.5 h-3.5 text-gray-500" />
    </Link>
  )

  const statusBadge = (
    <Badge tone={tenant.status === 'Active' ? 'success' : 'danger'}>
      {tenant.status === 'Active' ? 'Ativo' : 'Suspenso'}
    </Badge>
  )

  {/* Plano deixou de ser texto livre: o nome tem que bater com a tabela de
      preços do backend, senão a loja fica com mensalidade 0 e some do MRR.
      Escolher um plano já aplica o valor de tabela; "Personalizado" existe
      pro preço negociado, que aí é digitado ao lado. */}
  const selectPlano = (
    <select
      className={clsx('input text-xs py-1', card ? 'w-full' : 'w-32')}
      value={planoSelecionado}
      disabled={savingBilling || billingTravado}
      title={tituloBilling}
      aria-label="Plano contratado"
      onChange={e => aplicarPlano(e.target.value)}
    >
      {PLANOS.map(p => <option key={p.nome} value={p.nome}>{p.nome}</option>)}
      <option value={PLANO_PERSONALIZADO}>{PLANO_PERSONALIZADO}</option>
    </select>
  )

  const campoMensalidade = (
    <>
      <div className="flex items-center gap-1">
        <span className="text-xs text-gray-500">R$</span>
        <input
          className={clsx('input text-xs py-1 tabular-nums', card ? 'w-full' : 'w-20')}
          type="number" min="0" step="0.01"
          value={mensalidade}
          onChange={e => setMensalidade(e.target.value)}
          onBlur={salvarMensalidade}
          disabled={savingBilling || billingTravado}
          aria-label="Mensalidade cobrada desta loja"
          title={tituloBilling ?? 'Mensalidade cobrada desta loja'}
        />
      </div>
      {/* Editável, e não mais um rótulo: a implantação é moeda de troca no
          fechamento — precisa dar pra baixar, subir e zerar por loja. Zero é
          um valor legítimo aqui: o backend só gera a cobrança de implantação
          quando o valor é maior que zero (PlatformController, geração de
          mensalidades), então zerar equivale a "sem taxa". */}
      <div className="flex items-center gap-1 mt-0.5">
        <span className="text-[10px] text-gray-500 shrink-0">implantação R$</span>
        <input
          className={clsx('input text-xs py-0.5 tabular-nums', card ? 'w-full' : 'w-20')}
          type="number" min="0" step="0.01"
          value={implantacao}
          onChange={e => setImplantacao(e.target.value)}
          onBlur={salvarImplantacao}
          disabled={savingBilling || billingTravado}
          aria-label="Taxa de implantação desta loja"
          title={tituloBilling ?? 'Taxa de implantação — cobrada uma vez. Zero = sem cobrança.'}
        />
      </div>
    </>
  )

  const selectPagamento = (
    <StatusPillSelect
      value={tenant.paymentStatus}
      options={PAYMENT_OPTIONS}
      styles={PAYMENT_STYLES}
      disabled={savingBilling || billingTravado}
      onChange={paymentStatus => saveBilling({ paymentStatus })}
    />
  )

  {/* Os 6 toggles de módulo moravam dentro da célula e sozinhos jogavam a
      linha pra ~180px de altura. Aqui fica só a leitura (o que está ligado);
      a edição abre num modal, que é onde cabe o texto explicando cada um. */}
  const botaoModulos = (
    <button
      type="button"
      onClick={() => setShowModules(true)}
      disabled={savingBilling || billingTravado}
      title={tituloBilling ?? 'Editar módulos contratados'}
      className={clsx(
        // `py-2.5` no celular: como `py-1` o botão ficava com 24px de altura, e
        // ele é a única porta de entrada pra edição dos módulos contratados.
        'flex items-center gap-1.5 text-left rounded-lg px-1.5 py-1 max-md:py-2.5 -mx-1.5 hover:bg-surface-700/60 transition-colors disabled:opacity-60',
        card ? 'w-full' : 'max-w-[240px]',
      )}
    >
      {tenant.enabledModules.length === 0 ? (
        <span className="text-xs text-gray-500">nenhum módulo</span>
      ) : (
        <>
          <span className="text-xs font-semibold text-brand-300 tabular-nums shrink-0">
            {tenant.enabledModules.length}/{TENANT_MODULES.length}
          </span>
          <span className="text-xs text-gray-400 truncate">
            {TENANT_MODULES.filter(m => tenant.enabledModules.includes(m.value)).map(m => m.label).join(', ')}
          </span>
        </>
      )}
    </button>
  )

  // Aqui é o contrário dos campos de cobrança: uma fileira de ícones que só
  // servem pra tomar 403 não informa nada, então o que o perfil não faz nem
  // chega a ser desenhado.
  const acoes = (
    <>
      {acoesPermitidas.simular && tenant.kind === 'Native' && <button
        onClick={acessarAdmin}
        disabled={impersonating || tenant.status !== 'Active'}
        title={tenant.status !== 'Active' ? 'Reative o tenant para acessar' : 'Acessar o admin desta loja'}
        aria-label={tenant.status !== 'Active' ? 'Reative o tenant para acessar' : 'Acessar o admin desta loja'}
        className={clsx(BTN_ACAO, 'border-surface-500 text-gray-300 hover:border-surface-400 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed')}
      >
        {impersonating ? <Spinner size="sm" /> : <LogIn className="w-3.5 h-3.5" />}
      </button>}
      {acoesPermitidas.gerenciar && <button
        onClick={toggleStatus}
        disabled={updatingStatus}
        title={tenant.status === 'Active' ? 'Suspender' : 'Reativar'}
        aria-label={tenant.status === 'Active' ? 'Suspender' : 'Reativar'}
        className={clsx(BTN_ACAO, 'border-surface-500',
          tenant.status === 'Active' ? 'text-gray-300 hover:text-red-400 hover:border-surface-400' : 'text-gray-300 hover:text-accent-green hover:border-surface-400')}
      >
        {updatingStatus
          ? <Spinner size="sm" />
          : tenant.status === 'Active' ? <PowerOff className="w-3.5 h-3.5" /> : <Power className="w-3.5 h-3.5" />}
      </button>}
      {acoesPermitidas.gerenciar && tenant.kind === 'Native' && <button
        onClick={baixarBackup}
        disabled={backingUp}
        title="Baixar backup (.sql) desta loja"
        aria-label="Baixar backup (.sql) desta loja"
        className={clsx(BTN_ACAO, 'border-surface-500 text-gray-300 hover:border-surface-400 hover:text-white')}
      >
        {backingUp ? <Spinner size="sm" /> : <Download className="w-3.5 h-3.5" />}
      </button>}
      {acoesPermitidas.apagar && <button
        onClick={() => setShowDelete(true)}
        title="Apagar esta loja permanentemente"
        aria-label="Apagar esta loja permanentemente"
        className={clsx(BTN_ACAO, 'border-red-500/30 text-red-400 hover:bg-red-500/10')}
      >
        <Trash2 className="w-3.5 h-3.5" />
      </button>}
    </>
  )

  /** Nenhuma ação liberada: a barra inteira sai, senão sobra uma borda
   *  separando um espaço vazio no card do celular. */
  const temAcoes = acoesPermitidas.gerenciar || acoesPermitidas.apagar ||
    (acoesPermitidas.simular && tenant.kind === 'Native')

  const modais = (
    <>
      {showDelete && createPortal(
        <DeleteTenantModal tenant={tenant} onClose={() => setShowDelete(false)} onDeleted={onChanged} />,
        document.body,
      )}
      {showModules && createPortal(
        <ModulesModal
          tenant={tenant}
          saving={savingBilling}
          onToggle={toggleModule}
          onClose={() => setShowModules(false)}
        />,
        document.body,
      )}
    </>
  )

  if (card) {
    return (
      <li className="card space-y-3 !p-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">{slugLink}</div>
          <div className="shrink-0">{statusBadge}</div>
        </div>
        {/* Os quatro campos de billing em dois pares: plano/mensalidade andam
            juntos (mudar um mexe no outro), pagamento/módulos são conferência. */}
        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1">
            <span className="block text-[10px] uppercase tracking-wider text-gray-500">Plano</span>
            {selectPlano}
          </label>
          <label className="space-y-1">
            <span className="block text-[10px] uppercase tracking-wider text-gray-500">Mensalidade</span>
            {campoMensalidade}
          </label>
        </div>
        <div className="space-y-1">
          <span className="block text-[10px] uppercase tracking-wider text-gray-500">Pagamento</span>
          {selectPagamento}
        </div>
        <div className="space-y-1">
          <span className="block text-[10px] uppercase tracking-wider text-gray-500">Módulos</span>
          {botaoModulos}
        </div>
        <dl className="space-y-1 border-t border-surface-600 pt-2">
          <div className="field-row"><dt>Criado em</dt><dd>{fmtDate(tenant.createdAt)}</dd></div>
          <div className="field-row">
            <dt>Última atividade</dt>
            <dd><span className={clsx('text-xs font-medium', ACTIVITY_TONE[activity.tone])}>{activity.text}</span></dd>
          </div>
        </dl>
        {temAcoes && <div className="flex items-center gap-1.5 border-t border-surface-600 pt-3">{acoes}</div>}
        {modais}
      </li>
    )
  }

  return (
    <tr className="border-b border-surface-700 last:border-0">
      <td className="py-3">{slugLink}</td>
      <td className="py-3">{statusBadge}</td>
      <td className="py-3">{selectPlano}</td>
      <td className="py-3">{campoMensalidade}</td>
      <td className="py-3">{selectPagamento}</td>
      <td className="py-3">{botaoModulos}</td>
      <td className="py-3 text-gray-400">{fmtDate(tenant.createdAt)}</td>
      <td className="py-3">
        <span className={clsx('text-xs font-medium', ACTIVITY_TONE[activity.tone])}>{activity.text}</span>
      </td>
      {temAcoes && (
        <td className="py-3 text-right">
          <div className="flex items-center justify-end gap-1.5">{acoes}</div>
        </td>
      )}
      {modais}
    </tr>
  )
}

export default function PlataformaTenantsPage() {
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [overview, setOverview] = useState<PlatformOverviewDto | null>(null)
  const [filtro, setFiltro] = useState<TenantFiltro>('todos')
  const [filtroTexto, setFiltroTexto] = useState('')
  const pode = usePlatformPermissions()

  const acoesPermitidas: TenantAcoes = {
    gerenciar: pode('platform.tenants.manage'),
    billing:   pode('platform.finance.manage'),
    apagar:    pode('platform.tenants.delete'),
    simular:   pode('platform.impersonate'),
  }
  const temAcoes = acoesPermitidas.gerenciar || acoesPermitidas.apagar || acoesPermitidas.simular

  /// `loading` só vale pra primeira carga. Recarregar depois de uma edição
  /// inline tem que ser silencioso: com KPIs, busca e tabela atrás de
  /// `!loading`, ligar o spinner a cada toggle apagaria a página inteira e
  /// desmontaria a linha — o que fecharia o modal de módulos a cada clique.
  const fetchTenants = useCallback((silencioso = false) => {
    if (!silencioso) setLoading(true)
    platformApi.listTenants()
      .then(r => setTenants(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar tenants')))
      .finally(() => { if (!silencioso) setLoading(false) })
  }, [])

  /// Passado às linhas: recarrega o dado sem piscar a tela.
  const recarregarSilencioso = useCallback(() => fetchTenants(true), [fetchTenants])

  const fetchOverview = useCallback(() => {
    // Falha aqui não pode derrubar a tabela de tenants — só a coluna de
    // atividade fica vazia (overview permanece null).
    platformApi.getOverview()
      .then(r => setOverview(r.data))
      .catch(() => {})
  }, [])

  useEffect(() => { fetchTenants() }, [fetchTenants])
  useEffect(() => { fetchOverview() }, [fetchOverview])

  const activityByTenant = new Map((overview?.tenants ?? []).map(t => [t.tenantId, t.lastActivityAt]))

  const qtdAtivos    = tenants.filter(t => t.status === 'Active').length
  const qtdSuspensos = tenants.length - qtdAtivos
  const qtdAtrasados = tenants.filter(t => t.paymentStatus === 'Atrasado').length

  const busca = filtroTexto.trim().toLowerCase()
  const tenantsVisiveis = tenants.filter(t => {
    if (busca && !t.slug.toLowerCase().includes(busca) && !t.planName.toLowerCase().includes(busca)) return false
    if (filtro === 'ativos')     return t.status === 'Active'
    if (filtro === 'suspensos')  return t.status !== 'Active'
    if (filtro === 'atrasados')  return t.paymentStatus === 'Atrasado'
    return true
  })

  return (
    <div className="space-y-5">
      <PageHeader
        icon={Building2}
        title="Tenants"
        description={loading ? 'Lojas cadastradas na plataforma' : `${tenants.length} loja${tenants.length === 1 ? '' : 's'} na plataforma`}
        actions={acoesPermitidas.gerenciar && (
          <button onClick={() => setShowCreate(true)} className="btn-primary text-sm py-1.5">
            <Plus className="w-4 h-4" /> Cadastrar Tenant
          </button>
        )}
      />

      {/* KPIs que também são filtro — mesmo padrão do Estoque, em vez de
          mandar o dono da plataforma ler a tabela inteira pra achar quem está
          suspenso ou devendo. */}
      {!loading && tenants.length > 0 && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <StatCard
            icon={Building2} label="Total de lojas" value={tenants.length} tone="brand"
            selected={filtro === 'todos'} onClick={() => setFiltro('todos')}
          />
          <StatCard
            icon={CheckCircle2} label="Ativas" value={qtdAtivos} tone="success"
            selected={filtro === 'ativos'} onClick={() => setFiltro(filtro === 'ativos' ? 'todos' : 'ativos')}
          />
          <StatCard
            icon={PauseCircle} label="Suspensas" value={qtdSuspensos} tone="warning"
            selected={filtro === 'suspensos'} onClick={() => setFiltro(filtro === 'suspensos' ? 'todos' : 'suspensos')}
          />
          <StatCard
            icon={AlertCircle} label="Pagamento atrasado" value={qtdAtrasados} tone="danger"
            selected={filtro === 'atrasados'} onClick={() => setFiltro(filtro === 'atrasados' ? 'todos' : 'atrasados')}
          />
        </div>
      )}

      {!loading && tenants.length > 0 && (
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500 pointer-events-none" />
          <input
            className="input pl-9"
            value={filtroTexto}
            onChange={e => setFiltroTexto(e.target.value)}
            placeholder="Buscar por slug ou plano..."
          />
        </div>
      )}

      {/* O contêiner só é `card` quando há tabela dentro. Na versão celular
          cada tenant JÁ é um card — envolvê-los noutro card daria caixa dentro
          de caixa, com a mesma cor de fundo e duas bordas concorrendo. */}
      {loading ? (
        <div className="card"><Spinner size="lg" block /></div>
      ) : tenants.length === 0 ? (
        <div className="card">
          <EmptyState
            icon={Building2}
            message="Nenhum tenant cadastrado ainda."
            action={acoesPermitidas.gerenciar && (
              <button onClick={() => setShowCreate(true)} className="btn-primary text-sm py-1.5 mt-3">
                <Plus className="w-4 h-4" /> Cadastrar o primeiro
              </button>
            )}
          />
        </div>
      ) : tenantsVisiveis.length === 0 ? (
        <div className="card"><EmptyState icon={Search} message="Nenhuma loja bate com esse filtro." compact /></div>
      ) : (
          <>
            {/* Nove colunas — a maior tabela do sistema. No celular cada tenant
                vira um card com os mesmos campos editáveis. */}
            <div className="card table-scroll hidden sm:block">
              <table className="w-full min-w-[760px] text-sm">
                <thead>
                  <tr className="text-left text-gray-500 border-b border-surface-600">
                    <th className="py-2 font-medium">Slug</th>
                    <th className="py-2 font-medium">Status</th>
                    <th className="py-2 font-medium">Plano</th>
                    <th className="py-2 font-medium">Mensalidade</th>
                    <th className="py-2 font-medium">Pagamento</th>
                    <th className="py-2 font-medium">Módulos</th>
                    <th className="py-2 font-medium">Criado em</th>
                    <th className="py-2 font-medium">Última atividade</th>
                    {/* Cabeçalho e célula andam juntos: esconder só um deles
                        desalinharia a tabela inteira. */}
                    {temAcoes && <th className="py-2 font-medium text-right">Ações</th>}
                  </tr>
                </thead>
                <tbody>
                  {tenantsVisiveis.map(t => (
                    <TenantRow key={t.id} tenant={t} lastActivityAt={activityByTenant.get(t.id)} onChanged={recarregarSilencioso} acoesPermitidas={acoesPermitidas} />
                  ))}
                </tbody>
              </table>
            </div>
            <ul className="space-y-2 sm:hidden">
              {tenantsVisiveis.map(t => (
                <TenantRow key={t.id} layout="card" tenant={t} lastActivityAt={activityByTenant.get(t.id)} onChanged={recarregarSilencioso} acoesPermitidas={acoesPermitidas} />
              ))}
            </ul>
          </>
      )}

      {showCreate && <CreateTenantModal onClose={() => setShowCreate(false)} onCreated={() => fetchTenants()} />}
    </div>
  )
}
