// =============================================================================
// Program.cs — Ponto de entrada e configuração central da aplicação
// Padrão: Minimal API (.NET 8+), sem Startup.cs separado
// =============================================================================

using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using CardGameStore.Configuration;
using CardGameStore.Data;
using CardGameStore.HealthChecks;
using CardGameStore.Hubs;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. CONFIGURAÇÕES
// ---------------------------------------------------------------------------
var jwtSettings   = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
var mongoSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()!;

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));

// ---------------------------------------------------------------------------
// 2. BANCO RELACIONAL — SQLite (dev local) ou PostgreSQL (produção/Docker)
// ---------------------------------------------------------------------------
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL");
var useSqlite = string.IsNullOrWhiteSpace(pgConnStr);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
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
// 3. BANCO DE DOCUMENTOS — MongoDB — opcional em dev
// ---------------------------------------------------------------------------
var mongoConStr = mongoSettings?.ConnectionString;
if (string.IsNullOrWhiteSpace(mongoConStr))
{
    var startupLogger = builder.Services.BuildServiceProvider()
        .GetRequiredService<ILogger<Program>>();
    startupLogger.LogWarning("MongoDB: ConnectionString não configurada — TCG cache e VendaAvulsa podem não funcionar.");
    mongoConStr = "mongodb://localhost:27017";
}

// Registro sempre ocorre — o MongoDB driver só conecta na primeira query (lazy connection).
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
            ClockSkew = TimeSpan.Zero
        };

        // Cookie HttpOnly tem prioridade; SignalR usa query string para hubs
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // 1. Cookie HttpOnly (prioridade máxima — seguro contra XSS)
                var cookieToken = context.Request.Cookies["accessToken"];
                if (!string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                    return Task.CompletedTask;
                }

                // 2. SignalR envia token via query string (?access_token=...)
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
    options.AddPolicy("AdminOnly",       policy => policy.RequireRole("Admin"));
    options.AddPolicy("CustomerOrAdmin", policy => policy.RequireRole("Admin", "Customer"));
});

// ---------------------------------------------------------------------------
// 6. RATE LIMITING — Proteção contra força bruta e abuso de API
//
// "auth"  → endpoints de login/refresh: 5 tentativas/minuto por IP.
//           Bloqueia ataques de força bruta sem afetar uso normal.
// "api"   → demais endpoints: 200 req/minuto por IP.
//           Evita scraping e abusos de bots.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    // Política global — protege TODOS os endpoints sem [EnableRateLimiting] explícito
    // 300 req/min por IP é generoso o suficiente para uso legítimo
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, IPAddress>(
        context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress ?? IPAddress.Loopback,
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit          = 300,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit              = 5;
        opt.Window                   = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder     = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit               = 0; // sem fila — rejeita imediatamente
    });

    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit          = 200;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 10;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { Message = "Muitas requisições. Aguarde 1 minuto antes de tentar novamente." },
            cancellationToken: token);
    };
});

// ---------------------------------------------------------------------------
// 7. TIMEOUT DE REQUISIÇÃO — Evita que requests lentos prendam threads
// ---------------------------------------------------------------------------
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    // Endpoints com processamento mais longo (busca TCG, exportações)
    options.AddPolicy("long", TimeSpan.FromSeconds(60));
});

// ---------------------------------------------------------------------------
// 8. SIGNALR — Comunicação em tempo real (comandas → dashboard)
// ---------------------------------------------------------------------------
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors      = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024;
    // Ping a cada 10s — detecta conexão morta mais rápido (padrão: 15s)
    options.KeepAliveInterval         = TimeSpan.FromSeconds(10);
    // Se não receber resposta em 20s, considera desconectado (padrão: 30s)
    options.ClientTimeoutInterval     = TimeSpan.FromSeconds(20);
});

