# =============================================================================
# Dockerfile — Build multi-stage para produção
# Stage 1: build com SDK  |  Stage 2: runtime mínimo (sem SDK)
# =============================================================================

# ---- STAGE 1: BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas o .csproj primeiro — camada em cache enquanto dependências não mudarem
COPY CardGameStore.csproj .
RUN dotnet restore

# Copia o restante e publica em modo Release
# Nota: --no-restore omitido intencionalmente — garante resolve correto de pacotes
# mesmo quando a camada de restore está em cache com versão anterior do .csproj
COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---- STAGE 2: RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl é necessário para o healthcheck do docker-compose
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 5000

# ASPNETCORE_ENVIRONMENT e demais configurações vêm do docker-compose / variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "CardGameStore.dll"]
