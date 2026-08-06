'use client'
// =============================================================================
// DanfeNfce.tsx — Representação do DANFE NFC-e, alimentada exclusivamente pelo
// DTO que o backend deriva do XML fiscal (opção C da seção 25.7 do plano).
//
// Nada aqui consulta cadastro, venda ou configuração: se o campo não veio no
// DTO, ele não veio no XML, e a ausência é mostrada como ausência — nunca
// preenchida com o valor "atual" de outra fonte. Foi essa substituição
// silenciosa que fazia a reimpressão divergir do documento autorizado.
//
// Usado pelo admin e pela área do cliente: uma fonte, uma representação.
// =============================================================================
import { useEffect, useState } from 'react'
import QRCode from 'qrcode'
import type { DanfeFiscalDto, DanfePagamentoDto } from '@/lib/api'

/** Larguras de bobina térmica suportadas. */
export type LarguraBobina = 58 | 80

const fmtMoeda = (v: number) =>
  v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

/** Quantidade com até 4 casas, sem zeros à toa (o XML manda "1.0000"). */
const fmtQtd = (v: number) =>
  v.toLocaleString('pt-BR', { maximumFractionDigits: 4 })

const fmtDataHora = (iso?: string) =>
  iso ? new Date(iso).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' }) : '—'

/** Agrupa a chave de 44 dígitos de 4 em 4, como manda o manual. */
const fmtChave = (chave?: string) =>
  chave ? (chave.match(/.{1,4}/g)?.join(' ') ?? chave) : ''

/**
 * Rótulo do meio de pagamento. A tradução vive AQUI, não no parser: o DTO sobe
 * o `tPag` cru para que um código errado apareça como código errado, e não
 * escondido atrás de um nome bonito. Códigos desconhecidos mostram o número.
 */
const TPAG_LABEL: Record<string, string> = {
  '01': 'Dinheiro',
  '02': 'Cheque',
  '03': 'Cartão de crédito',
  '04': 'Cartão de débito',
  '05': 'Crediário da loja',
  '10': 'Vale alimentação',
  '11': 'Vale refeição',
  '12': 'Vale presente',
  '13': 'Vale combustível',
  '15': 'Boleto',
  '16': 'Depósito bancário',
  '17': 'Pix dinâmico',
  '18': 'Transferência / carteira digital',
  '19': 'Cashback / fidelidade',
  '20': 'Pix estático',
  '21': 'Crédito em loja',
  '22': 'Pagamento eletrônico',
  '90': 'Sem pagamento',
  '99': 'Outros',
}

function rotuloPagamento(p: DanfePagamentoDto) {
  const base = TPAG_LABEL[p.codigoTPag] ?? `Meio ${p.codigoTPag}`
  // xPag só é exigido no 99, mas quando vier acrescenta contexto útil.
  return p.descricaoXPag && p.codigoTPag === '99' ? `${base} — ${p.descricaoXPag}` : base
}

