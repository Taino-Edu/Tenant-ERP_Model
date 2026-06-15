// =============================================================================
// privacidade/page.tsx â€” PolÃ­tica de Privacidade da SantuÃ¡rio Nerd
// Conforme LGPD (Lei 13.709/2018) â€” SÃ£o JosÃ© do Rio Preto, SP
// VersÃ£o 1.1 â€” tema claro/escuro + seÃ§Ã£o de IA expandida
// =============================================================================

import Link from 'next/link'
import type { Metadata } from 'next'
import ThemeToggle from '@/components/ThemeToggle'

export const metadata: Metadata = {
  title: 'Politica de Privacidade â€” SantuÃ¡rio Nerd',
  description: 'Saiba como a Santuario Nerd coleta, usa e protege seus dados pessoais em conformidade com a LGPD.',
}

export default function PrivacidadePage() {
  return (
    <div className="min-h-screen" style={{ backgroundColor: 'var(--bg-primary)', color: 'var(--text-primary)' }}>

      {/* CabeÃ§alho */}
      <header className="bg-[#1a0a2e] text-white py-5 px-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-1 text-2xl font-bold">
            <span className="text-[#42B6EE]">SantuÃ¡rio</span><span> Nerd</span>
          </Link>
          <div className="flex items-center gap-4">
            <span className="text-sm text-gray-400 hidden sm:block">SÃ£o JosÃ© do Rio Preto, SP</span>
            <ThemeToggle compact />
          </div>
        </div>
      </header>

      <main className="max-w-3xl mx-auto px-4 py-12">

        {/* TÃ­tulo */}
        <div className="mb-10 pb-6" style={{ borderBottom: '1px solid var(--border-color)' }}>
          <h1 className="text-3xl font-bold mb-2">PolÃ­tica de Privacidade</h1>
          <p className="text-sm" style={{ color: 'var(--text-faint)' }}>
            Ãšltima atualizaÃ§Ã£o: <strong>Maio de 2026</strong> â€” VersÃ£o 1.1
          </p>
          <p className="mt-3 leading-relaxed" style={{ color: 'var(--text-muted)' }}>
            Esta PolÃ­tica descreve como a <strong>SantuÃ¡rio Nerd</strong> coleta, utiliza, armazena e protege
            seus dados pessoais, em conformidade com a{' '}
            <strong>Lei Geral de ProteÃ§Ã£o de Dados (LGPD â€” Lei nÂº 13.709/2018)</strong>.
          </p>
        </div>

        <div className="space-y-10 leading-relaxed" style={{ color: 'var(--text-muted)' }}>

          {/* 1. Quem somos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>1. Quem Somos</h2>
            <p>
              A <strong>SantuÃ¡rio Nerd</strong> Ã© uma loja de card games em <strong>SÃ£o JosÃ© do Rio Preto, SP</strong>.
              Operamos um sistema digital de comandas, campeonatos, crediÃ¡rio e programa de pontos para nossos clientes.
            </p>
            <p className="mt-2">
              <strong>Controlador dos dados:</strong><br />
              SantuÃ¡rio Nerd â€” SÃ£o JosÃ© do Rio Preto, SP<br />
              E-mail:{' '}
              <a href="mailto:santuarionerd@gmail.com" className="text-[#42B6EE] underline">
                santuarionerd@gmail.com
              </a>
            </p>
          </section>

          {/* 2. Dados coletados */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>2. Dados que Coletamos</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li><strong>Nome completo</strong> â€” identificaÃ§Ã£o e personalizaÃ§Ã£o do atendimento</li>
              <li><strong>CPF</strong> â€” identificaÃ§Ã£o Ãºnica e prevenÃ§Ã£o de fraudes no crediÃ¡rio</li>
              <li><strong>WhatsApp</strong> â€” contato e login rÃ¡pido via QR Code</li>
              <li><strong>E-mail</strong> â€” confirmaÃ§Ãµes, notificaÃ§Ãµes e recuperaÃ§Ã£o de senha</li>
              <li><strong>Historico de comandas e compras</strong> ea” gestão do crediario e pontos</li>
              <li><strong>ParticipaÃ§Ã£o em campeonatos</strong> â€” nome do deck, colocaÃ§Ã£o</li>
              <li><strong>Saldo de pontos</strong> â€” programa de fidelidade</li>
              <li><strong>EndereÃ§o IP (hash SHA-256)</strong> â€” seguranÃ§a e prevenÃ§Ã£o de abusos; nunca armazenamos o IP em texto puro</li>
            </ul>
          </section>

          {/* 3. Finalidade */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>3. Finalidade do Tratamento</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Identificar e autenticar usuÃ¡rios no sistema</li>
              <li>Gerenciar comandas, pedidos e pagamentos (inclusive crediÃ¡rio)</li>
              <li>Administrar o programa de pontos e fidelidade</li>
              <li>Processar inscriÃ§Ãµes e resultados de campeonatos</li>
              <li>Enviar comunicaÃ§Ãµes transacionais (crediÃ¡rio, campeonatos, redefiniÃ§Ã£o de senha)</li>
              <li>Cumprir obrigaÃ§Ãµes legais e fiscais (retenÃ§Ã£o de 5 anos conforme legislaÃ§Ã£o)</li>
              <li>Prevenir fraudes e garantir a seguranÃ§a do sistema</li>
              <li>Gerar anÃ¡lises internas e anÃ´nimas para melhoria dos serviÃ§os</li>
            </ul>
          </section>

          {/* 4. Base legal */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>4. Base Legal (LGPD Art. 7Âº)</h2>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li><strong>Consentimento (Art. 7Âº, I):</strong> registro inicial via QR Code e envio de comunicaÃ§Ãµes</li>
              <li><strong>ExecuÃ§Ã£o de contrato (Art. 7Âº, V):</strong> gestÃ£o de comandas, crediÃ¡rio e campeonatos</li>
              <li><strong>LegÃ­timo interesse (Art. 7Âº, IX):</strong> seguranÃ§a do sistema e prevenÃ§Ã£o de fraudes</li>
              <li><strong>ObrigaÃ§Ã£o legal (Art. 7Âº, II):</strong> fins fiscais e contÃ¡beis</li>
            </ul>
          </section>

          {/* 5. InteligÃªncia Artificial */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>5. Uso de InteligÃªncia Artificial</h2>
            <p className="mb-3">
              Utilizamos o serviÃ§o <strong>Google Gemini 2.0 Flash</strong> (Google LLC) como assistente
              de anÃ¡lise de negÃ³cios acessÃ­vel exclusivamente pelos <strong>administradores da loja</strong>.
              Clientes <strong>nÃ£o interagem</strong> diretamente com a IA.
            </p>
            <p className="mb-3">
              <strong>O que a IA faz:</strong> responde perguntas do administrador sobre o desempenho
              da loja (vendas, estoque, crediÃ¡rio) com base nos dados internos do sistema.
            </p>
            <p className="mb-3">
              <strong>ProteÃ§Ã£o de dados na IA:</strong>
            </p>
            <ul className="list-disc list-inside space-y-2 ml-2 mb-3">
              <li>
                <strong>AnonimizaÃ§Ã£o obrigatÃ³ria:</strong> antes de qualquer dado ser enviado ao Gemini,
                nomes de clientes sÃ£o substituÃ­dos por identificadores neutros ("Cliente #1", "Cliente #2" etc.).
                CPF, e-mail e WhatsApp <strong>nunca sÃ£o enviados</strong> Ã  IA.
              </li>
              <li>
                <strong>Dados enviados:</strong> apenas valores financeiros agregados, datas e categorias
                â€” sem informaÃ§Ã£o que permita identificar individualmente qualquer pessoa.
              </li>
              <li>
                <strong>Finalidade exclusiva:</strong> os dados sÃ£o enviados somente para gerar respostas
                sobre o negÃ³cio e <strong>nÃ£o sÃ£o usados pelo Google para treinar modelos</strong>
                (conforme os Termos da API do Google AI Studio).
              </li>
              <li>
                <strong>Sem armazenamento pela IA:</strong> o Gemini nÃ£o retÃ©m as informaÃ§Ãµes entre sessÃµes.
                Cada consulta Ã© processada de forma independente.
              </li>
            </ul>
            <p>
              Para mais informaÃ§Ãµes sobre as prÃ¡ticas de privacidade do Google, consulte:{' '}
              <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer"
                className="text-[#42B6EE] underline">
                policies.google.com/privacy
              </a>.
            </p>
          </section>

          {/* 6. Outros terceiros */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>6. Outros Compartilhamentos</h2>
            <p className="mb-2">NÃ£o vendemos nem alugamos seus dados. Compartilhamos apenas com:</p>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li>
                <strong>APIs de cartas TCG</strong> (PokÃ©mon, Magic: The Gathering, Yu-Gi-Oh!):
                usadas apenas para buscar informaÃ§Ãµes de cartas. <em>Nenhum dado pessoal Ã© enviado.</em>
              </li>
              <li>
                <strong>Provedor de e-mail (SMTP):</strong> para envio de notificaÃ§Ãµes transacionais
                (redefiniÃ§Ã£o de senha, confirmaÃ§Ã£o de crediÃ¡rio etc.).
              </li>
              <li>
                <strong>Autoridades pÃºblicas:</strong> quando exigido por lei ou ordem judicial.
              </li>
            </ul>
          </section>

          {/* 7. Seus direitos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>7. Seus Direitos (LGPD Art. 18)</h2>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li><strong>Acesso:</strong> saber quais dados possuÃ­mos sobre vocÃª</li>
              <li><strong>RetificaÃ§Ã£o:</strong> corrigir dados incompletos, inexatos ou desatualizados</li>
              <li><strong>ExclusÃ£o:</strong> solicitar a anonimizaÃ§Ã£o ou exclusÃ£o dos seus dados</li>
              <li><strong>Portabilidade:</strong> receber seus dados em formato estruturado</li>
              <li><strong>OposiÃ§Ã£o:</strong> opor-se a tratamentos realizados com base em legÃ­timo interesse</li>
              <li><strong>RevogaÃ§Ã£o:</strong> retirar o consentimento a qualquer momento</li>
            </ul>
            <p className="mt-3">
              Acesse{' '}
              <Link href="/lgpd" className="text-[#42B6EE] underline font-medium">
                santuarionerd.tech/lgpd
              </Link>
              {' '}para exercer seus direitos ou envie e-mail para{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#42B6EE] underline">
                contato@santuarionerd.com.br
              </a>.
              Respondemos em atÃ© <strong>15 dias corridos</strong> (Art. 18 Â§ 5Âº).
            </p>
          </section>

          {/* 8. RetenÃ§Ã£o */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>8. RetenÃ§Ã£o de Dados</h2>
            <p>
              Mantemos seus dados pelo tempo necessÃ¡rio para as finalidades descritas, ou conforme exigido
              por lei. Dados de crediÃ¡rio e compras podem ser retidos por atÃ© <strong>5 anos</strong> para
              fins fiscais. ApÃ³s solicitaÃ§Ã£o de exclusÃ£o, anonimizamos os dados pessoais identificÃ¡veis,
              mantendo apenas o histÃ³rico transacional de forma desidentificada.
            </p>
          </section>

          {/* 9. SeguranÃ§a */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>9. SeguranÃ§a</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Senhas armazenadas com hash BCrypt (fator 12) â€” nunca em texto puro</li>
              <li>Tokens de sessÃ£o com curta validade (60 min) e rotaÃ§Ã£o automÃ¡tica</li>
              <li>Cookies HttpOnly e SameSite=Strict â€” inacessÃ­veis a scripts maliciosos</li>
              <li>EndereÃ§os IP armazenados exclusivamente como hash SHA-256</li>
              <li>HTTPS em todos os ambientes de produÃ§Ã£o</li>
              <li>Trilha de auditoria imutÃ¡vel de todos os acessos a dados sensÃ­veis</li>
              <li>Limite de tentativas de login (rate limiting) para prevenÃ§Ã£o de forÃ§a bruta</li>
            </ul>
          </section>

          {/* 10. Cookies */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>10. Cookies</h2>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li>
                <strong>Cookies essenciais:</strong> necessÃ¡rios para autenticaÃ§Ã£o e seguranÃ§a da sessÃ£o.
                NÃ£o podem ser desativados.
              </li>
              <li>
                <strong>Cookies de preferÃªncia:</strong> armazenam preferÃªncias de interface
                (tema claro/escuro). Podem ser limpos nas configuraÃ§Ãµes do navegador.
              </li>
            </ul>
            <p className="mt-2">
              Ao utilizar nosso sistema, vocÃª concorda com os cookies essenciais conforme descrito.
            </p>
          </section>

          {/* 11. Contato */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>11. Contato e ReclamaÃ§Ãµes</h2>
            <p>
              <strong>E-mail:</strong>{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#42B6EE] underline">
                contato@santuarionerd.com.br
              </a><br />
              <strong>EndereÃ§o:</strong> SantuÃ¡rio Nerd â€” SÃ£o JosÃ© do Rio Preto, SP
            </p>
            <p className="mt-2">
              Insatisfeito com nossa resposta? Registre reclamaÃ§Ã£o na{' '}
              <a href="https://www.gov.br/anpd" target="_blank" rel="noopener noreferrer"
                className="text-[#42B6EE] underline">
                ANPD â€” Autoridade Nacional de ProteÃ§Ã£o de Dados
              </a>.
            </p>
          </section>

          {/* 12. AlteraÃ§Ãµes */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>12. AlteraÃ§Ãµes desta PolÃ­tica</h2>
            <p>
              Podemos atualizar esta PolÃ­tica periodicamente. Quando realizarmos alteraÃ§Ãµes relevantes,
              notificaremos os usuÃ¡rios cadastrados por e-mail e atualizaremos a data de "Ãºltima atualizaÃ§Ã£o".
              O uso continuado do sistema apÃ³s as alteraÃ§Ãµes implica aceitaÃ§Ã£o da nova versÃ£o.
            </p>
          </section>

        </div>

        {/* Links rodapÃ© */}
        <div className="mt-12 pt-6 flex flex-wrap gap-4 text-sm text-[#42B6EE]"
             style={{ borderTop: '1px solid var(--border-color)' }}>
          <Link href="/termos" className="underline">Termos de Uso</Link>
          <Link href="/lgpd" className="underline">Exercer meus Direitos (LGPD)</Link>
          <a href="mailto:contato@santuarionerd.com.br" className="underline">
            contato@santuarionerd.com.br
          </a>
        </div>
      </main>
    </div>
  )
}
