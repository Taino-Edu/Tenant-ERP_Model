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
2. Subir PostgreSQL 16 + MongoDB 7 + API + Frontend Next.js
3. Aplicar as migrations automaticamente
4. Criar o usuário Admin via seed
5. Abrir o Swagger no seu navegador

**Primeira execução:** ~3-5 minutos (baixa as imagens Docker)  
**Execuções seguintes:** ~30 segundos

---

## URLs após subir

| Serviço | URL | Login |
|---------|-----|-------|
| **Frontend** | http://localhost:3000 | admin@cardgamestore.com.br / SenhaForte@123 |
| **Swagger / API** | http://localhost:5000 | — |
| **Health Check** | http://localhost:5000/health | — |
| **PostgreSQL** | localhost:5432 | cardgame_user / CardGame@2025 |
| **MongoDB** | localhost:27017 | sem auth (dev) |

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

# Ver logs do frontend
docker logs cardgamestore_frontend -f

# Ver logs do banco
docker logs cardgamestore_postgres -f
```

---

## Credenciais de referência

| Perfil | Email | Senha |
|--------|-------|-------|
| Admin | admin@cardgamestore.com.br | SenhaForte@123 |
| PostgreSQL | cardgame_user | CardGame@2025 |
| pgAdmin | admin@cardgame.com | admin |
