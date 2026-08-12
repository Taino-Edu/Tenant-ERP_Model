import type { Metadata } from 'next'
import Link from 'next/link'
import { LegalActions } from '@/components/LegalActions'

export const metadata: Metadata = {
  title: 'Termos de Uso',
  description: 'Termos de uso da plataforma multiempresa Octus para estabelecimentos, equipes e clientes finais.',
  alternates: { canonical: '/termos' },
}

const updated = '12 de agosto de 2026'

export default function TermosPage() {
  return (
    <LegalPage title="Termos de Uso" version="2.1" eyebrow="Plataforma Octus · legislação brasileira">
      <Notice>Estes Termos regulam o uso do Octus por empresas contratantes, seus usuários autorizados e clientes finais. A proposta comercial, o plano contratado e políticas específicas do estabelecimento complementam este documento. Em caso de relação de consumo, os direitos legais do consumidor prevalecem.</Notice>

      <Section title="1. Quem presta cada serviço">
        <p>A <strong>3E Systen</strong> fornece a plataforma tecnológica Octus. A empresa identificada pela marca, domínio ou unidade acessada — o <strong>Estabelecimento</strong> — é responsável pelas vendas, preços, produtos, atendimento, comandas, crédito concedido, documentos fiscais e demais relações com seus clientes.</p>
        <p>O Octus não se torna vendedor dos produtos do Estabelecimento nem substitui suas obrigações comerciais, fiscais, trabalhistas, contábeis ou regulatórias.</p>
      </Section>

      <Section title="2. Conta, acesso e segurança">
        <p>Os dados de cadastro devem ser verdadeiros e atualizados. Cada pessoa deve usar apenas o acesso que lhe foi autorizado, proteger suas credenciais e comunicar suspeitas de uso indevido. O Estabelecimento administra perfis e permissões de sua equipe.</p>
        <p>Podemos limitar sessões, exigir nova autenticação e suspender acessos diante de risco de segurança, fraude, violação destes Termos ou ordem legal, com preservação das evidências e comunicações cabíveis.</p>
      </Section>

      <Section title="3. Recursos da plataforma">
        <p>Conforme o plano e os módulos habilitados, o Octus pode oferecer PDV, estoque, comandas, pagamentos, crediário, financeiro, emissão e consulta fiscal, CRM, relatórios, integrações bancárias, portal do contador, restaurante e outros recursos.</p>
        <p>Comandas, orçamentos e relatórios são registros operacionais. O usuário deve conferir itens, valores, percentuais, descontos e dados fiscais antes da confirmação. Operações financeiras e fiscais podem depender de bancos, adquirentes, SEFAZ e outros terceiros.</p>
      </Section>

      <Section title="4. Contratação, cobrança e cancelamento">
        <p>Planos, período de teste, implantação, mensalidade, reajuste, vencimento e condições de comissão constam da oferta ou instrumento comercial aplicável. Recursos adicionais podem ter preço próprio, informado antes da contratação.</p>
        <p>O não pagamento pode resultar em aviso, limitação ou suspensão do acesso nos termos do contrato. O cancelamento não elimina valores vencidos nem obrigações legais de retenção. Antes do encerramento, o contratante poderá solicitar exportação dos dados disponíveis, respeitados prazos técnicos e legais.</p>
      </Section>

      <Section title="5. Uso aceitável">
        <p>É proibido usar o serviço para fraude, violação de direitos, acesso não autorizado, distribuição de malware, sobrecarga deliberada, engenharia reversa indevida ou tratamento ilícito de dados. Também é proibido inserir conteúdo sem autorização ou burlar limites técnicos, comerciais ou fiscais.</p>
      </Section>

      <Section title="6. Dados pessoais e confidencialidade">
        <p>O tratamento de dados segue a <Link href="/privacidade">Política de Privacidade</Link> e a <Link href="/cookies">Política de Cookies</Link>. Em regra, o Estabelecimento decide como tratar dados de seus clientes e atua como controlador; a 3E Systen opera esses dados para prestar a plataforma, sem prejuízo de situações em que trate dados para finalidades próprias legítimas e informadas.</p>
        <p>Cada contratante deve possuir base legal, prestar informações aos titulares e restringir o acesso de sua equipe ao mínimo necessário.</p>
      </Section>

      <Section title="7. Propriedade intelectual">
        <p>A licença de uso é limitada, revogável, não exclusiva e vinculada ao contrato. Software, interface, marcas, documentação e componentes do Octus pertencem à 3E Systen ou a seus licenciantes. Dados e conteúdos inseridos pelo Estabelecimento continuam sob sua responsabilidade e titularidade legítima.</p>
      </Section>

      <Section title="8. Disponibilidade e responsabilidade">
        <p>Adotamos medidas razoáveis de segurança, continuidade e recuperação, mas não prometemos funcionamento ininterrupto. Manutenções, internet, dispositivos locais e serviços de terceiros podem causar indisponibilidade. Incidentes relevantes serão tratados conforme a legislação e os compromissos contratuais.</p>
        <p>Nenhuma disposição exclui responsabilidade que a lei não permita excluir. Fora dessas hipóteses, perdas indiretas, decisões tomadas sem conferência e falhas atribuíveis a terceiros ou ao uso contrário à documentação serão avaliadas conforme a participação de cada parte.</p>
      </Section>

      <Section title="9. Alterações, legislação e solução de conflitos">
        <p>Podemos atualizar estes Termos para refletir mudanças legais, técnicas ou de produto. Alterações relevantes serão informadas por meio adequado e indicarão nova versão. A lei brasileira se aplica. O foro competente será definido pela legislação obrigatória e pelo instrumento de contratação, sem restringir o foro assegurado ao consumidor.</p>
      </Section>

      <Section title="10. Contato">
        <p>Dúvidas contratuais, técnicas ou de privacidade: <a href="mailto:3esysten@gmail.com">3esysten@gmail.com</a>. Para direitos sobre dados, também está disponível o <Link href="/lgpd">portal LGPD</Link>.</p>
      </Section>
    </LegalPage>
  )
}

