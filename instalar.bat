@echo off
chcp 65001 >nul
title CardGameStore — Instalador
color 0A

echo.
echo ============================================================
echo   CardGameStore — Instalador de Dependencias
echo ============================================================
echo.

:: ── Verifica .NET SDK ────────────────────────────────────────
echo [1/4] Verificando .NET SDK 8...
dotnet --version >nul 2>&1
if %errorlevel% == 0 (
    echo       OK - .NET SDK ja instalado:
    dotnet --version
) else (
    echo       Instalando .NET SDK 8 via winget...
    winget install Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
    echo       .NET SDK 8 instalado! Reinicie o script apos reiniciar o terminal.
)

:: ── Verifica PostgreSQL ──────────────────────────────────────
echo.
echo [2/4] Verificando PostgreSQL...
sc query postgresql* >nul 2>&1
if %errorlevel% == 0 (
    echo       OK - PostgreSQL ja instalado.
) else (
    where psql >nul 2>&1
    if %errorlevel% == 0 (
        echo       OK - PostgreSQL ja disponivel.
    ) else (
        echo       Instalando PostgreSQL 16 via winget...
        winget install PostgreSQL.PostgreSQL.16 --silent --accept-package-agreements --accept-source-agreements
        echo       PostgreSQL instalado!
    )
)

:: ── Verifica MongoDB ─────────────────────────────────────────
echo.
echo [3/4] Verificando MongoDB...
sc query MongoDB >nul 2>&1
if %errorlevel% == 0 (
    echo       OK - MongoDB ja instalado como servico.
) else (
    where mongod >nul 2>&1
    if %errorlevel% == 0 (
        echo       OK - MongoDB ja disponivel.
    ) else (
        echo       Instalando MongoDB Community Server via winget...
        winget install MongoDB.Server --silent --accept-package-agreements --accept-source-agreements
        echo       MongoDB instalado!
    )
)

:: ── Instala pacotes npm do frontend ─────────────────────────
echo.
echo [4/4] Instalando dependencias do frontend (npm install)...
cd /d "%~dp0frontend"
call npm install --legacy-peer-deps
echo       Dependencias do frontend instaladas!

echo.
echo ============================================================
echo   Instalacao concluida!
echo   Agora execute: rodar.bat
echo ============================================================
echo.
pause
