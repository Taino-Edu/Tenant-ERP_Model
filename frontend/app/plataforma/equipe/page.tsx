'use client'

import { useCallback, useEffect, useState } from 'react'
import { platformApi, PlatformAccessProfileDto, PlatformTeamMemberDto } from '@/lib/api'
import { getErrorMessage } from '@/lib/api'
import toast from 'react-hot-toast'
import { Check, Clock3, Crown, Loader2, MailPlus, RefreshCw, ShieldCheck, UserRound, Users } from 'lucide-react'

export default function EquipePlataformaPage() {
  const [members, setMembers] = useState<PlatformTeamMemberDto[]>([])
  const [profiles, setProfiles] = useState<PlatformAccessProfileDto[]>([])
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [profileKey, setProfileKey] = useState('partner_admin')

  const load = useCallback(async () => {
    try {
      const [teamResponse, profilesResponse] = await Promise.all([
        platformApi.listTeam(), platformApi.listTeamProfiles(),
      ])
      setMembers(teamResponse.data)
      setProfiles(profilesResponse.data)
      if (profilesResponse.data.length)
        setProfileKey(current => profilesResponse.data.some(profile => profile.key === current) ? current : profilesResponse.data[0].key)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível carregar a equipe.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function invite(event: React.FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    try {
      const response = await platformApi.inviteTeamMember({ name: name.trim(), email: email.trim(), profileKey })
      toast.success(response.data.message)
      setName('')
      setEmail('')
      await load()
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível criar o convite.'))
    } finally {
      setSubmitting(false)
    }
  }

  async function updateMember(member: PlatformTeamMemberDto, next: Partial<PlatformTeamMemberDto>) {
    const updated = { ...member, ...next }
    setMembers(current => current.map(item => item.id === member.id ? updated : item))
    try {
      const response = await platformApi.updateTeamMember(member.id, {
        name: updated.name, profileKey: updated.profileKey, isActive: updated.isActive,
      })
      setMembers(current => current.map(item => item.id === member.id ? response.data : item))
      toast.success('Acesso atualizado. Sessões antigas foram encerradas.')
    } catch (error) {
      setMembers(current => current.map(item => item.id === member.id ? member : item))
      toast.error(getErrorMessage(error, 'Não foi possível atualizar o acesso.'))
    }
  }

  async function resend(member: PlatformTeamMemberDto) {
    try {
      await platformApi.resendTeamInvite(member.id)
      toast.success('Convite reenviado.')
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível reenviar o convite.'))
    }
  }

  if (loading) return <div className="flex min-h-64 items-center justify-center"><Loader2 className="h-7 w-7 animate-spin text-brand-400" /></div>

  return (
    <div className="space-y-8">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-[.18em] text-brand-400">Segurança e acessos</p>
          <h1 className="mt-2 text-3xl font-black text-white">Equipe da plataforma</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-gray-400">Convide os outros responsáveis somente quando estiverem definidos. Cada pessoa cria a própria senha e recebe apenas as áreas do perfil escolhido.</p>
        </div>
        <div className="flex items-center gap-2 rounded-xl border border-surface-500 bg-surface-800 px-4 py-3 text-sm text-gray-300">
          <Users className="h-4 w-4 text-brand-400" /> {members.length} {members.length === 1 ? 'integrante' : 'integrantes'}
        </div>
      </div>

      <section className="rounded-2xl border border-surface-500 bg-surface-800 p-6">
        <div className="flex items-center gap-3"><span className="rounded-xl bg-brand-500/10 p-2.5 text-brand-400"><MailPlus className="h-5 w-5" /></span><div><h2 className="font-bold text-white">Convidar integrante</h2><p className="text-xs text-gray-400">Nenhuma senha é compartilhada pelo painel.</p></div></div>
        <form onSubmit={invite} className="mt-6 grid gap-4 lg:grid-cols-[1fr_1fr_1fr_auto] lg:items-end">
          <label className="text-sm font-medium text-gray-300">Nome<input required maxLength={150} value={name} onChange={event => setName(event.target.value)} className="input mt-2" placeholder="Nome completo" /></label>
          <label className="text-sm font-medium text-gray-300">E-mail<input required type="email" maxLength={255} value={email} onChange={event => setEmail(event.target.value)} className="input mt-2" placeholder="pessoa@empresa.com" /></label>
          <label className="text-sm font-medium text-gray-300">Perfil<select required value={profileKey} onChange={event => setProfileKey(event.target.value)} className="input mt-2"><option value="" disabled>Selecione</option>{profiles.map(profile => <option key={profile.key} value={profile.key}>{profile.name}</option>)}</select></label>
          <button disabled={submitting} className="btn-primary h-[46px] whitespace-nowrap">{submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <MailPlus className="h-4 w-4" />} Criar convite</button>
        </form>
      </section>

      <section className="grid gap-4">
        {members.map(member => (
          <article key={member.id} className={`rounded-2xl border p-5 ${member.isActive ? 'border-surface-500 bg-surface-800' : 'border-red-500/20 bg-red-500/5'}`}>
            <div className="flex flex-col gap-5 lg:flex-row lg:items-center">
              <div className="flex min-w-0 flex-1 items-center gap-4">
                <span className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${member.isPrimaryOwner ? 'bg-amber-400/10 text-amber-300' : 'bg-brand-500/10 text-brand-400'}`}>{member.isPrimaryOwner ? <Crown className="h-5 w-5" /> : <UserRound className="h-5 w-5" />}</span>
                <div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><h3 className="truncate font-bold text-white">{member.name}</h3>{member.isPrimaryOwner && <span className="rounded-full bg-amber-400/10 px-2 py-1 text-[10px] font-bold uppercase text-amber-300">Principal</span>}{member.invitationPending && <span className="inline-flex items-center gap-1 rounded-full bg-blue-500/10 px-2 py-1 text-[10px] font-bold uppercase text-blue-300"><Clock3 className="h-3 w-3" /> Convite pendente</span>}</div><p className="mt-1 truncate text-sm text-gray-400">{member.email}</p></div>
              </div>

              {member.isPrimaryOwner ? (
                <div className="flex items-center gap-2 rounded-xl border border-amber-300/20 bg-amber-300/5 px-4 py-3 text-sm font-semibold text-amber-200"><ShieldCheck className="h-4 w-4" /> Acesso total protegido</div>
              ) : (
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
                  <select value={member.profileKey} onChange={event => updateMember(member, { profileKey: event.target.value })} className="input min-w-52">{profiles.map(profile => <option key={profile.key} value={profile.key}>{profile.name}</option>)}</select>
                  <button type="button" onClick={() => updateMember(member, { isActive: !member.isActive })} className={member.isActive ? 'btn-secondary whitespace-nowrap' : 'btn-primary whitespace-nowrap'}>{member.isActive ? 'Desativar acesso' : <><Check className="h-4 w-4" /> Reativar</>}</button>
                  {member.invitationPending && <button type="button" onClick={() => resend(member)} className="btn-secondary whitespace-nowrap"><RefreshCw className="h-4 w-4" /> Reenviar</button>}
                </div>
              )}
            </div>
          </article>
        ))}
      </section>

      <section className="rounded-2xl border border-surface-500 bg-surface-800 p-6">
        <h2 className="font-bold text-white">Perfis disponíveis</h2>
        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">{profiles.map(profile => <div key={profile.key} className="rounded-xl border border-surface-500 bg-surface-900 p-4"><p className="font-semibold text-white">{profile.name}</p><p className="mt-2 text-sm leading-6 text-gray-400">{profile.description}</p></div>)}</div>
      </section>
    </div>
  )
}
