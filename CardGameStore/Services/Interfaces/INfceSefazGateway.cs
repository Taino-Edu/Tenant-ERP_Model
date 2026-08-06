// =============================================================================
// INfceSefazGateway.cs — Fronteira única entre o motor de emissão e a biblioteca
// que conversa com a SEFAZ (RES-001 do plano de go-live).
//
// Por que existe: a parte mais perigosa do motor fiscal é o que acontece quando
// a resposta da autorização se perde. Esse cenário não é reproduzível com a
// chamada da lib embutida no meio do método — precisaria de uma SEFAZ que
// engasga sob demanda. Com a chamada atrás desta interface, os cinco cenários
// que decidem o destino de um documento (falha antes do envio, resposta perdida
// depois da autorização, duplicidade, rejeição e retorno normal) viram teste.
//
// A interface é deliberadamente estreita: só autorização e consulta de chave —
// as duas operações que participam da máquina de estados do resultado incerto.
// Cancelamento e inutilização continuam chamando a lib diretamente, porque são
// fluxos com resposta síncrona e destino inequívoco.
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using NFe.Classes.Protocolo;
using NFe.Utils;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Services.Interfaces;

/// <summary>
/// Resposta da SEFAZ a um lote de autorização. <see cref="CStat"/> resolve a
/// precedência certa: o status do protocolo (situação DESTE documento) manda
/// sobre o status do lote (situação do envio).
/// </summary>
internal sealed record RespostaAutorizacaoNfce(
    int? CStatLote,
    string? MotivoLote,
    protNFe? Protocolo,
    string? XmlEnvio,
    string? XmlRetorno)
{
    public infProt? InfProtocolo => Protocolo?.infProt;

    public int? CStat => InfProtocolo?.cStat ?? CStatLote;

    public string? Motivo => InfProtocolo?.xMotivo ?? MotivoLote;
}

/// <summary>
/// Situação de uma chave de acesso na base da SEFAZ. É a autoridade sobre o
/// destino de um documento cuja resposta de autorização se perdeu.
/// </summary>
internal sealed record RespostaConsultaChaveNfce(
    int CStat,
    string? Motivo,
    protNFe? Protocolo,
    string? XmlRetorno);

internal interface INfceSefazGateway
{
    /// <summary>Transmite o documento assinado e devolve o que a SEFAZ respondeu.</summary>
    RespostaAutorizacaoNfce Autorizar(
        ConfiguracaoServico configuracao, X509Certificate2 certificado, NfeDocumento nfe);

    /// <summary>
    /// Pergunta à SEFAZ o que existe sob esta chave de acesso. É a única fonte
    /// legítima para decidir se um documento transmitido sem resposta foi
    /// autorizado — o estado local, nesse momento, não sabe.
    /// </summary>
    RespostaConsultaChaveNfce ConsultarChave(
        ConfiguracaoServico configuracao, X509Certificate2 certificado, string chaveAcesso);
}
