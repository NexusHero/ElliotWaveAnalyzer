using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end test for the price-alert pass on real PostgreSQL: a seeded, still-pending analysis
/// whose invalidation the (deterministic) fixture candles cross is picked up, an alert is
/// delivered through an enabled fake channel, the snapshot's alerted-outcome is advanced, and a
/// second pass sends nothing. Only the market-data boundary is the deterministic fixture.
/// </summary>
[TestFixture]
public sealed class AlertServiceAcceptanceTests
{
    /// <summary>Enabled delivery channel that records the artifacts it is asked to send.</summary>
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
    public async Task Run_PendingAnalysisThatInvalidated_DeliversOnceAndAdvancesOutcome()
    {
        // The service fetches 90 fixture candles for BTC; pick an invalidation their deepest low
        // is guaranteed to cross (bullish count, invalidation below), so the outcome is Invalidated.
        var candles = MarketDataFixtures.CreateCandles(90);
        var deepestLow = candles.Min(c => c.Low);

        var snapshotId = Guid.NewGuid();
        var capture = new CapturingChannel();
        using var host = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IReportDeliveryChannel>(capture)));

        // Seed a still-pending analysis created before the fixture window so every candle counts.
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // AnalysisSnapshot.UserId is a real foreign key to AspNetUsers (#168) — a row for it
            // must exist first.
            var userId = Guid.NewGuid();
            db.Users.Add(new AppUser { Id = userId, UserName = $"alert-{userId:N}@example.com", Email = $"alert-{userId:N}@example.com" });

            db.AnalysisSnapshots.Add(new AnalysisSnapshot
            {
                Id = snapshotId,
                UserId = userId,
                Symbol = "BTC",
                CreatedAt = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero),
                Structure = "Impulse",
                Bullish = true,
                InvalidationPrice = deepestLow + 1m, // certain to be crossed
                InvalidationAbove = false,
                Confidence = "high",
                AlertedOutcome = AnalysisOutcome.Pending,
            });
            await db.SaveChangesAsync();
        }

        int firstPass, secondPass;
        AnalysisOutcome storedOutcome;
        using (var scope = host.Services.CreateScope())
        {
            var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
            firstPass = await alerts.RunAsync();
            secondPass = await alerts.RunAsync();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            storedOutcome = (await db.AnalysisSnapshots.FindAsync(snapshotId))!.AlertedOutcome;
        }

        Assert.Multiple(() =>
        {
            Assert.That(firstPass, Is.EqualTo(1), "the invalidated analysis should alert once");
            Assert.That(secondPass, Is.EqualTo(0), "a settled analysis must not alert again");
            Assert.That(capture.Sent, Has.Count.EqualTo(1));
            Assert.That(capture.Sent[0].Symbol, Is.EqualTo("BTC"));
            Assert.That(capture.Sent[0].Caption, Does.Contain("invalidated"));
            Assert.That(storedOutcome, Is.EqualTo(AnalysisOutcome.Invalidated));
        });
    }
}
