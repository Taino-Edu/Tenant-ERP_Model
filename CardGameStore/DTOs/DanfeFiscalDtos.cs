// =============================================================================
// DanfeFiscalDtos.cs — Representação imutável do DANFE NFC-e, derivada
// EXCLUSIVAMENTE do XML fiscal (nfeProc autorizado ou XML assinado offline).
//
// Por que existe (DFE-001 do plano de go-live): a montagem anterior do cupom
// relia a comanda e a FiscalConfig atuais para desenhar a via do consumidor.
// Isso significa que mudar razão social, endereço ou nome de produto alterava a
// reimpressão de uma nota antiga — o papel passava a divergir do que a SEFAZ
// autorizou. Estes records são preenchidos só pelo parser do XML; nenhum campo
// aqui tem fallback para cadastro, venda ou configuração. O que não estiver no
// XML fica null e a representação decide como exibir a ausência.
//
// Todos os valores monetários são decimais em reais, exatamente como aparecem
// no XML — não são recalculados nem reconvertidos a partir de centavos do ERP.
// =============================================================================

namespace CardGameStore.DTOs;

/// <summary>Ambiente declarado no XML (<c>ide/tpAmb</c>).</summary>
public enum DanfeAmbiente
{
    Producao = 1,
    Homologacao = 2,
}

/// <summary>Forma de emissão declarada no XML (<c>ide/tpEmis</c>).</summary>
public enum DanfeTipoEmissao
{
    Normal = 1,
    ContingenciaOffline = 9,
    /// <summary>Qualquer outra forma prevista no leiaute e não tratada especificamente.</summary>
    Outra = 0,
}

/// <summary>Situação do documento na perspectiva da representação impressa.</summary>
public enum DanfeSituacao
{
    /// <summary>Tem protocolo de autorização.</summary>
    Autorizada,
    /// <summary>Assinada e entregue offline, ainda sem protocolo.</summary>
    ContingenciaSemProtocolo,
    /// <summary>Autorizada e posteriormente cancelada por evento.</summary>
    Cancelada,
}

public sealed record DanfeEnderecoDto(
    string? Logradouro, string? Numero, string? Complemento, string? Bairro,
    string? Municipio, string? Uf, string? Cep)
{
    /// <summary>Endereço em uma linha, omitindo as partes ausentes.</summary>
    public string Linha => string.Join(", ", new[]
    {
        string.Join(" ", new[] { Logradouro, Numero }.Where(p => !string.IsNullOrWhiteSpace(p))),
        Complemento, Bairro,
        string.Join("/", new[] { Municipio, Uf }.Where(p => !string.IsNullOrWhiteSpace(p))),
    }.Where(p => !string.IsNullOrWhiteSpace(p)));
}

public sealed record DanfeEmitenteDto(
    string? Cnpj, string? RazaoSocial, string? NomeFantasia,
    string? InscricaoEstadual, DanfeEnderecoDto Endereco);

/// <summary>
/// Consumidor. Em NFC-e o grupo <c>dest</c> é opcional: quando ausente, a via
/// precisa dizer que o consumidor não foi identificado — omitir a divisão não é
/// equivalente a declarar a ausência (DFE-004).
/// </summary>
public sealed record DanfeConsumidorDto(
    string? Cpf, string? Cnpj, string? Nome, DanfeEnderecoDto? Endereco)
{
    public bool Identificado => !string.IsNullOrWhiteSpace(Cpf)
                             || !string.IsNullOrWhiteSpace(Cnpj)
                             || !string.IsNullOrWhiteSpace(Nome);

    public static readonly DanfeConsumidorDto NaoIdentificado = new(null, null, null, null);
}

public sealed record DanfeItemDto(
    int Numero,
    string? Codigo,
    string? Descricao,
    string? Ncm,
    string? Cfop,
    string? UnidadeComercial,
    decimal Quantidade,
    decimal ValorUnitario,
    decimal ValorTotal,
    decimal Desconto,
    string? Gtin,
    // Tributos aproximados do item (imposto/vTotTrib), quando informados.
    decimal? TributosAproximados);

public sealed record DanfeTotaisDto(
    // Quantidade de itens da nota — divisão obrigatória do manual.
    int QuantidadeItens,
    decimal ValorProdutos,
    decimal ValorDesconto,
    decimal ValorTotal,
    decimal? TributosAproximados);

/// <summary>
/// Um grupo <c>detPag</c>. O código vem do XML; a descrição legível é
/// responsabilidade da representação, não do parser — traduzir aqui esconderia
/// um código errado atrás de um rótulo bonito.
/// </summary>
public sealed record DanfePagamentoDto(
    string CodigoTPag, decimal Valor, string? DescricaoXPag, string? Bandeira, string? Autorizacao);

/// <summary>Dados que só existem em emissão offline (<c>tpEmis=9</c>).</summary>
public sealed record DanfeContingenciaDto(DateTime? DataHora, string? Justificativa);

public sealed record DanfeProtocoloDto(string? Numero, DateTime? DataHora, string? Status, string? Motivo);

/// <summary>
/// Documento completo pronto para renderização. Serve tanto ao HTML impresso
/// pelo navegador quanto a um renderizador externo de PDF: a escolha do canal
/// (seção 25.7 do plano) não muda este contrato.
/// </summary>
public sealed record DanfeFiscalDto(
    string? ChaveAcesso,
    DanfeAmbiente Ambiente,
    DanfeTipoEmissao TipoEmissao,
    DanfeSituacao Situacao,
    int Serie,
    int Numero,
    DateTime? EmitidoEm,
    string? NaturezaOperacao,
    DanfeEmitenteDto Emitente,
    DanfeConsumidorDto Consumidor,
    IReadOnlyList<DanfeItemDto> Itens,
    DanfeTotaisDto Totais,
    IReadOnlyList<DanfePagamentoDto> Pagamentos,
    decimal? Troco,
    DanfeProtocoloDto? Protocolo,
    DanfeContingenciaDto? Contingencia,
    string? InformacoesComplementares,
    // URL completa do QR Code, exatamente como está em infNFeSupl/qrCode.
    string? QrCodeUrl,
    // URL de consulta por chave de acesso (infNFeSupl/urlChave).
    string? UrlConsultaChave)
{
    /// <summary>
    /// Em homologação o manual exige aviso destacado de que o documento não tem
    /// valor fiscal. É diferente do <c>xProd</c> especial do primeiro item, que a
    /// SEFAZ obriga dentro do XML — os dois convivem e nenhum substitui o outro.
    /// </summary>
    public bool ExigeAvisoSemValorFiscal => Ambiente == DanfeAmbiente.Homologacao;

    public bool EmContingencia => TipoEmissao == DanfeTipoEmissao.ContingenciaOffline;
}
