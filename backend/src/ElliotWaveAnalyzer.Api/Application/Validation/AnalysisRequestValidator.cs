using FluentValidation;

namespace ElliotWaveAnalyzer.Api.Application.Validation;

public sealed class AnalysisRequestValidator : AbstractValidator<AnalysisRequest>
{
    private static readonly string[] AllowedIntervals =
        ["1h", "4h", "1d", "1w"];

    public AnalysisRequestValidator()
    {
        // Symbols are no longer an allow-list — any instrument resolvable via ISymbolResolver is
        // valid (ADR-022). This is now the abuse guard only: a length cap + a ticker character
        // whitelist; existence is checked when the data is actually fetched.
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .Must(s => SymbolInput.IsValidSymbol(s))
            .WithMessage("Symbol must be a short ticker (letters, digits and . - ^ = / only).");

        RuleFor(x => x.Interval)
            .NotEmpty()
            .Must(i => AllowedIntervals.Contains(i))
            .WithMessage("Invalid interval. Allowed: " + string.Join(", ", AllowedIntervals));

        RuleFor(x => x.Limit)
            .InclusiveBetween(10, 500)
            .WithMessage("Limit must be between 10 and 500.");
    }
}
