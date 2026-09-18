'use client'
// =============================================================================
// /criar-loja/confirmar — Destino do link do e-mail de criação de loja.
//
// É aqui que a senha do admin nasce. O pedido feito no site é anônimo e não
// leva senha: com ela lá, quem digitasse o e-mail de outra pessoa escolheria a
// senha da loja que essa pessoa criaria ao clicar no link. Quem chega nesta
// página provou que lê a caixa de entrada, então é quem escolhe.
//
// Fluxo: confere se o link ainda serve (sem reservar nada) → pede a senha →
// cria a loja → manda a pessoa, já logada, para o guia inicial no subdomínio
// novo. Roda no domínio raiz; a sessão só existe depois do redeem-login no
// subdomínio, porque cookie de sessão é por host.
//
// O token é credencial de uso único: sai da barra de endereço antes de qualquer
// outra coisa, para não ficar no histórico nem ir junto num compartilhamento de
// tela. Fica só em memória.
// =============================================================================

import Link from 'next/link'
import { FormEvent, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { AlertTriangle, ArrowRight, Eye, EyeOff, Loader2 } from 'lucide-react'
import Logo from '@/components/Logo'
import { signupApi } from '@/lib/api'
import { enderecoDaLoja, urlDeEntradaNaLoja, urlDeLoginDaLoja } from '@/lib/criarLoja'

/** Mesma regra do backend (TenantSignupSenha): o BCrypt ignora o que passa de 72. */
const SENHA_MIN = 8
const SENHA_MAX = 72

type Loja = { slug: string; nomeLoja: string }

type Estado =
  | { tipo: 'verificando' }
  | { tipo: 'senha'; loja: Loja; erro?: string }
  | { tipo: 'criando'; loja: Loja }
  | { tipo: 'entrando'; slug: string }
  | { tipo: 'falha'; titulo: string; texto: string; acao: 'recomecar' | 'entrar' | 'tentar'; slug?: string }

type RespostaDeErro = {
  response?: { status?: number; data?: { errorCode?: string; message?: string; slug?: string } }
}

const CAMPO = 'mt-2 w-full rounded-xl border border-white/15 bg-white/5 px-4 py-3 pr-11 font-normal text-white outline-none placeholder:text-slate-500 focus:border-octus-400'

export default function ConfirmarLojaPage() {
  return (
    <Suspense fallback={<Moldura><Carregando titulo="Abrindo o seu link…" texto="Só um instante." /></Moldura>}>
      <Confirmacao />
    </Suspense>
  )
}

function Confirmacao() {
  const params = useSearchParams()
  const [estado, setEstado] = useState<Estado>({ tipo: 'verificando' })
  const token = useRef<string | null>(null)
  const iniciou = useRef(false)
  // "Tentar de novo" repete o que falhou: a conferência do link ou a criação.
  const tentarDeNovo = useRef<() => void>(() => {})

  const verificar = useCallback(async () => {
    tentarDeNovo.current = () => void verificar()
    if (!token.current) {
      setEstado(falhaDoToken('token_invalido'))
      return
    }
    setEstado({ tipo: 'verificando' })
    try {
      const { data } = await signupApi.verificarLink(token.current)
      setEstado({ tipo: 'senha', loja: data })
    } catch (error) {
      setEstado(traduzirFalha(error as RespostaDeErro))
    }
  }, [])

  const criar = useCallback(async (loja: Loja, senha: string) => {
    tentarDeNovo.current = () => void criar(loja, senha)
    setEstado({ tipo: 'criando', loja })
    try {
      const { data } = await signupApi.confirmar(token.current ?? '', senha)
      setEstado({ tipo: 'entrando', slug: data.slug })
      window.location.assign(urlDeEntradaNaLoja(data.slug, data.ticket))
    } catch (error) {
      const dados = (error as RespostaDeErro).response?.data
      if (dados?.errorCode === 'senha_invalida') {
        setEstado({ tipo: 'senha', loja, erro: dados.message })
        return
      }
      setEstado(traduzirFalha(error as RespostaDeErro))
    }
  }, [])

  useEffect(() => {
    if (iniciou.current) return
    iniciou.current = true
    token.current = params.get('token')
    window.history.replaceState(null, '', window.location.pathname)
    void verificar()
  }, [params, verificar])

  return (
    <Moldura>
      {estado.tipo === 'verificando' && <Carregando titulo="Abrindo o seu link…" texto="Só um instante." />}
      {estado.tipo === 'senha' && <FormularioDeSenha loja={estado.loja} erroDoServidor={estado.erro} onCriar={criar} />}
      {estado.tipo === 'criando' && (
        <Carregando titulo="Criando sua loja…" texto="Preparando o banco de dados, o painel e o seu acesso. Leva poucos segundos." />
      )}
      {estado.tipo === 'entrando' && (
        <Carregando titulo="Abrindo sua loja…" texto={`Levando você para ${enderecoDaLoja(estado.slug)}.`} />
      )}
      {estado.tipo === 'falha' && <Falha estado={estado} onTentar={() => tentarDeNovo.current()} />}
    </Moldura>
  )
}

function FormularioDeSenha({ loja, erroDoServidor, onCriar }: {
  loja: Loja
  erroDoServidor?: string
  onCriar: (loja: Loja, senha: string) => void
}) {
  const [senha, setSenha] = useState('')
  const [repeticao, setRepeticao] = useState('')
  const [mostrar, setMostrar] = useState(false)
  const [erro, setErro] = useState<string | null>(erroDoServidor ?? null)

  function enviar(event: FormEvent) {
    event.preventDefault()
    if (senha.length < SENHA_MIN || senha.length > SENHA_MAX) {
      setErro(`A senha precisa ter de ${SENHA_MIN} a ${SENHA_MAX} caracteres.`)
      return
    }
    if (senha !== repeticao) {
      setErro('As duas senhas não são iguais.')
      return
    }
    onCriar(loja, senha)
  }

  return (
    <form onSubmit={enviar} className="text-left">
      <h1 className="mt-6 text-center text-2xl font-black">Crie a senha da sua loja</h1>
      <p className="mt-3 text-center leading-7 text-slate-300">
        <strong className="text-white">{loja.nomeLoja}</strong> vai ficar em{' '}
        <strong className="text-white">{enderecoDaLoja(loja.slug)}</strong>. Você entra com o seu e-mail e esta senha.
      </p>

      <label htmlFor="confirmar-senha" className="mt-7 block text-sm font-bold">Senha</label>
      <div className="relative">
        <input
          id="confirmar-senha"
          required
          autoFocus
          minLength={SENHA_MIN}
          maxLength={SENHA_MAX}
          type={mostrar ? 'text' : 'password'}
          autoComplete="new-password"
          value={senha}
          onChange={e => { setSenha(e.target.value); setErro(null) }}
          className={CAMPO}
          placeholder={`Mínimo de ${SENHA_MIN} caracteres`}
          aria-describedby="confirmar-senha-erro"
        />
        <button
          type="button"
          onClick={() => setMostrar(v => !v)}
          aria-label={mostrar ? 'Esconder senha' : 'Mostrar senha'}
          className="absolute right-3 top-1/2 mt-1 -translate-y-1/2 text-slate-400 hover:text-white"
        >
          {mostrar ? <EyeOff size={18} /> : <Eye size={18} />}
        </button>
      </div>

      <label htmlFor="confirmar-senha-repeticao" className="mt-4 block text-sm font-bold">Repita a senha</label>
      <input
        id="confirmar-senha-repeticao"
        required
        minLength={SENHA_MIN}
        maxLength={SENHA_MAX}
        type={mostrar ? 'text' : 'password'}
        autoComplete="new-password"
        value={repeticao}
        onChange={e => { setRepeticao(e.target.value); setErro(null) }}
        className={CAMPO}
        aria-describedby="confirmar-senha-erro"
      />

      <p id="confirmar-senha-erro" role="alert" className="mt-3 min-h-5 text-sm text-red-300">{erro}</p>

      <button className="mt-2 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-octus-600 px-5 py-4 font-extrabold text-white transition hover:bg-octus-500">
        Criar minha loja <ArrowRight size={18} />
      </button>
    </form>
  )
}

function falhaDoToken(codigo: 'token_invalido' | 'expirado'): Estado {
  return codigo === 'expirado'
    ? { tipo: 'falha', titulo: 'Esse link expirou', texto: 'O link vale por 24 horas e esse já passou. Faça o cadastro de novo, leva um minuto.', acao: 'recomecar' }
    : { tipo: 'falha', titulo: 'Link inválido', texto: 'Confira se o endereço do e-mail abriu inteiro. Se não resolver, faça o cadastro de novo.', acao: 'recomecar' }
}

function traduzirFalha(error: RespostaDeErro): Estado {
  const status = error.response?.status
  const dados = error.response?.data
  switch (dados?.errorCode) {
    case 'token_invalido':
    case 'expirado':
      return falhaDoToken(dados.errorCode)
    case 'ja_confirmada':
      return {
        tipo: 'falha', titulo: 'Sua loja já está criada', acao: 'entrar', slug: dados.slug,
        texto: `Entre em ${enderecoDaLoja(dados.slug ?? '')} com o e-mail e a senha que você criou.`,
      }
    case 'em_andamento':
      return { tipo: 'falha', titulo: 'Sua loja está sendo criada', texto: 'Isso leva alguns segundos. Tente de novo em instantes.', acao: 'tentar' }
    case 'slug_indisponivel':
      return { tipo: 'falha', titulo: 'Endereço ocupado', texto: dados.message ?? 'Outra loja confirmou esse endereço antes. Faça o cadastro de novo escolhendo outro.', acao: 'recomecar' }
  }
  if (status === 429)
    return { tipo: 'falha', titulo: 'Muitas tentativas', texto: 'Aguarde um minuto e tente de novo.', acao: 'tentar' }
  return {
    tipo: 'falha', titulo: 'Não conseguimos criar sua loja agora', acao: 'tentar',
    texto: dados?.message ?? 'Nada foi cobrado e seu cadastro continua guardado. Tente de novo em alguns minutos.',
  }
}

function Moldura({ children }: { children: React.ReactNode }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-[#071f3d] px-5 py-16 text-white">
      <div className="w-full max-w-md rounded-2xl border border-white/10 bg-white/5 p-8 text-center">
        <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-white">
          <Logo className="h-9 w-9" title="Octus" />
        </span>
        {children}
      </div>
    </main>
  )
}

function Carregando({ titulo, texto }: { titulo: string; texto: string }) {
  return (
    <>
      <Loader2 size={36} className="mx-auto mt-6 animate-spin text-octus-400" />
      <h1 className="mt-4 text-2xl font-black">{titulo}</h1>
      <p className="mt-3 leading-7 text-slate-300">{texto}</p>
    </>
  )
}

function Falha({ estado, onTentar }: { estado: Extract<Estado, { tipo: 'falha' }>; onTentar: () => void }) {
  return (
    <>
      <AlertTriangle size={36} className="mx-auto mt-6 text-amber-400" />
      <h1 className="mt-4 text-2xl font-black">{estado.titulo}</h1>
      <p className="mt-3 leading-7 text-slate-300">{estado.texto}</p>
      <div className="mt-7">
        {estado.acao === 'tentar' && (
          <button type="button" onClick={onTentar} className="rounded-xl bg-octus-600 px-5 py-3 font-extrabold hover:bg-octus-500">
            Tentar de novo
          </button>
        )}
        {estado.acao === 'entrar' && estado.slug && (
          <a href={urlDeLoginDaLoja(estado.slug)} className="inline-block rounded-xl bg-octus-600 px-5 py-3 font-extrabold hover:bg-octus-500">
            Entrar na minha loja
          </a>
        )}
        {estado.acao === 'recomecar' && (
          <Link href="/#contato" className="inline-block rounded-xl bg-octus-600 px-5 py-3 font-extrabold hover:bg-octus-500">
            Fazer o cadastro de novo
          </Link>
        )}
      </div>
    </>
  )
}
