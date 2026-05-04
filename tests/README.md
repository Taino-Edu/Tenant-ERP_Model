# 🧪 softNerd — Pasta de Testes

Esta pasta contém dois tipos de testes para cobrir todos os componentes do sistema.

---

## 📁 Estrutura

```
tests/
├── api/                          # Testes de integração via REST Client (VS Code)
│   ├── http-client.env.json      # Variáveis de ambiente (tokens, IDs)
│   ├── 01-auth.http              # Login, quick-login, refresh, logout
│   ├── 02-products.http          # CRUD de produtos + ajuste de estoque
│   ├── 03-comanda.http           # Comanda: abrir, adicionar, remover, pontos, fechar
│   ├── 04-championship.http      # Campeonatos: criar, registrar, alterar status
│   ├── 05-users-points.http      # Usuários: listar, perfil, adicionar pontos
│   └── 06-venda-avulsa.http      # Venda avulsa no balcão
│
└── unit/
    └── CardGameStore.Tests/
        ├── CardGameStore.Tests.csproj
        └── Services/
            ├── ComandaServiceTests.cs   # Estoque e pontos
            └── AuthServiceTests.cs      # Login, quick-login, BCrypt
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

### Fluxo completo recomendado

```
01-auth.http      → #1 (admin login)   → copiar adminToken
01-auth.http      → #4 (quick-login)   → copiar clientToken + comandaId
02-products.http  → #4 (criar produto) → copiar productId
05-users-points.http → #1 (listar)     → copiar userId
03-comanda.http   → #4, #8, #10        → fluxo completo de comanda
04-championship.http → #2              → copiar championshipId
06-venda-avulsa.http → #1, #2          → venda no balcão
```

---

## 🔬 Como rodar os testes unitários (xUnit)

### Pré-requisitos
- .NET 8 SDK instalado (`dotnet --version`)

### Comandos

```bash
# Na raiz do projeto
cd tests/unit/CardGameStore.Tests

# Restaurar pacotes
dotnet restore

# Executar todos os testes
dotnet test

# Executar com output detalhado
dotnet test --logger "console;verbosity=detailed"

# Executar apenas testes de comanda
dotnet test --filter "FullyQualifiedName~ComandaServiceTests"

# Executar apenas testes de auth
dotnet test --filter "FullyQualifiedName~AuthServiceTests"
```

---

## ✅ Cobertura dos testes

| Componente         | REST Client | xUnit |
|--------------------|:-----------:|:-----:|
| Autenticação       | ✅ 01-auth  | ✅    |
| Produtos/Estoque   | ✅ 02       | ✅    |
| Comanda            | ✅ 03       | ✅    |
| Campeonatos        | ✅ 04       | —     |
| Usuários/Pontos    | ✅ 05       | ✅    |
| Venda Avulsa       | ✅ 06       | ✅    |

---

## ⚠️ Atenção

- O arquivo `http-client.env.json` **já está no .gitignore** — não commitá-lo com tokens reais
- Os testes unitários usam banco **InMemory** (sem necessidade de Docker)
- Os testes de API precisam do backend rodando na porta `5000`
