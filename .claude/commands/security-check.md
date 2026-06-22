---
description: Security review against project security standards
---

Führe einen Security-Review des geänderten oder angegebenen Codes durch.
Prüfe dabei folgende Punkte aus dem Projektstandard:

## Checkliste

### Authentication & Authorization
- [ ] Alle Business-Endpoints haben `.RequireAuthorization()`
- [ ] Auth-Endpoints (`/api/auth/*`) sind explizit öffentlich (`.AllowAnonymous()`) oder erfordern keine Authorization
- [ ] Cookie ist HttpOnly, Secure, SameSite=Lax
- [ ] Google OAuth ClientId/ClientSecret kommen aus Configuration, nicht aus dem Quellcode

### Rate Limiting
- [ ] Jeder Endpoint hat `.RequireRateLimiting(...)` mit passendem Policy-Namen
- [ ] Gemini/LLM-Endpoints verwenden die engere `"gemini-analysis"`-Policy (5 req/min)
- [ ] Read-Endpoints verwenden `"ip-global"` (30 req/min)
- [ ] Login-Endpoint verwendet `"login"`-Policy (5 req/min, Brute-Force-Schutz)

### Input Validation
- [ ] Jeder POST/PUT-Endpoint hat einen FluentValidation-Validator
- [ ] Symbol-Inputs gegen Allowlist validiert (kein freier String an externe APIs)
- [ ] Numerische Ranges explizit begrenzt (z.B. Limit: 10–500, days: 1–365)

### CORS
- [ ] Kein `AllowAnyOrigin()` verwendet
- [ ] `WithOrigins(...)` enthält nur bekannte Frontend-Origins
- [ ] `AllowCredentials()` ist gesetzt (benötigt für Auth-Cookies)

### Secrets
- [ ] Kein API-Key, Secret oder Connection String im Quellcode
- [ ] Neue Config-Werte in `appsettings.json` als leere Strings, Wert via User Secrets / Env Vars
- [ ] `appsettings.Development.json` steht in `.gitignore`

### Security Headers
- [ ] `UseSecurityHeaders()` ist in der Pipeline vor den Endpoints
- [ ] CSP erlaubt nur bekannte externe Domains (CoinGecko, Yahoo Finance)
- [ ] HSTS ist in nicht-Development-Umgebungen aktiv

### Middleware-Reihenfolge
- [ ] Reihenfolge: SecurityHeaders → CORS → HTTPS → HSTS → Auth → AuthZ → RateLimiter → Endpoints

### Logging
- [ ] Keine personenbezogenen Daten (E-Mail, Name) in Log-Einträgen
- [ ] Security-Events (Auth-Fehler, Rate-Limit-Hits) werden mit `Log.Warning` geloggt

## Ausgabe

Für jeden gefundenen Verstoß:
1. Datei + Zeilennummer
2. Beschreibung des Problems
3. Konkreter Fix-Vorschlag als Codeblock

Wenn alles in Ordnung ist: kurze Bestätigung mit grüner Zusammenfassung.
