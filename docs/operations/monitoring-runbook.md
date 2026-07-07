# Monitoring & Alerting Runbook (#173)

This is the operator-facing companion to ADR-059 (`docs/architecture.md`). It covers what the
health/monitoring surface actually does, how to configure it, and how to prove the alert path
works before you rely on it in production.

## Endpoints

| Endpoint | Purpose | Checks | Suitable for |
|---|---|---|---|
| `GET /health/live` | Process is up | none | Orchestrator liveness probe (restart-on-fail) |
| `GET /health/ready` | Process can actually serve requests | database (reachable + fully migrated), market-data provider (a real probe request) | Load balancer / orchestrator readiness gate (traffic routing) |

`/health/ready` returns HTTP 200 with `{"status":"Healthy", "checks":[...]}` when every check
passes, and HTTP 503 with the same shape (each failing check's `description` explains why) the
moment any dependency is down — including during startup before migrations have finished, so an
instance never receives traffic before it's actually ready.

## Traces & metrics

Already wired (ADR — see `TelemetryExtensions.cs`): traces and metrics export via OTLP to
`OpenTelemetry:Endpoint` (config key / `OpenTelemetry__Endpoint` env var), empty by default (no
export until an endpoint is configured). Point it at your collector (Grafana Tempo/Mimir, Honeycomb,
Datadog's OTLP intake, etc.) — no code change needed.

## Sustained-downtime alerting

`HealthMonitor` (opt-in, `HealthMonitor:Enabled=true`) polls the same readiness checks on a cron
schedule (default: every minute) and fires exactly one alert per *sustained* failure episode — once
`HealthMonitor:ConsecutiveFailureThreshold` (default 3) consecutive polls have failed. A single
transient blip never pages anyone; recovering resets the streak so a later recurrence pages again.

By default the alert lands as a `Critical`-level structured log line
(`"OPERATOR ALERT: {Message}"`) via the built-in `LoggingAlertSink` — always active, so an alert
genuinely fires with zero external configuration. **Wiring a real paging/chat backend (PagerDuty,
Opsgenie, Slack) is a named follow-on**: implement `IAlertSink` and register it in place of
`LoggingAlertSink` in `HealthCheckExtensions.AddAppHealthChecks` — the detection logic (`SustainedFailureDetector`,
`HealthMonitorBackgroundService`) does not change.

### Configuration

```jsonc
// appsettings.json (or an environment override)
"HealthMonitor": {
  "Enabled": true,
  "Cron": "* * * * *",          // every minute
  "ConsecutiveFailureThreshold": 3
}
```

## Reproducing the alert trigger (AC3)

To prove the whole path — readiness check → sustained-failure detection → alert — end to end
against a real deployment:

1. Set `HealthMonitor:Enabled=true` and note your log aggregator/console output.
2. Take the database down (or block network access to it) so `/health/ready` starts returning 503.
3. Wait `ConsecutiveFailureThreshold × Cron interval` (≈3 minutes with the defaults) — watch
   `GET /health/ready` flip to 503 immediately, and a `Critical` log line
   (`"OPERATOR ALERT: Readiness has been failing for database for 3 consecutive checks."`) appear
   once the streak crosses the threshold, not on the first failed poll.
4. Restore the database. The next successful poll resets the streak. Repeat step 2 — a fresh alert
   fires again, proving recovery doesn't leave the detector permanently silenced.

The same detection logic is also unit-tested directly (`SustainedFailureDetectorTests`) without
needing a live database or waiting on real cron timing — the tests exercise the exact
threshold/reset semantics described above deterministically.

## Known follow-ons (not silently assumed done)

- No real paging backend is wired — only the structured-log sink. An operator relying on this in
  production should either configure log-based alerting on the `Critical` level / "OPERATOR ALERT"
  marker in their aggregator, or implement a real `IAlertSink`.
- The market-data readiness check makes a real upstream request (through the existing caching
  decorator, so a healthy upstream costs at most one live call per cache TTL) — if that upstream
  rate-limits aggressively, consider raising the cache TTL rather than disabling the check.
