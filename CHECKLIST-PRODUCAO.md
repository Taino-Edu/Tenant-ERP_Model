# 🚀 Checklist de Produção — softNerd

> Use esta lista antes de colocar o sistema no ar. Marque cada item conforme for resolvendo.

---

## ✅ Correções já aplicadas (04/05/2026)

Estes bugs foram corrigidos diretamente no código:

- [x] **AuthController.cs criado** — endpoints `/api/auth/login`, `/quick-login`, `/refresh`, `/logout` agora existem
- [x] **Docker usava SQLite** — `ASPNETCORE_ENVIRONMENT` trocado para `Production`; a lógica de seleção de banco corrigida para usar PostgreSQL sempre que a connection string estiver configurada
- [x] **Crash no MigrateAsync** — substituído por `EnsureCreated` (sem precisar de migration files)
- [x] **Bug de segurança no preço** — `AddItemAsync` agora busca o preço real do banco, ignora o que o cliente envia
- [x] **Bug de segurança no RemoveItem** — verificação adicionada: o item deve pertencer à comanda
- [x] **QuickLogin não abria comanda** — `AuthService.QuickLoginAsync` agora chama `OpenComandaAsync` e retorna o `comandaId`
- [x] **HTTPS quebrando Docker** — redirect HTTPS removido (deve ser feito por reverse proxy externo)

---

## 🔴 Obrigatório antes de ir a produção real

Sem esses itens o sistema está **vulnerável ou não vai funcionar**.

### Segurança

- [ ] **Trocar o JWT Secret Key**
  - Arquivo: `docker-compose.yml` linha `JwtSettings__SecretKey`
  - Valor atual: `CardGameStore_SecretKey_2025_MinLength32Chars!` ← qualquer um que leu o código pode forjar tokens de admin
  - Ação: gere uma chave aleatória com pelo menos 32 caracteres:
    ```powershell
    [System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    ```
  - Substitua o valor no docker-compose pelo resultado

- [ ] **Trocar a senha do PostgreSQL**
  - Atual: `CardGame@2025` (exposta no docker-compose)
  - Ação: gere uma senha forte e atualize em:
    - `docker-compose.yml` → `POSTGRES_PASSWORD` e `ConnectionStrings__PostgreSQL`

- [ ] **Trocar a senha do pgAdmin**
  - Atual: `admin` (muito fraca)
  - Ação: atualize `PGADMIN_DEFAULT_PASSWORD` no docker-compose

- [ ] **Não comitar o docker-compose com senhas reais no Git**
  - Crie um `docker-compose.override.yml` ou use um arquivo `.env` separado com as variáveis sensíveis
  - Adicione `.env` ao `.gitignore`

### Infraestrutura

- [ ] **Configurar domínio e HTTPS**
  - O sistema atualmente só funciona em `localhost`
  - Para produção real, você precisa de:
    1. Um domínio (ex: `softnerd.com.br`)
    2. Um servidor (VPS, cloud, etc.)
    3. Um reverse proxy (Nginx ou Cloudflare Tunnel) que:
       - Recebe HTTPS na porta 443
       - Repassa para a API na porta 5000 e frontend na porta 3000

- [ ] **Atualizar CORS para o domínio real**
  - Arquivo: `CardGameStore/Program.cs` linhas com `.WithOrigins(...)`
  - Adicionar o domínio real: `.WithOrigins("https://softnerd.com.br")`

- [ ] **Atualizar NEXT_PUBLIC_API_URL**
  - Arquivo: `docker-compose.yml` no serviço `frontend`
  - Trocar `http://localhost:5000` pelo endereço real da API em produção

- [ ] **Atualizar JwtSettings__Issuer**
  - Arquivo: `docker-compose.yml`
  - Trocar `http://localhost:5000` pela URL real da API

- [ ] **Configurar backups do PostgreSQL**
  - Os dados ficam no volume Docker `postgres_data`
  - Configure backup automático diário (ex: `pg_dump` agendado)

---

## 🟡 Importante — fazer logo após subir

Esses itens não travam o lançamento, mas são necessários para operar bem.

- [ ] **Trocar a senha do admin (Maikon)**
  - O seed cria o admin com senha `SenhaForte@123`
  - Após o primeiro login, troque por uma senha única e guarde em local seguro
  - Ou: altere o hash BCrypt no `AppDbContext.cs` antes do primeiro `EnsureCreated`

- [ ] **Monitorar os logs**
  - O Docker tem logs acessíveis via:
    ```powershell
    docker logs cardgamestore_api -f
    docker logs cardgamestore_frontend -f
    ```
  - Fique de olho nas primeiras horas para pegar erros inesperados

- [ ] **Testar o fluxo completo em produção**
  - Login admin → criar produto → abrir QR Code → quick-login cliente → adicionar item → ver no dashboard → fechar comanda

- [ ] **Verificar que o SignalR funciona com o domínio real**
  - O SignalR (WebSocket) precisa que o reverse proxy passe os headers `Upgrade: websocket` e `Connection: upgrade`
  - No Nginx, adicione:
    ```nginx
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    ```

---

## 🟢 Melhorias para semanas seguintes

Não bloqueiam o lançamento, mas aumentam qualidade e segurança.

- [ ] Implementar **validação de CPF** (algoritmo dos dígitos verificadores)
- [ ] Implementar **Venda Avulsa** (venda direta sem QR Code/mesa)
- [ ] Adicionar **paginação** no `GET /api/product`
- [ ] Implementar **rate limiting** nos endpoints de login (evitar força bruta)
- [ ] Gerar **migration files** do EF Core para mudanças futuras de schema
- [ ] Adicionar **testes automatizados** (ao menos nos Services)
- [ ] Configurar **GitHub Actions** para CI/CD

---

## 📋 Sequência recomendada para subir em produção

```
1. Trocar JWT Secret (OBRIGATÓRIO)
2. Trocar senhas do PostgreSQL e pgAdmin (OBRIGATÓRIO)
3. Configurar servidor e domínio
4. Atualizar CORS + NEXT_PUBLIC_API_URL + JwtSettings__Issuer com o domínio real
5. Criar arquivo .env com as variáveis sensíveis (não commitar no Git)
6. Subir com: docker compose up --build -d
7. Verificar logs: docker logs cardgamestore_api
8. Testar o fluxo completo
9. Configurar backup automático do banco
10. Trocar senha do admin no sistema
```

---

## 🆘 Comandos úteis em produção

```powershell
# Subir tudo em background
docker compose up --build -d

# Ver logs em tempo real
docker logs cardgamestore_api -f
docker logs cardgamestore_frontend -f

# Reiniciar apenas a API (sem recriar os bancos)
docker compose restart api

# Parar tudo (mantém os dados)
docker compose down

# Parar e apagar todos os dados (CUIDADO — irreversível!)
docker compose down -v

# Ver status de todos os containers
docker compose ps

# Backup manual do banco
docker exec cardgamestore_postgres pg_dump -U cardgame_user cardgamestore > backup_$(date +%Y%m%d).sql

# Restaurar backup
docker exec -i cardgamestore_postgres psql -U cardgame_user cardgamestore < backup_20260504.sql
```

---

*Gerado em 04/05/2026 — softNerd v1*
