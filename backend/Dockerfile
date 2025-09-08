# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

# 🔧 utilidades para healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# 🔓 CORS por defecto (se puede sobreescribir en compose)
ENV ALLOWED_ORIGINS="http://localhost:8081"
ENV ASPNETCORE_URLS="http://0.0.0.0:8080"

EXPOSE 8080

# Healthcheck del API
HEALTHCHECK --interval=10s --timeout=3s --retries=10 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]
