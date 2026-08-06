// =============================================================================
// NfceSefazGateway.cs — Implementação real da fronteira com a SEFAZ (RES-001).
//
// Só traduz: chama a lib e devolve os campos que o motor usa para decidir. Toda
// regra de decisão (o que é autorização, o que é duplicidade, o que fazer com
// resposta perdida) fica no NfceEmissionService — aqui não há política nenhuma,
// porque este é justamente o pedaço que os testes substituem.
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using CardGameStore.Services.Interfaces;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Utils;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Services.Implementations;

internal sealed class NfceSefazGateway : INfceSefazGateway
{
    public RespostaAutorizacaoNfce Autorizar(
        ConfiguracaoServico configuracao, X509Certificate2 certificado, NfeDocumento nfe)
    {
        using var servico = new ServicosNFe(configuracao, certificado);
        var retorno = servico.NFeAutorizacao(
            1, IndicadorSincronizacao.Sincrono, new List<NfeDocumento> { nfe }, false);

        return new RespostaAutorizacaoNfce(
            CStatLote: retorno.Retorno?.cStat,
            MotivoLote: retorno.Retorno?.xMotivo ?? retorno.RetornoStr,
            Protocolo: retorno.Retorno?.protNFe,
            XmlEnvio: retorno.EnvioStr,
            XmlRetorno: retorno.RetornoStr);
    }

    public RespostaConsultaChaveNfce ConsultarChave(
        ConfiguracaoServico configuracao, X509Certificate2 certificado, string chaveAcesso)
    {
        using var servico = new ServicosNFe(configuracao, certificado);
        var retorno = servico.NfeConsultaProtocolo(chaveAcesso);

        return new RespostaConsultaChaveNfce(
            CStat: retorno.Retorno?.cStat ?? 0,
            Motivo: retorno.Retorno?.xMotivo ?? retorno.RetornoStr,
            Protocolo: retorno.Retorno?.protNFe,
            XmlRetorno: retorno.RetornoStr);
    }
}
