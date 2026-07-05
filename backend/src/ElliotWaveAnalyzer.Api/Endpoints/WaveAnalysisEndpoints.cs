using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for Elliott Wave validation and token usage reporting.
/// </summary>
public static class WaveAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapWaveAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api")
            .WithTags("Wave Analysis")
            .RequireAuthorization();

        // ── POST /api/wave-analysis ───────────────────────────────────────────
        // Uses the strict "gemini-analysis" policy: LLM calls are expensive and
        // must be tightly throttled to control upstream cost and prevent abuse.
        group.MapPost("/wave-analysis", ValidateWaveCount)
            .WithName("ValidateWaveCount")
            .WithSummary("Validate an Elliott Wave annotation set via the active LLM provider")
            .WithDescription("""
                Submit user-placed wave annotations (date + price + label).
                The active LLM provider (Gemini / Claude / OpenAI) validates the count
                against the canonical Elliott Wave rules and returns structured feedback.
                The response includes token usage for this call.
                Configure the active provider via LlmProvider:Active in appsettings.json.
                """)
            .Produces<WaveAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("gemini-analysis");

        // ── POST /api/wave-analysis/verify ────────────────────────────────────
        // Analyst-in-the-loop re-verification: takes an EDITED annotation set and returns the full
        // DETERMINISTIC read (snapped pivots, hard rules, projections, score). No LLM — geometry never
        // depends on a model — so it uses the cheaper per-user throttle and can run on every edit.
        group.MapPost("/wave-analysis/verify", VerifyEditedCount)
            .WithName("VerifyEditedWaveCount")
            .WithSummary("Deterministically re-verify an analyst-edited wave count (no LLM)")
            .WithDescription("""
                Submit an edited annotation set (date + price + label) for a symbol. Each pivot is
                snapped to a real candle extreme (so a dragged pivot lands on real data; ones that
                don't snap are reported, not trusted), then the hard Elliott rules, the forward
                projections (invalidation, support/target zones, confluence, channels) and a guideline
                score are computed in code and returned. Fully deterministic — no LLM, no token usage —
                so the analyst-in-the-loop sees the objective verdict of their own count live.
                """)
            .Produces<WaveVerification>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("per-user");

        // ── POST /api/wave-analysis/auto ──────────────────────────────────────
        // Full-auto ("magic button"): the system detects swing pivots, generates
        // rule-valid candidate counts, and the LLM ranks + explains them. Same strict
        // throttle as manual analysis since it makes an LLM call.
        group.MapPost("/wave-analysis/auto", AutoAnalyze)
            .WithName("AutoAnalyzeWaves")
            .WithSummary("Detect and rank Elliott Wave counts automatically")
            .WithDescription("""
                Full-auto market analysis. Provide a symbol (and optionally a lookback window
                and ZigZag sensitivity); the system detects swing pivots, builds rule-valid
                candidate wave counts, and the active LLM provider ranks them and reads the
                market structure. Returns the ranked counts (best first), a market summary,
                and token usage. Rankings is empty when no valid structure is found.
                """)
            .Produces<AutoWaveAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("gemini-analysis");

        // ── GET /api/wave-analysis/topdown ────────────────────────────────────
        // Deterministic multi-timeframe read: no LLM, so it uses the cheaper per-user
        // throttle rather than the strict LLM policy.
        group.MapGet("/wave-analysis/topdown", TopDownAnalyze)
            .WithName("TopDownWaveAnalysis")
            .WithSummary("Deterministic top-down, multi-timeframe Elliott Wave consistency")
            .WithDescription("""
                Analyzes a symbol across a weekly → daily → 4-hour ladder and returns a chain:
                the best count for each timeframe, each constrained to live inside the wave
                unfolding on the timeframe above it, plus a consistency verdict per link
                (Consistent / Tension / Contradiction). Finer counts that contradict the
                higher-timeframe direction are rejected; class or price-window mismatches are
                penalized. Fully deterministic — no LLM call, no token usage. Timeframes an
                instrument cannot serve (e.g. no intraday source for 4H) are omitted honestly.
                """)
            .Produces<TopDownAnalysis>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("ip-global");

        // ── GET /api/wave-analysis/analogs ────────────────────────────────────
        // Historical-analog retrieval: fingerprint the current count, find the nearest PAST setups on
        // the same symbol (no-lookahead), aggregate their measured resolution, and add a fact-guarded
        // summary. Makes at most one small LLM call for the prose → strict throttle.
        group.MapGet("/wave-analysis/analogs", HistoricalAnalogs)
            .WithName("HistoricalAnalogs")
            .WithSummary("Find historical analogs of the current count and how they resolved")
            .WithDescription("""
                Fingerprints the current deterministic count for a symbol (structure, direction, score,
                confluence, reward:risk, distance-to-invalidation, momentum) and retrieves the most
                similar PAST setups on the same instrument — restricted to ones that concluded before
                now, so nothing leaks the future. Returns their measured resolution (hit-rate, median
                days to resolution) and the ranked analogs, plus a short fact-guarded natural-language
                summary. All statistics are deterministic; the LLM only narrates them and cannot cite a
                figure the engine did not compute. Below a minimum sample the response is marked
                insufficient rather than showing an unreliable rate. Daily or weekly timeframes.
                """)
            .Produces<AnalogResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("gemini-analysis");

        // ── POST /api/wave-analysis/verify-image ──────────────────────────────
        // Vision import: a vision LLM extracts the claimed count from an uploaded chart, then the
        // deterministic pipeline verifies it against real data. Makes an LLM call → strict throttle.
        group.MapPost("/wave-analysis/verify-image", VerifyImage)
            .WithName("VerifyChartImage")
            .WithSummary("Verify a claimed Elliott Wave count from an uploaded chart screenshot")
            .WithDescription("""
                Multipart upload with a single 'file' field (PNG/JPEG) and optional 'symbol' and
                'timeframe' form fields (used if the image doesn't make them legible). A vision model
                extracts the claimed count; every claimed pivot is then snapped to a real candle
                extreme (±0.5% / ±1 bar) — pivots that don't snap are reported, not trusted — and the
                deterministic rules verify what survives, side-by-side with our own count. When too
                few pivots snap, the report says the image couldn't be reliably extracted rather than
                guessing. The image is parsed in-request and never stored.
                """)
            .DisableAntiforgery()
            .Produces<ImageVerificationReport>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting("gemini-analysis");

        // ── GET /api/tokens ───────────────────────────────────────────────────
        group.MapGet("/tokens", GetTokenUsage)
            .WithName("GetTokenUsage")
            .WithSummary("Session token usage report")
            .WithDescription("""
                Returns cumulative token consumption since the server started.
                Includes total tokens, per-provider breakdown, configured budget,
                remaining budget, and whether the budget has been exceeded.
                Budget is configured via LlmProvider:TokenBudget in appsettings.json (0 = unlimited).
                """)
            .Produces<TokenUsageReport>(StatusCodes.Status200OK)
            .RequireRateLimiting("ip-global");

        return app;
    }

    // Chart screenshots are small; cap the upload and restrict to images.
    private const long MaxImageBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/jpg", "image/webp" };

    private static async Task<IResult> VerifyImage(
        IFormFile? file,
        HttpRequest request,
        IImageVerificationService verification,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Results.Problem("Upload a non-empty image in the 'file' field.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxImageBytes)
        {
            return Results.Problem("Image is too large (max 8 MB).", statusCode: StatusCodes.Status400BadRequest);
        }

        var contentType = file.ContentType ?? "application/octet-stream";
        if (!AllowedImageTypes.Contains(contentType))
        {
            return Results.Problem(
                "Unsupported image type; upload a PNG, JPEG or WebP.", statusCode: StatusCodes.Status400BadRequest);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var symbol = request.Form.TryGetValue("symbol", out var s) ? s.ToString() : null;
        var timeframe = request.Form.TryGetValue("timeframe", out var t) ? t.ToString() : null;

        try
        {
            var report = await verification.VerifyAsync(
                buffer.ToArray(), contentType, symbol, timeframe, cancellationToken);
            return Results.Ok(report);
        }
        catch (ChartExtractionException ex)
        {
            return Results.Problem(
                title: "Could not read the chart", detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.Problem(
                title: "Could not verify the chart", detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> ValidateWaveCount(
        WaveValidationRequest request,
        IWaveAnalysisService waveAnalysisService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await waveAnalysisService.ValidateAsync(
                request.Symbol.ToUpperInvariant(),
                request.Annotations,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            // Input-validation feedback is safe and actionable for the caller.
            return Results.Problem(
                title: "Invalid wave annotations",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // InvalidOperationException may carry provider details or raw model output;
            // HttpRequestException is an upstream market-data failure. Log server-side and
            // return a generic message so nothing internal leaks.
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Wave analysis failed for {Symbol}", request.Symbol);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The analysis service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> VerifyEditedCount(
        WaveValidationRequest request,
        IWaveVerificationService verificationService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // ~2y of daily context is plenty to snap any edited pivot and project levels.
        const int lookbackDays = 730;
        try
        {
            var result = await verificationService.VerifyAsync(
                request.Symbol.ToUpperInvariant(), request.Annotations, lookbackDays, cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid request", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Wave verification failed for {Symbol}", request.Symbol);
            return Results.Problem(
                title: "Verification unavailable",
                detail: "The market-data service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> AutoAnalyze(
        AutoWaveAnalysisRequest request,
        IAutoWaveAnalysisService autoWaveAnalysisService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Clamp inputs to sane bounds so a bad request can't blow up history fetches or the
        // pivot detector. Defaults give ~1y of daily context at 3% reversal sensitivity.
        var lookbackDays = Math.Clamp(request.LookbackDays ?? 365, 30, 1825);
        var threshold = Math.Clamp(request.ThresholdPercent ?? 2.5m, 0.5m, 25m);

        try
        {
            var result = await autoWaveAnalysisService.AnalyzeAsync(
                request.Symbol.ToUpperInvariant(),
                lookbackDays,
                threshold,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // InvalidOperationException = LLM/budget failure; HttpRequestException = upstream
            // market-data provider failure. Both are transient/external — log server-side and
            // return a generic 502 so nothing internal leaks.
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Auto wave analysis failed for {Symbol}", request.Symbol);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The analysis service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> TopDownAnalyze(
        string symbol,
        decimal? threshold,
        ITopDownAnalysisService topDownAnalysisService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!Application.SymbolInput.IsValidSymbol(symbol))
        {
            return Results.Problem(
                title: "Invalid symbol",
                detail: "Symbol must be a short ticker of letters, digits and . - ^ = / characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var thresholdPercent = Math.Clamp(threshold ?? 3m, 0.5m, 25m);

        try
        {
            var result = await topDownAnalysisService.AnalyzeAsync(
                symbol.ToUpperInvariant(), thresholdPercent, cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Top-down analysis failed for {Symbol}", symbol);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The market-data service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> HistoricalAnalogs(
        string symbol,
        string? interval,
        IHistoricalAnalogService analogService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!Application.SymbolInput.IsValidSymbol(symbol))
        {
            return Results.Problem(
                title: "Invalid symbol",
                detail: "Symbol must be a short ticker of letters, digits and . - ^ = / characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryParseAnalogInterval(interval, out var candleInterval, out var timeframe))
        {
            return Results.Problem(
                title: "Invalid interval",
                detail: "interval must be '1d' (daily) or '1w' (weekly).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var normalized = symbol.ToUpperInvariant();
        try
        {
            var report = await analogService.AnalyzeAsync(normalized, candleInterval, cancellationToken);
            return Results.Ok(report is null
                ? AnalogResponse.Insufficient(
                    normalized, timeframe, "No current count, or not enough history to compare.")
                : AnalogResponse.From(normalized, timeframe, report));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Historical analogs failed for {Symbol}", normalized);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The market-data service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // Only daily and weekly are supported (both derive from the daily series); intraday analog history
    // is out of scope for now. An unrecognised value is a 400 rather than a silent default.
    private static bool TryParseAnalogInterval(string? interval, out CandleInterval candleInterval, out string timeframe)
    {
        switch ((interval ?? "1d").Trim().ToLowerInvariant())
        {
            case "1d" or "daily" or "1day":
                candleInterval = CandleInterval.OneDay;
                timeframe = "1D";
                return true;
            case "1w" or "weekly" or "1week":
                candleInterval = CandleInterval.OneWeek;
                timeframe = "1W";
                return true;
            default:
                candleInterval = CandleInterval.OneDay;
                timeframe = "1D";
                return false;
        }
    }

    private static IResult GetTokenUsage(ITokenTracker tokenTracker)
        => Results.Ok(tokenTracker.GetReport());
}
