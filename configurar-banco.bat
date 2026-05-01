@echo off
chcp 65001 >nul
title CardGameStore — Configurar Banco de Dados
color 0B

echo.
echo ============================================================
echo   CardGameStore — Configurar PostgreSQL
echo ============================================================
echo.
echo Criando usuario e banco de dados no PostgreSQL...
echo Quando pedir senha, digite a senha do postgres (que voce definiu na instalacao)
echo.

:: Tenta adicionar o psql ao PATH (caminhos comuns do PostgreSQL)
set PGPATH=
if exist "C:\Program Files\PostgreSQL\16\bin\psql.exe" set PGPATH=C:\Program Files\PostgreSQL\16\bin
if exist "C:\Program Files\PostgreSQL\15\bin\psql.exe" set PGPATH=C:\Program Files\PostgreSQL\15\bin
if exist "C:\Program Files\PostgreSQL\14\bin\psql.exe" set PGPATH=C:\Program Files\PostgreSQL\14\bin

if not "%PGPATH%"=="" (
    set PATH=%PATH%;%PGPATH%
    echo Encontrado PostgreSQL em: %PGPATH%
)

echo.
echo Criando usuario cardgame_user...
psql -U postgres -c "DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'cardgame_user') THEN CREATE USER cardgame_user WITH PASSWORD 'CardGame@2025'; END IF; END $$;"

echo.
echo Criando banco cardgamestore...
psql -U postgres -c "SELECT 'CREATE DATABASE cardgamestore OWNER cardgame_user' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'cardgamestore')\gexec"

echo.
echo Dando privilegios...
psql -U postgres -c "GRANT ALL PRIVILEGES ON DATABASE cardgamestore TO cardgame_user;"

echo.
echo ============================================================
echo   Banco configurado! Agora execute: rodar.bat
echo ============================================================
echo.
pause
