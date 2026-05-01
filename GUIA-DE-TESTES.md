# CardGameStore — Guia de Testes

## Pré-requisito único: Docker Desktop

Instale em: https://www.docker.com/products/docker-desktop  
Após instalar, certifique-se de que o ícone do Docker está rodando na bandeja do sistema.

---

## Como subir a stack (1 comando)

Abra o **PowerShell** na pasta `vendasMTG` e execute:

```powershell
.\start.ps1
```

O script irá:
1. Fazer o build da imagem .NET 8
2. Subir PostgreSQL 16 + MongoDB 7 + API
3. Aplicar as migrations automaticamente
4. Criar o usuário Admin (Maikon) via seed
5. Abrir o Swagger no seu navegador

**Primeira execução:** ~3-5 minutos (baixa as imagens Docker)  
**Execuções seguintes:** ~30 segundos

---

## URLs após subir

| Serviço | URL | Login |
|---------|-----|-------|
| **Swagger / API** | http://localhost:5000 | — |
| **Health Check** | http://localhost:5000/health | — |
| **PostgreSQL** | localhost:5432 | cardgame_user / CardGame@2025 |
| **MongoDB** | localhost:27017 | sem auth (dev) |

---

## Fluxo de teste completo no Swagger

### 1. Login como Admin (Maikon)

`POST /api/auth/login`
```json
{
  "email": "admin@cardgamestore.com.br",
  "password": "SenhaForte@123"
}
```
> Copie o `accessToken` retornado → clique em **Authorize** (cadeado no topo do Swagger) → cole `Bearer <token>`

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
> Retorna o token do cliente E a comanda já aberta. Use o token do cliente nos próximos passos.

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
(com o token do Admin)

### 6. Admin fecha a comanda

`PUT /api/comanda/{id}/close`

---

## Teste do SignalR em tempo real

Abra **duas abas** do navegador:

**Aba 1 (Admin)** — conecte ao hub como Admin:
```javascript
// No console do navegador (instale signalr via CDN ou npm)
const conn = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/comanda?access_token=<token-admin>")
  .build();
conn.on("ComandaUpdated", data => console.log("DASHBOARD:", data));
await conn.start();
```

**Aba 2 (Cliente)** — adicione um item via REST e veja o evento aparecer na aba do Admin instantaneamente.

---

## Comandos úteis

```powershell
# Parar os containers
.\start.ps1 -Stop

# Resetar banco do zero (apaga todos os dados)
.\start.ps1 -Reset

# Subir com pgAdmin (interface visual do PostgreSQL)
.\start.ps1 -WithAdmin
# pgAdmin: http://localhost:5050 (admin@cardgame.com / admin)

# Ver logs da API
docker logs cardgamestore_api -f

# Ver logs do banco
docker logs cardgamestore_postgres -f
```

---

## Nota sobre a senha do Admin seed

O seed no `AppDbContext.cs` usa um hash placeholder. Para ativar o login real do Admin, execute:

```powershell
# No container da API
docker exec -it cardgamestore_api dotnet CardGameStore.dll --seed-admin "SenhaForte@123"
```

Ou use `dotnet user-secrets` em desenvolvimento local.
