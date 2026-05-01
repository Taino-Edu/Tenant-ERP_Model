@echo off
chcp 65001 >nul
title CardGameStore - Iniciando Projeto
color 0A

echo.
echo ============================================================
echo   CardGameStore - Iniciando todos os servicos
echo ============================================================
echo.

set ROOT=%~dp0
set ASPNETCORE_ENVIRONMENT=Development

:: Verifica .NET
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERRO: .NET SDK nao encontrado!
    echo Execute instalar.bat primeiro e reinicie o computador.
    pause
    exit /b 1
)

echo .NET SDK encontrado:
dotnet --version

:: Restaura pacotes NuGet
echo.
echo [1/3] Restaurando pacotes NuGet do backend...
cd /d "%ROOT%CardGameStore"
dotnet restore
if %errorlevel% neq 0 (
    echo ERRO no restore. Verifique a conexao com a internet.
    pause
    exit /b 1
)
echo OK - Pacotes restaurados!

:: Inicia a API em janela separada
echo.
echo [2/3] Iniciando a API (backend SQLite)...
echo       O banco SQLite sera criado automaticamente na primeira execucao.
start "CardGameStore API" cmd /k "cd /d "%ROOT%CardGameStore" && set ASPNETCORE_ENVIRONMENT=Development && dotnet run --urls http://localhost:5000"

echo Aguardando API subir (15 segundos)...
timeout /t 15 /nobreak >nul

:: Inicia o Frontend em janela separada
echo.
echo [3/3] Iniciando o Frontend (Next.js)...
start "CardGameStore Frontend" cmd /k "cd /d "%ROOT%frontend" && npm run dev"

echo Aguardando frontend subir (10 segundos)...
timeout /t 10 /nobreak >nul

echo.
echo ============================================================
echo   Tudo iniciado!
echo.
echo   Frontend:    http://localhost:3000
echo   API/Swagger: http://localhost:5000
echo.
echo   Login: admin@cardgamestore.com.br
echo   Senha: SenhaForte@123
echo.
echo   Banco: SQLite (cardgamestore.db) - sem servidor necessario
echo ============================================================
echo.

start "" "http://localhost:5000"
timeout /t 3 /nobreak >nul
start "" "http://localhost:3000"

pause
