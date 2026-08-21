'use client'
import { Eye } from 'lucide-react'

/**
 * Aviso de que a tela abriu em modo de consulta.
 *
 * Existe por causa da separação entre `platform.x.read` e `platform.x`: agora
 * há perfis (Auditoria) que ABREM uma tela sem poder agir nela. Sem o aviso, a
 * pessoa vê a página completa com os botões sumidos e conclui que o sistema
 * está quebrado — some botão, some formulário, e nada explica por quê.
 *
 * Não é controle de acesso, é explicação. Quem barra é o
 * PlatformAccessMiddleware.
 */
export default function SomenteLeitura({ children }: { children?: React.ReactNode }) {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-brand-500/20 bg-brand-500/5 px-4 py-3 text-sm text-gray-300">
      <Eye className="mt-0.5 h-4 w-4 shrink-0 text-brand-400" />
      <p>
        <span className="font-semibold text-white">Somente consulta.</span>{' '}
        {children ?? 'Seu perfil abre esta tela para leitura; as ações ficam com quem responde pela área.'}
      </p>
    </div>
  )
}
