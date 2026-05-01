// =============================================================================
// Program.cs — Ponto de entrada e configuração central da aplicação
// Padrão: Minimal API (.NET 8+), sem Startup.cs separado
// =============================================================================

using System.Text;
using CardGameStore.Configuration;
using CardGameStore.Data;
using CardGameStore.Hubs;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. CONFIGURAÇÕES — Lê seções do appsettings.json para objetos fortemente tipados
// ---------------------------------------------------------------------------
var jwtSettings   = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
var mongoSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()!;

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));

// ---------------------------------------------------------------------------
// 2. BANCO RELACIONAL — SQLite (dev local) ou PostgreSQL (produção)
// Usa SQLite quando: ambiente é Development OU não há connection string PostgreSQL configurada
// ---------------------------------------------------------------------------
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL");
var useSqlite = builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(pgConnStr);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        // SQLite — sem necessidade de servidor instalado para dev local
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "cardgamestore.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
    else
    {
        options.UseNpgsql(
            pgConnStr,
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5)
        );
    }
});

// ---------------------------------------------------------------------------
// 3. BANCO DE DOCUMENTOS — MongoDB (cache de cartas TCG) — opcional em dev
// ---------------------------------------------------------------------------
var mongoConStr = mongoSettings?.ConnectionString ?? "mongodb://localhost:27017";
try
{
    builder.Services.AddSingleton<IMongoClient>(_ =>
    {
        var settings = MongoClientSettings.FromConnectionString(mongoConStr);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
        return new MongoClient(settings);
    });

    builder.Services.AddSingleton(sp =>
    {
        var client = sp.GetRequiredService<IMongoClient>();
        return client.GetDatabase(mongoSettings?.DatabaseName ?? "cardgamestore_cache");
    });
}
catch
{
    // MongoDB não disponível — TCG card cache não vai funcionar, mas o resto sim
}

// ---------------------------------------------------------------------------
// 4. AUTENTICAÇÃO — JWT Bearer Token
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings.Issuer,
            ValidAudience            = jwtSettings.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
            ),
            ClockSkew                = TimeSpan.Zero
        };

        // Permite que o SignalR envie o token via query string (?access_token=...)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path        = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// ---------------------------------------------------------------------------
// 5. AUTORIZAÇÃO — RBAC com políticas por perfil
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",        policy => policy.RequireRole("Admin"));
    options.AddPolicy("CustomerOrAdmin",  policy => policy.RequireRole("Admin", "Customer"));
});

// ---------------------------------------------------------------------------
// 6. SIGNALR — Comunicação em tempo real (comandas → dashboard)
// ---------------------------------------------------------------------------
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors           = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize      = 32 * 1024;
});

// ---------------------------------------------------------------------------
// 7. HTTP CLIENT — Para chamadas à API TCG externa
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient("TcgApi", client =>
{
    client.BaseAddress = new Uri("https://api.tcgplayer.com/");
    client.Timeout     = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ---------------------------------------------------------------------------
// 8. SERVIÇOS DE APLICAÇÃO — Injeção dos serviços de domínio
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAuthService,         AuthService>();
builder.Services.AddScoped<IComandaService,      ComandaService>();
builder.Services.AddScoped<IProductService,      ProductService>();
builder.Services.AddScoped<IChampionshipService, ChampionshipService>();
builder.Services.AddSingleton<ITcgApiClient,     TcgApiClient>();
builder.Services.AddSingleton<ITcgService,       TcgService>();

// ---------------------------------------------------------------------------
// 9. CORS
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:5000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ---------------------------------------------------------------------------
// 10. SWAGGER
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "CardGameStore API",
        Version     = "v1",
        Description = "API para gestão da loja de Card Games — Fase 1: Painel do Maikon"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Informe: Bearer {seu_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

// ---------------------------------------------------------------------------
// 11. BUILD
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// 12. BANCO DE DADOS — EnsureCreated em dev (SQLite), Migrations em produção
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (useSqlite)
        {
            // SQLite: EnsureCreated cria o schema direto sem migrations
            logger.LogInformation("Usando banco SQLite (modo dev/sem PostgreSQL)...");
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Banco SQLite pronto: cardgamestore.db");
        }
        else
        {
            logger.LogInformation("Aplicando migrations do PostgreSQL...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations aplicadas com sucesso.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao inicializar o banco. Detalhes: {Msg}", ex.Message);
        // Em dev/SQLite, continua mesmo com erro
        if (!useSqlite) throw;
    }
}

// ---------------------------------------------------------------------------
// 13. MIDDLEWARE PIPELINE
// ---------------------------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CardGameStore API v1");
    c.RoutePrefix = string.Empty; // Swagger na raiz "/"
    c.DocumentTitle = "CardGameStore — Painel do Maikon";
});

// HTTPS redirect: apenas em produção (em dev evita redirecionar http→https quebrando CORS)
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ComandaHub>("/hubs/comanda");

// Endpoint de health check simples
app.MapGet("/health", () => new { Status = "OK", Timestamp = DateTime.UtcNow })
   .AllowAnonymous();

app.Run();
