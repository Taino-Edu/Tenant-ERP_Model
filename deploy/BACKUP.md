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

## Cópia off-site no Drive da empresa

Sem isto o backup mora no mesmo disco do banco, e uma falha de disco leva os dois
juntos. A configuração abaixo usa **conta de serviço + Drive Compartilhado**, que
não depende da conta de nenhuma pessoa: ninguém sai da empresa e derruba o backup.

### 1. Conta de serviço no Google Cloud

1. [console.cloud.google.com](https://console.cloud.google.com) → crie (ou escolha) um projeto
2. **APIs e serviços → Biblioteca** → ative a **Google Drive API**
3. **APIs e serviços → Credenciais → Criar credenciais → Conta de serviço**
4. Na conta criada: **Chaves → Adicionar chave → Criar nova chave → JSON**. Baixa um arquivo
5. Anote o **e-mail** da conta de serviço (algo como `backup-vps@projeto.iam.gserviceaccount.com`)

### 2. Dar acesso ao Drive Compartilhado

1. Abra o **Drive Compartilhado** da empresa → **Gerenciar membros**
2. Adicione o e-mail da conta de serviço como **Gerenciador de conteúdo**
3. Crie uma pasta, ex. `Backups/Octus`
4. Copie o **ID do Drive Compartilhado** da URL: `drive.google.com/drive/folders/`**`0AB...`**

> Conta de serviço não tem cota de armazenamento própria. Em Drive Compartilhado
> isso não importa (a cota é do Drive), mas é por isso que **não** funciona
> apontando para o "Meu Drive" de alguém.

### 3. rclone no VPS

```bash
sudo apt-get update && sudo apt-get install -y rclone gnupg
sudo mkdir -p /etc/rclone && sudo chmod 700 /etc/rclone
```

Copie o JSON da conta de serviço para `/etc/rclone/octus-backup.json` e feche o acesso:

```bash
sudo chmod 600 /etc/rclone/octus-backup.json
```

Crie `/root/.config/rclone/rclone.conf` (troque o `team_drive`):

```ini
[drive]
type = drive
scope = drive
service_account_file = /etc/rclone/octus-backup.json
team_drive = 0ABxxxxxxxxxxxxUk9PVA
```

Teste antes de confiar:

```bash
sudo rclone lsd drive:
```

### 4. Ligar no backup

No `/opt/tenant-erp/.env` (o mesmo do docker-compose):

```bash
BACKUP_REMOTE_CMD=rclone copy --config /root/.config/rclone/rclone.conf --drive-chunk-size 32M drive:Backups/Octus
BACKUP_ENCRYPT_PASSPHRASE=<frase longa e aleatória>
```

O `backup.sh` lê as duas do `.env`, e não só do ambiente — o cron roda com
ambiente vazio, e se dependesse de `export` o backup rodaria todo dia sem enviar
nada e sem reclamar.

Gere a frase com `openssl rand -base64 32`.

> **Guarde a frase-secreta fora do Drive.** No mesmo lugar do backup ela não
> protege de nada, e sem ela os arquivos enviados são irrecuperáveis. Gerenciador
> de senhas da empresa ou cofre físico.

Rode uma vez na mão e confira que os **dois** arquivos aparecem no Drive:

```bash
cd /opt/tenant-erp && bash deploy/backup.sh
```

## Restaurar

O que sai do servidor é `.sql.gz.gpg`; o que fica no VPS é `.sql.gz` em texto puro.

**Do arquivo local (VPS de pé, rollback de deploy):**

```bash
gunzip -c /opt/tenant-erp/backups/postgres_<TS>.sql.gz \
  | docker exec -i cardgamestore_postgres psql -U <POSTGRES_USER> <POSTGRES_DB>
```

**Do Drive (VPS perdido — o caso que justifica tudo isto):**

```bash
rclone copy drive:Backups/Octus/postgres_<TS>.sql.gz.gpg .
gpg --decrypt postgres_<TS>.sql.gz.gpg > postgres_<TS>.sql.gz
gunzip -c postgres_<TS>.sql.gz \
  | docker exec -i cardgamestore_postgres psql -U <POSTGRES_USER> <POSTGRES_DB>
```

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
rclone lsl drive:Backups/Octus | tail -5   # a data do último arquivo diz tudo
```

## Limites conhecidos

- **O rollback do `update.sh` reverte código, não schema.** As migrations rodam no
  boot da API e não são desfeitas. Se uma migration destrutiva corromper dados, a
  saída é restaurar o dump — por isso ele é tirado *antes* de qualquer mudança.
- **A cifra protege o backup no Drive, não o servidor.** Quem tiver acesso de root
  ao VPS alcança o `.env` com a frase-secreta e o banco em si. A ameaça coberta
  aqui é "alguém com acesso à pasta do Drive não deve ler dado de cliente".
- **Retenção no Drive é manual.** O script só limpa a cópia local. Defina uma
  regra de retenção na pasta do Drive, ou os dumps acumulam para sempre.
