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
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

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
    Console.WriteLine("VAPID__Subject=mailto:contato@santuarionerd.tech");
    return;
}

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
    // AdminOnly: Admin e Operator passam — OperatorPermissionMiddleware cuida do controle granular por rota.
    options.AddPolicy("AdminOnly",       policy => policy.RequireRole("Admin", "Operator"));
    options.AddPolicy("CustomerOrAdmin", policy => policy.RequireRole("Admin", "Customer", "Operator"));
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

// AwesomeAPI — Cotação USD/BRL em tempo real (gratuita, sem autenticação)
builder.Services.AddHttpClient("AwesomeApi", client =>
{
    client.BaseAddress = new Uri("https://economia.awesomeapi.com.br/");
    client.Timeout     = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// LoL Riftbound — Riftcodex API (gratuita, sem auth) https://api.riftcodex.com
builder.Services.AddHttpClient("RiftboundApi", client =>
{
    client.BaseAddress = new Uri("https://api.riftcodex.com/");
    client.Timeout     = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "CardGameStore/1.0 (softnerd.com.br)");
});

// Scrydex API — fonte paralela com preços de mercado (requer TcgSettings:ScrydexApiKey e ScrydexTeamId)
builder.Services.AddHttpClient("ScrydexApi", client =>
{
    client.BaseAddress = new Uri("https://api.scrydex.com/");
    client.Timeout     = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "CardGameStore/1.0 (softnerd.com.br)");
});

// OPTCG API — One Piece TCG (gratuita, sem auth, cobre OP-01..OP-15 + starter decks)
builder.Services.AddHttpClient("OptcgApi", client =>
{
    client.BaseAddress = new Uri("https://optcgapi.com/");
    client.Timeout     = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "CardGameStore/1.0 (softnerd.com.br)");
});

