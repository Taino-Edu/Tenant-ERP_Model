'use client'
// =============================================================================
// CriarLojaForm.tsx — "Começar meu teste grátis" que cria a loja de verdade.
//
// Substitui o formulário de lead da landing. Aquele botão prometia um teste e
// entregava "a equipe vai falar com você em breve": a loja dependia de alguém
// provisionar à mão. Agora a pessoa escolhe o endereço, recebe um link no
// e-mail e cai logada na loja nova — sem ninguém do nosso lado.
//
// Por que confirmar e-mail antes de criar: ver TenantSignupService. Em resumo,
// sem essa etapa qualquer script criaria schemas no banco de todo mundo.
// =============================================================================

import Link from 'next/link'
import { FormEvent, useEffect, useRef, useState } from 'react'
import { ArrowRight, Check, Eye, EyeOff, Loader2, MailCheck, X } from 'lucide-react'
import { signupApi } from '@/lib/api'
import { dominioDasLojas, enderecoDaLoja, PLANO_ESCOLHIDO_EVENT, SLUG_MAX, slugDaLoja, slugValido } from '@/lib/criarLoja'
import { PRIVACY_NOTICE_VERSION, publicFormErrorMessage } from '@/lib/institucional'
import { trackMarketingEvent } from '@/lib/marketing'
import { acharPlano, formatarReais, PLANOS } from '@/lib/planos'

type EstadoDoEndereco =
  | { tipo: 'vazio' }
  | { tipo: 'verificando' }
  | { tipo: 'livre' }
  | { tipo: 'ocupado'; motivo: string; sugestao?: string | null }
  | { tipo: 'sem-resposta' }

type RespostaDeErro = {
  response?: { status?: number; data?: { errorCode?: string; message?: string; sugestao?: string | null } }
}

const CAMPO = 'mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400'

/** O plano em destaque no site — o mesmo que o backend assume sem plano
 *  (TenantSignupService.PlanoPadrao). Mandado sempre explícito, para a loja
 *  nascer no plano que a pessoa viu marcado, e não num padrão invisível. */
const PLANO_PADRAO = PLANOS.find(p => p.destaque)?.nome ?? 'Rio'

