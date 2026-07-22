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
using CardGameStore.Middleware;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Comando utilitário: dotnet run -- gen-key  →  imprime nova chave AES-256 em Base64
if (args.Contains("gen-key"))
{
    Console.WriteLine("Nova chave AES-256 (copie para Encryption:Key no VPS):");
    Console.WriteLine(EncryptionService.GenerateKey());
    return;
}

// Comando utilitário: dotnet run -- gen-vapid  →  gera par de chaves VAPID para push browser
if (args.Contains("gen-vapid"))
{
    var keys = WebPush.VapidHelper.GenerateVapidKeys();
    Console.WriteLine("# Adicione ao .env do VPS:");
    Console.WriteLine($"VAPID__PublicKey={keys.PublicKey}");
    Console.WriteLine($"VAPID__PrivateKey={keys.PrivateKey}");
    Console.WriteLine("VAPID__Subject=mailto:contato@tenant-erp.local");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. CONFIGURAÇÕES
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// ---------------------------------------------------------------------------
// 2. BANCO RELACIONAL — SQLite (dev local) ou PostgreSQL (produção/Docker)
// ---------------------------------------------------------------------------
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL");
var useSqlite = string.IsNullOrWhiteSpace(pgConnStr);

// ITenantContext é scoped — cada request (ou cada escopo manual criado por um
// hosted service) tem sua própria instância, com o valor padrão já apontando
// pro tenant-zero (schema "public").
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    if (useSqlite)
    {
        // SQLite não tem conceito de schema/search_path — o interceptor de
        // tenant só faz sentido contra Postgres.
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "cardgamestore.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
    else
    {
        // A tabela de histórico de migrations (__EFMigrationsHistory) precisa de
        // schema explícito, não pode depender do search_path como o resto do
        // model: a checagem "a tabela existe?" do provider Npgsql não é
        // consistentemente scoped pelo search_path da mesma forma que a query
        // real de leitura, o que causava um mismatch — a checagem "achava" a
        // tabela (via public, de outro tenant) mas a leitura real (isolada só
        // no schema do tenant atual) não encontrava nada, derrubando o
        // provisionamento de tenant novo com "relation does not exist".
        var tenantSchemaForHistory = sp.GetRequiredService<ITenantContext>().SchemaName;
        options.UseNpgsql(
            pgConnStr,
            npgsqlOptions => npgsqlOptions
                .EnableRetryOnFailure(maxRetryCount: 5)
                .MigrationsHistoryTable("__EFMigrationsHistory", tenantSchemaForHistory)
        );
        // Resolve do próprio IServiceProvider scoped (sp) — pega a MESMA
        // instância de ITenantContext que o TenantResolutionMiddleware populou
        // nesta requisição, não uma nova.
        options.AddInterceptors(new TenantConnectionInterceptor(
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<ILogger<TenantConnectionInterceptor>>()));
    }

    // Diff automático de auditoria (Product/VendaAvulsa/User) — independe do
    // provider, roda em dev (SQLite) e produção (Postgres) igual.
    options.AddInterceptors(new AuditSaveChangesInterceptor(
        sp.GetRequiredService<IHttpContextAccessor>(),
        sp.GetRequiredService<ILogger<AuditSaveChangesInterceptor>>(),
        sp.GetRequiredService<IConfiguration>()));
});

