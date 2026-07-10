import type { Metadata } from 'next'
import Link from 'next/link'
import {
  Store, ShieldCheck, Layers, Smartphone, Receipt, Sparkles, ArrowRight, CheckCircle2,
} from 'lucide-react'

export const metadata: Metadata = {
  title: '2Esysten — ERP completo para lojas e varejo',
  description:
    'Plataforma de gestão white-label para lojistas: PDV, estoque, fiscal, crediário e app próprio — tudo em um só sistema.',
}

const PILARES = [
  {
    icon: Store,
    title: 'Sua loja, sua identidade',
    desc: 'Cada cliente recebe um espaço próprio, com subdomínio, cores e logo dele — como se fosse um sistema exclusivo, feito sob medida.',
  },
  {
    icon: Receipt,
    title: 'Fiscal sem dor de cabeça',
    desc: 'Emissão de NFC-e, controle de impostos e integração com o contador rodando por baixo, sem o lojista precisar entender de SEFAZ.',
  },
  {
    icon: Layers,
    title: 'Tudo em um só lugar',
    desc: 'PDV, estoque, crediário, financeiro e relatórios conversando entre si — sem planilha solta, sem sistema remendado.',
  },
  {
    icon: Smartphone,
    title: 'App próprio, sem custo de loja',
    desc: 'Instala na tela inicial do celular do cliente como um app de verdade, com a marca do lojista — sem passar pela Apple Store ou Google Play.',
  },
  {
    icon: ShieldCheck,
    title: 'Dados isolados e seguros',
    desc: 'Cada loja opera em um espaço isolado no banco de dados — o que é de um cliente nunca se mistura com o de outro.',
  },
  {
    icon: Sparkles,
    title: 'Feito pra crescer junto',
    desc: 'Arquitetura pensada para atender de uma loja só a uma rede inteira, sem trocar de sistema no meio do caminho.',
  },
]

