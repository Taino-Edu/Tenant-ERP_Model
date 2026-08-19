# Backup e restauração

## O que já roda sozinho

| | |
|---|---|
| **Quando** | Todo dia às 03:00 (cron instalado pelo `setup.sh`) e antes de cada deploy (`update.sh`) |
| **O quê** | `postgres_<timestamp>.sql.gz` (o ERP) e `evolution_<timestamp>.sql.gz` (sessões de WhatsApp, se a feature estiver ligada) |
| **Onde** | `/opt/tenant-erp/backups` |
| **Retenção local** | 7 dias (`BACKUP_RETAIN_DAYS`) |
| **Integridade** | `gzip -t` + tamanho mínimo. Dump corrompido ou vazio é apagado e o script falha na hora |

Os dois bancos são separados de propósito: o `pg_dump` do ERP **não** cobre o da
Evolution. Perder aquele banco significa pedir a cada cliente com WhatsApp que
leia o QR Code de novo.

## Cópia off-site no Cloudflare R2

Sem isto o backup mora no mesmo disco do banco, e uma falha de disco leva os dois
juntos.

O destino é R2 porque a Cloudflare já está na frente da aplicação (o rate limiter
lê `CF-Connecting-IP`), então não entra fornecedor novo. O dump comprimido tem
~2 MB; a faixa gratuita de 10 GB cobre anos disso.

### 1. Bucket e credenciais

1. Painel da Cloudflare → **R2** → ative o serviço (exige forma de pagamento
   cadastrada; nada é cobrado dentro da faixa gratuita)
