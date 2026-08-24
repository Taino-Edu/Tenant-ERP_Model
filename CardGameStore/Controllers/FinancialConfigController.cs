using System.ComponentModel.DataAnnotations;
using CardGameStore.Data;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/financial-config")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
[OperatorForbidden]
public class FinancialConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinancialConfigController(AppDbContext db) => _db = db;

    [HttpGet]
    [RequireIntegrationScope(IntegrationScope.FinanceRead)]
    public async Task<ActionResult<FinancialConfigDto>> Get()
    {
        var config = await _db.FinancialConfigs.FindAsync(FinancialConfig.SingletonId);
        return Ok(ToDto(config ?? new FinancialConfig()));
    }

    [HttpPut]
    [RequireIntegrationScope(IntegrationScope.FinanceWrite)]
    public async Task<ActionResult<FinancialConfigDto>> Save([FromBody] SaveFinancialConfigRequest request)
    {
        var config = await GetOrCreateAsync();
        config.CardFeePercent = request.CardFeePercent;
        config.CommissionPercent = request.CommissionPercent;
        config.FreightPercent = request.FreightPercent;
        config.ExpectedDailyNetCash = request.ExpectedDailyNetCash;
        config.MinimumCashReserve = request.MinimumCashReserve;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDto(config));
    }

    private async Task<FinancialConfig> GetOrCreateAsync()
    {
        var config = await _db.FinancialConfigs.FindAsync(FinancialConfig.SingletonId);
        if (config is not null) return config;

        config = new FinancialConfig();
        _db.FinancialConfigs.Add(config);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(config).State = EntityState.Detached;
            config = await _db.FinancialConfigs.FindAsync(FinancialConfig.SingletonId)
                ?? throw new InvalidOperationException("Falha ao obter configuracao financeira apos conflito de concorrencia.");
        }

        return config;
    }

    private static FinancialConfigDto ToDto(FinancialConfig config) => new()
    {
        CardFeePercent = config.CardFeePercent,
        CommissionPercent = config.CommissionPercent,
        FreightPercent = config.FreightPercent,
        ExpectedDailyNetCash = config.ExpectedDailyNetCash,
        MinimumCashReserve = config.MinimumCashReserve,
        UpdatedAt = config.UpdatedAt,
    };
}

public class FinancialConfigDto
{
    public decimal CardFeePercent { get; init; }
    public decimal CommissionPercent { get; init; }
    public decimal FreightPercent { get; init; }
    public decimal ExpectedDailyNetCash { get; init; }
    public decimal MinimumCashReserve { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public class SaveFinancialConfigRequest
{
    [Range(0, 100)] public decimal CardFeePercent { get; init; }
    [Range(0, 100)] public decimal CommissionPercent { get; init; }
    [Range(0, 100)] public decimal FreightPercent { get; init; }
    [Range(-999999999999.99, 999999999999.99)] public decimal ExpectedDailyNetCash { get; init; }
    [Range(0, 999999999999.99)] public decimal MinimumCashReserve { get; init; }
}
