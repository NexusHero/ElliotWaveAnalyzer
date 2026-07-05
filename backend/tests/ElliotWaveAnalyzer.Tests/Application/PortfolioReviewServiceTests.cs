using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="PortfolioReviewService"/> against stubbed collaborators: it produces one brief per
/// resolvable position, surfaces an unresolvable ISIN with a reason instead of throwing, and caches
/// per (ISIN, day) so a second review the same day does not re-run the analyzer or the narrator.
/// </summary>
[TestFixture]
public sealed class PortfolioReviewServiceTests
{
    private static readonly Guid User = Guid.NewGuid();

    private IDepotStore _depot = null!;
    private ISymbolResolver _resolver = null!;
    private ITopDownAnalysisService _topDown = null!;
    private ITechnicalAnalysisService _technical = null!;
    private IPositionNarrator _narrator = null!;
    private MemoryCache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _depot = Substitute.For<IDepotStore>();
        _resolver = Substitute.For<ISymbolResolver>();
        _topDown = Substitute.For<ITopDownAnalysisService>();
        _technical = Substitute.For<ITechnicalAnalysisService>();
        _narrator = Substitute.For<IPositionNarrator>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        // A resolvable ISIN resolves to a symbol; the analyzer returns a minimal chain; the narrator
        // returns a note; current price is unavailable (kept out of scope here).
        _resolver.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Resolve((string)ci[0]));
        _topDown.AnalyzeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(new TopDownAnalysis([new TimeframeCount("1D", WaveDegree.Primary, null, null, false)], [], "1D"));
        _technical.GetAnalysisAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("no data"));
        _narrator.NarrateAsync(Arg.Any<PositionBrief>(), Arg.Any<CancellationToken>())
            .Returns(PositionNarration.Of("A constructive count."));
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    private static Task<IReadOnlyList<ResolvedSymbol>> Resolve(string isin) => Task.FromResult<IReadOnlyList<ResolvedSymbol>>(
        isin.StartsWith("XX", StringComparison.Ordinal)
            ? []
            : [new ResolvedSymbol($"SYM_{isin}", $"Name {isin}", "EQUITY", "XETRA")]);

    private PortfolioReviewService Sut() => new(
        _depot, _resolver, _topDown, _technical, _narrator, _cache, TimeProvider.System,
        NullLogger<PortfolioReviewService>.Instance);

    private void GivenDepot(params string[] isins)
    {
        var positions = isins
            .Select(i => new DepotPosition(i, null, $"Name {i}", 1m, null, null, null, null, null, null, null))
            .ToList();
        _depot.GetLatestAsync(User, Arg.Any<CancellationToken>())
            .Returns(new DepotSnapshot(BrokerSource.SmartbrokerPlus, DateTimeOffset.UnixEpoch, null, "EUR", positions, null));
    }

    [Test]
    public async Task ReviewAsync_ThreeResolvablePositions_ProducesThreeBriefs()
    {
        GivenDepot("DE0000000001", "DE0000000002", "DE0000000003");

        var review = await Sut().ReviewAsync(User);

        Assert.Multiple(() =>
        {
            Assert.That(review.Briefs, Has.Count.EqualTo(3));
            Assert.That(review.Unresolved, Is.Empty);
            Assert.That(review.Summary.Reviewed, Is.EqualTo(3));
            Assert.That(review.Briefs[0].Narrative, Is.EqualTo("A constructive count."));
        });
    }

    [Test]
    public async Task ReviewAsync_UnresolvableIsin_AppearsInUnresolvedWithReason_NeverThrows()
    {
        GivenDepot("DE0000000001", "XX9999999999");

        var review = await Sut().ReviewAsync(User);

        Assert.Multiple(() =>
        {
            Assert.That(review.Briefs, Has.Count.EqualTo(1));
            Assert.That(review.Unresolved, Has.Count.EqualTo(1));
            Assert.That(review.Unresolved[0].Isin, Is.EqualTo("XX9999999999"));
            Assert.That(review.Unresolved[0].Reason, Does.Contain("resolve"));
            Assert.That(review.Summary.Unresolved, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ReviewAsync_NoDepot_ReturnsEmptyReview()
    {
        _depot.GetLatestAsync(User, Arg.Any<CancellationToken>()).Returns((DepotSnapshot?)null);

        var review = await Sut().ReviewAsync(User);

        Assert.Multiple(() =>
        {
            Assert.That(review.Briefs, Is.Empty);
            Assert.That(review.Summary.Positions, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ReviewAsync_CalledTwiceSameDay_DoesNotReRunAnalyzerOrNarrator()
    {
        GivenDepot("DE0000000001");
        var sut = Sut();

        await sut.ReviewAsync(User);
        await sut.ReviewAsync(User);

        await _topDown.Received(1).AnalyzeAsync("SYM_DE0000000001", Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _narrator.Received(1).NarrateAsync(Arg.Any<PositionBrief>(), Arg.Any<CancellationToken>());
    }
}
