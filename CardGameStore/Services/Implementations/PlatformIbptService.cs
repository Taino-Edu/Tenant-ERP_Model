using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public sealed record PlatformIbptStatusDto(
    string Uf,
    int Ncms,
    string? Versao,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim,
    DateTime AtualizadoEm);

public sealed record PlatformIbptImportResult(
    string Uf,
    int NcmsImportados,
    int LinhasIgnoradas,
    string? Versao,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim);

/// <summary>
/// Publica a tabela oficial do IBPT no catálogo compartilhado. Não depende de
/// tenant nem de configuração fiscal de loja: a UF vem obrigatoriamente do nome
/// oficial do arquivo e a carga passa a atender todos os tenants daquele estado.
/// </summary>
public sealed class PlatformIbptService(
    CatalogDbContext catalog,
    ILogger<PlatformIbptService> logger)
{
    private static readonly HashSet<string> Ufs = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS",
        "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC",
        "SP", "SE", "TO",
    };

    public async Task<IReadOnlyList<PlatformIbptStatusDto>> ListarAsync(CancellationToken ct = default)
    {
        var grupos = await catalog.IbptTabela.AsNoTracking()
            .GroupBy(e => e.Uf)
            .Select(g => new
            {
                Uf = g.Key,
                Ncms = g.Select(e => e.Ncm).Distinct().Count(),
                Versao = g.Max(e => e.Versao),
                VigenciaInicio = g.Min(e => e.VigenciaInicio),
                VigenciaFim = g.Min(e => e.VigenciaFim),
                AtualizadoEm = g.Max(e => e.AtualizadoEm),
            })
            .OrderBy(e => e.Uf)
            .ToListAsync(ct);

        return grupos.Select(g => new PlatformIbptStatusDto(
            g.Uf, g.Ncms, g.Versao, g.VigenciaInicio, g.VigenciaFim, g.AtualizadoEm)).ToList();
    }

    public async Task<PlatformIbptImportResult> ImportarAsync(
        Stream conteudo, string? nomeArquivo, CancellationToken ct = default)
    {
        var uf = IbptTabelaCsvImporter.UfDoNomeDoArquivo(nomeArquivo);
        if (uf is null || !Ufs.Contains(uf))
            throw new IbptIntegrationException(
                "Não foi possível identificar a UF pelo nome. Use o arquivo oficial " +
                "TabelaIBPTax<UF><versão>.csv sem renomeá-lo.");

        var leitura = IbptTabelaCsvImporter.Ler(conteudo);
        var agora = DateTime.UtcNow;
        var entradas = new List<IbptTabelaEntry>(leitura.Linhas.Count * 2);

        foreach (var linha in leitura.Linhas)
        {
            foreach (var importado in new[] { false, true })
                entradas.Add(new IbptTabelaEntry
                {
                    Ncm = linha.Ncm,
                    Uf = uf,
                    Importado = importado,
                    PercentualFederal = importado ? linha.ImportadoFederal : linha.NacionalFederal,
                    PercentualEstadual = linha.Estadual,
                    PercentualMunicipal = linha.Municipal,
                    Fonte = Limitar($"{linha.Fonte} {linha.Versao}".Trim(), 100),
                    Versao = linha.Versao,
                    Chave = linha.Chave,
                    VigenciaInicio = linha.VigenciaInicio,
                    VigenciaFim = linha.VigenciaFim,
                    AtualizadoEm = agora,
                });
        }

        var strategy = catalog.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await catalog.Database.BeginTransactionAsync(ct);
            await catalog.IbptTabela.Where(e => e.Uf == uf).ExecuteDeleteAsync(ct);

            var detectChanges = catalog.ChangeTracker.AutoDetectChangesEnabled;
            catalog.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                await catalog.IbptTabela.AddRangeAsync(entradas, ct);
                await catalog.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            finally
            {
                catalog.ChangeTracker.AutoDetectChangesEnabled = detectChanges;
                catalog.ChangeTracker.Clear();
            }
        });

        logger.LogInformation(
            "Tabela IBPT global publicada pelo painel: {Ncms} NCM(s), versão {Versao}, UF {Uf}.",
            leitura.Linhas.Count, leitura.Versao, uf);

        return new PlatformIbptImportResult(
            uf, leitura.Linhas.Count, leitura.LinhasIgnoradas, leitura.Versao,
            leitura.VigenciaInicio, leitura.VigenciaFim);
    }

    private static string? Limitar(string valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor[..Math.Min(valor.Length, max)];
}