export default function InstitucionalPage() {
  return (
    <main className="min-h-screen bg-[#0C1220] text-white">
      {/* ── Hero ─────────────────────────────────────────────────────────── */}
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-gradient-to-br from-brand-700/30 via-transparent to-transparent" />
        <div className="relative mx-auto max-w-6xl px-6 py-24 sm:py-32">
          <span className="inline-flex items-center gap-2 rounded-full border border-brand-400/30 bg-brand-500/10 px-4 py-1.5 text-sm font-semibold text-brand-300">
            <Sparkles size={16} /> Plataforma de gestão para lojistas
          </span>
          <h1 className="mt-6 max-w-3xl text-4xl font-extrabold leading-tight sm:text-6xl">
            O ERP que veste a{' '}
            <span className="bg-gradient-to-r from-brand-400 to-brand-200 bg-clip-text text-transparent">
              cara da sua loja
            </span>
          </h1>
          <p className="mt-6 max-w-2xl text-lg text-white/70">
            Somos a 2Esysten: construímos um sistema de gestão completo — PDV, estoque, fiscal,
            crediário e app próprio — que cada lojista pode chamar de seu, sem abrir mão da
            praticidade de uma plataforma pronta.
          </p>
          <div className="mt-10 flex flex-wrap gap-4">
            <a
              href="#contato"
              className="inline-flex items-center gap-2 rounded-xl bg-brand-500 px-6 py-3 font-semibold text-[#0C1220] transition hover:bg-brand-400"
            >
              Quero minha loja no sistema <ArrowRight size={18} />
            </a>
            <a
              href="#clientes"
              className="inline-flex items-center gap-2 rounded-xl border border-white/20 px-6 py-3 font-semibold text-white/90 transition hover:bg-white/5"
            >
              Ver quem já usa
            </a>
          </div>
        </div>
      </section>

      {/* ── Quem somos ───────────────────────────────────────────────────── */}
      <section className="mx-auto max-w-6xl px-6 py-20">
        <div className="grid gap-12 md:grid-cols-2 md:items-center">
          <div>
            <h2 className="text-sm font-bold uppercase tracking-widest text-brand-400">
              Quem somos
            </h2>
            <p className="mt-4 text-2xl font-bold leading-snug sm:text-3xl">
              Nascemos dentro de uma loja de verdade, resolvendo problema de verdade.
            </p>
          </div>
          <div className="space-y-4 text-white/70">
            <p>
              A 2Esysten começou como o sistema interno de uma loja de card games, construído pra
              resolver o dia a dia de quem vende, emite nota, controla estoque e fecha caixa —
              tudo ao mesmo tempo.
            </p>
            <p>
              Percebemos que o problema não era só nosso: todo lojista de médio porte lida com o
              mesmo emaranhado de planilha, sistema fiscal separado e falta de identidade digital
              própria. Transformamos aquele sistema interno em uma plataforma multi-loja, onde
              cada cliente ganha o próprio espaço — isolado, com a cara dele, sem perder a
              praticidade de uma solução pronta.
            </p>
          </div>
        </div>
      </section>

      {/* ── Pilares / o que fazemos ─────────────────────────────────────── */}
      <section className="border-y border-white/10 bg-white/[0.02] py-20">
        <div className="mx-auto max-w-6xl px-6">
          <h2 className="text-sm font-bold uppercase tracking-widest text-brand-400">
            O que fazemos
          </h2>
          <p className="mt-3 max-w-2xl text-2xl font-bold sm:text-3xl">
            Um sistema só, cobrindo o que hoje toma cinco ferramentas diferentes.
          </p>

          <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {PILARES.map(({ icon: Icon, title, desc }) => (
              <div
                key={title}
                className="rounded-2xl border border-white/10 bg-white/[0.03] p-6 transition hover:border-brand-400/40 hover:bg-white/[0.05]"
              >
                <div className="mb-4 inline-flex rounded-xl bg-brand-500/15 p-3 text-brand-300">
                  <Icon size={22} />
                </div>
                <h3 className="font-bold">{title}</h3>
                <p className="mt-2 text-sm text-white/60">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── Clientes ─────────────────────────────────────────────────────── */}
      <section id="clientes" className="mx-auto max-w-6xl px-6 py-20">
        <h2 className="text-sm font-bold uppercase tracking-widest text-brand-400">
          Quem já usa
        </h2>
        <p className="mt-3 max-w-2xl text-2xl font-bold sm:text-3xl">
          Primeira loja rodando, muitas outras vindo por aí.
        </p>

        <div className="mt-10 grid gap-6 sm:grid-cols-2">
          <div className="rounded-2xl border border-white/10 bg-gradient-to-br from-brand-500/10 to-transparent p-8">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-500/20 font-bold text-brand-300">
                SN
              </div>
              <div>
                <p className="font-bold">Santuário Nerd</p>
                <p className="text-sm text-white/50">Loja de card games</p>
              </div>
            </div>
            <p className="mt-4 text-sm text-white/70">
              Primeira loja a rodar a plataforma — PDV, comandas, crediário e emissão fiscal no
              dia a dia, com a marca própria do Santuário do início ao fim.
            </p>
          </div>

          <div className="flex flex-col items-start justify-center rounded-2xl border border-dashed border-white/15 p-8 text-white/50">
            <CheckCircle2 className="mb-3 text-brand-400" size={24} />
            <p className="font-semibold text-white/80">A próxima pode ser a sua loja</p>
            <p className="mt-2 text-sm">
              Estamos abrindo espaço para novos lojistas conforme a plataforma cresce.
            </p>
          </div>
        </div>
      </section>

      {/* ── CTA final ────────────────────────────────────────────────────── */}
      <section id="contato" className="border-t border-white/10 bg-white/[0.02] py-20">
        <div className="mx-auto max-w-3xl px-6 text-center">
          <h2 className="text-3xl font-extrabold sm:text-4xl">
            Quer sua loja rodando com a sua cara?
          </h2>
          <p className="mt-4 text-white/70">
            Fala com a gente e a gente monta seu espaço na plataforma — subdomínio, identidade
            visual e módulo fiscal configurados pra você vender no mesmo dia.
          </p>
          <div className="mt-8 flex flex-wrap justify-center gap-4">
            <Link
              href="/cadastro"
              className="inline-flex items-center gap-2 rounded-xl bg-brand-500 px-6 py-3 font-semibold text-[#0C1220] transition hover:bg-brand-400"
            >
              Falar com a gente <ArrowRight size={18} />
            </Link>
          </div>
        </div>
      </section>
    </main>
  )
}