export default function CriarLojaForm() {
  const [nome, setNome] = useState('')
  const [email, setEmail] = useState('')
  const [nomeLoja, setNomeLoja] = useState('')
  const [slug, setSlug] = useState('')
  const [slugEditadoAMao, setSlugEditadoAMao] = useState(false)
  const [senha, setSenha] = useState('')
  const [mostrarSenha, setMostrarSenha] = useState(false)
  const [whatsApp, setWhatsApp] = useState('')
  const [ciente, setCiente] = useState(false)
  const [plano, setPlano] = useState(PLANO_PADRAO)
  const [endereco, setEndereco] = useState<EstadoDoEndereco>({ tipo: 'vazio' })
  const [enviando, setEnviando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [enviadoPara, setEnviadoPara] = useState<string | null>(null)
  const ultimaConsulta = useRef(0)

  useEffect(() => {
    // Duas portas para a escolha do card de preços: ?plano= na URL (link
    // compartilhado, F5) e o evento, quando a pessoa clica no card na mesma página.
    const daUrl = acharPlano(new URLSearchParams(window.location.search).get('plano'))
    if (daUrl) setPlano(daUrl.nome)

    const aoEscolher = (event: Event) => {
      const escolhido = acharPlano((event as CustomEvent<string>).detail)
      if (escolhido) setPlano(escolhido.nome)
    }
    window.addEventListener(PLANO_ESCOLHIDO_EVENT, aoEscolher)
    return () => window.removeEventListener(PLANO_ESCOLHIDO_EVENT, aoEscolher)
  }, [])

  useEffect(() => {
    if (!slug) {
      setEndereco({ tipo: 'vazio' })
      return
    }
    if (!slugValido(slug)) {
      setEndereco({ tipo: 'ocupado', motivo: 'Use letras minúsculas, números e hífen, sem hífen no começo ou no fim.' })
      return
    }

    const consulta = ++ultimaConsulta.current
    setEndereco({ tipo: 'verificando' })
    const espera = window.setTimeout(async () => {
      try {
        const { data } = await signupApi.verificarSlug(slug)
        if (consulta !== ultimaConsulta.current) return
        setEndereco(data.disponivel
          ? { tipo: 'livre' }
          : { tipo: 'ocupado', motivo: data.motivo ?? 'Esse endereço não está disponível.', sugestao: data.sugestao })
      } catch {
        // Consulta de conforto: se falhar, o servidor confere de novo ao criar.
        if (consulta === ultimaConsulta.current) setEndereco({ tipo: 'sem-resposta' })
      }
    }, 400)
    return () => window.clearTimeout(espera)
  }, [slug])

  function alterarNomeDaLoja(valor: string) {
    setNomeLoja(valor)
    if (!slugEditadoAMao) setSlug(slugDaLoja(valor))
  }

  function alterarEndereco(valor: string) {
    setSlugEditadoAMao(true)
    setSlug(valor.toLowerCase().replace(/[^a-z0-9-]/g, '').slice(0, SLUG_MAX))
  }

  async function criar(event: FormEvent) {
    event.preventDefault()
    if (endereco.tipo === 'ocupado') {
      setErro(endereco.motivo)
      return
    }

    setEnviando(true)
    setErro(null)
    try {
      await signupApi.solicitar({
        nomeResponsavel: nome.trim(),
        email: email.trim(),
        nomeLoja: nomeLoja.trim(),
        slug,
        senha,
        whatsApp: whatsApp.trim() || undefined,
        plano,
        privacyNoticeAcknowledged: ciente,
        privacyNoticeVersion: PRIVACY_NOTICE_VERSION,
      })
      setEnviadoPara(email.trim())
      // Mesmo nome de evento do formulário antigo: é ele que o GTM conta como
      // conversão. lead_kind separa quem já criou loja de quem só deixou contato.
      trackMarketingEvent('lead_submit', { form: 'institucional', lead_kind: 'self_service' })
    } catch (error) {
      const resposta = (error as RespostaDeErro).response
      if (resposta?.data?.errorCode === 'slug_indisponivel') {
        setEndereco({
          tipo: 'ocupado',
          motivo: resposta.data.message ?? 'Esse endereço acabou de ser escolhido por outra loja.',
          sugestao: resposta.data.sugestao,
        })
        setErro('Escolha outro endereço para a sua loja.')
      } else if (resposta?.status === 503 && resposta.data?.message) {
        setErro(resposta.data.message)
      } else {
        setErro(publicFormErrorMessage(error, 'Não foi possível criar sua loja agora. Tente de novo em instantes.'))
      }
    } finally {
      setEnviando(false)
    }
  }

  if (enviadoPara) {
    return (
      <div className="flex min-h-80 flex-col items-center justify-center text-center">
        <MailCheck size={42} className="text-emerald-400" />
        <h3 className="mt-5 text-2xl font-black">Confirme seu e-mail</h3>
        <p className="mt-3 max-w-md leading-7 text-slate-300">
          Enviamos um link para <strong className="text-white">{enviadoPara}</strong>. Clique nele e a loja{' '}
          <strong className="text-white">{enderecoDaLoja(slug)}</strong> fica pronta na hora.
        </p>
        <p className="mt-4 text-sm text-slate-400">O link vale por 24 horas. Não chegou? Confira o spam e a aba Promoções.</p>
        <button type="button" onClick={() => setEnviadoPara(null)} className="mt-6 text-sm font-bold text-octus-300 underline">
          Corrigir o e-mail ou os dados
        </button>
      </div>
    )
  }

  return (
    <form onSubmit={criar} className="grid gap-4 sm:grid-cols-2">
      <fieldset className="sm:col-span-2">
        <legend className="text-sm font-bold">Plano para testar</legend>
        <div role="radiogroup" aria-label="Plano para testar" className="mt-2 grid grid-cols-3 gap-2">
          {PLANOS.map(opcao => {
            const marcado = plano === opcao.nome
            return (
              <button
                key={opcao.nome}
                type="button"
                role="radio"
                aria-checked={marcado}
                onClick={() => setPlano(opcao.nome)}
                className={`rounded-xl border px-3 py-2.5 text-left transition ${marcado ? 'border-octus-400 bg-octus-600/25' : 'border-white/15 bg-white/5 hover:border-white/30'}`}
              >
                <span className="block text-sm font-extrabold text-white">{opcao.nome}</span>
                <span className="block text-xs text-slate-400">{formatarReais(opcao.preco)}/mês</span>
              </button>
            )
          })}
        </div>
      </fieldset>

      <label className="text-sm font-bold">
        Seu nome
        <input required maxLength={150} autoComplete="name" value={nome} onChange={e => setNome(e.target.value)} className={CAMPO} placeholder="Como podemos te chamar?" />
      </label>
      <label className="text-sm font-bold">
        E-mail
        <input required type="email" maxLength={255} autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} className={CAMPO} placeholder="voce@empresa.com.br" />
      </label>

      <label className="text-sm font-bold sm:col-span-2">
        Nome da loja
        <input required maxLength={80} value={nomeLoja} onChange={e => alterarNomeDaLoja(e.target.value)} className={CAMPO} placeholder="Ex.: Empório da Ana" />
      </label>

      <div className="sm:col-span-2">
        <label htmlFor="criar-loja-endereco" className="text-sm font-bold">Endereço da loja</label>
        <div className="mt-2 flex items-stretch overflow-hidden rounded-xl border border-white/15 bg-white/5 focus-within:border-octus-400">
          <input
            id="criar-loja-endereco"
            required
            maxLength={SLUG_MAX}
            value={slug}
            onChange={e => alterarEndereco(e.target.value)}
            className="min-w-0 flex-1 bg-transparent px-4 py-3 text-white outline-none placeholder:text-slate-500"
            placeholder="sua-loja"
            aria-describedby="criar-loja-endereco-status"
            autoCapitalize="none"
            spellCheck={false}
          />
          <span className="flex items-center border-l border-white/10 px-3 text-sm text-slate-400">.{dominioDasLojas()}</span>
        </div>
        <p id="criar-loja-endereco-status" aria-live="polite" className="mt-2 min-h-5 text-xs">
          {endereco.tipo === 'verificando' && <span className="inline-flex items-center gap-1.5 text-slate-400"><Loader2 size={13} className="animate-spin" />Verificando…</span>}
          {endereco.tipo === 'livre' && <span className="inline-flex items-center gap-1.5 text-emerald-400"><Check size={13} />{enderecoDaLoja(slug)} está livre</span>}
          {endereco.tipo === 'sem-resposta' && <span className="text-slate-400">Não deu para verificar agora. A gente confere de novo ao criar.</span>}
          {endereco.tipo === 'ocupado' && (
            <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1 text-red-300">
              <span className="inline-flex items-center gap-1.5"><X size={13} />{endereco.motivo}</span>
              {endereco.sugestao && (
                <button type="button" onClick={() => { setSlugEditadoAMao(true); setSlug(endereco.sugestao!) }} className="font-bold text-octus-300 underline">
                  Usar {endereco.sugestao}
                </button>
              )}
            </span>
          )}
        </p>
      </div>

      <div>
        <label htmlFor="criar-loja-senha" className="text-sm font-bold">Senha</label>
        <div className="relative">
          <input
            id="criar-loja-senha"
            required
            minLength={8}
            maxLength={72}
            type={mostrarSenha ? 'text' : 'password'}
            autoComplete="new-password"
            value={senha}
            onChange={e => setSenha(e.target.value)}
            className={`${CAMPO} pr-11`}
            placeholder="Mínimo de 8 caracteres"
          />
          <button
            type="button"
            onClick={() => setMostrarSenha(v => !v)}
            aria-label={mostrarSenha ? 'Esconder senha' : 'Mostrar senha'}
            className="absolute right-3 top-1/2 mt-1 -translate-y-1/2 text-slate-400 hover:text-white"
          >
            {mostrarSenha ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        </div>
      </div>
      <label className="text-sm font-bold">
        WhatsApp <span className="font-normal text-slate-400">(opcional)</span>
        <input maxLength={30} autoComplete="tel" value={whatsApp} onChange={e => setWhatsApp(e.target.value)} className={CAMPO} placeholder="(17) 99999-9999" />
      </label>

      <label className="flex items-start gap-3 text-xs leading-relaxed text-slate-300 sm:col-span-2">
        <input required type="checkbox" checked={ciente} onChange={e => setCiente(e.target.checked)} className="mt-0.5 h-4 w-4 accent-octus-500" />
        <span>
          Li e estou ciente da <Link href="/privacidade" target="_blank" className="font-bold text-octus-300 underline">Política de Privacidade</Link>,
          inclusive sobre o uso dos dados para criar e manter a minha loja. Esta ciência não autoriza marketing opcional.
        </span>
      </label>

      {erro && <p role="alert" className="text-sm text-red-300 sm:col-span-2">{erro}</p>}

      <button disabled={enviando} className="inline-flex items-center justify-center gap-2 rounded-xl bg-octus-600 px-5 py-4 font-extrabold text-white transition hover:bg-octus-500 disabled:opacity-60 sm:col-span-2">
        {enviando ? <><Loader2 size={18} className="animate-spin" />Criando…</> : <>Criar minha loja grátis <ArrowRight size={18} /></>}
      </button>
      <p className="text-center text-xs text-slate-400 sm:col-span-2">
        15 dias grátis{plano ? ` no plano ${plano}` : ''}, sem cartão. Depois, você decide se continua.
      </p>
    </form>
  )
}
