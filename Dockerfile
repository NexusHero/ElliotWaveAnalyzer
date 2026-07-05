# syntax=docker/dockerfile:1

# ── Build stage ──────────────────────────────────────────────────────────────
# Restore + publish the .NET 10 API. Layer the restore before the source copy so
# a source-only change reuses the cached NuGet restore.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/ElliotWaveAnalyzer.sln ./backend/
COPY backend/src/ElliotWaveAnalyzer.Api/ElliotWaveAnalyzer.Api.csproj ./backend/src/ElliotWaveAnalyzer.Api/
RUN dotnet restore backend/src/ElliotWaveAnalyzer.Api/ElliotWaveAnalyzer.Api.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish backend/src/ElliotWaveAnalyzer.Api/ElliotWaveAnalyzer.Api.csproj \
        -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────────────────────
# ASP.NET Core runtime only. libfontconfig1 lets SkiaSharp render chart-PNG text
# (the native libSkiaSharp itself is bundled via SkiaSharp.NativeAssets.Linux.NoDependencies).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Kestrel listens on 8080; the connection string and secrets are provided at runtime
# (e.g. ConnectionStrings__Postgres, Gemini__ApiKey) — never baked into the image.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the image's non-root user (defined by the .NET base image).
USER $APP_UID

ENTRYPOINT ["dotnet", "ElliotWaveAnalyzer.Api.dll"]
