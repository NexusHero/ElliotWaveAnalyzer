# Elliott Wave Analyzer

Webanwendung zur technischen Analyse von Finanzmärkten (BTC, ETH, NASDAQ) nach der Elliott-Wellen-Theorie.

## Überblick

```
[CoinGecko / Yahoo Finance]
         ↓
  [ASP.NET Core Backend]
  Minimal API · Indikator-Berechnung · Gemini-Integration · SQLite-Persistenz
         ↓ JSON (REST)              ↓ PNG (SkiaSharp)
  [React Frontend]              [Telegram / E-Mail]
  TradingView Lightweight Charts
  Interaktive Elliott-Wellen-Annotation
```

## Monorepo-Struktur

```
ElliotWaveAnalyzer/
├── backend/
│   ├── src/
│   │   └── ElliotWaveAnalyzer.Api/   # ASP.NET Core Minimal API (.NET 9)
│   ├── tests/
│   │   └── ElliotWaveAnalyzer.Tests/ # NUnit + NSubstitute
│   └── ElliotWaveAnalyzer.sln
├── frontend/                          # React 18 + TypeScript + Vite
└── README.md
```

## Voraussetzungen

- .NET 10 SDK
- Node.js 20+
- (Optional) Docker für containerisiertes Deployment

## Lokaler Dev-Workflow

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test                          # alle Tests
dotnet run --project src/ElliotWaveAnalyzer.Api
# API läuft auf https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
```

### Frontend

```bash
cd frontend
npm install
npm run dev                          # Vite Dev Server auf http://localhost:5173
npm run test                         # Vitest
npm run build                        # Produktions-Build
```

### OpenAPI-Codegen (TypeScript-Typen aus Backend generieren)

```bash
cd frontend
npm run generate:api                 # openapi-typescript → src/api/generated.ts
```

## Architektur-Entscheidungen

Architekturentscheidungen werden als ADRs unter `docs/adr/` dokumentiert.

## Tech-Stack

| Schicht      | Technologie                             |
|-------------|----------------------------------------|
| Backend API | ASP.NET Core Minimal API (.NET 9)       |
| Indikatoren | Skender.Stock.Indicators                |
| Charts (srv)| SkiaSharp                               |
| LLM         | Google Gemini 2.5 Flash (konfigurierbar)|
| Persistenz  | SQLite (→ Postgres-Migration möglich)   |
| Logging     | Serilog (strukturiertes JSON)           |
| Frontend    | React 18 + TypeScript + Vite            |
| Charts (UI) | TradingView Lightweight Charts          |
| Tests BE    | NUnit + NSubstitute                     |
| Tests FE    | Vitest + React Testing Library          |

## Deployment

Self-Contained Single-File als Zielbild; Containerisierung für Home-Assistant-Add-on geplant.