2. **Create bucket** → nome `octus-backups`
3. Em **R2 → Manage R2 API Tokens → Create Account API token**
4. Permissão **Object Read and Write**, com escopo limitado ao bucket `octus-backups`
5. Guarde o **Access Key ID** e o **Secret Access Key** — o segredo só aparece uma vez
6. Anote também o **Account ID** ([onde encontrar](https://developers.cloudflare.com/fundamentals/account/find-account-and-zone-ids/))

> Token de conta, não de usuário: token de usuário herda as permissões da pessoa
> e morre junto se ela sair da conta da Cloudflare — o mesmo problema que faria o
> backup depender de um indivíduo.

### 2. rclone no VPS

**Não instale pelo `apt`.** O Ubuntu 22.04 empacota o rclone 1.53, e a
documentação da Cloudflare exige **1.59 ou superior** — abaixo disso o R2
responde HTTP 401 e o envio falha sem motivo aparente. Use o instalador oficial:

```bash
curl https://rclone.org/install.sh | sudo bash
sudo apt-get update && sudo apt-get install -y gnupg
rclone version   # confirme >= 1.59
```

Crie `/root/.config/rclone/rclone.conf`:

```ini
[r2]
type = s3
provider = Cloudflare
access_key_id = <ACCESS_KEY_ID>
secret_access_key = <SECRET_ACCESS_KEY>
endpoint = https://<ACCOUNT_ID>.r2.cloudflarestorage.com
region = auto
acl = private
no_check_bucket = true
```

`no_check_bucket = true` porque o token está limitado a um bucket: sem isso o
rclone tenta verificar/criar o bucket na primeira escrita e leva 403.

Feche o arquivo e teste:

```bash
sudo chmod 600 /root/.config/rclone/rclone.conf
sudo rclone ls r2:octus-backups
```

### 3. Ligar no backup

No `/opt/tenant-erp/.env` (o mesmo do docker-compose):

```bash
BACKUP_REMOTE_CMD=rclone copy --config /root/.config/rclone/rclone.conf r2:octus-backups
BACKUP_ENCRYPT_PASSPHRASE=<frase longa e aleatória>
```

O `backup.sh` lê as duas do `.env`, e não só do ambiente — o cron roda com
ambiente vazio, e se dependesse de `export` o backup rodaria todo dia sem enviar
nada e sem reclamar.

Gere a frase com `openssl rand -base64 32`.

**As duas variáveis andam juntas.** Com `BACKUP_REMOTE_CMD` definido e
`BACKUP_ENCRYPT_PASSPHRASE` vazio, o script para com erro em vez de enviar o dump
legível. O dump carrega CPF, e-mail, telefone e endereço dos clientes das lojas —
num produto que vende módulo de LGPD — mais as credenciais de sessão do WhatsApp
no dump do Evolution. Antes a cifra era opcional e o esquecimento era silencioso:
o log dizia "Enviado OK" todas as noites e nada indicava que o arquivo tinha
saído em texto puro. Se você quiser mesmo ficar só com o backup local, tire o
`BACKUP_REMOTE_CMD` — é uma decisão que precisa estar escrita no `.env`, não um
efeito colateral de uma linha esquecida.

Os dumps locais são escritos antes dessa checagem, então mesmo quando ela falha
você continua com backup em disco. O que falta é a redundância off-site, que é
exatamente o que está mal configurado.

> **Guarde a frase-secreta fora do R2.** No mesmo lugar do backup ela não protege
> de nada, e sem ela os arquivos enviados são irrecuperáveis. Gerenciador de
> senhas da empresa ou cofre físico.

Rode uma vez na mão e confira que os **dois** arquivos aparecem no bucket:

```bash
cd /opt/tenant-erp && bash deploy/backup.sh
sudo rclone ls r2:octus-backups
```

### 4. Retenção no bucket

O script só limpa a cópia local; sem uma regra no bucket os dumps acumulam para
sempre. No painel: **R2 → octus-backups → Settings → Object lifecycle rules** →
regra de expiração (30 ou 90 dias, conforme por quanto tempo você quer poder
voltar no tempo).

Isso é separado do `BACKUP_RETAIN_DAYS`, que vale só para o disco do VPS — e é
proposital: o local existe para restauração rápida, o remoto para histórico.

## Restaurar

O que sai do servidor é `.sql.gz.gpg`; o que fica no VPS é `.sql.gz` em texto puro.

**Do arquivo local (VPS de pé, rollback de deploy):**

```bash
gunzip -c /opt/tenant-erp/backups/postgres_<TS>.sql.gz \
  | docker exec -i cardgamestore_postgres psql -U <POSTGRES_USER> <POSTGRES_DB>
```

**Do R2 (VPS perdido — o caso que justifica tudo isto):**

```bash
rclone copy r2:octus-backups/postgres_<TS>.sql.gz.gpg .
gpg --decrypt postgres_<TS>.sql.gz.gpg > postgres_<TS>.sql.gz
gunzip -c postgres_<TS>.sql.gz \
  | docker exec -i cardgamestore_postgres psql -U <POSTGRES_USER> <POSTGRES_DB>
```

Numa máquina nova você precisa de três coisas para isso funcionar: o `rclone`
com as credenciais do R2, o `gpg` e a **frase-secreta**. Se as três estiverem
guardadas só no VPS que se perdeu, o backup não serve para nada — é por isso que
a frase mora no gerenciador de senhas da empresa.

O `evolution_<TS>.sql.gz.gpg` restaura igual, no banco `evolution`.

## Duas coisas que ninguém lembra de fazer

**Testar a restauração.** Backup nunca restaurado é hipótese, não backup. Uma vez
por trimestre, restaure num banco descartável e confira que os dados estão lá:

```bash
docker exec cardgamestore_postgres psql -U <USER> -d <DB> -c "CREATE DATABASE teste_restore;"
gunzip -c backups/postgres_<TS>.sql.gz | docker exec -i cardgamestore_postgres psql -U <USER> -d teste_restore
docker exec cardgamestore_postgres psql -U <USER> -d teste_restore -c "\dt"
docker exec cardgamestore_postgres psql -U <USER> -d <DB> -c "DROP DATABASE teste_restore;"
```

**Olhar se ainda está rodando.** O envio que falha derruba o script, então o cron
manda e-mail de erro — mas só se o `MAILTO` do crontab estiver configurado. Sem
isso, o jeito é conferir o log de vez em quando:

```bash
tail -20 /var/log/tenant-erp-backup.log
rclone lsl r2:octus-backups | tail -5   # a data do último arquivo diz tudo
```

## Limites conhecidos

- **O rollback do `update.sh` reverte código, não schema.** As migrations rodam no
  boot da API e não são desfeitas. Se uma migration destrutiva corromper dados, a
  saída é restaurar o dump — por isso ele é tirado *antes* de qualquer mudança.
- **A cifra protege o backup no R2, não o servidor.** Quem tiver acesso de root ao
  VPS alcança o `.env` com a frase-secreta e o banco em si. A ameaça coberta aqui
  é "alguém com acesso ao bucket não deve ler dado de cliente".

## Custo

Nada, com folga larga. O dump comprimido tem ~2 MB e sobem dois por dia.

| | Uso | Faixa gratuita |
|---|---|---|
| Armazenamento | ~120 MB (com expiração em 30 dias) | 10 GB-mês |
| Operações Classe A (escrita) | ~60 por mês | 1 milhão por mês |
| Egress | só numa restauração | sempre gratuito |

Fonte: [preços do R2](https://developers.cloudflare.com/r2/pricing/). A Cloudflare
exige forma de pagamento cadastrada para ativar o R2, mas dentro desses limites
não há cobrança.
