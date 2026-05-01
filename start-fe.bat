@echo off
cd /d "%~dp0frontend"
echo Iniciando Frontend Next.js na porta 3000...
where npm >nul 2>&1
if %errorlevel% == 0 (
    npm run dev
) else if exist "C:\Program Files\nodejs\npm.cmd" (
    "C:\Program Files\nodejs\npm.cmd" run dev
) else if exist "%APPDATA%\npm\npm.cmd" (
    "%APPDATA%\npm\npm.cmd" run dev
) else (
    echo ERRO: npm nao encontrado no PATH nem em locais padrao!
    pause
)
