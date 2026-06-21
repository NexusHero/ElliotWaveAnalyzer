# Elliott Wave Analyzer — Backend

ASP.NET Core Minimal API (.NET 10) für Marktdaten, technische Indikatoren und Elliott-Wellen-Analyse.

## Projektstruktur

```
backend/
├── ElliotWaveAnalyzer.sln
├── src/ElliotWaveAnalyzer.Api/
│   ├── Domain/              # Reine Domänenmodelle (keine Abhängigkeiten nach außen)
│   │   ├── MarketCandle.cs
│   │   ├── MacdResult.cs
│   │   ├── RsiResult.cs
│   │   └── TechnicalAnalysisResult.cs
│   ├── Interfaces/          # Abstraktionen (DI-Contracts)
│   │   ├── IMarketDataProvider.cs
│   │   ├── IIndicatorCalculator.cs
│   │   └── ITechnicalAnalysisService.cs
│   ├── Infrastructure/      # Externe Abhängigkeiten (CoinGecko, Skender)
│   │   ├── CoinGeckoMarketDataProvider.cs
│   │   └── SkenderIndicatorCalculator.cs
│   ├── Application/         # Orchestrierung (kein I/O, nur Koordination)
│   │   └── TechnicalAnalysisService.cs
│   ├── Endpoints/           # Minimal API Endpunkte
│   │   └── MarketDataEndpoints.cs
│   ├── Program.cs
│   └── appsettings.json
└── tests/ElliotWaveAnalyzer.Tests/
    ├── TestData/
    │   └── MarketDataFixtures.cs   # Deterministische Testdaten (seed=42)
    ├── Infrastructure/
    │   └── SkenderIndicatorCalculatorTests.cs
    └── Application/
        └── TechnicalAnalysisServiceTests.cs
```

## Setup & lokaler Dev-Workflow

```bash
cd backend
dotnet restore
dotnet build
dotnet test --logger "console;verbosity=detailed"

# API starten (Scalar API UI: https://localhost:5001/scalar/v1 · OpenAPI JSON: https://localhost:5001/openapi/v1.json)
dotnet run --project src/ElliotWaveAnalyzer.Api
```

## API-Endpunkte

| Methode | Pfad                        | Beschreibung                        |
|--------|-----------------------------|-------------------------------------|
| GET    | `/api/market-data/{symbol}` | OHLCV + MACD + RSI für BTC oder ETH |
|        | `?days=90`                  | Zeitraum in Tagen (default: 90)     |

Scalar UI zeigt alle Endpunkte mit Beispiel-Responses (ersetzt Swagger in .NET 10).

## SOLID-Architektur

| Prinzip | Umsetzung                                                                    |
|---------|------------------------------------------------------------------------------|
| **S**   | `CoinGeckoMarketDataProvider` holt nur Daten. `SkenderIndicatorCalculator` rechnet nur. `TechnicalAnalysisService` koordiniert nur. |
| **O**   | Neuer Datenanbieter (Yahoo Finance) = neue Klasse + eine DI-Zeile in `Program.cs`. Kein bestehender Code ändert sich. |
| **L**   | Alle `IMarketDataProvider`-Implementierungen sind austauschbar; `TechnicalAnalysisService` arbeitet nur gegen das Interface. |
| **I**   | Drei schmale Interfaces statt einem God-Interface. |
| **D**   | Alle Services hängen an Interfaces. Skender ist hinter `IIndicatorCalculator` versteckt. |

## TDD-Workflow

Tests zuerst schreiben (Red) → Implementierung (Green) → Refaktorierung (Refactor).

```bash
dotnet test                        # alle Tests
dotnet test --filter "Category=RSI"   # nur RSI-Tests
dotnet watch test                  # Watch-Modus während der Entwicklung
```

## Neue Datenquelle hinzufügen (Beispiel: Yahoo Finance für NASDAQ)

1. Neue Klasse `YahooFinanceMarketDataProvider : IMarketDataProvider` anlegen
2. `Supports("NASDAQ")` → `true`
3. In `Program.cs`: `builder.Services.AddTransient<IMarketDataProvider, YahooFinanceMarketDataProvider>()`
4. Fertig — kein bestehender Code ändert sich.

## Konfiguration

Alle externen URLs und API-Keys in `appsettings.json` (nie im Code hardcoden):

```json
{
  "MarketData": {
    "CoinGecko": {
      "BaseUrl": "https://api.coingecko.com/api/v3/",
      "ApiKey": ""
    }
  }
}
```
