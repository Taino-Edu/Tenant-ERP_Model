using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/contas-receber")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class ContasReceberController : ControllerBase
{
    private readonly AppDbContext       _db;
    private readonly OfxParserService   _ofx;
    private readonly SefazNfeService    _sefaz;
    private readonly EncryptionService  _enc;
    private readonly InterSyncService   _inter;
    private readonly IConfiguration     _config;

    public ContasReceberController(AppDbContext db, OfxParserService ofx, SefazNfeService sefaz, EncryptionService enc, InterSyncService inter, IConfiguration config)
    {
        _db     = db;
        _ofx    = ofx;
        _sefaz  = sefaz;
        _enc    = enc;
        _inter  = inter;
        _config = config;
    }

    /// <summary>
    /// Lista lançamentos financeiros (contas a pagar/receber) com filtros e
    /// paginação. Antes de listar, marca automaticamente como "overdue" qualquer
    /// lançamento pendente com vencimento já passado.
    /// </summary>
    /// <param name="type">Filtra por tipo: "income" (a receber) ou "expense" (a pagar).</param>
    /// <param name="status">Filtra por status: "pending", "paid", "overdue" ou "cancelled".</param>
    /// <param name="source">Filtra pela origem do lançamento (ex: "manual", "ofx", "inter", "sefaz").</param>
    /// <param name="search">Busca por texto na descrição ou no fornecedor.</param>
    /// <param name="from">Data inicial (vencimento ou criação).</param>
    /// <param name="to">Data final (vencimento ou criação).</param>
    /// <param name="page">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Registros por página (padrão 50).</param>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type     = null,   // "income" | "expense"
        [FromQuery] string? status   = null,   // "pending" | "paid" | "overdue" | "cancelled"
        [FromQuery] string? source   = null,
        [FromQuery] string? search   = null,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50)
    {
        var q = _db.ExternalTransactions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))   q = q.Where(t => t.Type   == type);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(t => t.Source == source);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(t => t.Description.Contains(search) ||
                              (t.Supplier != null && t.Supplier.Contains(search)));

        if (DateTime.TryParse(from, out var dtFrom)) q = q.Where(t => t.DueDate >= dtFrom || t.CreatedAt >= dtFrom);
        if (DateTime.TryParse(to,   out var dtTo))   q = q.Where(t => t.DueDate <= dtTo   || t.CreatedAt <= dtTo);

        // Auto-marca como overdue antes de retornar
        var today = DateTime.UtcNow.Date;
        await _db.ExternalTransactions
            .Where(t => t.Status == "pending" && t.DueDate.HasValue && t.DueDate.Value.Date < today)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "overdue"));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(t => t.DueDate ?? t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>
    /// Resumo financeiro: total a pagar (com atrasado e vencendo em 7 dias), total
    /// a receber, e saldo líquido pago no mês corrente.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var today   = DateTime.UtcNow.Date;
        var em7dias = today.AddDays(7);

        var all = await _db.ExternalTransactions
            .Where(t => t.Status != "cancelled")
            .ToListAsync();

        return Ok(new
        {
            aPagar = new
            {
                total     = all.Where(t => t.Type == "expense" && t.Status != "paid").Sum(t => t.Amount),
                atrasado  = all.Where(t => t.Type == "expense" && t.Status == "overdue").Sum(t => t.Amount),
                vence7d   = all.Where(t => t.Type == "expense" && t.Status == "pending" &&
                                           t.DueDate.HasValue && t.DueDate.Value.Date <= em7dias).Sum(t => t.Amount),
                qtd       = all.Count(t => t.Type == "expense" && t.Status is "pending" or "overdue"),
            },
            aReceber = new
            {
                total   = all.Where(t => t.Type == "income" && t.Status != "paid").Sum(t => t.Amount),
                qtd     = all.Count(t => t.Type == "income" && t.Status is "pending" or "overdue"),
            },
            pagoMes = all.Where(t => t.PaidAt.HasValue &&
                                      t.PaidAt.Value.Year  == today.Year &&
                                      t.PaidAt.Value.Month == today.Month)
                         .Sum(t => t.Type == "income" ? t.Amount : -t.Amount),
        });
    }

    /// <summary>Cria um lançamento financeiro manual (a pagar ou a receber).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tx = new ExternalTransaction
        {
            Source      = "manual",
            Type        = req.Type,
            Amount      = req.Amount,
            Description = req.Description,
            DueDate     = req.DueDate?.ToUniversalTime(),
            Status      = "pending",
            Category    = req.Category,
            Supplier    = req.Supplier,
            Notes       = req.Notes,
        };

        _db.ExternalTransactions.Add(tx);
        await _db.SaveChangesAsync();
        return Ok(ToDto(tx));
    }

    /// <summary>
    /// Atualiza um lançamento — inclui marcar como pago (seta PaidAt automaticamente)
    /// ou reverter isso ao mudar o status pra algo diferente de "paid".
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransactionRequest req)
    {
        var tx = await _db.ExternalTransactions.FindAsync(id);
        if (tx is null) return NotFound();

        if (req.Description is not null) tx.Description = req.Description;
        if (req.Amount.HasValue)         tx.Amount      = req.Amount.Value;
        if (req.DueDate.HasValue)        tx.DueDate     = req.DueDate.Value.ToUniversalTime();
        if (req.Category is not null)    tx.Category    = req.Category;
        if (req.Supplier is not null)    tx.Supplier    = req.Supplier;
        if (req.Notes    is not null)    tx.Notes       = req.Notes;

        if (req.Status is not null)
        {
            tx.Status = req.Status;
            if (req.Status == "paid" && tx.PaidAt is null)
                tx.PaidAt = DateTime.UtcNow;
            else if (req.Status != "paid")
                tx.PaidAt = null;
        }

        tx.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(tx));
    }

    /// <summary>Remove permanentemente um lançamento financeiro.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tx = await _db.ExternalTransactions.FindAsync(id);
        if (tx is null) return NotFound();
        _db.ExternalTransactions.Remove(tx);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Importa um extrato bancário OFX (máx 5 MB) — cada transação vira um
    /// lançamento já marcado como pago. Deduplica por identificador externo (FITID),
    /// então reimportar o mesmo extrato não duplica lançamentos.
    /// </summary>
    [HttpPost("import-ofx")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> ImportOfx(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { Message = "Arquivo OFX inválido ou vazio." });

        using var stream = file.OpenReadStream();
        var parsed = _ofx.Parse(stream);

        int imported = 0, skipped = 0;
        foreach (var p in parsed)
        {
            // Deduplicação por external_id
            if (p.ExternalId is not null)
            {
                var exists = await _db.ExternalTransactions
                    .AnyAsync(t => t.Source == "ofx" && t.ExternalId == p.ExternalId);
                if (exists) { skipped++; continue; }
            }

            var isIncome = p.TrnType.Equals("CREDIT", StringComparison.OrdinalIgnoreCase);
            var tx = new ExternalTransaction
            {
                Source      = "ofx",
                ExternalId  = p.ExternalId,
                Type        = isIncome ? "income" : "expense",
                Amount      = p.Amount,
                Description = p.Description ?? p.Memo ?? "Transação OFX",
                DueDate     = p.Date,
                PaidAt      = p.Date,       // OFX = já executada
                Status      = "paid",
            };
            _db.ExternalTransactions.Add(tx);
            imported++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { imported, skipped, total = parsed.Count });
    }

    /// <summary>
    /// Status das integrações financeiras (Inter, Mercado Pago, SEFAZ) — se estão
    /// ativas/conectadas e quando sincronizaram por último. Nunca retorna tokens/segredos.
    /// </summary>
    [HttpGet("integracoes")]
    public async Task<IActionResult> GetIntegracoes()
    {
        var configs = await _db.IntegrationConfigs.ToListAsync();
        var sources = new[] { "inter", "mercadopago", "sefaz" };

        return Ok(sources.Select(src =>
        {
            var cfg = configs.FirstOrDefault(c => c.Source == src);
            return new
            {
                source      = src,
                isActive    = cfg?.IsActive ?? false,
                isConnected = cfg is not null && cfg.IsActive,
                cnpj        = cfg?.Cnpj,
                pixKey      = cfg?.PixKey,
                lastSyncAt  = cfg?.LastSyncAt,
                expiresAt   = cfg?.ExpiresAt,
                // Nunca expõe tokens
            };
        }));
    }

    /// <summary>
    /// Salva/atualiza a configuração de uma integração ("inter", "mercadopago" ou
    /// "sefaz") — CNPJ, chave Pix, se está ativa. ClientSecret é sempre criptografado
    /// (AES-256-GCM) antes de persistir.
    /// </summary>
    [HttpPut("integracoes/{source}")]
    public async Task<IActionResult> SaveIntegracao(string source, [FromBody] SaveIntegracaoRequest req)
    {
        var allowed = new[] { "inter", "mercadopago", "sefaz" };
        if (!allowed.Contains(source)) return BadRequest(new { Message = "Source inválido." });

        var cfg = await _db.IntegrationConfigs.FirstOrDefaultAsync(c => c.Source == source);
        if (cfg is null)
        {
            cfg = new IntegrationConfig { Source = source };
            _db.IntegrationConfigs.Add(cfg);
        }

        // ClientId não é secreto — guarda em claro
        if (req.ClientId is not null) cfg.ClientId = req.ClientId;

        // ClientSecret, AccessToken e RefreshToken → AES-256-GCM
        if (req.ClientSecret is not null) cfg.ClientSecret = _enc.Encrypt(req.ClientSecret);

        if (req.Cnpj      is not null) cfg.Cnpj     = req.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "");
        if (req.PixKey    is not null) cfg.PixKey   = req.PixKey.Trim();
        if (req.IsActive.HasValue)     cfg.IsActive  = req.IsActive.Value;

        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { source, saved = true });
    }

    /// <summary>
    /// Salva tokens OAuth (access/refresh) de uma integração já configurada — uso
    /// interno, chamado pelo próprio fluxo de OAuth da integração, não pelo admin
    /// diretamente. Tokens são sempre criptografados antes de persistir.
    /// </summary>
    [HttpPost("integracoes/{source}/token")]
    public async Task<IActionResult> SaveToken(string source, [FromBody] SaveTokenRequest req)
    {
        var cfg = await _db.IntegrationConfigs.FirstOrDefaultAsync(c => c.Source == source);
        if (cfg is null) return NotFound(new { Message = "Integração não configurada." });

        cfg.AccessToken  = _enc.EncryptNullable(req.AccessToken);
        cfg.RefreshToken = _enc.EncryptNullable(req.RefreshToken);
        cfg.ExpiresAt    = req.ExpiresAt;
        cfg.LastSyncAt   = DateTime.UtcNow;
        cfg.UpdatedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { source, tokenSaved = true });
    }

    /// <summary>
    /// Status da integração com a Manifestação do Destinatário da SEFAZ: se está
    /// configurada/ativa, ambiente (produção/homologação), último NSU consultado
    /// e contagem de notas destinadas por status do pipeline.
    /// </summary>
    [HttpGet("sefaz-status")]
    [RequireModule("fiscal")] // F15: DF-e é parte do módulo fiscal — sem isso, tenant sem o módulo ainda vê/aciona
    public async Task<IActionResult> SefazStatus()
    {
        var fiscal     = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        var integracao = await _db.IntegrationConfigs.FirstOrDefaultAsync(c => c.Source == "sefaz");

        var porStatus = await _db.NotasDestinadas
            .GroupBy(n => n.Status)
            .Select(g => new { Status = g.Key, Qtd = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Qtd);

        return Ok(new
        {
            configured  = await _sefaz.IsConfiguredAsync(),
            ativa       = integracao?.IsActive ?? false,
            ambiente    = fiscal?.Ambiente.ToString(),
            ultimoNsu   = fiscal?.DistUltimoNsu ?? 0,
            lastSyncAt  = integracao?.LastSyncAt,
            notas = new
            {
                resumo        = porStatus.GetValueOrDefault(NotaDestinadaStatus.Resumo),
                ciencia       = porStatus.GetValueOrDefault(NotaDestinadaStatus.Ciencia),
                xmlBaixado    = porStatus.GetValueOrDefault(NotaDestinadaStatus.XmlBaixado),
                contasGeradas = porStatus.GetValueOrDefault(NotaDestinadaStatus.ContasGeradas),
                canceladas    = porStatus.GetValueOrDefault(NotaDestinadaStatus.Cancelada),
            },
        });
    }

    /// <summary>
    /// Dispara manualmente a sincronização com a SEFAZ (Distribuição DFe) — busca
    /// NF-e novas contra o CNPJ da loja, dá ciência da operação e baixa XMLs.
    /// Mesmo processo que roda automaticamente a cada 2h em background.
    /// </summary>
    [HttpPost("sefaz/sync")]
    [RequireModule("fiscal")]
    public async Task<IActionResult> SefazSync(CancellationToken ct)
    {
        var result = await _sefaz.SincronizarAsync(ct);
        if (!result.Executado)
            return BadRequest(new { message = result.Mensagem });
        return Ok(new
        {
            novasNotas    = result.NovasNotas,
            manifestadas  = result.Manifestadas,
            xmlsBaixados  = result.XmlsBaixados,
            contasCriadas = result.ContasCriadas,
            mensagem      = result.Mensagem,
        });
    }

    /// <summary>
    /// Lista as NF-e de fornecedores descobertas via SEFAZ (limitado às 200 mais
    /// recentes), com o status do pipeline (aguardando ciência → XML → contas geradas).
    /// </summary>
    /// <param name="status">Filtra por status do pipeline (ex: "Resumo", "Ciencia", "XmlBaixado", "ContasGeradas", "Cancelada").</param>
    [HttpGet("notas-destinadas")]
    [RequireModule("fiscal")]
    public async Task<IActionResult> NotasDestinadas([FromQuery] string? status = null)
    {
        var q = _db.NotasDestinadas.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(n => n.Status == status);

        var notas = await q
            .OrderByDescending(n => n.DataEmissao ?? n.CreatedAt)
            .Take(200)
            .Select(n => new
            {
                n.Id, n.ChaveAcesso, n.EmitenteCnpj, n.EmitenteNome,
                n.Valor, n.DataEmissao, n.Status, n.ContasGeradas,
                n.CienciaEm, n.Erro, n.CreatedAt,
            })
            .ToListAsync();

        return Ok(notas);
    }

    /// <summary>Sincroniza manualmente o extrato do Banco Inter dos últimos N dias.</summary>
    /// <param name="days">Quantos dias pra trás buscar no extrato (padrão 7).</param>
    [HttpPost("integracoes/inter/sync")]
    public async Task<IActionResult> InterSync([FromQuery] int days = 7)
    {
        var result = await _inter.SyncAsync(days);
        if (result.Skipped)
            return BadRequest(new { Message = result.Reason });
        if (result.Error is not null)
            return StatusCode(422, new { message = result.Error });
        return Ok(new { result.Imported, result.Duplicates });
    }

    [HttpGet("integracoes/inter/status")]
    public async Task<IActionResult> InterStatus()
    {
        var cfg = await _db.IntegrationConfigs.FirstOrDefaultAsync(c => c.Source == "inter");
        return Ok(new
        {
            configured      = cfg is not null && _inter.IsConfigured(cfg),
            certificateOk   = cfg is not null && !string.IsNullOrWhiteSpace(cfg.CertificateCrtEncrypted),
            hasCredentials  = !string.IsNullOrWhiteSpace(cfg?.ClientId) && !string.IsNullOrWhiteSpace(cfg?.ClientSecret),
            lastSyncAt      = cfg?.LastSyncAt,
        });
    }

    [HttpPost("integracoes/inter/certificado")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCertificado(IFormFile crt, IFormFile key)
    {
        const long maxBytes = 64 * 1024; // 64 KB
        if (crt.Length > maxBytes || key.Length > maxBytes)
            return BadRequest(new { message = "Arquivo muito grande (máx 64 KB)." });

        using var crtStream = new StreamReader(crt.OpenReadStream());
        var crtStr = await crtStream.ReadToEndAsync();
        using var keyStream = new StreamReader(key.OpenReadStream());
        var keyStr = await keyStream.ReadToEndAsync();

        var cfg = await _db.IntegrationConfigs.FirstOrDefaultAsync(c => c.Source == "inter");
        if (cfg is null)
        {
            cfg = new IntegrationConfig { Source = "inter" };
            _db.IntegrationConfigs.Add(cfg);
        }

        cfg.CertificateCrtEncrypted = _enc.Encrypt(crtStr);
        cfg.CertificateKeyEncrypted = _enc.Encrypt(keyStr);
        cfg.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Certificado instalado com sucesso.", certificateOk = true });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static object ToDto(ExternalTransaction t) => new
    {
        t.Id,
        t.Source,
        t.ExternalId,
        t.Type,
        t.Amount,
        t.Description,
        t.DueDate,
        t.PaidAt,
        t.Status,
        t.Category,
        t.Supplier,
        t.NfeKey,
        t.Notes,
        t.CreatedAt,
        t.UpdatedAt,
    };
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class CreateTransactionRequest
{
    [Required, MaxLength(10)]
    public string Type { get; init; } = "expense";

    [Required, Range(0.01, 9999999)]
    public decimal Amount { get; init; }

    [Required, MaxLength(500)]
    public string Description { get; init; } = "";

    public DateTime? DueDate  { get; init; }
    public string?   Category { get; init; }
    public string?   Supplier { get; init; }
    public string?   Notes    { get; init; }
}

public class UpdateTransactionRequest
{
    public string?   Description { get; init; }
    public decimal?  Amount      { get; init; }
    public DateTime? DueDate     { get; init; }
    public string?   Status      { get; init; }
    public string?   Category    { get; init; }
    public string?   Supplier    { get; init; }
    public string?   Notes       { get; init; }
}

public class SaveIntegracaoRequest
{
    public string? ClientId     { get; init; }
    public string? ClientSecret { get; init; }
    public string? Cnpj         { get; init; }
    public string? PixKey       { get; init; }
    public bool?   IsActive     { get; init; }
}

public class SaveTokenRequest
{
    public string?   AccessToken  { get; init; }
    public string?   RefreshToken { get; init; }
    public DateTime? ExpiresAt    { get; init; }
}