// Catálogo de tenants — em Postgres é o mesmo banco físico do AppDbContext
// (schema "public"), contexto leve e independente, sem o interceptor de
// search_path. Em SQLite (dev) usa um arquivo próprio: EnsureCreatedAsync só
// olha se o arquivo/banco já existe, não se as tabelas do model atual estão
// lá — dois contextos apontando pro mesmo arquivo faria o segundo virar no-op.
builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    if (useSqlite)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "cardgamestore_catalog.db");
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
    // AdminOnly: Admin e Operator passam — OperatorPermissionMiddleware cuida do controle granular por rota.
    options.AddPolicy("AdminOnly",         policy => policy.RequireRole("Admin", "Operator"));
    options.AddPolicy("CustomerOrAdmin",   policy => policy.RequireRole("Admin", "Customer", "Operator"));
    options.AddPolicy("PlatformOwnerOnly", policy => policy.RequireRole("PlatformOwner"));
    options.AddPolicy("ContadorOnly",      policy => policy.RequireRole("Contador"));
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
    // Cloudflare encaminha o IP real do cliente em CF-Connecting-IP.
    // Usar RemoteIpAddress aqui resultaria no IP do nó Cloudflare, fazendo todos os
    // usuários compartilharem o mesmo bucket de rate limiting.
    static string GetClientIp(HttpContext ctx) =>
        ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    // Política global — protege TODOS os endpoints sem [EnableRateLimiting] explícito
    // 300 req/min por IP é generoso o suficiente para uso legítimo
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(context),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit          = 300,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit              = 15; // era 5 — QA testando várias contas com autofill errado batia nisso toda hora
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

    // "locate-account" → bem mais apertado que "auth": cada chamada testa a
    // senha contra TODO tenant ativo (um schema por vez), bem mais caro que um
    // login normal (uma query só). 5/hora por IP é suficiente pro uso real
    // (clicar "procurar em outro lugar" depois de um login falhado de verdade),
    // sem abrir uma forma barata de forçar senha contra todas as lojas de uma vez.
    options.AddFixedWindowLimiter("locate-account", opt =>
    {
        opt.PermitLimit          = 5;
        opt.Window               = TimeSpan.FromHours(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // "comanda-hub" → conexões (negotiate + upgrade) ao ComandaHub: 30/minuto
    // por IP. JoinComandaGroup já valida dono da comanda antes de entrar no
    // grupo, mas não impedia spam de tentativas de conexão (DoS/botting) —
    // isso cobre esse ponto. 30/min é generoso pra WebSocket normal (só 1-2
    // requests por sessão) e pro fallback de long-polling (poll periódico,
    // bem abaixo de 30/min em uso normal); só limita tentativa de conexão
    // repetida em rajada.
    options.AddFixedWindowLimiter("comanda-hub", opt =>
    {
        opt.PermitLimit          = 30;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
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
// AwesomeAPI — Cotação USD/BRL em tempo real (gratuita, sem autenticação)
builder.Services.AddHttpClient("AwesomeApi", client =>
{
    client.BaseAddress = new Uri("https://economia.awesomeapi.com.br/");
    client.Timeout     = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Gemini 2.0 Flash — assistente IA conversacional
builder.Services.AddHttpClient("gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// IBPT — token vai na query string por exigência do contrato legado da API.
// Remove os loggers do HttpClient para a credencial nunca aparecer em logs de URL.
builder.Services.AddHttpClient("ibpt", client =>
{
    client.BaseAddress = new Uri("https://apidoni.ibpt.org.br/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).RemoveAllLoggers();

// ---------------------------------------------------------------------------
// 10. HEALTH CHECKS — Postgres via IHealthCheck com injeção correta
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("postgres", tags: ["db", "postgres"]);

// ---------------------------------------------------------------------------
// 11. SERVIÇOS DE APLICAÇÃO
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAuthService,         AuthService>();
builder.Services.AddScoped<IAccountLocatorService, AccountLocatorService>();
builder.Services.AddScoped<IComandaService,      ComandaService>();
builder.Services.AddScoped<IProductService,      ProductService>();
builder.Services.AddScoped<ICategoryService,     CategoryService>();
builder.Services.AddScoped<IUserService,         UserService>();
builder.Services.AddScoped<IVendaAvulsaService,  VendaAvulsaService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IEmailService,        EmailService>();
builder.Services.AddScoped<IPushService,         PushService>();
builder.Services.AddScoped<IAiChatService,       GeminiChatService>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddScoped<IFinanceiroCalculoService, FinanceiroCalculoService>();
builder.Services.AddHostedService<FechamentoBackgroundService>();
builder.Services.AddSingleton<CurrencyService>();
builder.Services.AddMemoryCache();

// LGPD — Auditoria e privacidade
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<OfxParserService>();
builder.Services.AddScoped<SefazNfeService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddScoped<InterSyncService>();
builder.Services.AddHostedService<InterSyncBackgroundService>();

// Fiscal — emissão de NFC-e, certificado A1, exportação de XMLs
builder.Services.AddScoped<FiscalCertificadoService>();
builder.Services.AddScoped<FiscalXmlExportService>();
builder.Services.AddScoped<IbptTaxService>();
builder.Services.AddHostedService<IbptSyncBackgroundService>();
builder.Services.AddScoped<IFiscalTaxEngine, ConfigurableFiscalTaxEngine>();
builder.Services.AddScoped<INfceEmissionService>(sp => new NfceEmissionService(
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<EncryptionService>(),
    sp.GetRequiredService<ILogger<NfceEmissionService>>(),
    sp.GetRequiredService<IFiscalTaxEngine>()));
builder.Services.AddHostedService<FiscalAlertBackgroundService>();
builder.Services.AddHostedService<FiscalXmlExportBackgroundService>();
builder.Services.AddHostedService<FiscalRetryBackgroundService>();
builder.Services.AddHostedService<SefazDistBackgroundService>();

// ---------------------------------------------------------------------------
// 12. CORS — origens lidas de config para facilitar deploy sem rebuild
// ---------------------------------------------------------------------------
// CORS: origens lidas de config para evitar hardcoded e facilitar deploy
var corsOrigins = (builder.Configuration["CorsSettings:AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// RootDomain (mesma config usada pelo TenantResolutionMiddleware) libera
// qualquer subdomínio de tenant, em paralelo com a lista de origens fixas
// acima (IP de teste, domínio raiz) — nenhuma substitui a outra.
var corsRootDomain = builder.Configuration["Multitenancy:RootDomain"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (corsOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    return true;

                if (string.IsNullOrWhiteSpace(corsRootDomain))
                    return false;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                    return false;

                var host = originUri.Host;
                return host.Equals(corsRootDomain, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("." + corsRootDomain, StringComparison.OrdinalIgnoreCase);
            })
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
        Title       = "Tenant-ERP API",
        Version     = "v1",
        Description = "API do Tenant-ERP — sistema de gestão para lojas e varejo"
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

    // Requisito de segurança é adicionado por operação (ver AuthorizeCheckOperationFilter),
    // não aqui globalmente — um requisito global de documento faz TODO endpoint mostrar
    // cadeado no Swagger UI, inclusive os [AllowAnonymous] (limitação do formato OpenAPI:
    // uma lista de segurança vazia por operação é omitida do JSON, não sobrescreve o default).
    c.OperationFilter<CardGameStore.Swagger.AuthorizeCheckOperationFilter>();

    // Comentários /// dos controllers/DTOs viram descrição de endpoint/campo na doc.
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
        c.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
});

builder.Services.AddControllers();

// ---------------------------------------------------------------------------
// 14. BUILD
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// 14.5 CHECAGEM DE SEGURANÇA — avisa em todo boot (não só no seed) se cookies
// não estão seguros em produção. COOKIE_SECURE=false é o padrão gerado pelo
// setup.sh pro primeiro deploy sem domínio/HTTPS (teste por IP puro) — mas é
// fácil esquecer de trocar pra true depois que domínio + Cloudflare entram no ar.
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment() && app.Configuration.GetValue<bool?>("COOKIE_SECURE") == false)
{
    app.Logger.LogWarning(
        "ATENÇÃO: COOKIE_SECURE=false em produção — cookies JWT trafegam sem o flag " +
        "Secure. Só é esperado no primeiro deploy sem domínio/HTTPS (teste por IP puro). " +
        "Assim que configurar domínio + Cloudflare, troque COOKIE_SECURE pra true no .env.");
}

// B4: Security:IpHashSalt cai num fallback fixo ("tenant-erp-ip-salt-dev", só pra
// dev) se IP_HASH_SALT não estiver no .env — setup.sh já gera um valor aleatório em
// todo deploy novo, então isso só dispara se alguém apagou a variável depois. Aviso
// (não fail-fast, mesma lição do M26) porque não sei se algum ambiente já em produção
// depende do fallback antigo.
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration["Security:IpHashSalt"]))
{
    app.Logger.LogWarning(
        "ATENÇÃO: Security:IpHashSalt não configurado em produção — caindo no salt fixo de " +
        "desenvolvimento, conhecido no código-fonte. Configure IP_HASH_SALT no .env (setup.sh já " +
        "gera um valor aleatório em deploys novos).");
}

// ---------------------------------------------------------------------------
// 15. BANCO DE DADOS — EnsureCreated em dev sem Postgres (SQLite), Migrations em Postgres
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    // C3: marca explicitamente o tenant-zero ANTES de resolver o AppDbContext — o
    // TenantConnectionInterceptor agora falha rápido se uma conexão abrir sem Set()
    // ter sido chamado neste escopo (ver ValidateSchemaName). As migrations deste
    // bloco (catálogo + schema "public") são a única operação legítima fora de
    // request HTTP que opera no tenant-zero por padrão, então marca de propósito
    // em vez de deixar cair no default silenciosamente.
    scope.ServiceProvider.GetRequiredService<ITenantContext>()
        .Set(TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, new[] { "fiscal" });

    var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var logger  = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Inicializando banco de dados...");

        if (useSqlite)
        {
            // SQLite (dev local sem Postgres configurado) não tem migrations —
            // schema é gerado direto do model atual a cada start.
            await db.Database.EnsureCreatedAsync();
            await catalog.Database.EnsureCreatedAsync();
        }
        else
        {
            // Postgres — schema versionado via EF Core Migrations (Data/Migrations).
            // As duas chamadas abaixo só migram o schema "public" (tenant-zero,
            // resolvido por padrão pelo ITenantContext fora de qualquer request
            // HTTP) e o catálogo — NENHUMA migration de AppDbContext propaga
            // sozinha pro schema de um tenant já provisionado antes (só
            // TenantProvisioningService roda migration num schema de tenant, e só
            // uma vez, na criação). Sem o loop abaixo, qualquer tabela/coluna nova
            // do AppDbContext (ex: FechamentoPeriodo, os ícones novos do
            // SiteConfig) nunca chega em tenants como loja-final/loja-teste3 —
            // bug real, confirmado em produção nesta sessão.
            await catalog.Database.MigrateAsync();
            await db.Database.MigrateAsync();

            var tenantsParaMigrar = await catalog.Tenants
                .Where(t => t.Status == TenantStatus.Active)
                .Select(t => new { t.Id, t.Slug, t.SchemaName, t.EnabledModules })
                .ToListAsync();

            // C4 (parcial — VPS único por enquanto, sem lock/processo migrador separado):
            // antes o catch abaixo só logava por tenant e o resumo final dizia sempre
            // "aplicadas em N tenant(s)", sem distinguir sucesso de falha — um tenant
            // ficava sem migrar silenciosamente até o próximo restart, sem nada gritando
            // sobre isso. Agora rastreia falhas e o resumo final é WARNING (não INFO) se
            // qualquer uma ocorreu, com a lista de slugs — visível no boot, não só grep de log.
            var tenantsComFalha = new List<string>();

            foreach (var tenant in tenantsParaMigrar)
            {
                try
                {
                    using var tenantScope = app.Services.CreateScope();
                    var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                    tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

                    var tenantDb = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await tenantDb.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    // Um schema quebrado/tenant com migration pendente conflitante
                    // não pode travar o boot dos outros — loga e segue o loop.
                    tenantsComFalha.Add(tenant.Slug);
                    logger.LogError(ex, "Falha ao migrar schema do tenant {Slug} ({SchemaName})", tenant.Slug, tenant.SchemaName);
                }
            }

            if (tenantsComFalha.Count > 0)
                logger.LogWarning(
                    "Migrations: {Ok}/{Total} tenant(s) OK — FALHOU em {Falha}: {Slugs}. Esses tenants " +
                    "continuam rodando no schema desatualizado até o próximo restart bem-sucedido — " +
                    "investigar antes que um endpoint novo quebre pra eles.",
                    tenantsParaMigrar.Count - tenantsComFalha.Count, tenantsParaMigrar.Count,
                    tenantsComFalha.Count, string.Join(", ", tenantsComFalha));
            else
                logger.LogInformation("Migrations aplicadas em {Count} tenant(s) ativo(s)", tenantsParaMigrar.Count);
        }

        logger.LogInformation("Banco pronto.");

        // Seed: cria o admin se não existir
        if (!db.Users.Any(u => u.Email == "admin@tenant-erp.local"))
        {
            var adminPassword = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD") ?? "SenhaForte@123";
            if (adminPassword == "SenhaForte@123")
                logger.LogWarning("ATENÇÃO: admin criado com senha padrão. Defina ADMIN_SEED_PASSWORD no ambiente de produção!");

            db.Users.Add(new CardGameStore.Models.PostgreSQL.User
            {
                Id           = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name         = "Admin",
                Email        = "admin@tenant-erp.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role         = CardGameStore.Models.PostgreSQL.UserRole.Admin,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Usuário admin criado com sucesso.");
        }

        // Seed: cria o dono da plataforma se não existir — mesma ideia do admin
        // acima, mas com role PlatformOwner. Vive no schema "public" (tenant-zero),
        // então só loga de verdade pelo domínio raiz (fora de qualquer subdomínio
        // de tenant) — ver TenantResolutionMiddleware/TenantClaimGuardMiddleware.
        var platformOwnerEmail = Environment.GetEnvironmentVariable("PLATFORM_OWNER_EMAIL");
        if (!string.IsNullOrWhiteSpace(platformOwnerEmail) && !db.Users.Any(u => u.Email == platformOwnerEmail))
        {
            var ownerPassword = Environment.GetEnvironmentVariable("PLATFORM_OWNER_SEED_PASSWORD") ?? "SenhaForte@123";
            if (ownerPassword == "SenhaForte@123")
                logger.LogWarning("ATENÇÃO: dono da plataforma criado com senha padrão. Defina PLATFORM_OWNER_SEED_PASSWORD no ambiente de produção!");

            db.Users.Add(new CardGameStore.Models.PostgreSQL.User
            {
                Name         = "Dono da Plataforma",
                Email        = platformOwnerEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(ownerPassword),
                Role         = CardGameStore.Models.PostgreSQL.UserRole.PlatformOwner,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Usuário dono da plataforma criado com sucesso.");
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

// Captura qualquer exceção não tratada de todo o pipeline abaixo — primeiro
// middleware de propósito, envolve literalmente tudo. Sem isso, uma exceção
// que escapasse de um controller virava um 500 vazio (produção) ou a página
// de erro em HTML do próprio .NET (dev) — nenhum dos dois dá pro frontend
// mostrar mensagem nenhuma pro usuário/QA, só um toast genérico de "erro
// desconhecido" sem pista nenhuma do que aconteceu de verdade.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Exceção não tratada em {Method} {Path} (trace {TraceId})",
            context.Request.Method, context.Request.Path, context.TraceIdentifier);

        if (context.Response.HasStarted)
            throw; // resposta já começou a ser escrita — não dá pra reescrever status/body

        context.Response.Clear();
        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var isDev = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        var message = isDev
            ? $"{ex.GetType().Name}: {ex.Message}"
            : "Erro interno. Tente novamente em instantes.";

        await context.Response.WriteAsJsonAsync(new { message, traceId = context.TraceIdentifier });
    }
});

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
    // CSP: API retorna apenas JSON/binários — bloquear todo conteúdo ativo.
    // Exceção: /swagger (só existe em Development) precisa carregar seu próprio
    // CSS/JS/imagens — mesma origem, sem CDN externo — pra sequer renderizar.
    context.Response.Headers["Content-Security-Policy"] =
        context.Request.Path.StartsWithSegments("/swagger")
            ? "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'"
            : "default-src 'none'; frame-ancestors 'none'";
    await next();
});

// Resolve o tenant da requisição (por Host) antes de qualquer coisa que possa
// tocar o AppDbContext — o schema que o TenantConnectionInterceptor usa vem
// do ITenantContext que este middleware popula.
app.UseTenantResolution();

// Swagger apenas em desenvolvimento — evita expor a estrutura da API em produção
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tenant-ERP API v1");
        c.RoutePrefix   = "swagger"; // UI disponível em /swagger
        c.DocumentTitle = "Tenant-ERP API";
    });
}

// SSL gerenciado pelo reverse proxy (Nginx/Cloudflare) — não redirecionar aqui
app.UseStaticFiles(); // serve wwwroot/uploads/* como arquivos estáticos
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseAuthentication();
app.UseTenantClaimGuard();
app.UseAuthorization();
app.UseOperatorPermissions();

app.MapControllers();
app.MapHub<ComandaHub>("/hubs/comanda").RequireRateLimiting("comanda-hub");

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

app.Run();
