# =============================================================================
# run-tests.ps1 — Sobe o PostgreSQL de testes e roda a suíte.
#
# Reaproveita o container entre execuções, mas confere se ele foi criado com a
# configuração que a suíte exige. Um container antigo (criado por `docker run`
# cru, ou anterior ao ajuste no compose) sobe com max_locks_per_transaction=64,
# o default do Postgres — e nessa configuração a suíte falha de 4 a 19 testes
# por rodada, sempre em testes diferentes, todos passando quando executados
# isolados. Cada DROP SCHEMA CASCADE da TestDbFactory pega um lock por objeto
# (~60 tabelas mais índices) e a tabela de locks do cluster estoura; o schema
# fica pela metade e o teste seguinte morre com "42P01: relation does not
# exist", erro que aponta pro código e não pro servidor.
#
# Por isso o `docker start` puro saiu daqui: ele reanimava justamente o
# container mal configurado, sem nunca aplicar a config nova do compose.
# =============================================================================
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$composeFile    = Join-Path $repositoryRoot 'tests\docker-compose.yml'
$testProject    = Join-Path $repositoryRoot 'tests\unit\CardGameStore.Tests\CardGameStore.Tests.csproj'
$containerName  = 'tenant-erp-test-db'
$minLocks       = 512

function Get-LocksPerTransaction {
    if (-not (docker ps --filter "name=^/$containerName$" --format '{{.Names}}')) { return 0 }
    $value = docker exec $containerName psql -U tenant_test -d tenant_erp_test -t -A `
        -c 'show max_locks_per_transaction' 2>$null
    if ($LASTEXITCODE -ne 0) { return 0 }
    return [int]$value
}

docker compose -f $composeFile up -d --wait

if ((Get-LocksPerTransaction) -lt $minLocks) {
    Write-Host "PostgreSQL de testes está com a configuração antiga — recriando o container..." -ForegroundColor Yellow
    docker rm -f $containerName 2>$null | Out-Null
    docker compose -f $composeFile up -d --wait
}

$locks = Get-LocksPerTransaction
if ($locks -lt $minLocks) {
    # Aviso, não erro: a suíte roda com os defaults do Postgres (é o que o CI
    # faz). Só significa que este container ficou sem a folga e sem o fsync=off
    # do compose, de onde vem a maior parte do ganho de tempo.
    Write-Host "Aviso: max_locks_per_transaction=$locks (recomendado $minLocks). Confira o command: em $composeFile." -ForegroundColor Yellow
}

dotnet test $testProject @args
