// =============================================================================
// FiscalConfigService.cs — Leitura e escrita da configuração fiscal do tenant.
//
// Extraído de FiscalController quando o portal do contador passou a poder
// editar a mesma configuração: são duas portas de entrada (o lojista em
// /admin/fiscal e o contador em /contador) para a MESMA linha singleton, com as
// mesmas regras — bloqueio de regime incompatível com CSOSN, guarda de
// titularidade do certificado ao ligar Produção, criptografia de CSC/IBPT.
// Duas cópias dessa lógica divergiriam, e divergir aqui significa emitir nota
// fiscal inválida ou em nome de terceiro.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>Resultado de uma escrita: <c>Erro</c> null significa sucesso.</summary>
public record FiscalConfigResultado(FiscalConfig? Config, string? Erro)
{
    public bool Ok => Erro is null;
    public static FiscalConfigResultado Falha(string mensagem) => new(null, mensagem);
    public static FiscalConfigResultado Sucesso(FiscalConfig cfg) => new(cfg, null);
}

public class FiscalConfigService
{
    private readonly AppDbContext             _db;
    private readonly EncryptionService        _enc;
    private readonly FiscalCertificadoService _certificado;
    private readonly ILogger<FiscalConfigService> _logger;

    public FiscalConfigService(
        AppDbContext db, EncryptionService enc, FiscalCertificadoService certificado,
        ILogger<FiscalConfigService> logger)
    {
        _db          = db;
        _enc         = enc;
        _certificado = certificado;
        _logger      = logger;
    }

    /// <summary>
    /// Busca a linha única de configuração fiscal pelo ID fixo, criando-a se necessário.
    /// Como o ID é fixo, uma segunda inserção concorrente vira uma violação de PK —
    /// nesse caso, descarta a tentativa local e relê a linha que a outra requisição criou.
    /// </summary>
    public async Task<FiscalConfig> GetOrCreateAsync()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        if (cfg is not null) return cfg;

