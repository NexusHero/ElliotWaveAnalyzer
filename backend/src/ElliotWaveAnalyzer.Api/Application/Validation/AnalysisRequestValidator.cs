using FluentValidation;

namespace ElliotWaveAnalyzer.Api.Application.Validation;

public sealed class AnalysisRequestValidator : AbstractValidator<AnalysisRequest>
{
    private static readonly string[] AllowedSymbols =
        ["BTC", "ETH", "NASDAQ", "AAPL", "MSFT"];

    private static readonly string[] AllowedIntervals =
        ["1h", "4h", "1d", "1w"];

    public AnalysisRequestValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .Must(s => AllowedSymbols.Contains(s.ToUpperInvariant()))
            .WithMessage("Symbol not supported. Allowed: " + string.Join(", ", AllowedSymbols));

        RuleFor(x => x.Interval)
            .NotEmpty()
            .Must(i => AllowedIntervals.Contains(i))
            .WithMessage("Invalid interval. Allowed: " + string.Join(", ", AllowedIntervals));

        RuleFor(x => x.Limit)
            .InclusiveBetween(10, 500)
            .WithMessage("Limit must be between 10 and 500.");
    }
}
