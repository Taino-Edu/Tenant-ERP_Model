'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { CalendarDays, Check, Copy, HandCoins, Link2, Mail, Pencil, RefreshCw, Undo2, UserPlus, Users, X } from 'lucide-react'
import Button from '@/components/admin/ui/Button'
import Spinner from '@/components/admin/ui/Spinner'
import {
  getErrorMessage, platformApi, referralApi,
  type ReferralCommissionDto, type ReferralInvitationDto, type ReferralPartnerDto, type ReferralSummaryDto,
  type SaveReferralPartnerRequest, type TenantReferralDto, type TenantSummary,
} from '@/lib/api'

const money = (value: number) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const date = (iso?: string | null) => iso ? iso.slice(0, 10).split('-').reverse().join('/') : '—'
const input = 'w-full rounded-lg border border-surface-500 bg-surface-700 px-3 py-2 text-sm text-white outline-none focus:border-brand-400'

const emptyPartner: SaveReferralPartnerRequest = {
  name: '', document: '', phone: '', email: '', pixKey: '',
  personType: 'PF', partnerKind: 'Vendedor', professionalRegistration: '',
  setupCommissionPercent: 30, monthlyCommissionPercent: 5, paymentDay: 10, paymentGraceDays: 5, active: true,
}

export default function IndicacoesPage() {
  const [summary, setSummary] = useState<ReferralSummaryDto | null>(null)
  const [partners, setPartners] = useState<ReferralPartnerDto[]>([])
  const [assignments, setAssignments] = useState<TenantReferralDto[]>([])
  const [commissions, setCommissions] = useState<ReferralCommissionDto[]>([])
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [invitations, setInvitations] = useState<ReferralInvitationDto[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [partnerFilter, setPartnerFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  const [editingId, setEditingId] = useState<string | null>(null)
  const [partnerForm, setPartnerForm] = useState<SaveReferralPartnerRequest>(emptyPartner)
  const [assignment, setAssignment] = useState({
    partnerId: '', tenantId: '', setupPercent: '', monthlyPercent: '', cycles: '', notes: '',
  })
  const [invite, setInvite] = useState({ name: '', email: '', partnerKind: 'Vendedor', setupCommissionPercent: 30, monthlyCommissionPercent: 5, paymentGraceDays: 5 })

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [s, p, a, c, t, i] = await Promise.all([
        referralApi.summary(), referralApi.partners(), referralApi.assignments(),
        referralApi.commissions(partnerFilter, statusFilter), platformApi.listTenants(), referralApi.invitations(),
      ])
      setSummary(s.data); setPartners(p.data); setAssignments(a.data); setCommissions(c.data); setTenants(t.data); setInvitations(i.data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível carregar o controle de indicações.'))
    } finally { setLoading(false) }
  }, [partnerFilter, statusFilter])

  useEffect(() => { load() }, [load])

  const activePartners = useMemo(() => partners.filter(p => p.active), [partners])

  async function savePartner(event: React.FormEvent) {
    event.preventDefault(); setSaving(true)
    try {
      if (editingId) await referralApi.updatePartner(editingId, partnerForm)
      else await referralApi.createPartner(partnerForm)
      toast.success(editingId ? 'Vendedor atualizado.' : 'Vendedor cadastrado.')
      setEditingId(null); setPartnerForm(emptyPartner); await load()
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível salvar o vendedor.')) }
    finally { setSaving(false) }
  }

  function editPartner(partner: ReferralPartnerDto) {
    setEditingId(partner.id)
    setPartnerForm({
      name: partner.name, document: partner.document, phone: partner.phone, email: partner.email,
      pixKey: partner.pixKey, setupCommissionPercent: partner.setupCommissionPercent,
      monthlyCommissionPercent: partner.monthlyCommissionPercent, paymentDay: partner.paymentDay,
      personType: partner.personType, partnerKind: partner.partnerKind,
      professionalRegistration: partner.professionalRegistration, paymentGraceDays: partner.paymentGraceDays,
      active: partner.active,
    })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  async function createInvitation(sendEmail: boolean) {
    if (sendEmail && !invite.email) return toast.error('Informe o e-mail do parceiro.')
    setSaving(true)
    try {
      const { data } = await referralApi.createInvitation({ ...invite, expiresInDays: 7, sendEmail, email: invite.email || null, name: invite.name || null })
      if (data.inviteUrl) {
        try {
          await navigator.clipboard.writeText(data.inviteUrl)
          toast.success(sendEmail ? 'Convite enviado e link copiado.' : 'Link de convite copiado.')
        } catch {
          toast.success(sendEmail ? 'Convite enviado por e-mail.' : 'Convite gerado; a cópia automática foi bloqueada pelo navegador.')
        }
      }
      setInvite({ name: '', email: '', partnerKind: 'Vendedor', setupCommissionPercent: 30, monthlyCommissionPercent: 5, paymentGraceDays: 5 })
      await load()
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível gerar o convite.')) }
    finally { setSaving(false) }
  }

  async function revokeInvitation(id: string) {
    try { await referralApi.revokeInvitation(id); toast.success('Convite revogado.'); await load() }
    catch (error) { toast.error(getErrorMessage(error, 'Não foi possível revogar o convite.')) }
  }

  async function saveAssignment(event: React.FormEvent) {
    event.preventDefault()
    if (!assignment.partnerId || !assignment.tenantId) return toast.error('Selecione o vendedor e o cliente.')
    setSaving(true)
    try {
      await referralApi.saveAssignment({
        partnerId: assignment.partnerId, tenantId: assignment.tenantId, active: true,
        setupCommissionPercent: assignment.setupPercent === '' ? null : Number(assignment.setupPercent),
        monthlyCommissionPercent: assignment.monthlyPercent === '' ? null : Number(assignment.monthlyPercent),
        monthlyCommissionCycles: assignment.cycles === '' ? null : Number(assignment.cycles),
        notes: assignment.notes || null,
      })
      toast.success('Indicação vinculada ao cliente.')
      setAssignment({ partnerId: '', tenantId: '', setupPercent: '', monthlyPercent: '', cycles: '', notes: '' })
      await load()
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível vincular a indicação.')) }
    finally { setSaving(false) }
  }

  async function togglePayment(item: ReferralCommissionDto) {
    try {
      const reference = item.paidAt ? null : window.prompt(`Informe a referência do documento ${item.fiscalDocumentType} deste repasse:`)
      if (!item.paidAt && !reference?.trim()) return
      await referralApi.setCommissionPayment(item.id, item.paidAt ? null : new Date().toISOString().slice(0, 10), reference?.trim())
      toast.success(item.paidAt ? 'Comissão reaberta.' : 'Comissão marcada como paga.')
      await load()
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível atualizar a comissão.')) }
  }

  return (
    <div className="space-y-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold text-white"><HandCoins className="text-brand-400" /> Indicações e comissões</h1>
          <p className="mt-1 text-sm text-gray-400">Controle gerencial dos vendedores autônomos, clientes indicados e datas de repasse.</p>
        </div>
        <Button variant="secondary" onClick={load}><RefreshCw className="h-4 w-4" /> Atualizar</Button>
      </div>

      {loading ? <div className="flex justify-center py-20"><Spinner /></div> : <>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
          <Metric title="Vendedores ativos" value={String(summary?.activePartners ?? 0)} />
          <Metric title="Clientes indicados" value={String(summary?.referredClients ?? 0)} />
          <Metric title="MRR indicado" value={money(summary?.referredMrr ?? 0)} />
          <Metric title="A pagar" value={money(summary?.pendingAmount ?? 0)} />
          <Metric title="Disponível" value={money(summary?.overdueAmount ?? 0)} />
          <Metric title="Já pago" value={money(summary?.paidAmount ?? 0)} />
        </div>

        <section className="space-y-4 rounded-xl border border-brand-500/40 bg-surface-800 p-5">
          <div><h2 className="flex items-center gap-2 font-semibold text-white"><Mail className="h-4 w-4 text-brand-400" /> Convidar parceiro comercial</h2><p className="mt-1 text-xs text-gray-400">Envie por e-mail ou gere um link. O regulamento e as regras ficam congelados na versão aceita.</p></div>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
            <Field label="Nome"><input className={input} value={invite.name} onChange={e => setInvite(v => ({ ...v, name: e.target.value }))} /></Field>
            <Field label="E-mail"><input type="email" className={input} value={invite.email} onChange={e => setInvite(v => ({ ...v, email: e.target.value }))} /></Field>
            <Field label="Tipo"><select className={input} value={invite.partnerKind} onChange={e => setInvite(v => ({ ...v, partnerKind: e.target.value }))}><option>Vendedor</option><option>Indicador</option><option>Contador</option></select></Field>
            <Field label="% implantação"><input type="number" min={0} max={100} className={input} value={invite.setupCommissionPercent} onChange={e => setInvite(v => ({ ...v, setupCommissionPercent: Number(e.target.value) }))} /></Field>
            <Field label="% mensalidade"><input type="number" min={0} max={100} className={input} value={invite.monthlyCommissionPercent} onChange={e => setInvite(v => ({ ...v, monthlyCommissionPercent: Number(e.target.value) }))} /></Field>
            <Field label="Carência (dias)"><input type="number" min={0} max={60} className={input} value={invite.paymentGraceDays} onChange={e => setInvite(v => ({ ...v, paymentGraceDays: Number(e.target.value) }))} /></Field>
          </div>
          <div className="flex flex-wrap gap-2"><Button type="button" onClick={() => createInvitation(true)} loading={saving}><Mail className="h-4 w-4" /> Enviar por e-mail</Button><Button type="button" variant="secondary" onClick={() => createInvitation(false)} disabled={saving}><Copy className="h-4 w-4" /> Gerar e copiar link</Button></div>
          {invitations.length > 0 && <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="text-left text-xs uppercase text-gray-500"><tr><th className="py-2">Parceiro</th><th>Regras</th><th>Validade</th><th>Status</th><th /></tr></thead><tbody>{invitations.slice(0, 10).map(i => <tr key={i.id} className="border-t border-surface-600"><td className="py-2 text-white">{i.name || i.email || 'Link aberto'}</td><td className="text-gray-300">{i.setupCommissionPercent}% + {i.monthlyCommissionPercent}% · {i.paymentGraceDays} dias</td><td className="text-gray-300">{date(i.expiresAt)}</td><td><InviteStatus value={i.status} /></td><td className="text-right">{i.status === 'Pendente' && <button onClick={() => revokeInvitation(i.id)} aria-label="Revogar convite" className="p-2 text-gray-400 hover:text-red-400"><X className="h-4 w-4" /></button>}</td></tr>)}</tbody></table></div>}
        </section>

        <div className="grid gap-6 xl:grid-cols-2">
          <form onSubmit={savePartner} className="space-y-4 rounded-xl border border-surface-500 bg-surface-800 p-5">
            <h2 className="flex items-center gap-2 font-semibold text-white"><UserPlus className="h-4 w-4 text-brand-400" /> {editingId ? 'Editar vendedor' : 'Cadastrar vendedor autônomo'}</h2>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Nome *"><input required className={input} value={partnerForm.name} onChange={e => setPartnerForm(f => ({ ...f, name: e.target.value }))} /></Field>
              <Field label="CPF/CNPJ"><input className={input} value={partnerForm.document ?? ''} onChange={e => setPartnerForm(f => ({ ...f, document: e.target.value }))} /></Field>
              <Field label="Telefone"><input className={input} value={partnerForm.phone ?? ''} onChange={e => setPartnerForm(f => ({ ...f, phone: e.target.value }))} /></Field>
              <Field label="E-mail"><input type="email" className={input} value={partnerForm.email ?? ''} onChange={e => setPartnerForm(f => ({ ...f, email: e.target.value }))} /></Field>
              <Field label="Chave Pix"><input className={input} value={partnerForm.pixKey ?? ''} onChange={e => setPartnerForm(f => ({ ...f, pixKey: e.target.value }))} /></Field>
              <Field label="Tipo de pessoa"><select className={input} value={partnerForm.personType} onChange={e => setPartnerForm(f => ({ ...f, personType: e.target.value as 'PF' | 'PJ' }))}><option value="PF">Pessoa física (RPA)</option><option value="PJ">Pessoa jurídica (NFS-e)</option></select></Field>
              <Field label="Carência após baixa (dias)"><input type="number" min={0} max={60} className={input} value={partnerForm.paymentGraceDays} onChange={e => setPartnerForm(f => ({ ...f, paymentGraceDays: Number(e.target.value) }))} /></Field>
              <Field label="% sobre implantação"><input type="number" min={0} max={100} step="0.01" className={input} value={partnerForm.setupCommissionPercent} onChange={e => setPartnerForm(f => ({ ...f, setupCommissionPercent: Number(e.target.value) }))} /></Field>
              <Field label="% sobre mensalidade"><input type="number" min={0} max={100} step="0.01" className={input} value={partnerForm.monthlyCommissionPercent} onChange={e => setPartnerForm(f => ({ ...f, monthlyCommissionPercent: Number(e.target.value) }))} /></Field>
            </div>
            <label className="flex items-center gap-2 text-sm text-gray-300"><input type="checkbox" checked={partnerForm.active} onChange={e => setPartnerForm(f => ({ ...f, active: e.target.checked }))} /> Vendedor ativo</label>
            <div className="flex gap-2"><Button type="submit" loading={saving}>{editingId ? 'Salvar alterações' : 'Cadastrar'}</Button>{editingId && <Button type="button" variant="secondary" onClick={() => { setEditingId(null); setPartnerForm(emptyPartner) }}>Cancelar</Button>}</div>
          </form>

          <form onSubmit={saveAssignment} className="space-y-4 rounded-xl border border-surface-500 bg-surface-800 p-5">
            <h2 className="flex items-center gap-2 font-semibold text-white"><Link2 className="h-4 w-4 text-brand-400" /> Vincular indicação a um cliente</h2>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Vendedor *"><select className={input} value={assignment.partnerId} onChange={e => setAssignment(a => ({ ...a, partnerId: e.target.value }))}><option value="">Selecione</option>{activePartners.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></Field>
              <Field label="Cliente / tenant *"><select className={input} value={assignment.tenantId} onChange={e => setAssignment(a => ({ ...a, tenantId: e.target.value }))}><option value="">Selecione</option>{tenants.map(t => <option key={t.id} value={t.id}>{t.slug} · {t.planName}</option>)}</select></Field>
              <Field label="% implantação (vazio = padrão)"><input type="number" min={0} max={100} step="0.01" className={input} value={assignment.setupPercent} onChange={e => setAssignment(a => ({ ...a, setupPercent: e.target.value }))} /></Field>
              <Field label="% mensalidade (vazio = padrão)"><input type="number" min={0} max={100} step="0.01" className={input} value={assignment.monthlyPercent} onChange={e => setAssignment(a => ({ ...a, monthlyPercent: e.target.value }))} /></Field>
              <Field label="Quantidade de mensalidades"><input type="number" min={1} placeholder="Vazio = recorrente" className={input} value={assignment.cycles} onChange={e => setAssignment(a => ({ ...a, cycles: e.target.value }))} /></Field>
              <Field label="Observação"><input className={input} value={assignment.notes} onChange={e => setAssignment(a => ({ ...a, notes: e.target.value }))} /></Field>
            </div>
            <p className="text-xs text-gray-500">A comissão só é liberada quando o pagamento do cliente recebe baixa no Financeiro.</p>
            <Button type="submit" loading={saving}><Link2 className="h-4 w-4" /> Salvar vínculo</Button>
          </form>
        </div>

        <section className="rounded-xl border border-surface-500 bg-surface-800">
          <div className="border-b border-surface-500 px-5 py-4"><h2 className="flex items-center gap-2 font-semibold text-white"><Users className="h-4 w-4" /> Vendedores e próximos pagamentos</h2></div>
          <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="text-left text-xs uppercase text-gray-500"><tr><th className="px-4 py-3">Vendedor</th><th className="px-4 py-3">Comissões</th><th className="px-4 py-3">Clientes</th><th className="px-4 py-3">Disponível em</th><th className="px-4 py-3 text-right">A pagar</th><th /></tr></thead><tbody>{partners.map(p => <tr key={p.id} className="border-t border-surface-600"><td className="px-4 py-3"><p className="font-medium text-white">{p.name}</p><p className="text-xs text-gray-500">{p.active ? 'Ativo' : 'Inativo'} · carência {p.paymentGraceDays} dia(s)</p></td><td className="px-4 py-3 text-gray-300">{p.setupCommissionPercent}% implantação · {p.monthlyCommissionPercent}% mensal</td><td className="px-4 py-3 text-gray-300">{p.referredClients}</td><td className="px-4 py-3 text-gray-300">{date(p.nextPaymentDate)}</td><td className="px-4 py-3 text-right font-semibold text-white">{money(p.pendingAmount)}</td><td className="px-4 py-3 text-right"><button onClick={() => editPartner(p)} className="rounded-lg p-2 text-gray-400 hover:bg-surface-600 hover:text-white" aria-label={`Editar ${p.name}`}><Pencil className="h-4 w-4" /></button></td></tr>)}</tbody></table></div>
        </section>

        <section className="rounded-xl border border-surface-500 bg-surface-800">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-surface-500 px-5 py-4"><h2 className="flex items-center gap-2 font-semibold text-white"><CalendarDays className="h-4 w-4" /> Agenda de comissões</h2><div className="flex gap-2"><select className={input} value={partnerFilter} onChange={e => setPartnerFilter(e.target.value)}><option value="">Todos os vendedores</option>{partners.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select><select className={input} value={statusFilter} onChange={e => setStatusFilter(e.target.value)}><option value="">Todos os status</option><option>Carência</option><option>Disponível</option><option>Pago</option></select></div></div>
          {commissions.length === 0 ? <p className="p-8 text-center text-sm text-gray-500">Nenhuma comissão encontrada. Ela será criada quando uma cobrança de cliente indicado for paga.</p> : <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="text-left text-xs uppercase text-gray-500"><tr><th className="px-4 py-3">Vendedor / cliente</th><th className="px-4 py-3">Origem</th><th className="px-4 py-3">Disponível em</th><th className="px-4 py-3">Status</th><th className="px-4 py-3 text-right">Comissão</th><th className="px-4 py-3 text-right">Ação</th></tr></thead><tbody>{commissions.map(c => <tr key={c.id} className="border-t border-surface-600"><td className="px-4 py-3"><p className="font-medium text-white">{c.partnerName}</p><p className="text-xs text-gray-500">{c.tenantName}</p></td><td className="px-4 py-3 text-gray-300">{c.type === 'Implantacao' ? 'Implantação' : 'Mensalidade'}<p className="text-xs text-gray-500">{c.commissionPercent}% de {money(c.baseAmount)}</p></td><td className="px-4 py-3 text-gray-300">{date(c.dueDate)}</td><td className="px-4 py-3"><Status value={c.status} /></td><td className="px-4 py-3 text-right font-semibold text-white">{money(c.amount)}</td><td className="px-4 py-3 text-right"><Button size="sm" variant={c.paidAt ? 'secondary' : 'success'} disabled={c.status === 'Carência'} onClick={() => togglePayment(c)}>{c.paidAt ? <Undo2 className="h-3.5 w-3.5" /> : <Check className="h-3.5 w-3.5" />}{c.paidAt ? 'Reabrir' : 'Pagar'}</Button></td></tr>)}</tbody></table></div>}
        </section>

        {assignments.length > 0 && <p className="text-xs text-gray-500">{assignments.length} cliente(s) com atribuição comercial registrada. As regras ficam congeladas por contrato, mesmo se o padrão do vendedor mudar.</p>}
      </>}
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="space-y-1 text-xs font-medium text-gray-400"><span>{label}</span>{children}</label> }
function Metric({ title, value, alert }: { title: string; value: string; alert?: boolean }) { return <div className="rounded-xl border border-surface-500 bg-surface-800 p-4"><p className="text-xs uppercase tracking-wide text-gray-500">{title}</p><p className={`mt-2 text-xl font-bold ${alert ? 'text-accent-red' : 'text-white'}`}>{value}</p></div> }
function Status({ value }: { value: ReferralCommissionDto['status'] }) { const color = value === 'Pago' ? 'bg-accent-green/15 text-accent-green' : value === 'Disponível' ? 'bg-accent-green/15 text-accent-green' : 'bg-surface-600 text-gray-300'; return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${color}`}>{value}</span> }
function InviteStatus({ value }: { value: ReferralInvitationDto['status'] }) { return <span className="rounded-full bg-surface-600 px-2.5 py-1 text-xs font-semibold text-gray-300">{value}</span> }