// ---------------------------------------------------------------------------
// 9. HTTP CLIENTS — APIs externas
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient("PokemonTcgApi", client =>
{
    client.BaseAddress = new Uri("https://api.pokemontcg.io/");
    client.Timeout     = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient("ScryfallApi", client =>
{
    client.BaseAddress = new Uri("https://api.scryfall.com/");
    client.Timeout     = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "CardGameStore/1.0 (softnerd.com.br)");
});

builder.Services.AddHttpClient("YugiohApi", client =>
{
    client.BaseAddress = new Uri("https://db.ygoprodeck.com/");
    client.Timeout     = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Gemini 2.0 Flash — assistente IA conversacional
builder.Services.AddHttpClient("gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ---------------------------------------------------------------------------
// 10. HEALTH CHECKS — Postgres + MongoDB via IHealthCheck com injeção correta
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>   ("postgres", tags: ["db", "postgres"])
    .AddCheck<MongoHealthCheck>("mongodb",  tags: ["db", "mongo"]);

// ---------------------------------------------------------------------------
// 11. SERVIÇOS DE APLICAÇÃO
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAuthService,         AuthService>();
builder.Services.AddScoped<IComandaService,      ComandaService>();
builder.Services.AddScoped<IProductService,      ProductService>();
builder.Services.AddScoped<ICategoryService,     CategoryService>();
builder.Services.AddScoped<IChampionshipService, ChampionshipService>();
builder.Services.AddScoped<IUserService,         UserService>();
builder.Services.AddScoped<IVendaAvulsaService,  VendaAvulsaService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IEmailService,        EmailService>();
builder.Services.AddScoped<IAiChatService,       GeminiChatService>();
builder.Services.AddSingleton<ITcgApiClient,     TcgApiClient>();
builder.Services.AddSingleton<ITcgService,       TcgService>();

// LGPD — Auditoria e privacidade
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

// ---------------------------------------------------------------------------
// 12. CORS — origens lidas de config para facilitar deploy sem rebuild
// ---------------------------------------------------------------------------
// CORS: origens lidas de config para evitar hardcoded e facilitar deploy
var corsOrigins = (builder.Configuration["CorsSettings:AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ---------------------------------------------------------------------------
// 13. SWAGGER
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "CardGameStore API",
        Version     = "v1",
        Description = "API para gestão da loja de Card Games — softNerd"
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
// 14. BUILD
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// 15. BANCO DE DADOS — EnsureCreated em dev, Migrations em produção
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // EnsureCreated usa o provider correto (SQLite em dev, Npgsql em prod)
        // e cria as tabelas com os tipos nativos de cada banco.
        // Migrations virão numa próxima fase quando o schema estiver estável.
        logger.LogInformation("Inicializando banco de dados...");
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Banco pronto.");

        // Seed: cria o admin se não existir
        if (!db.Users.Any(u => u.Email == "admin@cardgamestore.com.br"))
        {
            var adminPassword = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD") ?? "SenhaForte@123";
            if (adminPassword == "SenhaForte@123")
                logger.LogWarning("ATENÇÃO: admin criado com senha padrão. Defina ADMIN_SEED_PASSWORD no ambiente de produção!");

            db.Users.Add(new CardGameStore.Models.PostgreSQL.User
            {
                Id           = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name         = "Maikon",
                Email        = "admin@cardgamestore.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role         = CardGameStore.Models.PostgreSQL.UserRole.Admin,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Usuário admin criado com sucesso.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao inicializar o banco: {Msg}", ex.Message);
        throw;
    }
}

// ---------------------------------------------------------------------------
// 16. MIDDLEWARE PIPELINE
// ---------------------------------------------------------------------------

// ForwardedHeaders — lê X-Forwarded-For/Proto do proxy reverso (nginx/Cloudflare)
// de forma controlada pelo runtime, eliminando leitura manual do header nos serviços
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
// Aceita proxy da rede Docker interna (172.16.0.0/12) e loopback
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
forwardedOptions.KnownProxies.Add(IPAddress.Loopback);
forwardedOptions.KnownProxies.Add(IPAddress.IPv6Loopback);
app.UseForwardedHeaders(forwardedOptions);

// Headers de segurança HTTP em todas as respostas
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"]  = "nosniff";
    context.Response.Headers["X-Frame-Options"]         = "DENY";
    context.Response.Headers["X-XSS-Protection"]        = "1; mode=block";
    context.Response.Headers["Referrer-Policy"]         = "no-referrer";
    context.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
    // CSP: API retorna apenas JSON/binários — bloquear todo conteúdo ativo
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

// Swagger apenas em desenvolvimento — evita expor a estrutura da API em produção
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CardGameStore API v1");
        c.RoutePrefix   = "swagger"; // UI disponível em /swagger
        c.DocumentTitle = "CardGameStore — softNerd";
    });
}

// SSL gerenciado pelo reverse proxy (Nginx/Cloudflare) — não redirecionar aqui
app.UseStaticFiles(); // serve wwwroot/uploads/* como arquivos estáticos
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ComandaHub>("/hubs/comanda");

// /health — sem autenticação, sem rate limit
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = new
        {
            Status    = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            Checks    = report.Entries.Select(e => new
            {
                Name    = e.Key,
                Status  = e.Value.Status.ToString(),
                Message = e.Value.Description,
            })
        };
        await ctx.Response.WriteAsJsonAsync(result);
    }
})
.AllowAnonymous()
.DisableRateLimiting();

// Aplica migrations pendentes automaticamente na inicialização
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
