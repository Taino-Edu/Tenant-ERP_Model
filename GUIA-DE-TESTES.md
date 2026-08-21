# CardGameStore — Guia de Testes

## Pré-requisitos

Docker (só pro PostgreSQL), .NET 8 SDK e Node.js 20+.
Docker Desktop: https://www.docker.com/products/docker-desktop

---

## Como subir a stack

Três terminais, na raiz do repositório:

```bash
# 1. Banco — o mesmo container que a suíte de testes usa
docker compose -f tests/docker-compose.yml up -d

# 2. API (aplica as migrations e roda os seeds no boot)
cd CardGameStore && dotnet run

# 3. Frontend
cd frontend && npm install && npm run dev
```

Não existe mais `start.ps1` nem MongoDB: o banco é só PostgreSQL, e o
`appsettings.Development.json` já aponta pro container (porta 5433). Também não
há fallback pra SQLite — o multi-tenant inteiro é schema + `search_path`, que o
SQLite não tem.

---

## URLs após subir

| Serviço | URL | Login |
|---------|-----|-------|
| **Frontend** | http://localhost:3000 | admin@cardgamestore.com.br / SenhaForte@123 |
| **Swagger / API** | http://localhost:5000/swagger | — |
| **Health Check** | http://localhost:5000/health | — |
| **PostgreSQL** | localhost:5433 | ver `appsettings.Development.json` |

---

## Páginas do frontend

| Página | URL | Acesso |
|--------|-----|--------|
| Login | http://localhost:3000/login | público |
| Dashboard Admin | http://localhost:3000/admin/dashboard | autenticado |
| Estoque | http://localhost:3000/admin/estoque | autenticado |
| LGPD (admin) | http://localhost:3000/admin/lgpd | autenticado |
| LGPD (público) | http://localhost:3000/lgpd | público |
| Política de Privacidade | http://localhost:3000/privacidade | público |
| Termos de Uso | http://localhost:3000/termos | público |

---

## Fluxo de teste pelo frontend

### 1. Primeiro acesso — banner de cookies

Abra http://localhost:3000 em uma aba anônima.  
Deve aparecer um banner de cookies na parte inferior da tela pedindo consentimento.  
Aceite ou recuse e verifique que o banner não reaparece na mesma sessão.

### 2. Tema claro/escuro

Em qualquer página (incluindo `/privacidade`, `/termos`, `/lgpd`), clique no botão de alternância de tema no canto superior direito.  
O tema deve mudar imediatamente e persistir ao navegar entre páginas.

### 3. Login como Admin

Acesse http://localhost:3000/login e entre com:

- **Email:** admin@cardgamestore.com.br  
- **Senha:** SenhaForte@123

O login usa **HttpOnly cookies** — o token é armazenado automaticamente pelo navegador, sem necessidade de copiar nada manualmente.

### 4. Criar produto no estoque

Acesse http://localhost:3000/admin/estoque e clique em **Novo Produto**.

Preencha os campos e, se desejar testar o upload de imagem, arraste uma foto para a área de drag-and-drop ou clique para selecionar.  
Salve e confirme que o produto aparece na listagem.

### 5. Assistente IA

No painel http://localhost:3000/admin/dashboard, clique no botão flutuante de chat no **canto inferior direito**.

Sugestões de perguntas para testar:
- "quanto vendi hoje?"
- "quais produtos estão em falta?"
- "me mostre um resumo das comandas abertas"

O assistente usa o modelo Gemini 2.0 Flash e tem acesso ao contexto da loja.

### 6. Exercício de direitos LGPD

Acesse http://localhost:3000/lgpd (sem precisar de login).

Preencha o formulário com:
- **CPF:** 529.982.247-25 (CPF válido para teste)
- **Tipo de solicitação:** escolha qualquer opção (acesso, exclusão, portabilidade etc.)

Após enviar, você receberá um número de protocolo. Anote e use o campo de consulta na mesma página para verificar o status pelo protocolo.

Para ver a solicitação no painel admin, acesse http://localhost:3000/admin/lgpd.

---

## Fluxo de teste via Swagger

> **Atenção — autenticação por cookie:**  
> A partir desta versão, o login retorna o token via `Set-Cookie` (HttpOnly), não mais no body da resposta.  
> Para usar o Swagger com endpoints autenticados, veja as instruções abaixo.

### 1. Login e captura do token

`POST /api/auth/login`
```json
{
  "email": "admin@cardgamestore.com.br",
  "password": "SenhaForte@123"
}
```

Após executar, abra as ferramentas de desenvolvedor do navegador (F12) → aba **Application** → **Cookies** → copie o valor do cookie `accessToken`.

