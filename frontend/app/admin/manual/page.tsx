'use client'
import { useEffect } from 'react'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { MANUAL_SECOES as SECOES } from '@/lib/manualContent'

const VERSION = 'v1.21.0'
const DATA = '08/07/2026'

export default function ManualPdfPage() {
  const { site } = useSiteConfig()
  useEffect(() => {
    document.title = `Manual do Sistema — ${site.siteName} ${VERSION}`
  }, [site.siteName])

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap');

        * { box-sizing: border-box; margin: 0; padding: 0; }

        body {
          font-family: 'Inter', sans-serif;
          background: #fff;
          color: #111;
          font-size: 13px;
          line-height: 1.55;
        }

        .page { max-width: 820px; margin: 0 auto; padding: 32px 40px; }

        /* Botão — some no print */
        .print-btn {
          display: flex;
          align-items: center;
          gap: 8px;
          background: #00F0A8;
          color: #000;
          font-weight: 700;
          font-size: 13px;
          border: none;
          border-radius: 10px;
          padding: 10px 20px;
          cursor: pointer;
          margin-bottom: 32px;
        }
        .print-btn:hover { background: #00d494; }

        /* Capa */
        .capa {
          border-bottom: 3px solid #00F0A8;
          padding-bottom: 24px;
          margin-bottom: 32px;
        }
        .capa-badge {
          display: inline-block;
          background: #00F0A820;
          color: #00b87a;
          border: 1px solid #00F0A840;
          border-radius: 20px;
          padding: 3px 12px;
          font-size: 11px;
          font-weight: 700;
          letter-spacing: .05em;
          text-transform: uppercase;
          margin-bottom: 12px;
        }
        .capa h1 {
          font-size: 26px;
          font-weight: 800;
          color: #0C3D5A;
          margin-bottom: 4px;
        }
        .capa-meta {
          font-size: 12px;
          color: #6b7280;
          margin-top: 6px;
        }

        /* Índice */
        .indice { margin-bottom: 32px; }
        .indice h2 { font-size: 11px; font-weight: 700; color: #9ca3af; text-transform: uppercase; letter-spacing: .08em; margin-bottom: 10px; }
        .indice-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 4px 32px; }
        .indice-item { display: flex; align-items: center; gap: 6px; font-size: 12px; color: #374151; padding: 3px 0; }
        .indice-num { font-weight: 700; color: #9ca3af; font-size: 11px; width: 20px; }

        /* Seção */
        .secao {
          margin-bottom: 28px;
          break-inside: avoid;
          page-break-inside: avoid;
        }
        .secao-header {
          display: flex;
          align-items: center;
          gap: 10px;
          margin-bottom: 10px;
          padding-bottom: 8px;
          border-bottom: 2px solid #f3f4f6;
        }
        .secao-num {
          font-size: 11px;
          font-weight: 800;
          color: #9ca3af;
          min-width: 22px;
        }
        .secao-title {
          font-size: 15px;
          font-weight: 700;
          color: #111;
          flex: 1;
        }
        .secao-dot {
          width: 8px;
          height: 8px;
          border-radius: 50%;
          flex-shrink: 0;
        }

        /* Itens */
        .item { display: flex; gap: 10px; margin-bottom: 6px; }
        .item-bullet {
          width: 18px;
          height: 18px;
          border-radius: 50%;
          background: #f3f4f6;
          border: 1px solid #e5e7eb;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 9px;
          font-weight: 700;
          color: #9ca3af;
          flex-shrink: 0;
          margin-top: 2px;
        }
        .item-t { font-weight: 600; color: #111; font-size: 12px; }
        .item-d { color: #4b5563; font-size: 12px; margin-top: 1px; }

        /* Dicas */
        .dicas { margin-top: 8px; background: #f9fafb; border-radius: 8px; padding: 8px 12px; border-left: 3px solid #00F0A8; }
        .dicas-label { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; color: #00b87a; margin-bottom: 4px; }
        .dica { font-size: 11px; color: #4b5563; margin-bottom: 2px; }
        .dica::before { content: '→ '; color: #00b87a; font-weight: 700; }

        /* Rodapé */
        .rodape { margin-top: 40px; padding-top: 16px; border-top: 1px solid #e5e7eb; display: flex; justify-content: space-between; font-size: 11px; color: #9ca3af; }

        @media print {
          .print-btn { display: none !important; }
          body { font-size: 12px; }
          .page { padding: 16px 24px; max-width: 100%; }
          .capa h1 { font-size: 22px; }
          .secao { margin-bottom: 20px; }
          @page { margin: 1.5cm; size: A4; }
        }
      `}</style>

      <div className="page">
        {/* Botão imprimir */}
        <button className="print-btn" onClick={() => window.print()}>
          🖨️ Imprimir / Salvar como PDF
        </button>

        {/* Capa */}
        <div className="capa">
          <div className="capa-badge">Manual do Usuário</div>
          <h1>{site.siteName} — Sistema de Gestão</h1>
          <div className="capa-meta">
            Versão {VERSION} &nbsp;·&nbsp; Atualizado em {DATA} &nbsp;·&nbsp; Uso interno
          </div>
        </div>

        {/* Índice */}
        <div className="indice">
          <h2>Índice</h2>
          <div className="indice-grid">
            {SECOES.map(s => (
              <div key={s.num} className="indice-item">
                <span className="indice-num">{s.num}</span>
                <span>{s.titulo}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Seções */}
        {SECOES.map(s => (
          <div key={s.num} className="secao">
            <div className="secao-header">
              <span className="secao-num">{s.num}</span>
              <span className="secao-title">{s.titulo}</span>
              <span className="secao-dot" style={{ background: s.cor }} />
            </div>

            {s.itens.map((item, i) => (
              <div key={i} className="item">
                <span className="item-bullet">{i + 1}</span>
                <div>
                  <div className="item-t">{item.t}</div>
                  <div className="item-d">{item.d}</div>
                </div>
              </div>
            ))}

            {s.dicas && s.dicas.length > 0 && (
              <div className="dicas">
                <div className="dicas-label">Dicas</div>
                {s.dicas.map((d, i) => (
                  <div key={i} className="dica">{d}</div>
                ))}
              </div>
            )}
          </div>
        ))}

        {/* Rodapé */}
        <div className="rodape">
          <span>{site.siteName} · Sistema de Gestão Interno</span>
          <span>{VERSION} · {DATA}</span>
        </div>
      </div>
    </>
  )
}
