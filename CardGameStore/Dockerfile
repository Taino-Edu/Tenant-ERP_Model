# =============================================================================
# Dockerfile — Build multi-stage para produção e desenvolvimento
# Stage 1: build | Stage 2: runtime mínimo
# =============================================================================

# ---- STAGE 1: BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas o .csproj primeiro (cache de camadas — só rebuilda se mudar dependências)
COPY CardGameStore.csproj .
RUN dotnet restore

# Copia o restante do código e publica
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- STAGE 2: RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia o binário publicado do stage de build
COPY --from=build /app/publish .

# Porta exposta (HTTP e HTTPS)
EXPOSE 5000
EXPOSE 5001

# Variável para o ASP.NET Core escutar em todas as interfaces
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development

# Ponto de entrada
ENTRYPOINT ["dotnet", "CardGameStore.dll"]
