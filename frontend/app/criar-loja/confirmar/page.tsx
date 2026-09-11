'use client'
// =============================================================================
// /criar-loja/confirmar — Destino do link do e-mail de criação de loja.
//
// Troca o token pela loja criada e manda a pessoa, já logada, para o guia
// inicial no subdomínio novo. Roda no domínio raiz; a sessão só existe depois do
// redeem-login no subdomínio, porque cookie de sessão é por host.
//
// O token é credencial de uso único: sai da barra de endereço antes de qualquer
// outra coisa, para não ficar no histórico nem ir junto num compartilhamento de
// tela. Fica só em memória, para o botão "tentar de novo".
// =============================================================================

import Link from 'next/link'
import { Suspense, useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { AlertTriangle, Loader2 } from 'lucide-react'
import Logo from '@/components/Logo'
import { signupApi } from '@/lib/api'
import { enderecoDaLoja, urlDeEntradaNaLoja, urlDeLoginDaLoja } from '@/lib/criarLoja'

type Estado =
  | { tipo: 'criando' }
  | { tipo: 'entrando'; slug: string }
  | { tipo: 'falha'; titulo: string; texto: string; acao: 'recomecar' | 'entrar' | 'tentar'; slug?: string }

type RespostaDeErro = {
  response?: { status?: number; data?: { errorCode?: string; message?: string; slug?: string } }
}

export default function ConfirmarLojaPage() {
  return (
    <Suspense fallback={<Tela estado={{ tipo: 'criando' }} />}>
      <Confirmacao />
    </Suspense>
  )
}

function Confirmacao() {
  const params = useSearchParams()
  const [estado, setEstado] = useState<Estado>({ tipo: 'criando' })
  const token = useRef<string | null>(null)
  const iniciou = useRef(false)

  const confirmar = useCallback(async () => {
    if (!token.current) {
      setEstado(falhaDoToken('token_invalido'))
      return
    }
    setEstado({ tipo: 'criando' })
    try {
      const { data } = await signupApi.confirmar(token.current)
      setEstado({ tipo: 'entrando', slug: data.slug })
      window.location.assign(urlDeEntradaNaLoja(data.slug, data.ticket))
    } catch (error) {
      setEstado(traduzirFalha(error as RespostaDeErro))
    }
  }, [])

  useEffect(() => {
    if (iniciou.current) return
    iniciou.current = true
    token.current = params.get('token')
    window.history.replaceState(null, '', window.location.pathname)
    void confirmar()
  }, [params, confirmar])

  return <Tela estado={estado} onTentar={confirmar} />
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
        texto: `Entre em ${enderecoDaLoja(dados.slug ?? '')} com o e-mail e a senha que você cadastrou.`,
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

function Tela({ estado, onTentar }: { estado: Estado; onTentar?: () => void }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-[#071f3d] px-5 py-16 text-white">
      <div className="w-full max-w-md rounded-2xl border border-white/10 bg-white/5 p-8 text-center">
        <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-white">
          <Logo className="h-9 w-9" title="Octus" />
        </span>

        {estado.tipo === 'falha' ? (
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
        ) : (
          <>
            <Loader2 size={36} className="mx-auto mt-6 animate-spin text-octus-400" />
            <h1 className="mt-4 text-2xl font-black">
              {estado.tipo === 'entrando' ? 'Abrindo sua loja…' : 'Criando sua loja…'}
            </h1>
            <p className="mt-3 leading-7 text-slate-300">
              {estado.tipo === 'entrando'
                ? `Levando você para ${enderecoDaLoja(estado.slug)}.`
                : 'Preparando o banco de dados, o painel e o seu acesso. Leva poucos segundos.'}
            </p>
          </>
        )}
      </div>
    </main>
  )
}
