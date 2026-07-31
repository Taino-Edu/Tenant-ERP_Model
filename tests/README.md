# 🧪 softNerd — Pasta de Testes

Esta pasta contém dois tipos de testes para cobrir todos os componentes do sistema.

---

## 📁 Estrutura

```
tests/
├── api/                          # Testes de integração via REST Client (VS Code)
│   ├── http-client.env.json      # Variáveis de ambiente (tokens, IDs)
│   ├── 01-auth.http              # Login, quick-login, refresh, logout (HttpOnly cookies)
│   ├── 02-products.http          # CRUD de produtos + ajuste de estoque
│   ├── 03-comanda.http           # Comanda: abrir, adicionar, remover, pontos, fechar
│   ├── 05-users-points.http      # Usuários: listar, perfil, adicionar pontos
│   ├── 06-venda-avulsa.http      # Venda avulsa no balcão
│   ├── 07-lgpd.http              # LGPD: exercício de direitos, protocolo, resposta admin
│   ├── 08-audit.http             # Audit Log: listagem paginada e filtrada (admin)
│   └── 09-upload.http            # Upload de imagem (admin only, JPEG/PNG/WebP, máx. 5MB)
│
└── unit/
    └── CardGameStore.Tests/
        ├── CardGameStore.Tests.csproj
        └── Services/
            ├── ComandaServiceTests.cs      # Estoque, pontos, comanda
            ├── AuthServiceTests.cs         # Login, quick-login, BCrypt
            ├── ProductServiceTests.cs      # CRUD de produtos
            ├── UserServiceTests.cs         # Perfil de usuário, LGPD UpdateMe/Anonimizar
            ├── AnnouncementServiceTests.cs # Anúncios
            ├── VendaAvulsaServiceTests.cs  # Venda avulsa
            ├── LgpdServiceTests.cs         # UpdateMeAsync, AnonimizarAsync (LGPD)
            └── AuditServiceTests.cs        # Hash de IP, claims, contexto nulo
```

---

## ▶️ Como rodar os testes de API (REST Client)

### Pré-requisitos
- VS Code com extensão **REST Client** (Humao Feng)
- Backend rodando (`make up` ou `docker compose up -d`)

### Passo a passo

1. Abra qualquer arquivo `.http` no VS Code
2. No canto superior direito, selecione o ambiente **dev**
3. Execute os requests **em ordem** — cada um popula variáveis usadas pelos próximos
4. Após o login (#1 em 01-auth.http), copie o `accessToken` e cole em `adminToken` no `http-client.env.json`
5. Após o quick-login (#4), copie `accessToken` → `clientToken` e `comandaId` → `comandaId`
6. Após submeter uma solicitação LGPD (#1 em 07-lgpd.http), copie o `protocol` → `lgpdProtocol`

### Cookies HttpOnly — autenticação JWT

O backend emite os tokens como cookies **HttpOnly** (invisíveis ao JavaScript):

| Situação | Como testar |
|---|---|
| Browser (produção) | Cookies enviados automaticamente com `withCredentials: true` |
| REST Client (VS Code) | Copie o valor do `Set-Cookie` da resposta e cole em `Cookie:` nos próximos requests |
| Swagger UI | Use a aba *Authorize* para inserir o token manualmente |

### Fluxo completo recomendado

```
01-auth.http      → #1 (admin login)   → copiar adminToken
01-auth.http      → #4 (quick-login)   → copiar clientToken + comandaId
02-products.http  → #4 (criar produto) → copiar productId
05-users-points.http → #1 (listar)     → copiar userId
03-comanda.http   → #4, #8, #10        → fluxo completo de comanda
06-venda-avulsa.http → #1, #2          → venda no balcão
07-lgpd.http      → #1 (solicitação)   → copiar lgpdProtocol
08-audit.http     → #1 (listar logs)
09-upload.http    → #1 (upload imagem)
```

---

## 🔬 Como rodar os testes unitários (xUnit)

### Pré-requisitos
- .NET 8 SDK instalado (`dotnet --version`)
- Docker com Compose (o banco de testes é PostgreSQL 16 real)

> **Suba o banco sempre pelo compose** (`tests/docker-compose.yml`), nunca com um
> `docker run` cru. O servidor precisa de `max_locks_per_transaction` bem acima
> do default de 64: cada teste dropa e recria o próprio schema, e um
> `DROP SCHEMA CASCADE` de ~60 tabelas estoura a tabela de locks do cluster sob
> concorrência. Quando isso acontece o schema fica pela metade e testes
> aleatórios falham com `42P01: relation ... does not exist` — parece bug de
> isolamento entre testes, mas é configuração do servidor. `run-tests.ps1`
> detecta um container antigo e o recria sozinho; a `TestDbFactory` aborta com
> mensagem explícita se o servidor estiver abaixo do mínimo.

### Comandos

```bash
# Na raiz do projeto, sobe o PostgreSQL isolado e executa toda a suíte
./run-tests.ps1

# Ou execute os passos separadamente
docker compose -f tests/docker-compose.yml up -d --wait
dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj

# Executar com output detalhado
./run-tests.ps1 --logger "console;verbosity=detailed"

# Executar apenas testes de LGPD
./run-tests.ps1 --filter "FullyQualifiedName~LgpdServiceTests"

# Executar apenas testes de Audit
./run-tests.ps1 --filter "FullyQualifiedName~AuditServiceTests"

# Executar apenas testes de comanda
./run-tests.ps1 --filter "FullyQualifiedName~ComandaServiceTests"
```

### Cobertura de código (coverlet)

```bash
cd tests/unit/CardGameStore.Tests

# Gerar relatório de cobertura em XML (Cobertura format)
dotnet test --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# Gerar relatório HTML com ReportGenerator (instale uma vez)
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator \
  -reports:"./TestResults/**/coverage.cobertura.xml" \
  -targetdir:"./TestResults/coverage-report" \
  -reporttypes:Html

# Abra TestResults/coverage-report/index.html no browser
```

---

## ✅ Cobertura dos testes

| Componente         | REST Client | xUnit |
|--------------------|:-----------:|:-----:|
| Autenticação       | ✅ 01-auth  | ✅    |
| Produtos/Estoque   | ✅ 02       | ✅    |
| Comanda            | ✅ 03       | ✅    |
| Usuários/Pontos    | ✅ 05       | ✅    |
| Venda Avulsa       | ✅ 06       | ✅    |
| LGPD — Direitos    | ✅ 07       | ✅    |
| Audit Log          | ✅ 08       | ✅    |
| Upload de Imagem   | ✅ 09       | —     |
| Anúncios           | —           | ✅    |

---

## ⚠️ Atenção

- O arquivo `http-client.env.json` **já está no .gitignore** — não commitá-lo com tokens reais
- Os testes xUnit usam **PostgreSQL real**, com um schema isolado por teste
- Os testes de API precisam do backend rodando na porta `5000`
- O upload de imagem (09-upload.http) requer um arquivo local — edite `@imagePath` no arquivo
