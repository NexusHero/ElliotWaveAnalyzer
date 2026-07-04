using ElliotWaveAnalyzer.Api.Application.Validation;
using FluentValidation.TestHelper;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="AnalysisRequestValidator"/>: the symbol allow-list (case-insensitive),
/// the interval allow-list, and the inclusive limit bounds. Pure logic — no I/O.
/// </summary>
[TestFixture]
public sealed class AnalysisRequestValidatorTests
{
    private readonly AnalysisRequestValidator _validator = new();

    [Test]
    public void ValidRequest_PassesAllRules()
    {
        var result = _validator.TestValidate(new AnalysisRequest("btc", "1d", 100));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCase("BTC")]
    [TestCase("eth")]
    [TestCase("Nasdaq")]
    [TestCase("AAPL")]
    [TestCase("msft")]
    public void AllowedSymbols_AnyCase_AreAccepted(string symbol)
    {
        var result = _validator.TestValidate(new AnalysisRequest(symbol, "1d", 100));
        result.ShouldNotHaveValidationErrorFor(x => x.Symbol);
    }

    [Test]
    public void EmptySymbol_IsRejected()
    {
        var result = _validator.TestValidate(new AnalysisRequest("", "1d", 100));
        result.ShouldHaveValidationErrorFor(x => x.Symbol);
    }

    [Test]
    public void UnsupportedSymbol_IsRejected()
    {
        var result = _validator.TestValidate(new AnalysisRequest("DOGE", "1d", 100));
        result.ShouldHaveValidationErrorFor(x => x.Symbol);
    }

    [TestCase("1h")]
    [TestCase("4h")]
    [TestCase("1d")]
    [TestCase("1w")]
    public void AllowedIntervals_AreAccepted(string interval)
    {
        var result = _validator.TestValidate(new AnalysisRequest("BTC", interval, 100));
        result.ShouldNotHaveValidationErrorFor(x => x.Interval);
    }

    [Test]
    public void EmptyInterval_IsRejected()
    {
        var result = _validator.TestValidate(new AnalysisRequest("BTC", "", 100));
        result.ShouldHaveValidationErrorFor(x => x.Interval);
    }

    [Test]
    public void UnsupportedInterval_IsRejected()
    {
        var result = _validator.TestValidate(new AnalysisRequest("BTC", "1m", 100));
        result.ShouldHaveValidationErrorFor(x => x.Interval);
    }

    [TestCase(10)]
    [TestCase(255)]
    [TestCase(500)]
    public void Limit_WithinBounds_IsAccepted(int limit)
    {
        var result = _validator.TestValidate(new AnalysisRequest("BTC", "1d", limit));
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [TestCase(9)]
    [TestCase(0)]
    [TestCase(501)]
    public void Limit_OutOfBounds_IsRejected(int limit)
    {
        var result = _validator.TestValidate(new AnalysisRequest("BTC", "1d", limit));
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }
}
