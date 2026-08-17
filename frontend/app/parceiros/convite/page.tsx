'use client'

import { Suspense, useEffect, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { CheckCircle2, HandCoins } from 'lucide-react'
import toast, { Toaster } from 'react-hot-toast'
import Button from '@/components/admin/ui/Button'
import Spinner from '@/components/admin/ui/Spinner'
import { getErrorMessage, publicReferralApi, type ReferralInvitationDto } from '@/lib/api'

const input = 'w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2.5 text-white outline-none focus:border-blue-500'

export default function ReferralInvitationPage() {
  return <Suspense fallback={<main className="grid min-h-screen place-items-center bg-slate-950"><Spinner /></main>}><InvitationForm /></Suspense>
}

function InvitationForm() {
  const token = useSearchParams().get('token') ?? ''
  const [invitation, setInvitation] = useState<ReferralInvitationDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [done, setDone] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [form, setForm] = useState({ name: '', email: '', document: '', phone: '', pixKey: '', personType: 'PF' as 'PF' | 'PJ', professionalRegistration: '', acceptedTerms: false })

  useEffect(() => {
    if (!token) { setLoading(false); return }
    publicReferralApi.invitation(token).then(({ data }) => {
      setInvitation(data)
      setForm(f => ({ ...f, name: data.name ?? '', email: data.email ?? '' }))
    }).catch(error => {
      const message = getErrorMessage(error, 'Convite inválido ou expirado.')
      setErrorMessage(message)
      toast.error(message)
    }).finally(() => setLoading(false))
  }, [token])

  async function accept(event: React.FormEvent) {
    event.preventDefault()
    setErrorMessage(null)

    const name = form.name.trim()
    const email = form.email.trim()
    const document = form.document.replace(/\D/g, '')
    let validationError: string | null = null

    if (!name) validationError = 'Informe o nome completo ou a razão social.'
    else if (!/^\S+@\S+\.\S+$/.test(email)) validationError = 'Informe um e-mail válido.'
    else if (form.personType === 'PF' && document.length !== 11) validationError = 'Informe um CPF com 11 dígitos.'
    else if (form.personType === 'PJ' && document.length !== 14) validationError = 'Informe um CNPJ com 14 dígitos.'
    else if (!form.acceptedTerms) validationError = 'Leia e marque o aceite do regulamento para continuar.'

    if (validationError) {
      setErrorMessage(validationError)
      toast.error(validationError)
      return
    }

    setSaving(true)
    try {
      await publicReferralApi.accept(token, { ...form, name, email, document })
      setDone(true)
    }
    catch (error) {
      const message = getErrorMessage(error, 'Não foi possível concluir o cadastro.')
      setErrorMessage(message)
      toast.error(message)
    }
    finally { setSaving(false) }
  }

  const notifications = <Toaster position="top-center" toastOptions={{ style: { background: '#1e293b', color: '#fff', border: '1px solid #334155' } }} />

  if (loading) return <main className="grid min-h-screen place-items-center bg-slate-950">{notifications}<Spinner /></main>
  if (done) return <main className="grid min-h-screen place-items-center bg-slate-950 p-4">{notifications}<div className="max-w-lg rounded-2xl border border-emerald-500/30 bg-slate-900 p-8 text-center text-white"><CheckCircle2 className="mx-auto mb-4 h-12 w-12 text-emerald-400" /><h1 className="text-2xl font-bold">Parceria registrada</h1><p className="mt-2 text-slate-400">Seus dados e o aceite foram registrados. A equipe 3ESYSTEN dará continuidade ao processo.</p></div></main>
  if (!invitation) return <main className="grid min-h-screen place-items-center bg-slate-950 p-4 text-center text-slate-300">{notifications}<div><h1 className="text-xl font-bold text-white">Convite indisponível</h1><p className="mt-2">{errorMessage ?? 'O link pode estar inválido, expirado, revogado ou já ter sido utilizado.'}</p></div></main>

  return <main className="min-h-screen bg-slate-950 px-4 py-10 text-slate-200">{notifications}<form noValidate onSubmit={accept} className="mx-auto max-w-3xl space-y-6 rounded-2xl border border-slate-800 bg-slate-900 p-5 sm:p-8">
    <div><h1 className="flex items-center gap-2 text-2xl font-bold text-white"><HandCoins className="text-blue-400" /> Programa de Parcerias 3ESYSTEN</h1><p className="mt-2 text-sm text-slate-400">Convite para {invitation.partnerKind}. Confira as condições antes de aceitar.</p></div>
    <div className="grid gap-3 rounded-xl bg-slate-950 p-4 sm:grid-cols-3"><Rule title="Implantação" value={`${invitation.setupCommissionPercent}%`} detail="somente se o plano possuir taxa e ela for paga" /><Rule title="Mensalidades" value={`${invitation.monthlyCommissionPercent}%`} detail="enquanto o cliente indicado continuar pagando" /><Rule title="Disponibilidade" value={`${invitation.paymentGraceDays} dias`} detail="após a liquidação de cada pagamento" /></div>
    <div className="grid gap-4 sm:grid-cols-2">
      <Field label="Nome completo / razão social"><input required className={input} value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} /></Field>
      <Field label="E-mail"><input required type="email" readOnly={Boolean(invitation.email)} className={input} value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} /></Field>
      <Field label="Tipo de pessoa"><select className={input} value={form.personType} onChange={e => setForm(f => ({ ...f, personType: e.target.value as 'PF' | 'PJ' }))}><option value="PF">Pessoa física</option><option value="PJ">Pessoa jurídica</option></select></Field>
      <Field label={form.personType === 'PF' ? 'CPF' : 'CNPJ'}><input required className={input} value={form.document} onChange={e => setForm(f => ({ ...f, document: e.target.value }))} /></Field>
      <Field label="Telefone"><input className={input} value={form.phone} onChange={e => setForm(f => ({ ...f, phone: e.target.value }))} /></Field>
      <Field label="Chave Pix"><input className={input} value={form.pixKey} onChange={e => setForm(f => ({ ...f, pixKey: e.target.value }))} /></Field>
      <Field label="Registro profissional (CORE/CRC, se aplicável)"><input className={input} value={form.professionalRegistration} onChange={e => setForm(f => ({ ...f, professionalRegistration: e.target.value }))} /></Field>
    </div>
    <section><h2 className="mb-2 font-semibold text-white">Regulamento — versão {invitation.contractVersion}</h2><div className="max-h-80 overflow-y-auto whitespace-pre-wrap rounded-xl border border-slate-700 bg-slate-950 p-4 text-sm leading-6 text-slate-300">{invitation.contractText}</div></section>
    <label className="flex items-start gap-3 rounded-xl border border-slate-700 p-4 text-sm"><input required type="checkbox" className="mt-1" checked={form.acceptedTerms} onChange={e => setForm(f => ({ ...f, acceptedTerms: e.target.checked }))} /><span>Li integralmente o regulamento, compreendi as regras de comissão, documentação fiscal e tratamento de dados, e aceito eletronicamente esta versão.</span></label>
    {errorMessage && <div role="alert" aria-live="assertive" className="rounded-xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-200">{errorMessage}</div>}
    <Button type="submit" loading={saving}>Aceitar e concluir cadastro</Button>
  </form></main>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="space-y-1 text-xs font-medium text-slate-400"><span>{label}</span>{children}</label> }
function Rule({ title, value, detail }: { title: string; value: string; detail: string }) { return <div><p className="text-xs uppercase text-slate-500">{title}</p><p className="text-xl font-bold text-white">{value}</p><p className="text-xs text-slate-400">{detail}</p></div> }