Em seguida, clique em **Authorize** (cadeado no topo do Swagger) e cole:
```
Bearer <valor-do-cookie>
```

### 2. Criar um produto no estoque

`POST /api/product`
```json
{
  "name": "Coca-Cola Lata",
  "description": "350ml gelada",
  "category": "Bebida",
  "priceInCents": 500,
  "stockQuantity": 50,
  "minimumStock": 10
}
```

### 3. Simular cliente via QR Code

`POST /api/auth/quick-login`
```json
{
  "name": "João Silva",
  "cpf": "12345678901",
  "whatsApp": "5511999999999",
  "tableIdentifier": "Mesa-03"
}
```
> Retorna o token do cliente e a comanda já aberta. Use o token do cliente nos próximos passos.

### 4. Cliente adiciona item à comanda

`POST /api/comanda/{id}/items`  
(use o `comandaId` retornado no quick-login)
```json
{
  "productId": "<id-do-produto-criado>",
  "itemName": "Coca-Cola Lata",
  "unitPriceInCents": 500,
  "quantity": 2
}
```

### 5. Admin vê o dashboard em tempo real

`GET /api/comanda/dashboard`  
(com o token do Admin no Authorize)

### 6. Admin fecha a comanda

`PUT /api/comanda/{id}/close`

---

## Teste do SignalR em tempo real

Abra **duas abas** do navegador:

**Aba 1 (Admin)** — conecte ao hub como Admin:
```javascript
// No console do navegador
const conn = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/comanda?access_token=<token-admin>")
  .build();
conn.on("ComandaUpdated", data => console.log("DASHBOARD:", data));
await conn.start();
```

**Aba 2 (Cliente)** — adicione um item via REST e veja o evento aparecer na aba do Admin instantaneamente.

---

## Fluxo de teste dos perfis de acesso

O que quebrou aqui no passado não foi a autorização e sim a interface: o painel
oferecia ações que a API recusava, e o menu pedia uma permissão diferente da que a
rota exigia. Os dois roteiros abaixo cobrem exatamente isso.

### Equipe da plataforma

1. Entre como dono da plataforma e vá em **Equipe** (`/plataforma/equipe`).
2. Convide um integrante com perfil **Auditoria**. Abra o convite em uma janela
   anônima e defina a senha.
3. Logado como a auditoria, confira que:
   - As abas **Leads**, **Prospecção** e **Equipe** não aparecem.
   - Em **Tenants**, plano e mensalidade aparecem preenchidos mas travados, e não
     existem os botões de suspender, backup, apagar nem simular loja.
   - Em **Financeiro**, não existem "Gerar mensalidades" nem "Dar baixa".
   - Em **Indicações**, não existem os formulários de cadastro e vínculo.
4. Volte ao dono, mude o perfil dessa conta para **Sócio administrador**.
5. Na janela da outra conta, espere a renovação de sessão (ou recarregue): o menu
   e os botões passam a aparecer sem precisar deslogar.
6. Como sócio, tente mudar o **próprio** perfil: o campo está travado. E o
   proprietário principal aparece como "Acesso total protegido", sem controles.

### Perfis de operador (dentro da loja)

1. Como Admin da loja, crie um perfil em **Perfis de Acesso** com apenas `comandas`.
2. Crie um operador com esse perfil e entre com ele.
3. Confira que **Comanda** aparece no menu e abre — antes o item pedia `dashboard`,
   então quem tinha só `comandas` não achava a tela e quem tinha só `dashboard`
   entrava para tomar 403.
4. Pressione `?` para abrir os atalhos: só devem aparecer os que o perfil alcança.
5. Com a sessão do operador aberta, remova a permissão pelo Admin. A próxima ação
   dele já é recusada — a autorização relê o banco, não o token.

---

## Comandos úteis

```bash
# Parar o banco
docker compose -f tests/docker-compose.yml down

# Resetar do zero (apaga todos os dados)
docker compose -f tests/docker-compose.yml down -v

# Ver logs do banco
docker compose -f tests/docker-compose.yml logs -f

# Suíte do backend
dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj

# Testes de unidade do frontend (não precisam de servidor)
cd frontend && npx playwright test --config=playwright.unit.config.ts
```

---

## Credenciais de referência

| Perfil | Email | Senha |
|--------|-------|-------|
| Admin da loja | admin@cardgamestore.com.br | `ADMIN_SEED_PASSWORD` (default `SenhaForte@123`) |
| Dono da plataforma | `PLATFORM_OWNER_EMAIL` | `PLATFORM_OWNER_SEED_PASSWORD` |
| PostgreSQL | ver `appsettings.Development.json` | idem |
