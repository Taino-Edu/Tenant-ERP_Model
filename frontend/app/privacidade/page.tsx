// =============================================================================
// privacidade/page.tsx — Política de Privacidade da Santuário Nerd
// Conforme LGPD (Lei 13.709/2018) — José Bonifácio, SP
// Versão 1.2 — junho/2026
// =============================================================================

import Link from 'next/link'
import type { Metadata } from 'next'
import ThemeToggle from '@/components/ThemeToggle'
import { LegalActions } from '@/components/LegalActions'

export const metadata: Metadata = {
  title: 'Política de Privacidade — Santuário Nerd',
  description: 'Saiba como a Santuário Nerd coleta, usa e protege seus dados pessoais em conformidade com a LGPD.',
}

export default function PrivacidadePage() {
  return (
    <div className="min-h-screen" style={{ backgroundColor: 'var(--bg-primary)', color: 'var(--text-primary)' }}>

      {/* Cabeçalho */}
      <header className="bg-[#1a0a2e] text-white py-5 px-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-1 text-2xl font-bold">
            <span className="text-[#42B6EE]">Santuário</span><span> Nerd</span>
          </Link>
          <div className="flex items-center gap-3">
            <LegalActions />
            <span className="text-sm text-gray-400 hidden sm:block">José Bonifácio, SP</span>
            <ThemeToggle compact />
          </div>
        </div>
      </header>

      <main className="max-w-3xl mx-auto px-4 py-12">

        {/* Título */}
        <div className="mb-10 pb-6" style={{ borderBottom: '1px solid var(--border-color)' }}>
          <h1 className="text-3xl font-bold mb-2">Política de Privacidade</h1>
          <p className="text-sm" style={{ color: 'var(--text-faint)' }}>
            Última atualização: <strong>Junho de 2026</strong> — Versão 1.2
          </p>
          <p className="mt-3 leading-relaxed" style={{ color: 'var(--text-muted)' }}>
            Esta Política descreve como a <strong>Santuário Nerd</strong> coleta, utiliza, armazena e protege
            seus dados pessoais, em conformidade com a{' '}
            <strong>Lei Geral de Proteção de Dados (LGPD — Lei nº 13.709/2018)</strong>.
          </p>
        </div>

        <div className="space-y-10 leading-relaxed" style={{ color: 'var(--text-muted)' }}>

          {/* 1. Quem somos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>1. Quem Somos</h2>
            <p>
              A <strong>Santuário Nerd</strong> é uma loja de card games em <strong>José Bonifácio, SP</strong>.
              Operamos um sistema digital de comandas, campeonatos, crediário e programa de pontos para nossos clientes.
            </p>
            <p className="mt-2">
              <strong>Controlador dos dados:</strong><br />
              Santuário Nerd — José Bonifácio, SP<br />
              E-mail:{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#42B6EE] underline">
                contato@santuarionerd.com.br
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
              <li><strong>Saldo de pontos e cashback</strong> — programa de fidelidade</li>
              <li><strong>Endereço IP (hash SHA-256)</strong> — segurança e prevenção de abusos; nunca armazenamos o IP em texto puro</li>
            </ul>
          </section>

          {/* 3. Finalidade */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>3. Finalidade do Tratamento</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Identificar e autenticar usuários no sistema</li>
              <li>Gerenciar comandas, pedidos e pagamentos (inclusive crediário)</li>
              <li>Administrar o programa de pontos e cashback</li>
              <li>Processar inscrições e resultados de campeonatos</li>
              <li>Enviar comunicações transacionais (crediário, campeonatos, redefinição de senha)</li>
              <li>Cumprir obrigações legais e fiscais</li>
              <li>Prevenir fraudes e garantir a segurança do sistema</li>
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

          {/* 5. Compartilhamento */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>5. Compartilhamento de Dados</h2>
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

          {/* 6. Seus direitos */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>6. Seus Direitos (LGPD Art. 18)</h2>
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
              <Link href="/lgpd" className="text-[#42B6EE] underline font-medium">
                o formulário LGPD
              </Link>
              {' '}para exercer seus direitos ou envie e-mail para{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#42B6EE] underline">
                contato@santuarionerd.com.br
              </a>.
              Respondemos em até <strong>15 dias corridos</strong> (Art. 18 § 5º).
            </p>
          </section>

          {/* 7. Retenção */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>7. Retenção de Dados</h2>
            <p>
              Mantemos seus dados pelo tempo necessário para as finalidades descritas, ou conforme exigido
              por lei. Dados de crediário e compras podem ser retidos por até <strong>5 anos</strong> para
              fins fiscais. Após solicitação de exclusão, anonimizamos os dados pessoais identificáveis,
              mantendo apenas o histórico transacional de forma desidentificada.
            </p>
          </section>

          {/* 8. Segurança */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>8. Segurança</h2>
            <ul className="list-disc list-inside space-y-1 ml-2">
              <li>Senhas armazenadas com hash BCrypt — nunca em texto puro</li>
              <li>Tokens de sessão com curta validade (60 min) e renovação automática</li>
              <li>Cookies HttpOnly e SameSite=Strict — inacessíveis a scripts maliciosos</li>
              <li>Endereços IP armazenados exclusivamente como hash SHA-256</li>
              <li>HTTPS em todos os ambientes de produção</li>
              <li>Trilha de auditoria de todos os acessos a dados sensíveis</li>
              <li>Limite de tentativas de login para prevenção de força bruta</li>
            </ul>
          </section>

          {/* 9. Cookies */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>9. Cookies</h2>
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
          </section>

          {/* 10. Contato */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>10. Contato e Reclamações</h2>
            <p>
              <strong>E-mail:</strong>{' '}
              <a href="mailto:contato@santuarionerd.com.br" className="text-[#42B6EE] underline">
                contato@santuarionerd.com.br
              </a><br />
              <strong>Endereço:</strong> Santuário Nerd — José Bonifácio, SP
            </p>
            <p className="mt-2">
              Insatisfeito com nossa resposta? Registre reclamação na{' '}
              <a href="https://www.gov.br/anpd" target="_blank" rel="noopener noreferrer"
                className="text-[#42B6EE] underline">
                ANPD — Autoridade Nacional de Proteção de Dados
              </a>.
            </p>
          </section>

          {/* 11. Alterações */}
          <section>
            <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--text-primary)' }}>11. Alterações desta Política</h2>
            <p>
              Podemos atualizar esta Política periodicamente. Quando realizarmos alterações relevantes,
              notificaremos os usuários cadastrados por e-mail e atualizaremos a data de "última atualização".
              O uso continuado do sistema após as alterações implica aceitação da nova versão.
            </p>
          </section>

        </div>

        {/* Links rodapé */}
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
