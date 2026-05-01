@echo off
cd /d "%~dp0CardGameStore"
set ASPNETCORE_ENVIRONMENT=Development
echo Iniciando API na porta 5000...
dotnet run --launch-profile http
