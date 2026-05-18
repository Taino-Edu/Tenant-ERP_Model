// =============================================================================
// termos/page.tsx — Termos de Uso da softNerd
// São José do Rio Preto, SP — Foro: Comarca de São José do Rio Preto
// =============================================================================

import Link from 'next/link'
import ThemeToggle from '@/components/ThemeToggle'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Termos de Uso',
  description: 'Termos e condições de uso dos serviços da softNerd, loja de card games em São José do Rio Preto, SP.',
}

export default function TermosPage() {
  return (
    <div className="min-h-screen" style={{ backgroundColor: "var(--bg-primary)", color: "var(--text-primary)" }}>
      {/* Cabeçalho */}
      <header className="bg-[#1a0a2e] text-white py-6 px-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-2xl font-bold">
            <span className="text-[#7839F3]">soft</span>
            <span>Nerd</span>
          </Link>
          <div className="flex items-center gap-4"><span className="text-sm text-gray-400 hidden sm:block">São José do Rio Preto, SP</span><ThemeToggle compact /></div>
        </div>
      </header>

      <main className="max-w-3xl mx-auto px-4 py-12">
        {/* Título */}
        <div className="mb-10 pb-6" style={{ borderBottom: "1px solid var(--border-color)" }}>
          <h1 className="text-3xl font-bold mb-2">Termos de Uso</h1>
          <p className="text-sm" style={{ color: "var(--text-faint)" }}>
            Última atualização: <strong>Maio de 2026</strong> — Versão 1.0
          </p>
          <p className="mt-3 leading-relaxed" style={{ color: "var(--text-muted)" }}>
            Ao utilizar o sistema digital da <strong>softNerd</strong>, você concorda com os presentes
            Termos de Uso. Leia com atenção antes de prosseguir.
          </p>
        </div>

        <div className="space-y-10 leading-relaxed" style={{ color: "var(--text-muted)" }}>

          {/* 1. Serviços */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>1. Serviços Oferecidos</h2>
            <p>
              A <strong>softNerd</strong> é uma loja física de card games localizada em São José do Rio Preto, SP.
              Nosso sistema digital oferece:
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>Abertura e gestão de comandas na loja</li>
              <li>Programa de pontos e fidelidade</li>
              <li>Crediário para clientes cadastrados</li>
              <li>Inscrição e acompanhamento de campeonatos</li>
              <li>Busca de cartas TCG (Pokémon, Magic: The Gathering, Yu-Gi-Oh!)</li>
              <li>Assistente de atendimento baseado em inteligência artificial</li>
            </ul>
          </section>

          {/* 2. Cadastro */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>2. Cadastro e Conta</h2>
            <p>
              O cadastro é realizado presencialmente na loja via QR Code ou pelo administrador do sistema.
              Ao se cadastrar, você declara que as informações fornecidas (nome, CPF, WhatsApp) são verídicas.
            </p>
            <p className="mt-2">
              O uso indevido do sistema — incluindo fornecimento de dados falsos, tentativas de acesso
              não autorizado ou abuso do programa de pontos — pode resultar no cancelamento da conta
              sem aviso prévio.
            </p>
            <p className="mt-2">
              Você é responsável pela confidencialidade de suas credenciais de acesso. Em caso de
              suspeita de uso não autorizado, entre em contato imediatamente.
            </p>
          </section>

          {/* 3. Crediário */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>3. Crediário</h2>
            <p>
              O crediário é uma linha de crédito oferecida pela softNerd a clientes cadastrados, sujeita
              à análise e aprovação.
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>O prazo padrão de pagamento é de <strong>30 dias corridos</strong> a partir da abertura</li>
              <li>Somente um crediário pode estar aberto por cliente por vez</li>
              <li>Crediários em aberto bloqueiam a abertura de novas comandas</li>
              <li>O não pagamento no prazo pode implicar restrição de acesso aos serviços da loja</li>
              <li>A softNerd reserva-se o direito de negar o crediário a qualquer cliente</li>
            </ul>
          </section>

          {/* 4. Comandas */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>4. Comandas</h2>
            <p>
              A comanda é o registro digital dos itens consumidos na loja durante uma visita.
              O cliente é responsável pelos itens registrados em sua comanda.
            </p>
            <p className="mt-2">
              O encerramento da comanda implica concordância com os valores e itens listados.
              Em caso de divergência, o cliente deve sinalizar imediatamente ao responsável pela loja.
            </p>
          </section>

          {/* 5. Campeonatos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>5. Campeonatos</h2>
            <p>
              A participação em campeonatos sujeita-se às regras específicas de cada evento,
              divulgadas com antecedência.
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>A taxa de inscrição pode ser cobrada antecipadamente e não é reembolsável salvo cancelamento do evento pela organização</li>
              <li>O participante deve apresentar deck válido conforme o regulamento do jogo</li>
              <li>Comportamento antidesportivo pode resultar em desclassificação</li>
              <li>A softNerd reserva-se o direito de alterar datas e regras de campeonatos com aviso prévio</li>
            </ul>
          </section>

          {/* 6. Propriedade intelectual */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>6. Propriedade Intelectual</h2>
            <p>
              Os nomes, logotipos e conteúdos de jogos de cartas (Pokémon, Magic: The Gathering, Yu-Gi-Oh!
              e outros) são propriedade de seus respectivos detentores de direitos. A softNerd não reivindica
              qualquer direito sobre esses conteúdos.
            </p>
            <p className="mt-2">
              O sistema digital, o código-fonte e o design da plataforma softNerd são de propriedade
              exclusiva da softNerd e não podem ser reproduzidos, modificados ou distribuídos sem
              autorização expressa.
            </p>
          </section>

          {/* 7. Limitação de responsabilidade */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>7. Limitação de Responsabilidade</h2>
            <p>
              A softNerd não se responsabiliza por:
            </p>
            <ul className="list-disc list-inside space-y-1 ml-2 mt-2">
              <li>Danos decorrentes do uso indevido do sistema pelo usuário</li>
              <li>Indisponibilidade temporária do sistema por manutenção ou falhas técnicas</li>
              <li>Conteúdo gerado pelo assistente de IA — as respostas são informativas e não constituem aconselhamento profissional</li>
              <li>Perda ou dano a cartões físicos que não sejam de responsabilidade da loja</li>
            </ul>
          </section>

          {/* 8. CDC */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>8. Direitos do Consumidor (CDC)</h2>
            <p>
              Estes Termos não afastam ou limitam os direitos previstos no{' '}
              <strong>Código de Defesa do Consumidor (Lei n° 8.078/1990)</strong>. Em caso de reclamação,
              você pode entrar em contato com a softNerd pelo e-mail{' '}
              <a href="mailto:privacidade@softnerd.com.br" className="text-[#7839F3] underline">
                privacidade@softnerd.com.br
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
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>9. Foro e Legislação Aplicável</h2>
            <p>
              Estes Termos são regidos pelas leis da República Federativa do Brasil. Para dirimir
              quaisquer controvérsias decorrentes deste instrumento, fica eleito o foro da{' '}
              <strong>Comarca de São José do Rio Preto, SP</strong>, com renúncia expressa a qualquer outro,
              por mais privilegiado que seja.
            </p>
          </section>

          {/* 10. Contato */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: "var(--text-primary)" }}>10. Contato</h2>
            <p>
              Para dúvidas, sugestões ou reclamações sobre estes Termos de Uso:
            </p>
            <p className="mt-2">
              <strong>E-mail:</strong>{' '}
              <a href="mailto:privacidade@softnerd.com.br" className="text-[#7839F3] underline">
                privacidade@softnerd.com.br
              </a><br />
              <strong>Local:</strong> softNerd — São José do Rio Preto, SP
            </p>
          </section>

        </div>

        {/* Links */}
        <div className="mt-12 pt-6 flex flex-wrap gap-4 text-sm text-[#7839F3]">
          <Link href="/privacidade" className="underline">Política de Privacidade</Link>
          <Link href="/lgpd" className="underline">Exercer meus Direitos (LGPD)</Link>
          <a href="mailto:privacidade@softnerd.com.br" className="underline">
            privacidade@softnerd.com.br
          </a>
        </div>
      </main>
    </div>
  )
}