// TCGdex — fonte multilíngue de cartas Pokémon (suporte a português nativo, gratuita, sem auth)
builder.Services.AddHttpClient("TcgDexApi", client =>
{
    client.BaseAddress = new Uri("https://api.tcgdex.net/");
    client.Timeout     = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "CardGameStore/1.0 (softnerd.com.br)");
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
builder.Services.AddScoped<IPushService,         PushService>();
builder.Services.AddScoped<IAiChatService,       GeminiChatService>();
builder.Services.AddSingleton<ITcgApiClient,     TcgApiClient>();
builder.Services.AddSingleton<ITcgService,       TcgService>();
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
builder.Services.AddScoped<INfceEmissionService, NfceEmissionService>();
builder.Services.AddHostedService<FiscalAlertBackgroundService>();
builder.Services.AddHostedService<FiscalXmlExportBackgroundService>();
builder.Services.AddHostedService<FiscalRetryBackgroundService>();

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

        // Cria tabelas/colunas novas que EnsureCreated não alcança em bancos já existentes.
        // EnsureCreated retorna false sem alterar nada se já existirem tabelas no banco,
        // por isso usamos DDL explícito com IF NOT EXISTS para tornar o startup idempotente.
        if (!useSqlite)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS perfis (
                    id                  UUID         NOT NULL DEFAULT gen_random_uuid(),
                    nome                VARCHAR(100) NOT NULL,
                    permissoes_json     TEXT         NOT NULL DEFAULT '[]',
                    criado_por_admin_id UUID         NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    criado_em           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    atualizado_em       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_perfis PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_perfis_nome ON perfis (nome);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS perfil_id                    UUID         REFERENCES perfis(id) ON DELETE SET NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS password_reset_token        VARCHAR(200) NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS password_reset_token_expiry TIMESTAMPTZ  NULL;

                ALTER TABLE comanda_items ADD COLUMN IF NOT EXISTS variant_id UUID NULL REFERENCES product_variants(id) ON DELETE SET NULL;
                ALTER TABLE lgpd_requests ADD COLUMN IF NOT EXISTS anexo_nome VARCHAR(255) NULL;
                ALTER TABLE lgpd_requests ADD COLUMN IF NOT EXISTS anexo_dados BYTEA NULL;

                CREATE TABLE IF NOT EXISTS decks (
                    id          UUID         NOT NULL DEFAULT gen_random_uuid(),
                    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    name        VARCHAR(100) NOT NULL,
                    game        VARCHAR(50)  NOT NULL DEFAULT 'Pokemon',
                    format      VARCHAR(20)  NOT NULL DEFAULT 'Standard',
                    cards_json  TEXT         NOT NULL DEFAULT '[]',
                    is_public   BOOLEAN      NOT NULL DEFAULT FALSE,
                    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_decks PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_decks_user        ON decks (user_id);
                CREATE INDEX IF NOT EXISTS ix_decks_user_public ON decks (user_id, is_public);

                CREATE TABLE IF NOT EXISTS product_waitlist (
                    id          UUID        NOT NULL DEFAULT gen_random_uuid(),
                    product_id  UUID        NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                    user_id     UUID        REFERENCES users(id) ON DELETE SET NULL,
                    name        VARCHAR(150) NOT NULL,
                    whatsapp    VARCHAR(20)  NOT NULL,
                    position    INT          NOT NULL,
                    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    notified_at TIMESTAMPTZ,
                    CONSTRAINT pk_product_waitlist PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_product_waitlist_product ON product_waitlist (product_id);
                CREATE INDEX IF NOT EXISTS ix_product_waitlist_user    ON product_waitlist (user_id) WHERE user_id IS NOT NULL;

                -- Deck em pré-inscrição e participante de campeonato
                ALTER TABLE championship_preinscricoes ADD COLUMN IF NOT EXISTS deck_id   UUID         NULL;
                ALTER TABLE championship_preinscricoes ADD COLUMN IF NOT EXISTS deck_name VARCHAR(200) NULL;
                ALTER TABLE championship_participants   ADD COLUMN IF NOT EXISTS deck_id   UUID         NULL;

                -- Timers de torneio
                CREATE TABLE IF NOT EXISTS timers (
                    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    name             VARCHAR(100) NOT NULL DEFAULT 'Timer',
                    duration_seconds INTEGER      NOT NULL DEFAULT 1800,
                    paused_remaining INTEGER      NULL,
                    state            INTEGER      NOT NULL DEFAULT 0,
                    started_at       TIMESTAMPTZ  NULL,
                    sound_preset     VARCHAR(50)  NOT NULL DEFAULT 'bell',
                    warn_at_seconds  INTEGER      NOT NULL DEFAULT 60,
                    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );

                -- Marketplace de cartas entre usuários
                CREATE TABLE IF NOT EXISTS card_listings (
                    id             UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id        UUID          NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    card_name      VARCHAR(200)  NOT NULL,
                    card_game      VARCHAR(100)  NULL,
                    card_image_url VARCHAR(500)  NULL,
                    price_in_cents INTEGER       NOT NULL DEFAULT 0,
                    condition      VARCHAR(50)   NOT NULL DEFAULT 'NM',
                    description    VARCHAR(1000) NULL,
                    status         VARCHAR(20)   NOT NULL DEFAULT 'Available',
                    created_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                    updated_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_card_listings_user   ON card_listings (user_id);
                CREATE INDEX IF NOT EXISTS ix_card_listings_status ON card_listings (status);

                CREATE TABLE IF NOT EXISTS listing_interests (
                    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    listing_id UUID         NOT NULL REFERENCES card_listings(id) ON DELETE CASCADE,
                    user_id    UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    message    VARCHAR(500) NULL,
                    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    UNIQUE (listing_id, user_id)
                );
                CREATE INDEX IF NOT EXISTS ix_listing_interests_listing ON listing_interests (listing_id);
                CREATE INDEX IF NOT EXISTS ix_listing_interests_user    ON listing_interests (user_id);

                -- Consentimento LGPD: comprador autoriza expor WhatsApp ao vendedor
                ALTER TABLE listing_interests ADD COLUMN IF NOT EXISTS share_contact BOOLEAN NOT NULL DEFAULT FALSE;

                -- Variantes de produto (grade tamanho/cor para roupas e similares)
                ALTER TABLE products ADD COLUMN IF NOT EXISTS has_variants BOOLEAN NOT NULL DEFAULT FALSE;

                CREATE TABLE IF NOT EXISTS product_variants (
                    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    product_id      UUID         NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                    size            VARCHAR(50)  NULL,
                    color           VARCHAR(100) NULL,
                    stock_quantity  INTEGER      NOT NULL DEFAULT 0,
                    price_in_cents  INTEGER      NULL,
                    sku             VARCHAR(100) NULL,
                    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_product_variants_product ON product_variants (product_id);

                -- Reservas de produtos via site (não usadas no PDV)
                CREATE TABLE IF NOT EXISTS product_reservations (
                    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id       UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    product_id    UUID         NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                    variant_id    UUID         NULL REFERENCES product_variants(id) ON DELETE SET NULL,
                    quantity      INTEGER      NOT NULL DEFAULT 1,
                    status        VARCHAR(20)  NOT NULL DEFAULT 'active',
                    notes         VARCHAR(500) NULL,
                    reserved_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    expires_at    TIMESTAMPTZ  NOT NULL,
                    fulfilled_at  TIMESTAMPTZ  NULL,
                    cancelled_at  TIMESTAMPTZ  NULL
                );
                CREATE INDEX IF NOT EXISTS ix_product_reservations_user    ON product_reservations (user_id);
                CREATE INDEX IF NOT EXISTS ix_product_reservations_product ON product_reservations (product_id);
                CREATE INDEX IF NOT EXISTS ix_product_reservations_status  ON product_reservations (status);

                -- Fiscal: emissão de NFC-e (certificado A1, config da empresa emitente)
                CREATE TABLE IF NOT EXISTS fiscal_config (
                    id                                UUID         NOT NULL DEFAULT gen_random_uuid(),
                    cnpj                              VARCHAR(18)  NOT NULL,
                    inscricao_estadual                VARCHAR(20)  NULL,
                    regime_tributario                 VARCHAR(30)  NOT NULL DEFAULT 'SimplesNacional',
                    ambiente                          VARCHAR(20)  NOT NULL DEFAULT 'Homologacao',
                    serie_nfce                        INTEGER      NOT NULL DEFAULT 1,
                    proximo_numero_nfce               INTEGER      NOT NULL DEFAULT 1,
                    email_contador                    VARCHAR(200) NULL,
                    certificado_pfx_encrypted         TEXT         NULL,
                    certificado_senha_encrypted       TEXT         NULL,
                    certificado_validade              TIMESTAMPTZ  NULL,
                    certificado_uploaded_at           TIMESTAMPTZ  NULL,
                    certificado_ultimo_alerta_limiar  INTEGER      NULL,
                    created_at                        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at                         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_fiscal_config PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_fiscal_config_cnpj ON fiscal_config (cnpj);

                -- Fiscal: endereço do estabelecimento (obrigatório no XML da NFC-e)
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS razao_social         VARCHAR(150) NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS logradouro           VARCHAR(150) NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS numero               VARCHAR(20)  NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS complemento          VARCHAR(100) NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS bairro               VARCHAR(100) NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS codigo_municipio_ibge VARCHAR(7)  NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS municipio            VARCHAR(100) NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS uf                   VARCHAR(2)  NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS cep                  VARCHAR(9)  NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS csc_id               VARCHAR(10) NULL;
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS csc_token            VARCHAR(100) NULL;

                -- Fiscal: natureza de operação reutilizável (CFOP/CSOSN, estilo Bling)
                CREATE TABLE IF NOT EXISTS naturezas_operacao (
                    id          UUID         NOT NULL DEFAULT gen_random_uuid(),
                    descricao   VARCHAR(150) NOT NULL,
                    cfop        VARCHAR(4)   NOT NULL,
                    csosn       VARCHAR(3)   NULL,
                    is_padrao   BOOLEAN      NOT NULL DEFAULT FALSE,
                    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
                    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_naturezas_operacao PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_naturezas_operacao_descricao ON naturezas_operacao (descricao);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_naturezas_operacao_unica_padrao
                    ON naturezas_operacao (is_padrao) WHERE is_padrao = true;

                -- Fiscal: NCM e natureza de operação por produto
                ALTER TABLE products ADD COLUMN IF NOT EXISTS ncm VARCHAR(8) NULL;
                ALTER TABLE products ADD COLUMN IF NOT EXISTS natureza_operacao_id UUID NULL REFERENCES naturezas_operacao(id) ON DELETE SET NULL;
                CREATE INDEX IF NOT EXISTS ix_products_natureza_operacao ON products (natureza_operacao_id);

                -- Fiscal: controle do envio mensal automático do ZIP de XMLs pro contador
                ALTER TABLE fiscal_config ADD COLUMN IF NOT EXISTS ultimo_envio_mensal_xmls TIMESTAMPTZ NULL;

                -- Fiscal: notas fiscais emitidas (NFC-e por Comanda/Venda Avulsa)
                CREATE TABLE IF NOT EXISTS notas_fiscais_emitidas (
                    id                       UUID         NOT NULL DEFAULT gen_random_uuid(),
                    origem                   VARCHAR(20)  NOT NULL,
                    comanda_id               UUID         NULL,
                    venda_avulsa_id          VARCHAR(50)  NULL,
                    status                   VARCHAR(20)  NOT NULL DEFAULT 'PendenteEmissao',
                    valor_total_em_centavos  INTEGER      NOT NULL DEFAULT 0,
                    serie                    INTEGER      NULL,
                    numero                   INTEGER      NULL,
                    chave_acesso             VARCHAR(44)  NULL,
                    protocolo                VARCHAR(30)  NULL,
                    motivo_rejeicao          TEXT         NULL,
                    xml_autorizado           TEXT         NULL,
                    emitido_em               TIMESTAMPTZ  NULL,
                    cancelado_em             TIMESTAMPTZ  NULL,
                    created_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_notas_fiscais_emitidas PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_notas_fiscais_status      ON notas_fiscais_emitidas (status);
                CREATE INDEX IF NOT EXISTS ix_notas_fiscais_comanda     ON notas_fiscais_emitidas (comanda_id);
                CREATE INDEX IF NOT EXISTS ix_notas_fiscais_emitido_em  ON notas_fiscais_emitidas (emitido_em);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_notas_fiscais_chave_acesso
                    ON notas_fiscais_emitidas (chave_acesso) WHERE chave_acesso IS NOT NULL;

                -- Fiscal: cancelamento, inutilização e controle de reprocessamento
                ALTER TABLE notas_fiscais_emitidas ADD COLUMN IF NOT EXISTS justificativa_cancelamento TEXT        NULL;
                ALTER TABLE notas_fiscais_emitidas ADD COLUMN IF NOT EXISTS inutilizado_em             TIMESTAMPTZ NULL;
                ALTER TABLE notas_fiscais_emitidas ADD COLUMN IF NOT EXISTS protocolo_inutilizacao      VARCHAR(30) NULL;
                ALTER TABLE notas_fiscais_emitidas ADD COLUMN IF NOT EXISTS tentativas_reprocessamento  INTEGER     NOT NULL DEFAULT 0;

                -- Financeiro: chave Pix cadastrada no Inter (para emitir cobrança via API)
                ALTER TABLE integration_configs ADD COLUMN IF NOT EXISTS pix_key VARCHAR(100) NULL;

                -- Financeiro: cobranças Pix imediatas (Crediário, Comanda ou Venda Avulsa)
                CREATE TABLE IF NOT EXISTS pix_cobrancas (
                    id                   UUID         NOT NULL DEFAULT gen_random_uuid(),
                    origem               VARCHAR(20)  NOT NULL DEFAULT 'Crediario',
                    crediario_id         UUID         NULL REFERENCES crediarios(id) ON DELETE CASCADE,
                    comanda_id           UUID         NULL REFERENCES comandas(id) ON DELETE CASCADE,
                    venda_avulsa_id      VARCHAR(50)  NULL,
                    tx_id                VARCHAR(35)  NOT NULL,
                    valor_em_centavos    INTEGER      NOT NULL DEFAULT 0,
                    status               VARCHAR(40)  NOT NULL DEFAULT 'ATIVA',
                    pix_copia_cola       TEXT         NULL,
                    imagem_qrcode        TEXT         NULL,
                    nome_devedor         VARCHAR(200) NULL,
                    criado_por_admin_id  UUID         NOT NULL,
                    criado_em            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    expira_em            TIMESTAMPTZ  NULL,
                    pago_em              TIMESTAMPTZ  NULL,
                    CONSTRAINT pk_pix_cobrancas PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_pix_cobrancas_tx_id    ON pix_cobrancas (tx_id);
                CREATE INDEX IF NOT EXISTS ix_pix_cobrancas_crediario      ON pix_cobrancas (crediario_id);
                CREATE INDEX IF NOT EXISTS ix_pix_cobrancas_comanda        ON pix_cobrancas (comanda_id);

                -- Financeiro: tabelas que dependiam só do EnsureCreated (no-op em banco já existente)
                CREATE TABLE IF NOT EXISTS external_transactions (
                    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
                    source      VARCHAR(30)     NOT NULL DEFAULT 'manual',
                    external_id VARCHAR(200)    NULL,
                    type        VARCHAR(10)     NOT NULL DEFAULT 'expense',
                    amount      NUMERIC(10,2)   NOT NULL DEFAULT 0,
                    description VARCHAR(500)    NOT NULL DEFAULT '',
                    due_date    TIMESTAMPTZ     NULL,
                    paid_at     TIMESTAMPTZ     NULL,
                    status      VARCHAR(20)     NOT NULL DEFAULT 'pending',
                    category    VARCHAR(100)    NULL,
                    supplier    VARCHAR(200)    NULL,
                    nfe_key     VARCHAR(44)     NULL,
                    notes       VARCHAR(2000)   NULL,
                    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
                    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_ext_tx_source_external_id
                    ON external_transactions (source, external_id)
                    WHERE external_id IS NOT NULL;

                CREATE TABLE IF NOT EXISTS integration_configs (
                    id            UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
                    source        VARCHAR(30)   NOT NULL UNIQUE,
                    access_token  VARCHAR(2000) NULL,
                    refresh_token VARCHAR(2000) NULL,
                    client_id     VARCHAR(200)  NULL,
                    client_secret VARCHAR(200)  NULL,
                    expires_at    TIMESTAMPTZ   NULL,
                    is_active     BOOLEAN       NOT NULL DEFAULT TRUE,
                    cnpj          VARCHAR(18)   NULL,
                    last_sync_at  TIMESTAMPTZ   NULL,
                    created_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                    updated_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS notifications (
                    id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id    UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    title      VARCHAR(120) NOT NULL,
                    body       VARCHAR(500) NOT NULL,
                    link       VARCHAR(300) NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    read_at    TIMESTAMPTZ NULL
                );
                CREATE INDEX IF NOT EXISTS ix_notifications_user ON notifications (user_id);

                CREATE TABLE IF NOT EXISTS push_subscriptions (
                    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id    UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    endpoint   VARCHAR(600) NOT NULL,
                    p256dh     VARCHAR(300) NOT NULL,
                    auth       VARCHAR(150) NOT NULL,
                    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_push_subscriptions_endpoint ON push_subscriptions (endpoint);
            ");
        }

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
app.UseOperatorPermissions();

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

app.Run();
