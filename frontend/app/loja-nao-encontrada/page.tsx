'use client'
import { SearchX } from 'lucide-react'

export default function LojaNaoEncontradaPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-900 px-6">
      <div className="max-w-md w-full text-center">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-surface-700 border border-surface-500 flex items-center justify-center mb-6">
          <SearchX className="w-8 h-8 text-gray-400" />
        </div>
        <h1 className="text-xl font-bold text-white mb-2">Loja não encontrada</h1>
        <p className="text-gray-400 text-sm">
          O endereço acessado não corresponde a nenhuma loja ativa. Confira se o
          subdomínio está correto ou entre em contato com a loja.
        </p>
        {/* Saída obrigatória: o SiteConfigContext chega aqui via
            window.location.href, então a URL fica na barra do visitante. Sem
            este botão, uma indisponibilidade de segundos deixava a loja
            "fechada" pra sempre pra quem foi redirecionado — recarregar e
            voltar caem nesta mesma tela estática, que não checa nada.

            <a> e não <Link>: só a navegação com reload remonta o
            SiteConfigProvider (ele vive no layout raiz e busca a config uma vez,
            na montagem). Com navegação client-side a config não seria
            reconsultada e a home renderizaria com os defaults genéricos.

            É exatamente o caso que a regra no-html-link-for-pages não prevê:
            ela existe pra ninguém perder o roteamento client-side por descuido,
            e aqui perdê-lo é o objetivo. Trocar por <Link> devolveria o bug. */}
        {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
        <a
          href="/"
          className="mt-6 inline-block rounded-xl border border-surface-500 bg-surface-700 px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-surface-500"
        >
          Tentar de novo
        </a>
      </div>
    </div>
  )
}
