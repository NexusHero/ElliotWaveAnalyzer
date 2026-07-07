using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// Full-lifecycle acceptance tests on real PostgreSQL for the scenario tree (Phase 4): the
/// auto-switch when the primary's invalidation breaks, zone-entry alert idempotency, and the GET
/// shape (per-scenario zones + probability/insufficient-data marker). Only the market-data and
/// delivery boundaries are faked.
/// </summary>
[TestFixture]
public sealed class ScenarioTreeAcceptanceTests
{
    private sealed class CapturingChannel : IReportDeliveryChannel
    {
        public List<ReportArtifact> Sent { get; } = [];
        public string Name => "Capture";
        public bool IsEnabled => true;

        public Task SendAsync(ReportArtifact report, CancellationToken cancellationToken = default)
        {
            Sent.Add(report);
            return Task.CompletedTask;
        }
    }

    private AcceptanceWebApplicationFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        TestDocker.SkipIfUnavailable();
        _factory = new AcceptanceWebApplicationFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task AutoSwitch_PrimaryInvalidated_PromotesAlternateAndKeepsHistory()
    {
        var candles = MarketDataFixtures.CreateCandles(90);
        var deepestLow = candles.Min(c => c.Low);
        var highestHigh = candles.Max(c => c.High);
        var snapshotId = Guid.NewGuid();
        var capture = new CapturingChannel();

        using var host = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IReportDeliveryChannel>(capture)));

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // AnalysisSnapshot.UserId is a real foreign key to AspNetUsers (#168).
            var userId = Guid.NewGuid();
            db.Users.Add(new AppUser { Id = userId, UserName = $"scenario-{userId:N}@example.com", Email = $"scenario-{userId:N}@example.com" });

            db.AnalysisSnapshots.Add(new AnalysisSnapshot
            {
                Id = snapshotId,
                UserId = userId,
                Symbol = "BTC",
                CreatedAt = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero),
                Structure = "Impulse",
                Bullish = true,
                InvalidationPrice = deepestLow + 1m,   // certain to be crossed → invalidated
                InvalidationAbove = false,
                Confidence = "high",
                Score = 0.5m,
                AlertedOutcome = AnalysisOutcome.Pending,
                Scenarios =
                [
                    new AnalysisScenarioRow
                    {
                        Id = Guid.NewGuid(), AnalysisSnapshotId = snapshotId, Role = ScenarioRole.Primary,
                        OrderIndex = 0, Label = "Primary", Structure = "Impulse", Bullish = true,
                        InvalidationPrice = deepestLow + 1m, InvalidationAbove = false, Confidence = "high", Score = 0.5m,
                    },
                    new AnalysisScenarioRow
                    {
                        Id = Guid.NewGuid(), AnalysisSnapshotId = snapshotId, Role = ScenarioRole.Alternate,
                        OrderIndex = 1, Label = "Alt 1", Structure = "Diagonal", Bullish = false,
                        InvalidationPrice = highestHigh + 1m, InvalidationAbove = true, Confidence = "medium", Score = 0.7m,
                    },
                ],
            });
            await db.SaveChangesAsync();
        }

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAlertService>().RunAsync();
        }

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var snap = await db.AnalysisSnapshots
                .Include(s => s.Scenarios)
                .Include(s => s.SwitchEvents)
                .FirstAsync(s => s.Id == snapshotId);

            var activePrimary = snap.Scenarios.Single(r => r.Role == ScenarioRole.Primary && !r.Retired);
            var oldPrimary = snap.Scenarios.Single(r => r.Label == "Primary");

            Assert.Multiple(() =>
            {
                // (1) the invalidation alert fired
                Assert.That(capture.Sent.Any(a => a.Caption.Contains("invalidated")), Is.True);
                // (2) the alternate is now the primary (flat fields synced too)
                Assert.That(activePrimary.Label, Is.EqualTo("Alt 1"));
                Assert.That(snap.Structure, Is.EqualTo("Diagonal"));
                Assert.That(snap.AlertedOutcome, Is.EqualTo(AnalysisOutcome.Pending)); // re-opened under the new primary
                // (3) a switch event with timestamp + from/to
                Assert.That(snap.SwitchEvents, Has.Count.EqualTo(1));
                Assert.That(snap.SwitchEvents[0].FromLabel, Is.EqualTo("Primary"));
                Assert.That(snap.SwitchEvents[0].ToLabel, Is.EqualTo("Alt 1"));
                // (4) the old primary is retained in history
                Assert.That(oldPrimary.Retired, Is.True);
            });
        }
    }

    [Test]
    public async Task ZoneEntry_PriceEntersZone_AlertsExactlyOnceAcrossTwoRuns()
    {
        var candles = MarketDataFixtures.CreateCandles(90);
        var deepestLow = candles.Min(c => c.Low);
        var snapshotId = Guid.NewGuid();
        var capture = new CapturingChannel();

        using var host = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IReportDeliveryChannel>(capture)));

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // AnalysisSnapshot.UserId is a real foreign key to AspNetUsers (#168).
            var userId = Guid.NewGuid();
            db.Users.Add(new AppUser { Id = userId, UserName = $"scenario-{userId:N}@example.com", Email = $"scenario-{userId:N}@example.com" });

            db.AnalysisSnapshots.Add(new AnalysisSnapshot
            {
                Id = snapshotId,
                UserId = userId,
                Symbol = "BTC",
                CreatedAt = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero),
                Structure = "Impulse",
                Bullish = true,
                InvalidationPrice = deepestLow - 10_000m, // far below → never invalidated
                InvalidationAbove = false,
                EntryLow = deepestLow,
                EntryHigh = deepestLow + 500m, // price certainly trades into this band
                Confidence = "high",
                AlertedOutcome = AnalysisOutcome.Pending,
            });
            await db.SaveChangesAsync();
        }

        using (var scope = host.Services.CreateScope())
        {
            var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
            await alerts.RunAsync();
            await alerts.RunAsync();
        }

        Assert.That(
            capture.Sent.Count(a => a.Caption.Contains("entry zone")),
            Is.EqualTo(1),
            "the entry-zone alert must fire exactly once, not on every pass");
    }

    [Test]
    public async Task SaveWithTree_ThenGet_ReturnsScenariosWithProbabilityMarker()
    {
        using var client = _factory.CreateClient();
        await _factory.AuthenticateAsync(client);

        var request = new
        {
            symbol = "BTC",
            structure = "Impulse",
            bullish = true,
            invalidationPrice = 30_000m,
            invalidationAbove = false,
            targetLow = 60_000m,
            targetHigh = 65_000m,
            confidence = "high",
            score = 0.8m,
            entryLow = 34_000m,
            entryHigh = 36_000m,
            alternates = new[]
            {
                new
                {
                    structure = "Diagonal",
                    bullish = false,
                    invalidationPrice = 70_000m,
                    invalidationAbove = true,
                    entryLow = (decimal?)null,
                    entryHigh = (decimal?)null,
                    targetLow = 20_000m,
                    targetHigh = 18_000m,
                    confidence = "low",
                    score = 0.4m,
                },
            },
        };

        var save = await client.PostAsJsonAsync("/api/analyses", request);
        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var list = await client.GetAsync("/api/analyses");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var analysis = doc.RootElement.EnumerateArray()
            .First(a => a.GetProperty("symbol").GetString() == "BTC"
                && a.GetProperty("score").GetDecimal() == 0.8m);
        var scenarios = analysis.GetProperty("scenarios");

        Assert.Multiple(() =>
        {
            Assert.That(scenarios.GetArrayLength(), Is.EqualTo(2));
            Assert.That(scenarios[0].GetProperty("role").GetString(), Is.EqualTo("Primary"));
            Assert.That(scenarios[0].GetProperty("label").GetString(), Is.EqualTo("Primary"));
            Assert.That(scenarios[0].GetProperty("entryLow").GetDecimal(), Is.EqualTo(34_000m));
            Assert.That(scenarios[1].GetProperty("role").GetString(), Is.EqualTo("Alternate"));
            // Too few concluded analyses → probability withheld (null), marked explicitly.
            Assert.That(scenarios[0].GetProperty("probabilityBasis").GetString(), Is.EqualTo("InsufficientData"));
            var hasProbability = scenarios[0].TryGetProperty("probability", out var prob);
            Assert.That(!hasProbability || prob.ValueKind == JsonValueKind.Null, Is.True);
        });
    }
}
