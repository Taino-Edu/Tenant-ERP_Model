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

# Habilita o provider "legacy" do OpenSSL 3 — certificados A1 ICP-Brasil (.pfx)
# costumam ser empacotados com PBE antigo (RC2/3DES), que o OpenSSL 3 desativa
# por padrão. Sem isso, abrir o .pfx falha como se a senha estivesse errada.
RUN sed -i 's/^default = default_sect$/default = default_sect\nlegacy = legacy_sect/' /etc/ssl/openssl.cnf && \
    printf '\n[legacy_sect]\nactivate = 1\n' >> /etc/ssl/openssl.cnf && \
    grep -q '^legacy = legacy_sect$' /etc/ssl/openssl.cnf

COPY --from=build /app/publish .

EXPOSE 5000

# ASPNETCORE_ENVIRONMENT e demais configurações vêm do docker-compose / variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "CardGameStore.dll"]
