import type { Metadata } from 'next'
import Link from 'next/link'
import CookieSettingsButton from '@/components/CookieSettingsButton'

export const metadata: Metadata = {
  title: 'Política de Cookies',
  description: 'Entenda quais tecnologias de armazenamento a plataforma Octus utiliza e gerencie suas preferências.',
  alternates: { canonical: '/cookies' },
}

const categories = [
  ['Necessários', 'Sempre ativos', 'Sessão e autenticação, segurança, prevenção a fraude, balanceamento da aplicação e registro da sua decisão de privacidade. Sem eles, o serviço solicitado não funciona corretamente.'],
  ['Preferências locais', 'Sempre ativos quando solicitados', 'Tema claro ou escuro, instalação do aplicativo e configurações escolhidas no dispositivo. Não são usados para publicidade.'],
  ['Análise e desempenho', 'Somente com autorização', 'Eventos de navegação e desempenho que ajudam a localizar falhas e priorizar melhorias. A recusa não limita o uso do sistema.'],
  ['Marketing', 'Somente com autorização', 'Medição de campanhas ou publicidade, caso essas ferramentas sejam configuradas. A recusa não limita o uso do sistema.'],
]

export default function CookiesPage() {
  return (
    <main className="min-h-screen bg-[#f7fbfd] text-[#22384A]">
      <header className="bg-[#0C3D5A] px-4 py-5 text-white"><div className="mx-auto flex max-w-4xl items-center justify-between"><Link href="/" className="text-xl font-black">Octus</Link><Link href="/privacidade" className="text-sm underline">Privacidade</Link></div></header>
      <article className="mx-auto max-w-4xl px-4 py-12">
        <p className="text-xs font-bold uppercase tracking-widest text-brand-700">Transparência e controle</p>
        <h1 className="mt-2 text-3xl font-black text-[#0C3D5A]">Política de Cookies</h1>
        <p className="mt-2 text-sm text-[#6B8598]">Versão 2.0 · atualizada em 11 de agosto de 2026</p>

        <section className="mt-8 space-y-3 text-sm leading-relaxed">
          <p>Cookies e tecnologias semelhantes são pequenos registros usados pelo navegador para manter uma sessão, lembrar escolhas e, quando você permitir, medir o uso da aplicação. O Octus aplica a sua decisão neste navegador e neste domínio.</p>
          <p>Cookies estritamente necessários independem de autorização porque viabilizam o serviço solicitado. Categorias opcionais permanecem desligadas até uma escolha positiva e podem ser recusadas ou revogadas a qualquer momento.</p>
        </section>

        <section className="mt-8 grid gap-3">
          {categories.map(([title, status, description]) => (
            <div key={title} className="rounded-2xl border border-[#0C3D5A]/10 bg-white p-5">
              <div className="flex flex-wrap items-center justify-between gap-2"><h2 className="font-bold text-[#0C3D5A]">{title}</h2><span className="rounded-full bg-brand-50 px-2.5 py-1 text-[10px] font-bold uppercase text-brand-700">{status}</span></div>
              <p className="mt-2 text-sm text-[#526E80]">{description}</p>
            </div>
          ))}
        </section>

        <section className="mt-8 rounded-2xl bg-[#0C3D5A] p-6 text-white">
          <h2 className="font-bold">Gerencie sua escolha</h2>
          <p className="mt-1 text-sm text-white/75">Você pode aceitar, recusar ou alterar categorias opcionais. Revogar não afeta o tratamento realizado licitamente antes da mudança.</p>
          <div className="mt-4"><CookieSettingsButton /></div>
        </section>

        <section className="mt-8 space-y-3 text-sm leading-relaxed">
          <h2 className="text-lg font-bold text-[#0C3D5A]">Armazenamento e terceiros</h2>
          <p>A decisão fica salva no armazenamento local até a política mudar, você apagar os dados do navegador ou renovar sua escolha. Cookies de sessão expiram conforme a configuração de segurança da conta.</p>
          <p>Serviços externos só podem ser carregados quando necessários ao recurso solicitado ou quando houver a autorização correspondente. Consulte também a <Link href="/privacidade" className="font-semibold text-brand-700 underline">Política de Privacidade</Link> e use o <Link href="/lgpd" className="font-semibold text-brand-700 underline">portal de direitos LGPD</Link>.</p>
        </section>
      </article>
    </main>
  )
}
