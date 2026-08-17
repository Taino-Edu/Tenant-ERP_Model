# Tenant-ERP (Plataforma 3esysten)

> **Plataforma SaaS Multi-tenant de Gestão para Lojas e Varejo**
> Plataforma white-label completa para gerenciamento de lojas — nasceu como sistema local de uma loja de card games e virou genérica para qualquer varejo. Oferece isolamento físico de dados por schema no PostgreSQL, painel administrativo personalizado por loja, frente de caixa (PDV), comandas por QR Code com SignalR, crediário, portal do contador cross-tenant, super-admin do dono da plataforma, assistente IA com Gemini e conformidade com a LGPD.

**Produção:** [3esysten.com.br](https://3esysten.com.br) — cada loja é atendida em `<slug>.3esysten.com.br` ou no domínio próprio dela (`Tenant.CustomDomain`), resolvidos pelo `TenantResolutionMiddleware` a partir do Host da requisição.

---

## Principais Funcionalidades

### 🌐 Multi-tenancy e Gestão de Plataforma
- **Isolamento por Schema:** Cada loja (tenant) possui seu próprio schema lógico no banco de dados PostgreSQL (isolado via `search_path`), garantindo total privacidade e segurança de dados.
- **Painel do Dono da Plataforma (`/plataforma`):** Interface super-admin para listar, cadastrar e suspender/reativar tenants.
- **Provisionamento Dinâmico:** Cadastrar uma loja provisiona o schema no PostgreSQL de forma síncrona, executa as migrations iniciais e cria o administrador padrão da loja.
- **Planos e Módulos (Billing Ciclo 1 e 2):** Controle manual de planos (`PlanName`), status de pagamento (`PaymentStatus`: Pago/Atrasado/Isento) e módulos habilitados (`EnabledModules`), com gates técnicos no backend e frontend (ex: restrição do módulo Fiscal e recursos avançados de Estoque).

### 🧾 Portal do Contador Cross-Tenant (`/contador`)
- **Acesso Multi-Loja:** Contadores possuem conta global no catálogo e gerenciam múltiplas lojas (tenants) vinculadas de forma independente.
- **Dois Fluxos de Vínculo:** O lojista pode convidar o contador por e-mail (vincula direto caso o contador já tenha cadastro) ou o contador pode solicitar acesso informando o slug da loja (aguarda aprovação do lojista em `/admin/fiscal`).
- **Painel de Monitoramento (Badges de Saúde):** Indicadores visuais de saúde da loja: validade do Certificado Digital A1 e tempo desde a última emissão de nota fiscal ("sem nota há X dias" ou "nenhuma nota emitida").
- **Mural de Avisos Compartilhado:** Canal direto de avisos/mensagens entre o contador e o lojista no Painel Geral.
- **Lembrete de Vencimento do DAS:** Alerta visual do dia 20 para lojas optantes pelo Simples Nacional.
- **Resumo do Período:** Agrupamento e drill-down de notas fiscais autorizadas, canceladas e faturamento calculado a partir dos dados locais da loja.

### 🏪 Frente de Caixa (PDV / Venda Avulsa)
- **Venda Direta:** Registro de vendas diretamente no balcão sem necessidade de login do cliente.
- **Estoque em Tempo Real:** Catálogo de produtos integrado ao fluxo de venda, persistindo os itens vendidos no PostgreSQL em formato JSONB.
- **Impressão Térmica:** Geração de comprovante para impressão térmica (80mm) ou exportação em PDF.

### 📱 Comandas Digitais por QR Code
- **Autoatendimento:** Clientes escaneiam o QR Code na mesa e abrem/acompanham sua comanda pelo smartphone.
- **Login Simplificado:** Acesso rápido via CPF (validação Módulo 11) + WhatsApp, com consentimento LGPD integrado.
- **Painel Admin em Tempo Real:** Dashboard administrativo atualizado via **SignalR**; novas comandas e alterações de itens aparecem na hora sem recarregar a página.
- **Fidelidade (Pontos e Cashback):** Aplicação de pontos ou cashback acumulados direto na comanda para desconto no fechamento, com recalque automático do total. Programa de pontos é opcional — cada loja liga/desliga em Personalizar Site, sem apagar saldo/histórico existente.

### 💳 Crediário e Contas a Receber
- **Geração Automática:** Criação de débito automático ao fechar comandas/vendas na modalidade crediário.
- **Histórico e Quitações:** Registro de pagamentos parciais e controle completo de inadimplência por cliente.

### 📦 Estoque e Catálogo
- **Categorias:** Aba própria dentro de Estoque (emoji, ordem de exibição) — sem tela separada.
- **Gestão Avançada:** Alertas de estoque baixo, suporte a variantes de produtos (grades de tamanho/cor/tipo) e análise de Curva ABC.

### 🤖 Assistente IA (Gemini)
- **Chat no Admin:** Widget de chat flutuante alimentado pelo **Google Gemini 2.5 Flash** integrado diretamente via HTTP (sem SDK).
- **IA Contextual:** O assistente tem acesso dinâmico aos dados da loja (estoque atual, comandas abertas, crediários) para responder a perguntas operacionais e sugerir melhorias.

### 🛡️ Conformidade LGPD
- **Painel de Direitos do Titular:** Página pública `/lgpd` para consulta de dados cadastrais e solicitação de exclusão ou portabilidade.
- **Segurança de Logs:** Logs de auditoria imutáveis com anonimização de IP utilizando hash SHA-256 com salt configurável.

---

## Stack Tecnológica

### Backend — `CardGameStore/`
- **Framework:** ASP.NET Core 8 (C#)
- **Banco de Dados:** PostgreSQL 16
- **ORM:** Entity Framework Core 8 (sem migrations automáticas globais; isolado por Tenant no `TenantConnectionInterceptor`)
- **Comunicação em Tempo Real:** SignalR (SSE + Long Polling)
- **Autenticação:** JWT (HttpOnly Cookies + Refresh Token)
- **Segurança de Senhas:** BCrypt.Net

### Frontend — `frontend/`
- **Framework:** Next.js 14 (App Router, React SSR)
- **Tipagem:** TypeScript 5
- **Estilização:** Tailwind CSS 3
- **Cliente HTTP:** Axios (com interceptor para renovação silenciosa de token via refresh token)
- **Comunicação:** `@microsoft/signalr`

### Infraestrutura e Deploy
- **Containers:** Docker + Docker Compose
- **Proxy Reverso:** Nginx (configuração de proxy reverso e headers forward)
- **Gerenciamento de DNS/SSL:** Cloudflare (SSL/TLS Flexible em desenvolvimento/produção)

---

## Estrutura do Projeto

```
Tenant-ERP/
├── CardGameStore/                  # Backend ASP.NET Core 8
│   ├── Controllers/                # Controllers (Auth, Product, Comanda, Contador, Platform...)
│   ├── Multitenancy/               # Lógica de Multi-tenant, isolamento, provisionamento e catálogo
│   │   ├── CatalogDbContext.cs     # Contexto global do catálogo (tenants, contadores)
│   │   ├── Tenant.cs               # Entidade do Tenant (loja, plano, módulos, status)
│   │   ├── ContadorAccount.cs      # Conta cross-tenant do Contador
│   │   ├── TenantConnectionInterceptor.cs  # Intercepta conexões para aplicar search_path por tenant
│   │   └── TenantResolutionMiddleware.cs    # Resolução do tenant com base no subdomínio da requisição
│   ├── Data/                       # DbContext específico do Tenant e migrations
│   ├── Hubs/                       # Hubs SignalR (ComandaHub)
│   ├── Models/                     # Entidades PostgreSQL do Tenant (Product, Comanda, Crediario...)
│   └── Program.cs                  # Configuração do pipeline da API e injeção de dependências
│
├── frontend/                       # Frontend Next.js 14
│   ├── app/
│   │   ├── admin/                  # Painel da loja (estoque, comanda, financeiro, fiscal...)
│   │   ├── plataforma/             # Painel do Dono da Plataforma (gerenciador de tenants)
│   │   ├── contador/               # Portal do Contador (cross-tenant, convites, mural)
│   │   ├── institucional/          # Landing page institucional da plataforma 3esysten
│   │   ├── cliente/                # Área do cliente (histórico, pontos, cashback)
│   │   └── mesa/[mesa]/            # Autoatendimento via QR Code
│   ├── components/                 # Componentes compartilhados
│   └── lib/                        # Integração com API (api.ts) e SignalR
│
├── tests/
│   ├── unit/                       # Testes de unidade com xUnit e Moq
│   └── api/                        # Testes rápidos de endpoints (.http)
│
└── deploy/                         # Scripts de implantação e Dockerfiles
    ├── docker-compose.prod.yml     # Orquestração de produção
    ├── nginx/                      # Configurações do Nginx
    ├── setup.sh                    # Setup automático da VPS (instala Docker, clona e gera envs)
    └── update.sh                   # Script de deploy automatizado via git pull e docker rebuild
```

---

## Configuração do Ambiente (.env)

Copie e configure as variáveis de ambiente necessárias no arquivo `.env` localizado na raiz do projeto ou no diretório de deploy:

```env
# --- Banco de Dados ---
POSTGRES_DB=cardgamestore
POSTGRES_USER=cardgame_user
POSTGRES_PASSWORD=sua_senha_segura

# --- Segurança e JWT ---
JWT_SECRET=seu_segredo_jwt_super_longo_e_seguro
COOKIE_SECURE=false # Defina como true em produção (com HTTPS configurado)
IP_HASH_SALT=sal_aleatorio_para_anonimizar_ip

# --- Seeds do Sistema (Executados no primeiro boot) ---
PLATFORM_OWNER_EMAIL=dono@3esysten.com.br
PLATFORM_OWNER_SEED_PASSWORD=senha_forte_dono_plataforma
ADMIN_SEED_PASSWORD=senha_forte_admin_loja_padrao

# --- Serviços Externos ---
GEMINI_API_KEY=chave_do_google_ai_studio
SMTP_PASSWORD=chave_de_envio_do_resend

# --- Push do navegador (VAPID) ---
# Geradas automaticamente por setup.sh (deploy novo) e por update.sh (instalação
# já existente). Sem elas o push é um no-op silencioso: o usuário concede a
# permissão de notificação e nunca recebe nada.
# Para gerar à mão: docker run --rm cardgamestore_api:latest gen-vapid
# ⚠️ Não regenere sem necessidade — trocar o par invalida todas as subscrições
#    já salvas e cada cliente precisa reativar o push.
VAPID_PUBLIC_KEY=
VAPID_PRIVATE_KEY=
VAPID_SUBJECT=mailto:contato@seudominio.com.br

# --- Referência da API ---
# Publica o Swagger UI em dev-swagger.<seu domínio> (ver deploy/nginx/dev-swagger.conf).
# false desliga a documentação por completo.
SWAGGER_ENABLED=true
```

> **Nomes de SMTP:** a configuração lida pela API é `SmtpSettings__*`
> (`Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`, `AppUrl`) — o
> compose faz a ponte a partir das `SMTP_*` acima. Um bloco `EmailSettings` com
> outros nomes já existiu no `appsettings.json` e **nenhum código o lia**: quem
> configurava por ele não enviava e-mail nenhum e não via erro. Foi removido.

---

## Como Executar Localmente

### Pré-requisitos
- .NET 8 SDK, Node.js 20+ e Docker (só pro PostgreSQL).

### Passos
1. PostgreSQL — não há mais fallback pra SQLite; a API não sobe sem banco. Use o
   mesmo container da suíte de testes:
   ```bash
   docker compose -f tests/docker-compose.yml up -d
   ```
   `appsettings.Development.json` já aponta pra ele (porta 5433) e o script de
   inicialização cria a credencial restrita usada em runtime. O motivo de não
   existir modo "zero setup": o multi-tenant inteiro é schema + `search_path`,
   que o SQLite não tem — o caminho SQLite não exercitava nada do que importa e
   ainda escondia bug real (ele aceita `DateTime` com `Kind=Unspecified` onde o
   Postgres recusa, o que já quebrou o fechamento financeiro em produção sem a
   suíte acusar nada).
2. Backend:
   ```bash
   cd CardGameStore
   dotnet run
   ```
   API em `http://localhost:5000`, Swagger em `http://localhost:5000/swagger`
   (ligado por padrão em Development; fora dele exige `Swagger__Enabled=true`).
3. Frontend:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```
   Site em `http://localhost:3000`.

Não existe `docker-compose.yml` na raiz — o Compose (`deploy/docker-compose.prod.yml`) é só pra produção/VPS (ver seção de Deploy abaixo).

---

## Deploy em Produção

### Primeira instalação no VPS
```bash
curl -fsSL https://raw.githubusercontent.com/Taino-Edu/Tenant-ERP_Model/main/deploy/setup.sh | bash
```
Instala Docker, configura firewall (UFW), clona o repositório em `/opt/tenant-erp`, gera segredos e sobe os containers.

### Atualizar após novo commit
```bash
bash /opt/tenant-erp/deploy/update.sh
```

### Backup do PostgreSQL
```bash
bash /opt/tenant-erp/deploy/backup.sh   # manual, ou agendado via cron (instruções no próprio script)
```

### Referência da API (Swagger)
Publicada num host dedicado: **`dev-swagger.3esysten.com.br`** — já coberto pelo
wildcard DNS existente, sem registro novo.

Por que host separado e não `/swagger` no domínio principal: qualquer subdomínio
de `*.3esysten.com.br` é lido como **slug de loja** pelo `TenantResolutionMiddleware`,
e "dev-swagger" não é tenant nenhum. Por isso a API registra o Swagger *antes* da
resolução de tenant e o nginx encaminha só `/swagger` nesse host — `/api`, `/hubs`,
`/mcp` e o frontend respondem 404 lá (`deploy/nginx/dev-swagger.conf`).

⚠️ O host é **público**: a doc lista todos os endpoints e campos da API. Não dá
acesso a dado nenhum (todo endpoint segue exigindo o mesmo JWT), mas é um mapa da
superfície de ataque. Para fechar com usuário/senha, descomente as duas linhas
`auth_basic` em `deploy/nginx/dev-swagger.conf` e crie o `.htpasswd-swagger`
(comando no cabeçalho do arquivo). Para desligar de vez: `SWAGGER_ENABLED=false`.

### Limpar espaço em disco (build cache acumula rápido)
```bash
bash /opt/tenant-erp/deploy/cleanup.sh
```

---

## Testes

### Executar Testes Unitários (Backend)
```bash
# Executa todos os testes do xUnit
dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj
```

### Executar Testes E2E (Frontend)
```bash
cd frontend
npx playwright test
```

---

## Licença

Software proprietário e confidencial. Todos os direitos reservados.  
Consulte o arquivo [LICENSE](./LICENSE) para termos de uso.
