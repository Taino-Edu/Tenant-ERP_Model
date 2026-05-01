@echo off
echo.
echo ============================================================
echo   Resolvendo Frontend - Movendo node_modules para fora
echo   do OneDrive para evitar corrompimento
echo ============================================================
echo.

set FRONTEND=%~dp0frontend
set MODULES_TEMP=%LOCALAPPDATA%\CardGameStore\node_modules

echo [1] Encerrando OneDrive...
taskkill /f /im OneDrive.exe >nul 2>&1
taskkill /f /im Microsoft.SharePoint.exe >nul 2>&1
echo OK.

echo [2] Encerrando node.exe...
taskkill /f /im node.exe >nul 2>&1
echo OK.

echo [3] Aguardando handles liberarem...
timeout /t 5 /nobreak >nul

echo [4] Removendo node_modules atual (pasta ou symlink)...
if exist "%FRONTEND%\node_modules" (
    rmdir "%FRONTEND%\node_modules" >nul 2>&1
    if exist "%FRONTEND%\node_modules" (
        rmdir /s /q "%FRONTEND%\node_modules" >nul 2>&1
    )
    timeout /t 3 /nobreak >nul
)

echo [5] Criando pasta para modules FORA do OneDrive...
echo     Destino: %MODULES_TEMP%
if exist "%MODULES_TEMP%" (
    echo Limpando modulos antigos...
    rmdir /s /q "%MODULES_TEMP%" >nul 2>&1
    timeout /t 3 /nobreak >nul
)
mkdir "%MODULES_TEMP%"
echo OK.

echo [6] Criando symlink node_modules ^-> fora do OneDrive...
mklink /D "%FRONTEND%\node_modules" "%MODULES_TEMP%"
if %errorlevel% neq 0 (
    echo ERRO ao criar symlink. Tentando sem symlink...
    mkdir "%FRONTEND%\node_modules"
)

echo [7] Instalando dependencias (fora do OneDrive)...
cd /d "%FRONTEND%"

where npm >nul 2>&1
if %errorlevel% == 0 (
    npm install
) else if exist "C:\Program Files\nodejs\npm.cmd" (
    "C:\Program Files\nodejs\npm.cmd" install
) else (
    echo ERRO: npm nao encontrado!
    pause
    exit /b 1
)

echo.
echo [8] Verificando se next-dev.js foi instalado...
if exist "%MODULES_TEMP%\next\dist\cli\next-dev.js" (
    echo OK - next-dev.js encontrado!
) else if exist "%FRONTEND%\node_modules\next\dist\cli\next-dev.js" (
    echo OK - next-dev.js encontrado via symlink!
) else (
    echo AVISO: next-dev.js nao encontrado. Tentando novamente...
    npm install next --save-exact
)

echo.
echo [9] Iniciando Frontend...
start "CardGameStore Frontend" cmd /k "%~dp0start-fe.bat"

echo.
echo ============================================================
echo   Frontend iniciado! Aguarde 30-60s e acesse:
echo   http://localhost:3000
echo ============================================================
echo.
pause
