@echo off
set ROOT=%~dp0

echo.
echo ============================================================
echo   CardGameStore - Iniciando Sistema
echo ============================================================
echo.

echo [1] Encerrando OneDrive para evitar corrompimento...
taskkill /f /im OneDrive.exe >nul 2>&1
taskkill /f /im Microsoft.SharePoint.exe >nul 2>&1
echo OK.

echo [2] Encerrando processos antigos...
taskkill /f /im dotnet.exe >nul 2>&1
taskkill /f /im node.exe >nul 2>&1
timeout /t 3 /nobreak >nul
echo OK.

echo [3] Localizando npm...
set NPM_PATH=
for /f "delims=" %%i in ('where npm 2^>nul') do (
    if not defined NPM_PATH set NPM_PATH=%%i
)
if not defined NPM_PATH (
    if exist "C:\Program Files\nodejs\npm.cmd" set NPM_PATH=C:\Program Files\nodejs\npm.cmd
)
if not defined NPM_PATH (
    if exist "%APPDATA%\npm\npm.cmd" set NPM_PATH=%APPDATA%\npm\npm.cmd
)
if not defined NPM_PATH (
    echo ERRO: npm nao encontrado! Instale Node.js.
    pause
    exit /b 1
)
echo npm em: %NPM_PATH%

echo [4] Verificando node_modules do frontend...
if exist "%ROOT%frontend\node_modules\next\dist\cli\next-dev.js" (
    echo node_modules OK - pulando npm install.
    echo Reaplicando patch Unicode...
    node "%ROOT%frontend\scripts\patch-next.js"
) else (
    echo node_modules ausente ou corrompido - reinstalando...
    if exist "%ROOT%frontend\node_modules" (
        rmdir /s /q "%ROOT%frontend\node_modules" >nul 2>&1
        timeout /t 3 /nobreak >nul
    )
    cd /d "%ROOT%frontend"
    "%NPM_PATH%" install
    echo OK - dependencias instaladas e patch aplicado via postinstall.
)

echo [5] Restaurando pacotes NuGet da API...
cd /d "%ROOT%CardGameStore"
dotnet restore
echo OK.

echo [6] Iniciando API na porta 5000...
start "CardGameStore API" cmd /k "%ROOT%start-api.bat"

echo [7] Aguardando API iniciar (12s)...
timeout /t 12 /nobreak >nul

echo [8] Iniciando Frontend na porta 3000...
start "CardGameStore Frontend" cmd /k "%ROOT%start-fe.bat"

echo.
echo ============================================================
echo   Aguardando servicos subirem (40s)...
echo ============================================================
timeout /t 40 /nobreak >nul

echo.
echo ============================================================
echo   Abrindo no navegador...
echo   API/Swagger: http://localhost:5000
echo   Frontend:    http://localhost:3000
echo.
echo   Login Admin:
echo   Email: admin@cardgamestore.com.br
echo   Senha: SenhaForte@123
echo ============================================================
echo.

start "" "http://localhost:5000"
timeout /t 2 /nobreak >nul
start "" "http://localhost:3000"

pause