        cfg = new FiscalConfig();
        _db.FiscalConfigs.Add(cfg);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(cfg).State = EntityState.Detached;
            cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId)
                ?? throw new InvalidOperationException("Falha ao obter configuração fiscal após conflito de concorrência.");
        }

        return cfg;
    }

    /// <summary>
    /// Aplica um update parcial: qualquer campo null/omitido mantém o valor atual.
    /// Não persiste nada quando alguma validação falha.
    /// </summary>
    public async Task<FiscalConfigResultado> SalvarAsync(SaveFiscalConfigRequest req)
    {
        var cfg = await GetOrCreateAsync();

        if (req.Cnpj is not null)
            cfg.Cnpj = Cnpj.Normalizar(req.Cnpj);
        if (req.RazaoSocial       is not null) cfg.RazaoSocial       = req.RazaoSocial;
        if (req.InscricaoEstadual is not null) cfg.InscricaoEstadual = req.InscricaoEstadual;
        if (req.EmailContador     is not null) cfg.EmailContador     = req.EmailContador;
        if (req.SerieNfce.HasValue)            cfg.SerieNfce         = req.SerieNfce.Value;

        if (req.Logradouro          is not null) cfg.Logradouro          = req.Logradouro;
        if (req.Numero              is not null) cfg.Numero              = req.Numero;
        if (req.Complemento         is not null) cfg.Complemento         = req.Complemento;
        if (req.Bairro              is not null) cfg.Bairro              = req.Bairro;
        if (req.CodigoMunicipioIbge is not null) cfg.CodigoMunicipioIbge = req.CodigoMunicipioIbge;
        if (req.Municipio           is not null) cfg.Municipio           = req.Municipio;
        if (req.Uf                  is not null) cfg.Uf                  = req.Uf.ToUpperInvariant();
        if (req.Cep                 is not null) cfg.Cep                 = new string(req.Cep.Where(char.IsDigit).ToArray());
        // Campos que o usuário digita e que têm coluna curta. Sem esta checagem o
        // valor grande demais só é recusado pelo PostgreSQL (22001), já dentro do
        // SaveChanges — vira DbUpdateException, sobe sem tratamento e o admin
        // recebe "Erro interno. Tente novamente em instantes", que é falso: tentar
        // de novo dá exatamente o mesmo resultado, e a mensagem não diz qual campo
        // está errado nem por quê.
        var excedido = PrimeiroCampoExcedido(req);
        if (excedido is not null) return FiscalConfigResultado.Falha(excedido);

        if (req.CscId               is not null) cfg.CscId               = req.CscId.Trim();
        // CSC e IBPT são segredos independentes, ambos criptografados por tenant.
        if (req.CscToken            is not null) cfg.CscTokenEncrypted   = _enc.Encrypt(req.CscToken);

        if (req.RemoverIbptToken == true)
        {
            cfg.IbptTokenEncrypted = null;
            cfg.IbptAutoSyncEnabled = false;
            cfg.IbptUltimoErro = null;
        }
        else if (!string.IsNullOrWhiteSpace(req.IbptToken))
        {
            cfg.IbptTokenEncrypted = _enc.Encrypt(req.IbptToken.Trim());
            cfg.IbptUltimoErro = null;
        }
        if (req.IbptAutoSyncEnabled.HasValue)
        {
            if (req.IbptAutoSyncEnabled.Value && string.IsNullOrWhiteSpace(cfg.IbptTokenEncrypted))
                return FiscalConfigResultado.Falha("Configure o token IBPT antes de ativar o preenchimento automático.");
            cfg.IbptAutoSyncEnabled = req.IbptAutoSyncEnabled.Value;
        }

        if (req.RegimeTributario is not null)
        {
            if (!Enum.TryParse<RegimeTributario>(req.RegimeTributario, out var regime))
                return FiscalConfigResultado.Falha($"Regime tributário \"{req.RegimeTributario}\" inválido.");

            // Os três regimes são aceitos aqui: o regime é dado cadastral da
            // empresa e alimenta a apuração contábil (DRE, comparativo Simples x
            // Presumido, fechamento) — travar a escrita impedia o contador de
            // registrar a realidade de um cliente que simplesmente não é do
            // Simples.
            //
            // O que continua bloqueado é EMITIR: a montagem de itens
            // (NfceEmissionService.MontarIcmsSimplesNacional) só gera classes
            // ICMSSN* (CSOSN), e fora do Simples o XML exigiria CST de ICMS
            // normal, que este sistema não calcula — a nota seria rejeitada pela
            // SEFAZ. Essa recusa vive no próprio momento da emissão
            // (NfceEmissionService.CarregarContextoAsync), com mensagem própria,
            // e não aqui: é lá que o dado errado causaria dano.
            cfg.RegimeTributario = regime;
        }

        // RTC-001: as duas condições que o regime declarado não revela. Só fazem
        // sentido para optantes do Simples — fora dele o perfil já é RegimeNormal
        // e estes campos não são consultados.
        if (req.ExcedeuSublimiteSimples.HasValue)
            cfg.ExcedeuSublimiteSimples = req.ExcedeuSublimiteSimples.Value;
        if (req.OptouRegimeRegularIbsCbs.HasValue)
            cfg.OptouRegimeRegularIbsCbs = req.OptouRegimeRegularIbsCbs.Value;

        // ── Parâmetros de apuração (não entram no XML) ────────────────────────
        if (req.AnexoSimples is not null)
        {
            if (!Enum.TryParse<AnexoSimplesNacional>(req.AnexoSimples, out var anexo))
                return FiscalConfigResultado.Falha($"Anexo do Simples \"{req.AnexoSimples}\" inválido (use I a V).");
            cfg.AnexoSimples = anexo;
        }
        if (req.FolhaPagamento12mEmCentavos.HasValue)
        {
            if (req.FolhaPagamento12mEmCentavos.Value < 0)
                return FiscalConfigResultado.Falha("A folha de 12 meses não pode ser negativa.");
            cfg.FolhaPagamento12mEmCentavos = req.FolhaPagamento12mEmCentavos.Value;
        }
        if (req.FolhaPagamentoMensalEmCentavos.HasValue)
        {
            if (req.FolhaPagamentoMensalEmCentavos.Value < 0)
                return FiscalConfigResultado.Falha("A folha mensal não pode ser negativa.");
            cfg.FolhaPagamentoMensalEmCentavos = req.FolhaPagamentoMensalEmCentavos.Value;
        }

        var percentuais = new (decimal? Valor, string Nome)[]
        {
            (req.PercentualPresuncaoIrpj, "presunção do IRPJ"),
            (req.PercentualPresuncaoCsll, "presunção da CSLL"),
            (req.AliquotaIcmsPercentual,  "alíquota de ICMS"),
            (req.AliquotaIssPercentual,   "alíquota de ISS"),
        };
        foreach (var (valor, nome) in percentuais)
            if (valor is < 0 or > 100)
                return FiscalConfigResultado.Falha($"O percentual de {nome} precisa ficar entre 0 e 100.");

        if (req.PercentualPresuncaoIrpj.HasValue) cfg.PercentualPresuncaoIrpj = req.PercentualPresuncaoIrpj.Value;
        if (req.PercentualPresuncaoCsll.HasValue) cfg.PercentualPresuncaoCsll = req.PercentualPresuncaoCsll.Value;
        if (req.AliquotaIcmsPercentual.HasValue)  cfg.AliquotaIcmsPercentual  = req.AliquotaIcmsPercentual.Value;
        if (req.AliquotaIssPercentual.HasValue)   cfg.AliquotaIssPercentual   = req.AliquotaIssPercentual.Value;

        var ambienteFinal = cfg.Ambiente;
        if (req.Ambiente is not null &&
            Enum.TryParse<AmbienteFiscal>(req.Ambiente, out var ambiente))
            ambienteFinal = ambiente;

        // Homologação não tem valor fiscal, então só Produção precisa de guarda:
        // é onde a nota existe de verdade e emitir com certificado de outra
        // empresa vira uso indevido. Cobre as duas portas — ligar Produção, e
        // trocar o CNPJ com Produção já ligada (cfg.Cnpj acima já é o novo).
        if (ambienteFinal == AmbienteFiscal.Producao &&
            (cfg.Ambiente != AmbienteFiscal.Producao || req.Cnpj is not null))
        {
            var erro = ValidarTitularidadeDoCertificado(cfg);
            if (erro is not null) return FiscalConfigResultado.Falha(erro);
        }
        cfg.Ambiente = ambienteFinal;

        if (req.FormasPagamentoAutoEmissao is not null)
        {
            var invalidas = req.FormasPagamentoAutoEmissao.Where(f => !PaymentMethod.IsValid(f)).ToList();
            if (invalidas.Count > 0)
                return FiscalConfigResultado.Falha($"Forma(s) de pagamento inválida(s): {string.Join(", ", invalidas)}.");

            cfg.FormasPagamentoAutoEmissao = string.Join(",", req.FormasPagamentoAutoEmissao.Distinct());
        }

        cfg.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Rede de segurança para o que a checagem acima não previu. Qualquer
            // recusa do banco aqui é dado do usuário, não falha do servidor —
            // devolver 500 faria o admin tentar de novo indefinidamente, porque
            // "Erro interno. Tente novamente em instantes" sugere transitório e
            // isto é determinístico.
            _db.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Configuração fiscal recusada pelo banco ao salvar.");
            return FiscalConfigResultado.Falha(
                "Não foi possível salvar: algum campo excede o tamanho aceito ou tem formato inválido. " +
                "Revise CSC, CNPJ, inscrição estadual e endereço.");
        }

        return FiscalConfigResultado.Sucesso(cfg);
    }

    /// <summary>
    /// Valida e guarda o certificado A1. Um .pfx inválido (senha errada, expirado,
    /// de outro CNPJ) é recusado sem alterar a configuração atual.
    /// </summary>
    public async Task<(string? Erro, CertificadoInfo? Info)> SalvarCertificadoAsync(byte[] pfxBytes, string senha)
    {
        if (pfxBytes.Length == 0)
            return ("Arquivo de certificado (.pfx) inválido ou vazio.", null);
        if (string.IsNullOrWhiteSpace(senha))
            return ("Informe a senha do certificado.", null);

        CertificadoInfo info;
        try
        {
            info = _certificado.Validar(pfxBytes, senha);
        }
        catch (CertificadoInvalidoException ex)
        {
            return (ex.Message, null);
        }

        var cfg = await GetOrCreateAsync();

        // A NFC-e é assinada com este certificado e o emitente do XML é o CNPJ da
        // loja. Se forem CNPJs diferentes, ou a SEFAZ rejeita, ou — se o CNPJ da
        // loja tiver sido preenchido com o do dono do certificado — a loja emite
        // nota fiscal real em nome de terceiro. Barra antes de guardar o .pfx.
        var cnpjLoja   = Cnpj.Normalizar(cfg.Cnpj);
        var emProducao = cfg.Ambiente == AmbienteFiscal.Producao;

        // Em Produção falha fechada: trocar o certificado por um de titular
        // desconhecido não pode ser um jeito de contornar a guarda do ambiente.
        // Em Homologação (onde a nota não tem valor fiscal) só barra o que dá pra
        // afirmar que está errado, pra não travar quem ainda vai preencher o CNPJ.
        if (emProducao && info.Cnpj is null)
            return ("Não foi possível identificar o CNPJ do titular no certificado. " +
                    "Com a loja em Produção, só é aceito certificado e-CNPJ A1 do próprio emitente.", null);

        if (info.Cnpj is not null && cnpjLoja.Length == 14 && info.Cnpj != cnpjLoja)
            return ($"O certificado pertence ao CNPJ {Cnpj.Formatar(info.Cnpj)}, mas a loja está " +
                    $"configurada como {Cnpj.Formatar(cnpjLoja)}. Envie o certificado do próprio " +
                    "emitente — assinar NFC-e com certificado de outra empresa é uso indevido e a " +
                    "SEFAZ rejeita a nota.", null);

        cfg.CertificadoPfxEncrypted       = _enc.Encrypt(Convert.ToBase64String(pfxBytes));
        cfg.CertificadoSenhaEncrypted     = _enc.Encrypt(senha);
        cfg.CertificadoValidade           = info.NotAfter;
        cfg.CertificadoUploadedAt         = DateTime.UtcNow;
        cfg.CertificadoUltimoAlertaLimiar = null; // reseta o ciclo de alertas pro novo certificado
        cfg.UpdatedAt                     = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (null, info);
    }

    /// <summary>
    /// Confere se o certificado guardado é do mesmo CNPJ da loja. Reabre o .pfx
    /// em vez de guardar o CNPJ numa coluna: a virada de ambiente é rara e assim
    /// não precisa de migration nem de manter dado derivado sincronizado.
    /// </summary>
    public string? ValidarTitularidadeDoCertificado(FiscalConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.CertificadoPfxEncrypted) ||
            string.IsNullOrWhiteSpace(cfg.CertificadoSenhaEncrypted))
            return "Envie o certificado digital A1 antes de ligar o ambiente de Produção.";

        CertificadoInfo info;
        try
        {
            info = _certificado.Validar(
                Convert.FromBase64String(_enc.Decrypt(cfg.CertificadoPfxEncrypted)),
                _enc.Decrypt(cfg.CertificadoSenhaEncrypted));
        }
        catch (CertificadoInvalidoException ex)
        {
            return $"Certificado inválido para Produção: {ex.Message}";
        }

        var cnpjLoja = Cnpj.Normalizar(cfg.Cnpj);
        if (cnpjLoja.Length != 14)
            return "Configure o CNPJ da loja antes de ligar o ambiente de Produção.";

        // Falha fechada: se não dá pra dizer de quem é o certificado (e-CPF, Subject
        // fora do padrão da ICP-Brasil), a titularidade não foi provada — e provar a
        // titularidade é a única razão desta checagem existir.
        if (info.Cnpj is null)
            return "Não foi possível identificar o CNPJ do titular no certificado. " +
                   "Produção exige um certificado e-CNPJ A1 do próprio emitente.";

        if (info.Cnpj != cnpjLoja)
            return $"O certificado instalado é do CNPJ {Cnpj.Formatar(info.Cnpj)} e a loja emite como " +
                   $"{Cnpj.Formatar(cnpjLoja)}. Em Produção a nota tem valor fiscal — emitir com " +
                   "certificado de outra empresa é uso indevido. Instale o certificado do emitente.";

        return null;
    }

    /// <summary>Projeção pública da configuração — nunca inclui senha do certificado, CSC token ou token IBPT.</summary>
    public static object ToDto(FiscalConfig? cfg)
    {
        cfg ??= new FiscalConfig();

        int? diasParaVencer = cfg.CertificadoValidade.HasValue
            ? (int)(cfg.CertificadoValidade.Value.Date - DateTime.UtcNow.Date).TotalDays
            : null;

        return new
        {
            cfg.Cnpj,
            cfg.RazaoSocial,
            cfg.InscricaoEstadual,
            cfg.Logradouro,
            cfg.Numero,
            cfg.Complemento,
            cfg.Bairro,
            cfg.CodigoMunicipioIbge,
            cfg.Municipio,
            cfg.Uf,
            cfg.Cep,
            CscConfigurado = !string.IsNullOrWhiteSpace(cfg.CscId) && !string.IsNullOrWhiteSpace(cfg.CscTokenEncrypted),
            cfg.CscId, // não sensível isoladamente; o token nunca é retornado
            RegimeTributario = cfg.RegimeTributario.ToString(),
            cfg.ExcedeuSublimiteSimples,
            cfg.OptouRegimeRegularIbsCbs,
            PerfilIbsCbs = CatalogoRegrasIbsCbs.PerfilDe(cfg).ToString(),
            Ambiente         = cfg.Ambiente.ToString(),
            cfg.SerieNfce,
            cfg.ProximoNumeroNfce,
            cfg.EmailContador,
            cfg.CertificadoConfigurado,
            cfg.CertificadoValidade,
            DiasParaVencer = diasParaVencer,
            FormasPagamentoAutoEmissao = string.IsNullOrWhiteSpace(cfg.FormasPagamentoAutoEmissao)
                ? Array.Empty<string>()
                : cfg.FormasPagamentoAutoEmissao.Split(',', StringSplitOptions.RemoveEmptyEntries),
            IbptConfigurado = cfg.IbptConfigurado,
            cfg.IbptAutoSyncEnabled,
            cfg.IbptUltimaSincronizacao,
            cfg.IbptUltimaVersao,
            cfg.IbptVigenciaInicio,
            cfg.IbptVigenciaFim,
            cfg.IbptUltimoErro,
            // Parâmetros de apuração — usados pelo comparativo Simples x Presumido.
            AnexoSimples = cfg.AnexoSimples.ToString(),
            cfg.FolhaPagamento12mEmCentavos,
            cfg.FolhaPagamentoMensalEmCentavos,
            cfg.PercentualPresuncaoIrpj,
            cfg.PercentualPresuncaoCsll,
            cfg.AliquotaIcmsPercentual,
            cfg.AliquotaIssPercentual,
        };
    }

    /// <summary>
    /// Limites das colunas correspondentes. Manter em sincronia com FiscalConfig —
    /// duplicar o número aqui é pior do que a alternativa, que é o usuário
    /// descobrir o limite por 500 sem mensagem.
    /// </summary>
    private static readonly (string Campo, int Limite, Func<SaveFiscalConfigRequest, string?> Ler)[] LimitesDeTexto =
    {
        ("ID do CSC",              10,  r => r.CscId),
        ("CNPJ",                   18,  r => r.Cnpj),
        ("Razão social",           150, r => r.RazaoSocial),
        ("Inscrição estadual",     20,  r => r.InscricaoEstadual),
        ("Logradouro",             150, r => r.Logradouro),
        ("Número",                 20,  r => r.Numero),
        ("Complemento",            100, r => r.Complemento),
        ("Bairro",                 100, r => r.Bairro),
        ("Município",              100, r => r.Municipio),
        ("Código IBGE do município", 7, r => r.CodigoMunicipioIbge),
        ("UF",                     2,   r => r.Uf),
        ("CEP",                    9,   r => r.Cep),
        ("E-mail do contador",     200, r => r.EmailContador),
    };

    private static string? PrimeiroCampoExcedido(SaveFiscalConfigRequest req)
    {
        foreach (var (campo, limite, ler) in LimitesDeTexto)
        {
            var valor = ler(req)?.Trim();
            if (valor is not null && valor.Length > limite)
                return $"{campo}: máximo de {limite} caracteres (você informou {valor.Length}).";
        }
        return null;
    }
}
