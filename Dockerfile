# Multi-stage build for .NET 9 API
# Production image: ~110MB (Alpine base + trimmed runtime)

ARG DOTNET_VERSION=9.0

# === Stage 1: Build ===
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

# Restore (cache layer — csproj'lar değişmedikçe yeniden indirme)
COPY ["src/SearchConsoleApp.Web/SearchConsoleApp.Web.csproj", "src/SearchConsoleApp.Web/"]
COPY ["src/SearchConsoleApp.Web.Framework/SearchConsoleApp.Web.Framework.csproj", "src/SearchConsoleApp.Web.Framework/"]
COPY ["src/SearchConsoleApp.Services/SearchConsoleApp.Services.csproj", "src/SearchConsoleApp.Services/"]
COPY ["src/SearchConsoleApp.Data/SearchConsoleApp.Data.csproj", "src/SearchConsoleApp.Data/"]
COPY ["src/SearchConsoleApp.Core/SearchConsoleApp.Core.csproj", "src/SearchConsoleApp.Core/"]
COPY ["global.json", "./"]
# Sadece Web projesini restore et (transitif proje referanslarını da çeker).
# Solution'ı restore etmek test projesinin csproj'unu da gerektirirdi.
RUN dotnet restore "src/SearchConsoleApp.Web/SearchConsoleApp.Web.csproj"

# Tüm kaynak kodu kopyala ve publish et
COPY . .
RUN dotnet publish "src/SearchConsoleApp.Web/SearchConsoleApp.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# === Stage 2: Runtime ===
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime
WORKDIR /app

# SQL Server client requires ICU on Alpine (invariant mode is not supported)
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# .NET 8+ alpine imajları zaten non-root "app" kullanıcısı içerir;
# yeniden oluşturmaya çalışmak "group in use" hatası verir.
COPY --from=build --chown=app:app /app/publish .

# App_Data klasörü (GeoIP DB, vb. için mount edilebilir) — root iken oluştur,
# sahipliğini app'e ver, sonra non-root kullanıcıya geç.
RUN mkdir -p /app/App_Data /app/blobs && chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Health check — Docker /health endpoint'i ile container'ı izler
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "SearchConsoleApp.Web.dll"]
