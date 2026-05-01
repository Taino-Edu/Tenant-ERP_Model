@echo off
echo.
echo ============================================================
echo   Desativando OneDrive permanentemente desta maquina
echo ============================================================
echo.
echo [1] Encerrando todos os processos OneDrive...
taskkill /f /im OneDrive.exe >/dev/null 2>&1
taskkill /f /im Microsoft.SharePoint.exe >/dev/null 2>&1
taskkill /f /im OneDriveStandaloneUpdater.exe >/dev/null 2>&1
echo OK.
echo [2] Removendo OneDrive do startup do Windows (registro)...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v OneDrive /f >/dev/null 2>&1
echo OK - OneDrive nao vai mais iniciar com o Windows.
echo [3] Verificando se ainda rodando...
timeout /t 3 /nobreak >/dev/null
taskkill /f /im OneDrive.exe >/dev/null 2>&1
echo OK.
echo.
echo ============================================================
echo   OneDrive DESATIVADO!
echo   Nao iniciara mais automaticamente com o Windows.
echo   Para reativar: procure OneDrive no menu iniciar.
echo ============================================================
echo.
pause
