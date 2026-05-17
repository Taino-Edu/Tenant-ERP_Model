// =============================================================================
// privacidade/page.tsx — Política de Privacidade da softNerd
// Conforme LGPD (Lei 13.709/2018) — São José do Rio Preto, SP
// Versão 1.1 — tema claro/escuro + seção de IA expandida
// =============================================================================

import Link from 'next/link'
import type { Metadata } from 'next'
import ThemeToggle from '@/components/ThemeToggle'

export const metadata: Metadata = {
  title: 'Política de Privacidade — softNerd',
  description: 'Saiba como a softNerd coleta, usa e protege seus dados pessoais em conformidade com a LGPD.',
}

export default function PrivacidadePage() {
  return (
    <div className="min-h-screen" style={{ backgroundColor: 'var(--bg-primary)', color: 'var(--text-primary)' }}>

      {/* Cabeçalho */}
      <header className="bg-[#1a0a2e] text-white py-5 px-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-1 text-2xl font-bold">
            <span className="text-[#7839F3]">soft</span><span>Nerd</span>
          </Link>
          <div className="flex items-center gap-4">
            <span className="text-sm text-gray-400 hidden sm:block">São José do Rio Preto, SP</span>
            <ThemeToggle compact />
          </div>
        </div>
      </header>

      <main className="max-w-3xl mx-auto px-4 py-12">

        {/* Título */}
        <div className="mb-10 pb-6" style={{ borderBottom: '1px solid var(--border-color)' }}>
          <h1 className="text-3xl font-bold mb-2">Política de Privacidade</h1>
          <p className="text-sm" style={{ color: 'var(--text-faint)' }}>
            Última atualização: <strong>Maio de 2026</strong> — Versão 1.1
          </p>
          <p className="mt-3 leading-relaxed" style={{ color: 'var(--text-muted)' }}>
            Esta Política descreve como a <strong>softNerd</strong> coleta, utiliza, armazena e protege
            seus dados pessoais, em conformidade com a{' '}
            <strong>Lei Geral de Proteção de Dados (LGPD — Lei nº 13.709/2018)</strong>.
          </p>
        </div>

        <div className="space-y-10 leading-relaxed" style={{ color: 'var(--text-muted)' }}>

          {/* 1. Quem somos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>1. Quem Somos</h2>
            <p>
              A <strong>softNerd</strong> é uma loja de card games em <strong>São José do Rio Preto, SP</strong>.
              Operamos um sistema digital de comandas, campeonatos, crediário e programa de pontos para nossos clientes.
            </p>
            <p className="mt-2">
              <strong>Controlador dos dados:</strong><br />
              softNerd — São José do Rio Preto, SP<br />
              E-mail:{' '}
              <a href="mailto:privacidade@softnerd.com.br" className="text-[#7839F3] underline">
                privacidade@softnerd.com.br
              </a>
            </p>
          </section>

          {/* 2. Dados coletados */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>2. Dados que Coletamos</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li><strong>Nome completo</strong> — identificação e personalização do atendimento</li>
              <li><strong>CPF</strong> — identificação única e prevenção de fraudes no crediário</li>
              <li><strong>WhatsApp</strong> — contato e login rápido via QR Code</li>
              <li><strong>E-mail</strong> — confirmações, notificações e recuperação de senha</li>
              <li><strong>Histórico de comandas e compras</strong> — gestão do crediário e pontos</li>
              <li><strong>Participação em campeonatos</strong> — nome do deck, colocação</li>
              <li><strong>Saldo de pontos</strong> — programa de fidelidade</li>
              <li><strong>Endereço IP (hash SHA-256)</strong> — segurança e prevenção de abusos; nunca armazenamos o IP em texto puro</li>
            </ul>
          </section>

          {/* 3. Finalidade */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>3. Finalidade do Tratamento</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Identificar e autenticar usuários no sistema</li>
              <li>Gerenciar comandas, pedidos e pagamentos (inclusive crediário)</li>
              <li>Administrar o programa de pontos e fidelidade</li>
              <li>Processar inscrições e resultados de campeonatos</li>
              <li>Enviar comunicações transacionais (crediário, campeonatos, redefinição de senha)</li>
              <li>Cumprir obrigações legais e fiscais (retenção de 5 anos conforme legislação)</li>
              <li>Prevenir fraudes e garantir a segurança do sistema</li>
              <li>Gerar análises internas e anônimas para melhoria dos serviços</li>
            </ul>
          </section>

          {/* 4. Base legal */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>4. Base Legal (LGPD Art. 7º)</h2>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li><strong>Consentimento (Art. 7º, I):</strong> registro inicial via QR Code e envio de comunicações</li>
              <li><strong>Execução de contrato (Art. 7º, V):</strong> gestão de comandas, crediário e campeonatos</li>
              <li><strong>Legítimo interesse (Art. 7º, IX):</strong> segurança do sistema e prevenção de fraudes</li>
              <li><strong>Obrigação legal (Art. 7º, II):</strong> fins fiscais e contábeis</li>
            </ul>
          </section>

          {/* 5. Inteligência Artificial */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>5. Uso de Inteligência Artificial</h2>
            <p className="mb-3">
              Utilizamos o serviço <strong>Google Gemini 2.0 Flash</strong> (Google LLC) como assistente
              de análise de negócios acessível exclusivamente pelos <strong>administradores da loja</strong>.
              Clientes <strong>não interagem</strong> diretamente com a IA.
            </p>
            <p className="mb-3">
              <strong>O que a IA faz:</strong> responde perguntas do administrador sobre o desempenho
              da loja (vendas, estoque, crediário) com base nos dados internos do sistema.
            </p>
            <p className="mb-3">
              <strong>Proteção de dados na IA:</strong>
            </p>
            <ul className="list-disc list-inside space-y-2 ml-2 mb-3">
              <li>
                <strong>Anonimização obrigatória:</strong> antes de qualquer dado ser enviado ao Gemini,
                nomes de clientes são substituídos por identificadores neutros ("Cliente #1", "Cliente #2" etc.).
                CPF, e-mail e WhatsApp <strong>nunca são enviados</strong> à IA.
              </li>
              <li>
                <strong>Dados enviados:</strong> apenas valores financeiros agregados, datas e categorias
                — sem informação que permita identificar individualmente qualquer pessoa.
              </li>
              <li>
                <strong>Finalidade exclusiva:</strong> os dados são enviados somente para gerar respostas
                sobre o negócio e <strong>não são usados pelo Google para treinar modelos</strong>
                (conforme os Termos da API do Google AI Studio).
              </li>
              <li>
                <strong>Sem armazenamento pela IA:</strong> o Gemini não retém as informações entre sessões.
                Cada consulta é processada de forma independente.
              </li>
            </ul>
            <p>
              Para mais informações sobre as práticas de privacidade do Google, consulte:{' '}
              <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer"
                className="text-[#7839F3] underline">
                policies.google.com/privacy
              </a>.
            </p>
          </section>

          {/* 6. Outros terceiros */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>6. Outros Compartilhamentos</h2>
            <p className="mb-2">Não vendemos nem alugamos seus dados. Compartilhamos apenas com:</p>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li>
                <strong>APIs de cartas TCG</strong> (Pokémon, Magic: The Gathering, Yu-Gi-Oh!):
                usadas apenas para buscar informações de cartas. <em>Nenhum dado pessoal é enviado.</em>
              </li>
              <li>
                <strong>Provedor de e-mail (SMTP):</strong> para envio de notificações transacionais
                (redefinição de senha, confirmação de crediário etc.).
              </li>
              <li>
                <strong>Autoridades públicas:</strong> quando exigido por lei ou ordem judicial.
              </li>
            </ul>
          </section>

          {/* 7. Seus direitos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>7. Seus Direitos (LGPD Art. 18)</h2>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li><strong>Acesso:</strong> saber quais dados possuímos sobre você</li>
              <li><strong>Retificação:</strong> corrigir dados incompletos, inexatos ou desatualizados</li>
              <li><strong>Exclusão:</strong> solicitar a anonimização ou exclusão dos seus dados</li>
              <li><strong>Portabilidade:</strong> receber seus dados em formato estruturado</li>
              <li><strong>Oposição:</strong> opor-se a tratamentos realizados com base em legítimo interesse</li>
              <li><strong>Revogação:</strong> retirar o consentimento a qualquer momento</li>
            </ul>
            <p className="mt-3">
              Acesse{' '}
              <Link href="/lgpd" className="text-[#7839F3] underline font-medium">
                softnerd.com.br/lgpd
              </Link>
              {' '}para exercer seus direitos ou envie e-mail para{' '}
              <a href="mailto:privacidade@softnerd.com.br" className="text-[#7839F3] underline">
                privacidade@softnerd.com.br
              </a>.
              Respondemos em até <strong>15 dias corridos</strong> (Art. 18 § 5º).
            </p>
          </section>

          {/* 8. Retenção */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>8. Retenção de Dados</h2>
            <p>
              Mantemos seus dados pelo tempo necessário para as finalidades descritas, ou conforme exigido
              por lei. Dados de crediário e compras podem ser retidos por até <strong>5 anos</strong> para
              fins fiscais. Após solicitação de exclusão, anonimizamos os dados pessoais identificáveis,
              mantendo apenas o histórico transacional de forma desidentificada.
            </p>
          </section>

          {/* 9. Segurança */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>9. Segurança</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Senhas armazenadas com hash BCrypt (fator 12) — nunca em texto puro</li>
              <li>Tokens de sessão com curta validade (60 min) e rotação automática</li>
              <li>Cookies HttpOnly e SameSite=Strict — inacessíveis a scripts maliciosos</li>
              <li>Endereços IP armazenados exclusivamente como hash SHA-256</li>
              <li>HTTPS em todos os ambientes de produção</li>
              <li>Trilha de auditoria imutável de todos os acessos a dados sensíveis</li>
              <li>Limite de tentativas de login (rate limiting) para prevenção de força bruta</li>
            </ul>
          </section>

          {/* 10. Cookies */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>10. Cookies</h2>
            <ul className="list-disc list-inside space-y-2 ml-2">
              <li>
                <strong>Cookies essenciais:</strong> necessários para autenticação e segurança da sessão.
                Não podem ser desativados.
              </li>
              <li>
                <strong>Cookies de preferência:</strong> armazenam preferências de interface
                (tema claro/escuro). Podem ser limpos nas configurações do navegador.
              </li>
            </ul>
            <p className="mt-2">
              Ao utilizar nosso sistema, você concorda com os cookies essenciais conforme descrito.
            </p>
          </section>

          {/* 11. Contato */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>11. Contato e Reclamações</h2>
            <p>
              <strong>E-mail:</strong>{' '}
              <a href="mailto:privacidade@softnerd.com.br" className="text-[#7839F3] underline">
                privacidade@softnerd.com.br
              </a><br />
              <strong>Endereço:</strong> softNerd — São José do Rio Preto, SP
            </p>
            <p className="mt-2">
              Insatisfeito com nossa resposta? Registre reclamação na{' '}
              <a href="https://www.gov.br/anpd" target="_blank" rel="noopener noreferrer"
                className="text-[#7839F3] underline">
                ANPD — Autoridade Nacional de Proteção de Dados
              </a>.
            </p>
          </section>

          {/* 12. Alterações */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>12. Alterações desta Política</h2>
            <p>
              Podemos atualizar esta Política periodicamente. Quando realizarmos alterações relevantes,
              notificaremos os usuários cadastrados por e-mail e atualizaremos a data de "última atualização".
              O uso continuado do sistema após as alterações implica aceitação da nova versão.
            </p>
          </section>

        </div>

        {/* Links rodapé */}
        <div className="mt-12 pt-6 flex flex-wrap gap-4 text-sm text-[#7839F3]"
             style={{ borderTop: '1px solid var(--border-color)' }}>
          <Link href="/termos" className="underline">Termos de Uso</Link>
          <Link href="/lgpd" className="underline">Exercer meus Direitos (LGPD)</Link>
          <a href="mailto:privacidade@softnerd.com.br" className="underline">
            privacidade@softnerd.com.br
          </a>
        </div>
      </main>
    </div>
  )
}
