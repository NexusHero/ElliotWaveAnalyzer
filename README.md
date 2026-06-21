# Elliott Wave Analyzer

Web application for technical analysis of financial markets (BTC, ETH, NASDAQ) based on Elliott Wave Theory.

## Overview

```
[CoinGecko / Yahoo Finance]
         ↓
  [ASP.NET Core Backend]
  Minimal API · Indicator calculation · Gemini integration · SQLite persistence
         ↓ JSON (REST)              ↓ PNG (SkiaSharp)
  [React Frontend]              [Telegram / Email]
  TradingView Lightweight Charts
  Interactive Elliott Wave annotation
```

## Monorepo structure

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

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- (Optional) Docker for containerized deployment

## Local dev workflow

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test                          # all tests
dotnet run --project src/ElliotWaveAnalyzer.Api
# API runs on https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
```

### Frontend

```bash
cd frontend
npm install
npm run dev                          # Vite dev server on http://localhost:5173
npm run test                         # Vitest
npm run build                        # Production build
```

### OpenAPI codegen (generate TypeScript types from the backend)

```bash
cd frontend
npm run generate:api                 # openapi-typescript → src/api/generated.ts
```

## Architecture decisions

Architecture decisions are documented as ADRs under `docs/adr/`.

## Tech stack

| Layer        | Technology                              |
|-------------|-----------------------------------------|
| Backend API | ASP.NET Core Minimal API (.NET 9)       |
| Indicators  | Skender.Stock.Indicators                |
| Charts (srv)| SkiaSharp                               |
| LLM         | Google Gemini 2.5 Flash (configurable)  |
| Persistence | SQLite (→ Postgres migration possible)  |
| Logging     | Serilog (structured JSON)               |
| Frontend    | React 18 + TypeScript + Vite            |
| Charts (UI) | TradingView Lightweight Charts          |
| Tests BE    | NUnit + NSubstitute                     |
| Tests FE    | Vitest + React Testing Library          |

## Deployment

Self-contained single-file as the target; containerization for a Home Assistant add-on is planned.
