# Correções de resiliência — 2026-09-08

Esta rodada fechou os achados RES-001 a RES-007 e AUTH-001 da auditoria de
resiliência. Nenhum deploy foi executado durante a implementação.

## Venda e crediário

- Venda avulsa recebe `IdempotencyKey`, grava a chave como identidade da venda e
  persiste um fingerprint SHA-256 da intenção. Retry igual devolve a venda já
  confirmada; reutilizar a chave com dados diferentes é recusado.
- A intenção fiscal passou a ser gravada dentro da mesma transação da venda.
- O PDV mantém a chave entre tentativas do mesmo payload e só cria outra quando
  a venda muda ou a anterior termina com sucesso.
- Alterações do crediário são serializadas por cliente com advisory lock do
  PostgreSQL. Venda avulsa e fechamento de comanda usam a mesma chave.
- O banco passa a garantir um único crediário aberto por cliente com índice único
  parcial. A migration consolida duplicados legados antes de criar o índice,
  somando dívida e pagamentos, juntando itens e redirecionando referências.

## Autenticação e configuração

- Produção recusa segredo JWT ausente, curto ou com placeholder conhecido.
- Uma conta privilegiada nova não pode ser seedada sem senha configurada ou com
  `SenhaForte@123`. Instalações onde a conta já existe não são bloqueadas por uma
  variável de seed removida depois.
- Todos os JWTs normais carregam `session_version`. Logout e reset de senha
  incrementam essa versão; o middleware rejeita imediatamente tokens antigos ou
  contas desativadas.
- O cliente HTTP tem timeout. Uma falha de rede, timeout, 429 ou 5xx durante o
  refresh não limpa a sessão; somente 401/403 confirma que é preciso entrar de novo.

## Migrations e operação

- O catálogo registra prontidão, versão, horário e último erro de migration por
  tenant. Uma loja cujo schema falhou responde 503 com `Retry-After`, sem executar
  endpoints contra uma estrutura antiga. As outras lojas continuam atendendo.
- `/health` retorna estado degradado quando existe tenant isolado por migration.
- O backup inclui `api_uploads` em `uploads_<timestamp>.tar.gz`, valida o arquivo,
  aplica a mesma retenção e o inclui na cópia off-site cifrada.
- O deploy usa `flock`, rejeita checkout com alterações locais e publica o SHA de
  40 caracteres aprovado pelo CI. O job de produção também tem exclusão mútua.
- `COOKIE_SECURE` passou a ter default `true` no Compose de produção.

## Validação local

- `dotnet test softNerd.sln --no-restore --verbosity quiet`: 997 aprovados, zero
  falhas e zero ignorados, contra PostgreSQL real.
- Migration de venda aplicada de ponta a ponta em schema descartável contendo
  dois crediários abertos legados; consolidação e índice único aprovados.
- `npm run lint`, `npx tsc --noEmit` e `npm run build`: aprovados.
- `bash -n deploy/backup.sh` e `bash -n deploy/update.sh`: aprovados.
- `.github/workflows/ci.yml`: YAML válido.

Antes do deploy, confirme que `/opt/tenant-erp/.env` contém `JWT_SECRET` forte e
que `flock` está disponível (`command -v flock`). O `setup.sh` já gera as senhas
de seed e instala o ambiente esperado em instalações novas.
