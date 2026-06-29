namespace CardGameStore.Services.Implementations;

/// <summary>
/// Consulta NF-e endereçadas ao CNPJ do estabelecimento via SEFAZ DFe Distribuição.
/// Requer certificado digital A1 — configurado via appsettings["Sefaz:CertificatePath"].
/// Enquanto o certificado não está disponível, IsConfigured retorna false e o serviço
/// é usado apenas para fallback manual.
/// </summary>
public class SefazNfeService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SefazNfeService> _logger;

    public SefazNfeService(IConfiguration config, ILogger<SefazNfeService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config["Sefaz:CertificatePath"]) &&
        File.Exists(_config["Sefaz:CertificatePath"]!);

    /// <summary>
    /// Consulta NF-e via nfeDistDFeInteresse webservice.
    /// Retorna lista de chaves de NF-e recebidas desde lastNSU.
    /// </summary>
    public Task<IReadOnlyList<SefazNfeResult>> ConsultarAsync(string cnpj, long lastNsu = 0)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("SEFAZ NF-e: certificado não configurado. Configure Sefaz:CertificatePath no appsettings.");
            return Task.FromResult<IReadOnlyList<SefazNfeResult>>(Array.Empty<SefazNfeResult>());
        }

        // TODO: implementar chamada SOAP ao nfeDistDFeInteresse com DFe.NET após
        //       o certificado A1 estar disponível.
        throw new NotImplementedException("Integração SEFAZ aguarda certificado A1.");
    }
}

public class SefazNfeResult
{
    public string ChaveAcesso { get; set; } = "";
    public string? Emitente   { get; set; }
    public decimal Valor      { get; set; }
    public DateTime DataEmissao { get; set; }
    public long NSU           { get; set; }
}
