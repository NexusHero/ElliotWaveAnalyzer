# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 1.x     | :white_check_mark: |

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Please report security vulnerabilities by emailing:
**suhay.sevinc@gmail.com**

Include in your report:
- A description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (optional)

You will receive an acknowledgement within 48 hours and a status update within 7 days.

## Security Measures

| Measure | Implementation |
|---------|----------------|
| API key storage | Read from `appsettings.json` or environment variables; never hardcoded in source |
| Dependency scanning | `dotnet list package --vulnerable` and `npm audit` run in CI on every push and weekly |
| Static analysis | CodeQL scans C# and TypeScript/JavaScript on every PR |
| License compliance | No GPL/AGPL dependencies allowed (enforced in CI) |

## API Keys

- **CoinGecko API Key**: Set via `appsettings.json → MarketData:CoinGecko:ApiKey` or env var `MarketData__CoinGecko__ApiKey`
- **Gemini API Key**: Set via `appsettings.json → Gemini:ApiKey` or env var `Gemini__ApiKey`

Never commit `appsettings.json` files containing real API keys. Use `appsettings.Development.json` (in `.gitignore`) for local secrets or use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).
