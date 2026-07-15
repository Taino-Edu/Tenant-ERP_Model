// =============================================================================
// ImportController.cs — Importação self-service de dados (contraparte do
// ExportController) — reduz fricção de onboarding pra lojista que já tem
// base de produtos/clientes/crediário em outro sistema.
//
// Comportamento em todos os 3 endpoints: valida linha a linha, importa as
// válidas, reporta as inválidas com motivo (não é tudo-ou-nada) — decisão
// consciente pra não travar uma planilha grande por 1 erro de digitação.
//
// GET  /api/export/*        → formato de referência (mesmas colunas aceitas aqui)
// POST /api/import/produtos → cria produtos (rejeita duplicata por Nome)
// POST /api/import/clientes → cria clientes (rejeita duplicata por CPF/e-mail)
// POST /api/import/crediario → cria crediário EM ABERTO — só se o cliente já existe
//   (por CPF ou e-mail). Nunca cria cliente novo só pra pendurar dívida nele —
//   é o ponto mais sensível deste controller, ver ImportCrediario.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using CardGameStore.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class ImportController : ControllerBase
{
    private readonly AppDbContext  _db;
    private readonly IAuditService _audit;

    public ImportController(AppDbContext db, IAuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    // ── Produtos ──────────────────────────────────────────────────────────────

    /// <summary>Importa produtos de um CSV (mesmas colunas de GET /api/export/produtos).
    /// Colunas obrigatórias: Nome, Categoria, PrecoVenda. Rejeita linha com nome
    /// já existente (no banco ou repetido dentro do próprio arquivo).</summary>
    [HttpPost("produtos")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<ImportResultDto>> ImportProdutos(IFormFile arquivo)
    {
        var texto = await ReadCsvAsync(arquivo);
        if (texto is null) return BadRequest(new { Message = "Envie um arquivo CSV." });

        var rows = CsvReader.ParseRows(texto);
        if (rows.Count < 2) return Ok(new ImportResultDto());

        var index  = CsvReader.HeaderIndex(rows[0]);
        var result = new ImportResultDto { TotalLinhas = rows.Count - 1 };

        var nomesExistentes = (await _db.Products.Select(p => p.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nomesNoArquivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var novos = new List<Product>();

        for (var i = 1; i < rows.Count; i++)
        {
            var linha = i + 1; // +1 pro admin ver o mesmo número que veria abrindo no Excel
            var row = rows[i];

            var nome = CsvReader.Cell(row, index, "Nome");
            if (string.IsNullOrWhiteSpace(nome)) { result.Erros.Add(Erro(linha, "Nome é obrigatório.")); continue; }

            var categoria = CsvReader.Cell(row, index, "Categoria");
            if (string.IsNullOrWhiteSpace(categoria)) { result.Erros.Add(Erro(linha, "Categoria é obrigatória.")); continue; }

            var precoVenda = ParseDecimalBr(CsvReader.Cell(row, index, "PrecoVenda"));
            if (precoVenda is null || precoVenda <= 0) { result.Erros.Add(Erro(linha, "PrecoVenda inválido — precisa ser maior que zero.")); continue; }

            if (nomesExistentes.Contains(nome) || !nomesNoArquivo.Add(nome))
            { result.Erros.Add(Erro(linha, $"Já existe um produto chamado \"{nome}\".")); continue; }

            var precoCusto = ParseDecimalBr(CsvReader.Cell(row, index, "PrecoCusto")) ?? 0;
            var precoPromo = ParseDecimalBr(CsvReader.Cell(row, index, "PrecoPromocional"));
            var estoque    = ParseInt(CsvReader.Cell(row, index, "Estoque")) ?? 0;
            var estoqueMin = ParseInt(CsvReader.Cell(row, index, "EstoqueMinimo")) ?? 5;

            novos.Add(new Product
            {
                Name              = nome,
                Category          = categoria,
                Description       = CsvReader.Cell(row, index, "Descricao"),
                Barcode           = string.IsNullOrWhiteSpace(CsvReader.Cell(row, index, "CodigoBarras")) ? null : CsvReader.Cell(row, index, "CodigoBarras"),
                Ncm               = string.IsNullOrWhiteSpace(CsvReader.Cell(row, index, "NCM")) ? null : CsvReader.Cell(row, index, "NCM"),
                PriceInCents      = (int)Math.Round(precoVenda.Value * 100),
                CostPriceInCents  = (int)Math.Round(precoCusto * 100),
                DiscountPriceInCents = precoPromo.HasValue ? (int)Math.Round(precoPromo.Value * 100) : null,
                StockQuantity     = Math.Max(0, estoque),
                MinimumStock      = Math.Max(0, estoqueMin),
                IsActive          = ParseBool(CsvReader.Cell(row, index, "Ativo")) ?? true,
                IsFeatured        = ParseBool(CsvReader.Cell(row, index, "Destaque")) ?? false,
            });
        }

        _db.Products.AddRange(novos);
        await _db.SaveChangesAsync();
        result.Importados = novos.Count;

        await _audit.LogAsync("ImportouDados", "Product", details: $"{result.Importados}/{result.TotalLinhas} produtos", httpContext: HttpContext);
        return Ok(result);
    }

    // ── Clientes ──────────────────────────────────────────────────────────────

    /// <summary>Importa clientes de um CSV (mesmas colunas de GET /api/export/clientes).
    /// Coluna obrigatória: Nome. CPF (se presente) precisa passar Módulo 11.
    /// Rejeita linha com CPF ou e-mail já cadastrado.</summary>
    [HttpPost("clientes")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<ImportResultDto>> ImportClientes(IFormFile arquivo)
    {
        var texto = await ReadCsvAsync(arquivo);
        if (texto is null) return BadRequest(new { Message = "Envie um arquivo CSV." });

        var rows = CsvReader.ParseRows(texto);
        if (rows.Count < 2) return Ok(new ImportResultDto());

        var index  = CsvReader.HeaderIndex(rows[0]);
        var result = new ImportResultDto { TotalLinhas = rows.Count - 1 };

        var cpfsExistentes   = (await _db.Users.Where(u => u.Cpf != null).Select(u => u.Cpf!).ToListAsync()).ToHashSet();
        var emailsExistentes = (await _db.Users.Where(u => u.Email != null).Select(u => u.Email!).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cpfsNoArquivo    = new HashSet<string>();
        var emailsNoArquivo  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var novos = new List<User>();

        for (var i = 1; i < rows.Count; i++)
        {
            var linha = i + 1;
            var row = rows[i];

            var nome = CsvReader.Cell(row, index, "Nome");
            if (string.IsNullOrWhiteSpace(nome)) { result.Erros.Add(Erro(linha, "Nome é obrigatório.")); continue; }

            var cpfRaw = CsvReader.Cell(row, index, "CPF");
            var cpf = string.IsNullOrWhiteSpace(cpfRaw) ? null : cpfRaw.Replace(".", "").Replace("-", "").Trim();
            if (cpf is not null && !CpfValidAttribute.ValidarCpf(cpf)) { result.Erros.Add(Erro(linha, $"CPF inválido: \"{cpfRaw}\".")); continue; }
            if (cpf is not null && (cpfsExistentes.Contains(cpf) || !cpfsNoArquivo.Add(cpf)))
            { result.Erros.Add(Erro(linha, $"CPF já cadastrado: {cpfRaw}.")); continue; }

            var emailRaw = CsvReader.Cell(row, index, "Email");
            var email = string.IsNullOrWhiteSpace(emailRaw) ? null : emailRaw.Trim();
            if (email is not null && !email.Contains('@')) { result.Erros.Add(Erro(linha, $"E-mail inválido: \"{emailRaw}\".")); continue; }
            if (email is not null && (emailsExistentes.Contains(email) || !emailsNoArquivo.Add(email)))
            { result.Erros.Add(Erro(linha, $"E-mail já cadastrado: {emailRaw}.")); continue; }

            var pontos   = ParseInt(CsvReader.Cell(row, index, "SaldoPontos")) ?? 0;
            var cashback = ParseDecimalBr(CsvReader.Cell(row, index, "SaldoCashback")) ?? 0;

            novos.Add(new User
            {
                Name             = nome,
                Email            = email,
                Cpf              = cpf,
                WhatsApp         = string.IsNullOrWhiteSpace(CsvReader.Cell(row, index, "WhatsApp")) ? null : CsvReader.Cell(row, index, "WhatsApp"),
                Role             = UserRole.Customer,
                PointsBalance    = Math.Max(0, pontos),
                BalanceInCents   = Math.Max(0, (int)Math.Round(cashback * 100)),
                IsActive         = ParseBool(CsvReader.Cell(row, index, "Ativo")) ?? true,
            });
        }

        _db.Users.AddRange(novos);
        await _db.SaveChangesAsync();
        result.Importados = novos.Count;

        await _audit.LogAsync("ImportouDados", "User", details: $"{result.Importados}/{result.TotalLinhas} clientes", httpContext: HttpContext);
        return Ok(result);
    }

    // ── Crediário ─────────────────────────────────────────────────────────────

    /// <summary>Importa crediário em aberto de um CSV (mesmas colunas de
    /// GET /api/export/crediario). Colunas obrigatórias: ClienteCPF ou
    /// ClienteEmail (pra resolver o cliente) e ValorTotal.
    /// <para>Ponto sensível: só cria dívida pra cliente que JÁ EXISTE no
    /// sistema (resolvido por CPF/e-mail) — nunca cria conta nova só pra
    /// isso. Se o cliente não existir, a linha vira erro pedindo pra
    /// importar clientes primeiro.</para></summary>
    [HttpPost("crediario")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<ImportResultDto>> ImportCrediario(IFormFile arquivo)
    {
        var texto = await ReadCsvAsync(arquivo);
        if (texto is null) return BadRequest(new { Message = "Envie um arquivo CSV." });

        var rows = CsvReader.ParseRows(texto);
        if (rows.Count < 2) return Ok(new ImportResultDto());

        var index  = CsvReader.HeaderIndex(rows[0]);
        var result = new ImportResultDto { TotalLinhas = rows.Count - 1 };
        var adminId = GetUserId();

        var usuariosPorCpf   = await _db.Users.Where(u => u.Cpf != null).ToDictionaryAsync(u => u.Cpf!);
        var usuariosPorEmail = await _db.Users.Where(u => u.Email != null).ToDictionaryAsync(u => u.Email!, StringComparer.OrdinalIgnoreCase);
        var novos = new List<Crediario>();

        for (var i = 1; i < rows.Count; i++)
        {
            var linha = i + 1;
            var row = rows[i];

            var cpfRaw   = CsvReader.Cell(row, index, "ClienteCPF")?.Replace(".", "").Replace("-", "").Trim();
            var emailRaw = CsvReader.Cell(row, index, "ClienteEmail")?.Trim();

            User? cliente = null;
            if (!string.IsNullOrWhiteSpace(cpfRaw)) usuariosPorCpf.TryGetValue(cpfRaw, out cliente);
            if (cliente is null && !string.IsNullOrWhiteSpace(emailRaw)) usuariosPorEmail.TryGetValue(emailRaw, out cliente);

            if (cliente is null)
            {
                result.Erros.Add(Erro(linha, "Cliente não encontrado (por CPF/e-mail) — importe os clientes primeiro."));
                continue;
            }

            var valorTotal = ParseDecimalBr(CsvReader.Cell(row, index, "ValorTotal"));
            if (valorTotal is null || valorTotal <= 0) { result.Erros.Add(Erro(linha, "ValorTotal inválido — precisa ser maior que zero.")); continue; }

            var valorPago = ParseDecimalBr(CsvReader.Cell(row, index, "ValorPago")) ?? 0;
            if (valorPago < 0 || valorPago > valorTotal) { result.Erros.Add(Erro(linha, "ValorPago não pode ser negativo nem maior que ValorTotal.")); continue; }

            var vencimento = ParseDate(CsvReader.Cell(row, index, "DataVencimento"));
            if (vencimento is null) { result.Erros.Add(Erro(linha, "DataVencimento inválida — use AAAA-MM-DD.")); continue; }

            var abertura = ParseDate(CsvReader.Cell(row, index, "DataAbertura")) ?? DateTime.UtcNow;
            var quitado  = valorPago >= valorTotal;

            novos.Add(new Crediario
            {
                UserId              = cliente.Id,
                ValorEmCentavos     = (int)Math.Round(valorTotal.Value * 100),
                ValorPagoEmCentavos = (int)Math.Round(valorPago * 100),
                DataAbertura        = DateTime.SpecifyKind(abertura, DateTimeKind.Utc),
                DataVencimento      = DateTime.SpecifyKind(vencimento.Value, DateTimeKind.Utc),
                Status              = quitado ? CrediariosStatus.Pago : CrediariosStatus.Aberto,
                DataPagamento       = quitado ? DateTime.UtcNow : null,
                Observacao          = string.IsNullOrWhiteSpace(CsvReader.Cell(row, index, "Observacao"))
                    ? "Importado de sistema externo" : $"{CsvReader.Cell(row, index, "Observacao")} (importado)",
                AbertoPorAdminId    = adminId,
                PagoPorAdminId      = quitado ? adminId : null,
            });
        }

        _db.Crediarios.AddRange(novos);
        await _db.SaveChangesAsync();
        result.Importados = novos.Count;

        await _audit.LogAsync("ImportouDados", "Crediario",
            details: $"{result.Importados}/{result.TotalLinhas} crediários, total R$ {novos.Sum(c => c.ValorEmCentavos) / 100m:N2}",
            httpContext: HttpContext, severity: AuditSeverity.Warning); // Warning: cria dívida em nome de terceiros, vale destacar no log

        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<string?> ReadCsvAsync(IFormFile? arquivo)
    {
        if (arquivo is null || arquivo.Length == 0) return null;
        using var reader = new StreamReader(arquivo.OpenReadStream(), System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static ImportRowErrorDto Erro(int linha, string motivo) => new() { Linha = linha, Motivo = motivo };

    private static decimal? ParseDecimalBr(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var normalizado = s.Trim().Replace(".", "").Replace(',', '.');
        // Se não tinha vírgula (formato "10.50" já em ponto), o replace acima
        // some com o ponto decimal — tenta de novo sem mexer nos separadores.
        if (decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var v1)) return v1;
        return decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v2) ? v2 : null;
    }

    private static int? ParseInt(string? s) =>
        int.TryParse(s?.Trim(), out var v) ? v : null;

    private static bool? ParseBool(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().ToLowerInvariant();
        if (t is "sim" or "true" or "1" or "s") return true;
        if (t is "não" or "nao" or "false" or "0" or "n") return false;
        return null;
    }

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de usuário ausente.");
        return id;
    }
}
