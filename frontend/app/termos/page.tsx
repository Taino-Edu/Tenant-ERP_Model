// =============================================================================
// termos/page.tsx â€” Termos de Uso da SantuÃ¡rio Nerd
// SÃ£o JosÃ© do Rio Preto, SP â€” Foro: Comarca de SÃ£o JosÃ© do Rio Preto
// =============================================================================

import Link from 'next/link'
import ThemeToggle from '@/components/ThemeToggle'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Termos de Uso',
  description: 'Termos e condiÃ§Ãµes de uso dos serviÃ§os da SantuÃ¡rio Nerd, loja de card games em SÃ£o JosÃ© do Rio Preto, SP.',
}

export default function TermosPage() {
  return (
    <div className="min-h-screen" style={{ backgroundColor: "var(--bg-primary)", color: "var(--text-primary)" }}>
      {/* CabeÃ§alho */}
      <header className="bg-[#1a0a2e] text-white py-6 px-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-2xl font-bold">
            <span className="text-[#42B6EE]">SantuÃ¡rio</span>
            <span> Nerd</span>
          </Link>
          <div className="flex items-center gap-4"><span className="text-sm text-gray-400 hidden sm:block">SÃ£o JosÃ© do Rio Preto, SP</span><ThemeToggle compact /></div>
        </div>
      </header>

      <main className="max-w-3xl mx-auto px-4 py-12">
        {/* TÃ­tulo */}
        <div className="mb-10 pb-6" style={{ borderBottom: "1px solid var(--border-color)" }}>
          <h1 className="text-3xl font-bold mb-2">Termos de Uso</h1>
          <p className="text-sm" style={{ color: "var(--text-faint)" }}>
            Ãšltima atualizaÃ§Ã£o: <strong>Maio de 2026</strong> â€” VersÃ£o 1.0
          </p>
          <p className="mt-3 leading-relaxed" style={{ color: "var(--text-muted)" }}>
            Ao utilizar o sistema digital da <strong>SantuÃ¡rio Nerd</strong>, vocÃª concorda com os presentes
            Termos de Uso. Leia com atenÃ§Ã£o antes de prosseguir.
          </p>
        </div>

        <div className="space-y-10 leading-relaxed" style={{ color: "var(--text-muted)" }}>

          {/* 1. ServiÃ§os */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>1. ServiÃ§os Oferecidos</h2>
            <p>
              A <strong>SantuÃ¡rio Nerd</strong> Ã© uma loja fÃ­sica de card games localizada em SÃ£o JosÃ© do Rio Preto, SP.
              Nosso sistema digital oferece:
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>Abertura e gestÃ£o de comandas na loja</li>
              <li>Programa de pontos e fidelidade</li>
              <li>CrediÃ¡rio para clientes cadastrados</li>
              <li>InscriÃ§Ã£o e acompanhamento de campeonatos</li>
              <li>Busca de cartas TCG (PokÃ©mon, Magic: The Gathering, Yu-Gi-Oh!)</li>
              <li>Assistente de atendimento baseado em inteligÃªncia artificial</li>
            </ul>
          </section>

          {/* 2. Cadastro */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>2. Cadastro e Conta</h2>
            <p>
              O cadastro Ã© realizado presencialmente na loja via QR Code ou pelo administrador do sistema.
              Ao se cadastrar, vocÃª declara que as informaÃ§Ãµes fornecidas (nome, CPF, WhatsApp) sÃ£o verÃ­dicas.
            </p>
            <p className="mt-2">
              O uso indevido do sistema â€” incluindo fornecimento de dados falsos, tentativas de acesso
              nÃ£o autorizado ou abuso do programa de pontos â€” pode resultar no cancelamento da conta
              sem aviso prÃ©vio.
            </p>
            <p className="mt-2">
              VocÃª Ã© responsÃ¡vel pela confidencialidade de suas credenciais de acesso. Em caso de
              suspeita de uso nÃ£o autorizado, entre em contato imediatamente.
            </p>
          </section>

          {/* 3. CrediÃ¡rio */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>3. CrediÃ¡rio</h2>
            <p>
              O crediÃ¡rio Ã© uma linha de crÃ©dito oferecida pela SantuÃ¡rio Nerd a clientes cadastrados, sujeita
              Ã  anÃ¡lise e aprovaÃ§Ã£o.
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>O prazo padrÃ£o de pagamento Ã© de <strong>30 dias corridos</strong> a partir da abertura</li>
              <li>Somente um crediÃ¡rio pode estar aberto por cliente por vez</li>
              <li>CrediÃ¡rios em aberto bloqueiam a abertura de novas comandas</li>
              <li>O nÃ£o pagamento no prazo pode implicar restriÃ§Ã£o de acesso aos serviÃ§os da loja</li>
              <li>A SantuÃ¡rio Nerd reserva-se o direito de negar o crediÃ¡rio a qualquer cliente</li>
            </ul>
          </section>

          {/* 4. Comandas */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>4. Comandas</h2>
            <p>
              A comanda Ã© o registro digital dos itens consumidos na loja durante uma visita.
              O cliente Ã© responsÃ¡vel pelos itens registrados em sua comanda.
            </p>
            <p className="mt-2">
              O encerramento da comanda implica concordÃ¢ncia com os valores e itens listados.
              Em caso de divergÃªncia, o cliente deve sinalizar imediatamente ao responsÃ¡vel pela loja.
            </p>
          </section>

          {/* 5. Campeonatos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>5. Campeonatos</h2>
            <p>
              A participaÃ§Ã£o em campeonatos sujeita-se Ã s regras especÃ­ficas de cada evento,
              divulgadas com antecedÃªncia.
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>A taxa de inscriÃ§Ã£o pode ser cobrada antecipadamente e nÃ£o Ã© reembolsÃ¡vel salvo cancelamento do evento pela organizaÃ§Ã£o</li>
              <li>O participante deve apresentar deck vÃ¡lido conforme o regulamento do jogo</li>
              <li>Comportamento antidesportivo pode resultar em desclassificaÃ§Ã£o</li>
              <li>A SantuÃ¡rio Nerd reserva-se o direito de alterar datas e regras de campeonatos com aviso prÃ©vio</li>
            </ul>
          </section>

          {/* 6. Propriedade intelectual */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>6. Propriedade Intelectual</h2>
            <p>
              Os nomes, logotipos e conteÃºdos de jogos de cartas (PokÃ©mon, Magic: The Gathering, Yu-Gi-Oh!
              e outros) sÃ£o propriedade de seus respectivos detentores de direitos. A SantuÃ¡rio Nerd nÃ£o reivindica
              qualquer direito sobre esses conteÃºdos.
            </p>
            <p className="mt-2">
              O sistema digital, o cÃ³digo-fonte e o design da plataforma SantuÃ¡rio Nerd sÃ£o de propriedade
              exclusiva da SantuÃ¡rio Nerd e nÃ£o podem ser reproduzidos, modificados ou distribuÃ­dos sem
              autorizaÃ§Ã£o expressa.
            </p>
          </section>

          {/* 7. LimitaÃ§Ã£o de responsabilidade */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>7. LimitaÃ§Ã£o de Responsabilidade</h2>
            <p>
              A SantuÃ¡rio Nerd nÃ£o se responsabiliza por:
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>Danos decorrentes do uso indevido do sistema pelo usuÃ¡rio</li>
              <li>Indisponibilidade temporÃ¡ria do sistema por manutenÃ§Ã£o ou falhas tÃ©cnicas</li>
              <li>ConteÃºdo gerado pelo assistente de IA â€” as respostas sÃ£o informativas e nÃ£o constituem aconselhamento profissional</li>
              <li>Perda ou dano a cartÃµes fÃ­sicos que nÃ£o sejam de responsabilidade da loja</li>
            </ul>
          </section>

          {/* 8. CDC */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>8. Direitos do Consumidor (CDC)</h2>
            <p>
              Estes Termos nÃ£o afastam ou limitam os direitos previstos no{' '}
              <strong>CÃ³digo de Defesa do Consumidor (Lei nÂ° 8.078/1990)</strong>. Em caso de reclamaÃ§Ã£o,
              vocÃª pode entrar em contato com a SantuÃ¡rio Nerd pelo e-mail{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#7839F3] underline">
                contato@santuarionerd.com.br
              </a>{' '}
              ou pelo portal do consumidor{' '}
              <a href="https://www.consumidor.gov.br" target="_blank" rel="noopener noreferrer"
                 className="text-[#7839F3] underline">
                consumidor.gov.br
              </a>.
            </p>
          </section>

          {/* 9. Foro */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>9. Foro e LegislaÃ§Ã£o AplicÃ¡vel</h2>
            <p>
              Estes Termos sÃ£o regidos pelas leis da RepÃºblica Federativa do Brasil. Para dirimir
              quaisquer controvÃ©rsias decorrentes deste instrumento, fica eleito o foro da{' '}
              <strong>Comarca de SÃ£o JosÃ© do Rio Preto, SP</strong>, com renÃºncia expressa a qualquer outro,
              por mais privilegiado que seja.
            </p>
          </section>

          {/* 10. Contato */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>10. Contato</h2>
            <p>
              Para dÃºvidas, sugestÃµes ou reclamaÃ§Ãµes sobre estes Termos de Uso:
            </p>
            <p className="mt-2">
              <strong>E-mail:</strong>{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#7839F3] underline">
                contato@santuarionerd.com.br
              </a><br />
              <strong>Local:</strong> SantuÃ¡rio Nerd â€” SÃ£o JosÃ© do Rio Preto, SP
            </p>
          </section>

        </div>

        {/* Links */}
        <div className="mt-12 pt-6 flex flex-wrap gap-4 text-sm text-[#7839F3]">
          <Link href="/privacidade" className="underline">PolÃ­tica de Privacidade</Link>
          <Link href="/lgpd" className="underline">Exercer meus Direitos (LGPD)</Link>
          <a href="mailto:contato@santuarionerd.com.br" className="underline">
            contato@santuarionerd.com.br
          </a>
        </div>
      </main>
    </div>
  )
}