export default function DanfeNfce({ danfe, largura = 80 }: {
  danfe: DanfeFiscalDto
  largura?: LarguraBobina
}) {
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!danfe.qrCodeUrl) { setQrDataUrl(null); return }
    // O conteúdo vem pronto do XML — aqui só vira imagem. Remontar a URL
    // exigiria CSC e é a origem da rejeição 397 (QR divergente da nota).
    QRCode.toDataURL(danfe.qrCodeUrl, { width: largura === 58 ? 130 : 170, margin: 0 })
      .then(setQrDataUrl)
      .catch(() => setQrDataUrl(null))
  }, [danfe.qrCodeUrl, largura])

  const cancelada = danfe.situacao === 'Cancelada'

  // CSS via dangerouslySetInnerHTML e nao como filho de <style>: o React escapa
  // aspas no HTML do servidor ("Courier New" vira &quot;) e nao no cliente, o
  // que quebrava a hidratacao e fazia o React substituir o documento inteiro.
  const css = `
        /* A bobina define a página: sem isto o navegador imprime em A4 e a
           térmica corta ou desperdiça papel. */
        @page { size: ${largura}mm auto; margin: 2mm; }
        @media print {
          .danfe-nao-imprime { display: none !important; }
          html, body { background: #fff !important; margin: 0 !important; padding: 0 !important; }
          .danfe { width: auto !important; margin: 0 !important; box-shadow: none !important; }
        }
        .danfe {
          width: ${largura}mm;
          font-family: ui-monospace, "Courier New", monospace;
          font-size: ${largura === 58 ? 9 : 11}px;
          line-height: 1.35;
          color: #000;
          background: #fff;
          padding: 3mm;
          margin: 16px auto;
          box-shadow: 0 1px 8px rgba(0,0,0,.15);
        }
        .danfe hr { border: none; border-top: 1px dashed #000; margin: 4px 0; }
        .danfe .centro { text-align: center; }
        .danfe .forte { font-weight: 700; }
        .danfe .linha { display: flex; justify-content: space-between; gap: 6px; }
        .danfe .aviso {
          border: 1px solid #000; padding: 3px; margin: 4px 0;
          text-align: center; font-weight: 700; text-transform: uppercase;
        }
        .danfe table { width: 100%; border-collapse: collapse; }
        .danfe th, .danfe td { text-align: left; padding: 1px 0; vertical-align: top; }
        .danfe .num { text-align: right; white-space: nowrap; }
`

  return (
    <>
      <style dangerouslySetInnerHTML={{ __html: css }} />

      <div className="danfe">
        {/* I — Emitente, do XML */}
        <div className="centro forte">{danfe.emitente.razaoSocial ?? '—'}</div>
        {danfe.emitente.cnpj && <div className="centro">CNPJ: {danfe.emitente.cnpj}</div>}
        {danfe.emitente.inscricaoEstadual && <div className="centro">IE: {danfe.emitente.inscricaoEstadual}</div>}
        <div className="centro">{danfe.emitente.endereco.linha}</div>
        <hr />

        {/* II — Identificação do documento */}
        <div className="centro forte">
          DANFE NFC-e — Documento Auxiliar da<br />Nota Fiscal de Consumidor Eletrônica
        </div>
        <div className="centro">Não permite aproveitamento de crédito de ICMS</div>

        {/* Homologação: exigência do manual, distinta do xProd especial do XML */}
        {danfe.exigeAvisoSemValorFiscal && (
          <div className="aviso">Emitida em ambiente de homologação — sem valor fiscal</div>
        )}
        {cancelada && <div className="aviso">Documento cancelado</div>}
        {danfe.emContingencia && (
          <div className="aviso">
            Emitida em contingência offline
            {!danfe.protocolo && <><br />Pendente de autorização</>}
          </div>
        )}
        <hr />

        {/* III — Itens */}
        <table>
          <thead>
            <tr>
              <th>Cód</th><th>Descrição</th>
              <th className="num">Qtd</th><th className="num">Un</th>
              <th className="num">Vl un</th><th className="num">Total</th>
            </tr>
          </thead>
          <tbody>
            {danfe.itens.map(item => (
              <tr key={item.numero}>
                <td>{item.codigo ?? item.numero}</td>
                <td>{item.descricao ?? '—'}</td>
                <td className="num">{fmtQtd(item.quantidade)}</td>
                <td className="num">{item.unidadeComercial ?? ''}</td>
                <td className="num">{fmtMoeda(item.valorUnitario)}</td>
                <td className="num">{fmtMoeda(item.valorTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <hr />

        {/* IV — Totais */}
        <div className="linha"><span>Qtd. total de itens</span><span>{danfe.totais.quantidadeItens}</span></div>
        <div className="linha"><span>Valor total dos produtos</span><span>{fmtMoeda(danfe.totais.valorProdutos)}</span></div>
        {danfe.totais.valorDesconto > 0 && (
          <div className="linha"><span>Descontos</span><span>−{fmtMoeda(danfe.totais.valorDesconto)}</span></div>
        )}
        <div className="linha forte"><span>VALOR A PAGAR</span><span>{fmtMoeda(danfe.totais.valorTotal)}</span></div>
        <hr />

        {/* V — Pagamentos, um por grupo detPag */}
        <div className="forte">FORMA DE PAGAMENTO</div>
        {danfe.pagamentos.length === 0 && <div>—</div>}
        {danfe.pagamentos.map((p, i) => (
          <div className="linha" key={`${p.codigoTPag}-${i}`}>
            <span>{rotuloPagamento(p)}</span>
            <span>{fmtMoeda(p.valor)}</span>
          </div>
        ))}
        {danfe.troco != null && danfe.troco > 0 && (
          <div className="linha"><span>Troco</span><span>{fmtMoeda(danfe.troco)}</span></div>
        )}
        <hr />

        {/* VI — Consumidor: ausência precisa ser declarada, não omitida */}
        <div className="centro">
          {danfe.consumidor.identificado ? (
            <>
              {danfe.consumidor.cpf && <div>CPF: {danfe.consumidor.cpf}</div>}
              {danfe.consumidor.cnpj && <div>CNPJ: {danfe.consumidor.cnpj}</div>}
              {danfe.consumidor.nome && <div>{danfe.consumidor.nome}</div>}
              {danfe.consumidor.endereco && <div>{danfe.consumidor.endereco.linha}</div>}
            </>
          ) : (
            <div className="forte">CONSUMIDOR NÃO IDENTIFICADO</div>
          )}
        </div>
        <hr />

        {/* VII — Identificação da NFC-e e protocolo */}
        <div className="centro">
          Número {danfe.numero} — Série {danfe.serie}<br />
          Emissão: {fmtDataHora(danfe.emitidoEm)}
        </div>
        {danfe.protocolo?.numero && (
          <div className="centro">
            Protocolo de autorização: {danfe.protocolo.numero}<br />
            {fmtDataHora(danfe.protocolo.dataHora)}
          </div>
        )}
        {danfe.contingencia && (
          <div className="centro">
            Contingência em {fmtDataHora(danfe.contingencia.dataHora)}<br />
            {danfe.contingencia.justificativa}
          </div>
        )}
        <hr />

        {/* VIII — Consulta e chave */}
        <div className="centro">Consulte pela chave de acesso em:</div>
        {danfe.urlConsultaChave && <div className="centro">{danfe.urlConsultaChave}</div>}
        <div className="centro" style={{ wordBreak: 'break-all' }}>{fmtChave(danfe.chaveAcesso)}</div>

        {/* IX — QR Code */}
        {qrDataUrl && (
          <div className="centro" style={{ marginTop: 6 }}>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={qrDataUrl} alt="QR Code da NFC-e" />
          </div>
        )}

        {/* X — Mensagens do XML (tributos da Lei 12.741, entre outras) */}
        {danfe.informacoesComplementares && (
          <>
            <hr />
            <div>{danfe.informacoesComplementares}</div>
          </>
        )}

        {/* Texto institucional: fora do DANFE, separado, para não parecer
            conteúdo fiscal originado do XML (DFE-008). */}
        <hr />
        <div className="centro" style={{ fontSize: '.9em' }}>Documento emitido eletronicamente</div>
      </div>
    </>
  )
}