function LegalPage({ title, version, eyebrow, children }: { title: string; version: string; eyebrow: string; children: React.ReactNode }) {
  return <main className="min-h-screen bg-[#f7fbfd] text-[#22384A]"><header className="bg-[#0C3D5A] px-4 py-5 text-white print:hidden"><div className="mx-auto flex max-w-4xl items-center justify-between"><Link href="/" className="text-xl font-black">Octus</Link><LegalActions /></div></header><article className="mx-auto max-w-4xl px-4 py-12"><p className="text-xs font-bold uppercase tracking-widest text-brand-700">{eyebrow}</p><h1 className="mt-2 text-3xl font-black text-[#0C3D5A]">{title}</h1><p className="mt-2 text-sm text-[#6B8598]">Versão {version} · atualizada em {updated}</p><div className="mt-8 space-y-3">{children}</div><nav className="mt-10 flex flex-wrap gap-4 border-t border-[#0C3D5A]/10 pt-6 text-sm font-semibold text-brand-700"><Link href="/privacidade">Privacidade</Link><Link href="/cookies">Cookies</Link><Link href="/lgpd">Direitos LGPD</Link></nav></article></main>
}

function Notice({ children }: { children: React.ReactNode }) { return <div className="rounded-2xl border border-brand-200 bg-brand-50 p-5 text-sm leading-relaxed">{children}</div> }
function Section({ title, children }: { title: string; children: React.ReactNode }) { return <section className="rounded-2xl border border-[#0C3D5A]/10 bg-white p-5 sm:p-6"><h2 className="font-bold text-[#0C3D5A]">{title}</h2><div className="mt-3 space-y-3 text-sm leading-relaxed text-[#526E80] [&_a]:font-semibold [&_a]:text-brand-700 [&_a]:underline">{children}</div></section> }
