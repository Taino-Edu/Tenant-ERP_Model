'use client'
// =============================================================================
// AvisoModuloFiscal.tsx — Faixa fixa no topo do portal delimitando o que este
// módulo é hoje.
//
// Não é decoração nem "aviso legal de rodapé": DRE, apuração e fechamento saem
// com cara de documento pronto, e sem esse recorte explícito é natural alguém
// tratar o DAS estimado como guia ou o fechamento como escrituração. Fica
// sempre visível, sem botão de fechar — é limite de escopo, não notificação.
// =============================================================================
import { Info } from 'lucide-react'

export default function AvisoModuloFiscal() {
  return (
    <div
      role="note"
      className="border-b border-brand-500/20 bg-brand-500/5 print:hidden"
    >
      <div className="max-w-6xl mx-auto px-6 py-2.5 flex items-start gap-2.5 text-xs text-gray-400">
        <Info className="w-4 h-4 text-brand-400 shrink-0 mt-0.5" />
        <p>
          <strong className="text-gray-200">Material de apoio ao contador.</strong>{' '}
          Relatórios, DRE, fechamento e apuração gerados aqui servem para conferência e
          escrituração — <strong className="text-gray-200">não são documentos fiscais</strong> e não
          substituem guia, livro ou declaração. O único documento fiscal que o sistema emite é a
          nota fiscal (NFC-e), e é dela que saem os XMLs nomeados na exportação. Estamos ampliando
          a cobertura da rotina fiscal a cada versão.
        </p>
      </div>
    </div>
  )
}
