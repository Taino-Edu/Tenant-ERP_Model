# =============================================================================
# start.ps1 — Script PowerShell para subir toda a stack com 1 comando
# Execute no Windows: .\start.ps1
# Requer: Docker Desktop instalado e rodando
# =============================================================================

param(
    [switch]$WithAdmin,   # -WithAdmin: sobe também o pgAdmin na porta 5050
    [switch]$Stop,        # -Stop: para e remove todos os containers
    [switch]$Reset        # -Reset: para, apaga volumes e reinicia do zero
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  CardGameStore — Inicializador Docker" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# Verifica se o Docker está rodando
try {
    docker info 2>&1 | Out-Null
} catch {
    Write-Host "ERRO: Docker Desktop não encontrado ou não está rodando." -ForegroundColor Red
    Write-Host "Instale em: https://www.docker.com/products/docker-desktop" -ForegroundColor Yellow
    exit 1
}

Set-Location $ProjectDir

if ($Stop) {
    Write-Host "`nParando containers..." -ForegroundColor Yellow
    docker compose down
    Write-Host "Containers parados." -ForegroundColor Green
    exit 0
}

if ($Reset) {
    Write-Host "`nResetando stack (apagando volumes)..." -ForegroundColor Yellow
    docker compose down -v --remove-orphans
    Write-Host "Stack resetada. Reconstruindo..." -ForegroundColor Green
}

# Determina o perfil
$profile = if ($WithAdmin) { "--profile tools" } else { "" }

Write-Host "`nConstruindo e subindo containers..." -ForegroundColor Green
Write-Host "(Primeira execução pode demorar ~3-5 min para baixar imagens)" -ForegroundColor DarkGray

if ($WithAdmin) {
    docker compose --profile tools up --build -d
} else {
    docker compose up --build -d
}

Write-Host "`nAguardando a API inicializar" -ForegroundColor Yellow
$maxWait = 60
$waited  = 0
do {
    Start-Sleep -Seconds 3
    $waited += 3
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) { break }
    } catch {}
    Write-Host "  Aguardando... ($waited/$maxWait segundos)" -ForegroundColor DarkGray
} while ($waited -lt $maxWait)

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Stack iniciada com sucesso!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Swagger / API:   http://localhost:5000" -ForegroundColor Cyan
Write-Host "  PostgreSQL:      localhost:5432  (cardgame_user / CardGame@2025)" -ForegroundColor Cyan
Write-Host "  MongoDB:         localhost:27017" -ForegroundColor Cyan
if ($WithAdmin) {
    Write-Host "  pgAdmin:         http://localhost:5050  (admin@cardgame.com / admin)" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "  Para testar, abra: http://localhost:5000" -ForegroundColor Yellow
Write-Host "  Para parar:        .\start.ps1 -Stop" -ForegroundColor Yellow
Write-Host "  Para resetar DB:   .\start.ps1 -Reset" -ForegroundColor Yellow
Write-Host ""

# Abre o Swagger no navegador padrão
Start-Process "http://localhost:5000"
