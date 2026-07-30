$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$composeFile = Join-Path $repositoryRoot 'tests\docker-compose.yml'
$testProject = Join-Path $repositoryRoot 'tests\unit\CardGameStore.Tests\CardGameStore.Tests.csproj'
$containerName = 'tenant-erp-test-db'

$existingContainer = docker ps -a --filter "name=^/$containerName$" --format '{{.Names}}'
if ($existingContainer -eq $containerName) {
    docker start $containerName | Out-Null

    $ready = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        docker exec $containerName pg_isready -U tenant_test -d tenant_erp_test *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        throw "O PostgreSQL de testes não ficou pronto em 30 segundos."
    }
}
else {
    docker compose -f $composeFile up -d --wait
}

dotnet test $testProject @args
