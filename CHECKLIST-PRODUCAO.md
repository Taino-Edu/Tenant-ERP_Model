# Checklist de Produção — softNerd

> Use esta lista antes de colocar o sistema em produção para um cliente.
> Atualizado em 15/05/2026 — v3.0

---

## Itens já implementados no código

- [x] AuthController completo (login, quick-login, refresh, logout, forgot/reset password)
- [x] Docker configurado para PostgreSQL em produção
- [x] EnsureCreated no boot (sem necessidade de migrations manuais)
- [x] Preço de itens resolvido no servidor (não confia no cliente)
- [x] Rate limiting: 5 req/min nos endpoints de auth, 200 req/min nos demais
- [x] Headers de segurança HTTP (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy)
- [x] Venda avulsa (PDV no balcão)
- [x] Crediário com bloqueio e email automático
- [x] Analytics: KPIs, curva horária, top produtos, insights por cliente
- [x] Notificações por email (reset senha, crediário, campeonatos, anúncios)
- [x] Sidebar responsiva (mobile/tablet)
- [x] HttpOnly cookies para JWT (accessToken + refreshToken, Secure em produção, SameSite=Strict)
- [x] CPF Módulo 11 validado no backend (CpfValidAttribute)
- [x] IP salt configurável (Security:IpHashSalt) para hash SHA-256 dos IPs no audit log
- [x] UseForwardedHeaders middleware (resolve IP real atrás de proxy reverso)
- [x] LGPD compliance completo: página pública /lgpd, /privacidade, /termos, cookie banner, formulário de direitos, audit log imutável com SHA-256 do IP
- [x] Painel LGPD admin (/admin/lgpd) com resposta a solicitações e audit log
- [x] Upload de imagens (POST /api/upload/image, JPEG/PNG/WebP, máx 5MB, validação dupla cliente+servidor)
- [x] Tema claro/escuro (ThemeToggle em todas as páginas)
- [x] Testes unitários: ~85 testes nos serviços principais
- [x] Cobertura de código via coverlet (`dotnet test --collect:"XPlat Code Coverage"`)
- [x] Yu-Gi-Oh API (ygoprodeck.com) além de Pokémon TCG e MTG (Scryfall)
- [x] Cartas próprias (cadastro manual)
- [x] Desconto por modo (atacado/varejo)
- [x] PWA (auto-hide, instalável)
- [x] Assistente IA conversacional (Gemini 2.0 Flash) no painel admin

---

## Obrigatório antes de ir a produção

Sem esses itens o sistema está **vulnerável ou não vai funcionar**.

### Segurança

- [ ] **Trocar o JWT Secret Key**
  ```powershell
  [System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
  ```
  Substitua em `docker-compose.yml` → `JwtSettings__SecretKey`

- [ ] **Trocar a senha do PostgreSQL**
  Atualize `POSTGRES_PASSWORD` e a connection string no `docker-compose.yml`

- [ ] **Trocar a senha do pgAdmin** (se usar)
  Atualize `PGADMIN_DEFAULT_PASSWORD`

- [ ] **Não commitar o `.env` com senhas reais no Git**
  O arquivo `.env` já está no `.gitignore` — nunca remova essa linha

- [ ] **Trocar a senha do admin (Maikon) no primeiro login**
  Seed cria com `SenhaForte@123` — troque imediatamente após primeiro acesso

- [ ] **Trocar SECURITY_IPHASHSALT**
  Gerar valor forte e configurar no `.env`:
  ```bash
  openssl rand -base64 32
  ```
  Variável: `Security__IpHashSalt`

- [ ] **Configurar GEMINI_API_KEY**
  Obter em aistudio.google.com (gratuito, sem cartão) e adicionar ao `.env`:
  ```
  GEMINI_API_KEY=sua-chave-aqui
  ```

- [ ] **Designar DPO (Data Protection Officer)**
  Criar email `privacidade@seudominio.com.br` e atualizar referência na página `/privacidade`

- [ ] **Revisar textos legais com advogado**
  Páginas `/privacidade` e `/termos` devem ser revisadas por advogado especializado em LGPD antes do primeiro cliente real

- [ ] **Verificar cookie banner em todos os browsers**
  Testar Chrome, Firefox e Safari mobile antes do lançamento

- [ ] **Verificar cookies HttpOnly com Secure=true em produção**
  HTTPS é obrigatório para que `Secure` funcione — confirme que o reverse proxy está passando HTTPS corretamente

### Infraestrutura

- [ ] **Configurar domínio e HTTPS**
  O sistema precisa de um reverse proxy (Nginx ou Cloudflare Tunnel) na frente:
  ```nginx
  proxy_http_version 1.1;
  proxy_set_header Upgrade $http_upgrade;
  proxy_set_header Connection "upgrade";
  proxy_set_header Host $host;
  ```

- [ ] **Atualizar CORS para o domínio real**
  `CardGameStore/Program.cs` → `.WithOrigins("https://seudominio.com.br")`

- [ ] **Atualizar variáveis no docker-compose**
  - `NEXT_PUBLIC_API_URL` → URL real da API
  - `JwtSettings__Issuer` → URL real da API
  - `JwtSettings__Audience` → URL real do frontend

- [ ] **Configurar email (SMTP)**
  No `.env`:
  ```
  EMAIL_HOST=smtp.gmail.com
  EMAIL_PORT=587
  EMAIL_USER=seu@gmail.com
  EMAIL_PASSWORD=senha-de-app-google
  EMAIL_FROM=noreply@seudominio.com.br
  APP_URL=https://seudominio.com.br
  ```
  > Gmail: ative "Senhas de app" em myaccount.google.com/security

- [ ] **Configurar backup automático do PostgreSQL**
  ```powershell
  # Backup manual
  docker exec cardgamestore_postgres pg_dump -U cardgame_user cardgamestore > backup_$(Get-Date -Format yyyyMMdd).sql
  ```
  Configure backup diário automático via cron ou Task Scheduler.

---

## Importante — fazer logo após subir

- [ ] **Testar o fluxo completo em produção**
  Login admin → criar produto → gerar QR Code → escanear com celular → abrir comanda → adicionar item → ver no dashboard → fechar comanda

- [ ] **Verificar SignalR com domínio real**
  O WebSocket precisa que o reverse proxy passe os headers de upgrade (ver acima)

- [ ] **Verificar email de reset de senha**
  Solicitar reset e confirmar recebimento antes de entregar ao cliente

- [ ] **Verificar email de crediário**
  Fechar uma comanda em crediário e confirmar que o cliente recebe o email

---

## Pendente de decisão / roadmap

- [ ] **Integração PIX** — gateway a definir (Mercado Pago, PagSeguro, Asaas)
- [ ] **Permissão de uso de pontos** — toggle admin para liberar/bloquear por cliente
- [ ] **Migrations EF Core** — substituir EnsureCreated para mudanças de schema em produção
- [ ] **GitHub Actions CI/CD** — build e deploy automático no push para main
- [ ] **Paginação no catálogo** — para lojas com muitos produtos

---

## Comandos úteis em produção

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

# Parar e apagar tudo (IRREVERSÍVEL — apaga o banco)
docker compose down -v

# Status dos containers
docker compose ps

# Backup do banco
docker exec cardgamestore_postgres pg_dump -U cardgame_user cardgamestore > backup.sql

# Restaurar backup
docker exec -i cardgamestore_postgres psql -U cardgame_user cardgamestore < backup.sql

# Rodar testes com cobertura
dotnet test --collect:"XPlat Code Coverage"
```

---

*softNerd © 2026 — v3.0*
