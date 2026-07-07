namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Public marketing pages (#179 AC1) — a landing page and a pricing page, reachable without
/// logging in, since the app otherwise goes straight to a login gate with nothing to show a
/// prospective user first. Plain server-rendered HTML for the same reason as `LegalEndpoints`
/// (#167): the frontend is a router-less SPA and the backend serves no static files, so this is
/// the smallest way to make two pages independently reachable and crawlable. The pricing tiers
/// describe what is actually shipped today (the free deterministic toolset, the per-user LLM
/// quota, bring-your-own-key) — the "Pro" card is explicitly marked pending, since #178 (paid
/// billing) has not been implemented; nothing here pretends a payment flow exists.
/// </summary>
internal static class MarketingEndpoints
{
    private const string BaseStyle =
        """
        :root { color-scheme: light dark; }
        * { box-sizing: border-box; }
        body { font-family: system-ui, sans-serif; margin: 0; padding: 0; line-height: 1.6; color: #1a1a1a; background: #fff; }
        .wrap { max-width: 880px; margin: 0 auto; padding: 0 20px; }
        a { color: #2563eb; }
        .cta { display: inline-block; background: #2563eb; color: #fff; text-decoration: none; padding: 12px 22px; border-radius: 8px; font-weight: 600; }
        .cta:hover { background: #1d4ed8; }
        header.hero { padding: 56px 0 40px; }
        header.hero h1 { font-size: clamp(28px, 5vw, 44px); margin: 0 0 14px; }
        header.hero p { font-size: 1.1em; max-width: 60ch; color: #444; }
        .points { list-style: none; padding: 0; margin: 28px 0; display: grid; gap: 10px; }
        .points li { padding-left: 24px; position: relative; }
        .points li::before { content: "✓"; position: absolute; left: 0; color: #2563eb; font-weight: 700; }
        .tiers { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 20px; margin: 32px 0; }
        .tier { border: 1px solid #ddd; border-radius: 12px; padding: 24px; }
        .tier.pending { opacity: 0.75; border-style: dashed; }
        .tier h3 { margin-top: 0; }
        .tier .price { font-size: 1.6em; font-weight: 700; margin: 8px 0 16px; }
        .tier ul { padding-left: 20px; }
        .pending-badge { display: inline-block; font-size: 0.75em; background: #fff3cd; border: 1px solid #ffe08a; border-radius: 999px; padding: 2px 10px; margin-bottom: 10px; }
        footer.site-footer { border-top: 1px solid #eee; margin-top: 40px; padding: 24px 0 40px; font-size: 0.85em; color: #666; display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; }
        footer.site-footer nav { display: flex; flex-wrap: wrap; gap: 14px; }
        """;

    internal static IEndpointRouteBuilder MapMarketingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/landing", () => Results.Content(LandingPage(), "text/html"))
            .WithTags("Marketing")
            .WithName("Landing")
            .WithSummary("Public landing page — no login required")
            .AllowAnonymous();

        app.MapGet("/pricing", () => Results.Content(PricingPage(), "text/html"))
            .WithTags("Marketing")
            .WithName("Pricing")
            .WithSummary("Public pricing page — no login required")
            .AllowAnonymous();

        return app;
    }

    private static string Head(string title, string description) =>
        $"""
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{title}</title>
        <meta name="description" content="{description}">
        <meta property="og:title" content="{title}">
        <meta property="og:description" content="{description}">
        <meta property="og:type" content="website">
        <meta name="twitter:card" content="summary">
        """;

    private const string Footer =
        """
        <footer class="site-footer">
          <span>Elliott Wave Analyzer — structural analysis, not investment advice.</span>
          <nav aria-label="Legal">
            <a href="/landing">Home</a>
            <a href="/pricing">Pricing</a>
            <a href="/legal/impressum">Impressum</a>
            <a href="/legal/privacy">Privacy Policy</a>
            <a href="/legal/terms">Terms of Service</a>
          </nav>
        </footer>
        """;

    private static string LandingPage()
    {
        const string title = "Elliott Wave Analyzer — Structural Market Analysis";
        const string description =
            "Full-auto Elliott Wave detection, objective rule checks, and an AI analyst's reading on live market data. Not investment advice.";

        return
            $"""
            <!doctype html>
            <html lang="en">
            <head>
            {Head(title, description)}
            <style>{BaseStyle}</style>
            </head>
            <body>
            <main class="wrap">
              <header class="hero">
                <h1>Analyze the market with Elliott Waves.</h1>
                <p>
                  Run a full-auto analysis that detects the wave structure on live data, or label
                  price yourself and get the canonical rules checked instantly with an AI analyst's
                  reading — structural analysis of price action, not investment advice.
                </p>
                <a class="cta" href="/">Create your free account</a>
              </header>

              <ul class="points">
                <li>Full-auto wave detection on live market data</li>
                <li>Objective rule checks on every count — deterministic, not a black box</li>
                <li>An AI analyst that reads the structure, grounded in the same rule report you see</li>
                <li>Risk sizing, projections, and a setup scanner — all work without an API key</li>
              </ul>

              <p><a href="/pricing">See pricing →</a></p>
            </main>
            {Footer}
            </body>
            </html>
            """;
    }

    private static string PricingPage()
    {
        const string title = "Pricing — Elliott Wave Analyzer";
        const string description =
            "Free deterministic wave analysis for everyone. Bring your own API key for unlimited AI readings. Pro plan billing integration pending.";

        return
            $"""
            <!doctype html>
            <html lang="en">
            <head>
            {Head(title, description)}
            <style>{BaseStyle}</style>
            </head>
            <body>
            <main class="wrap">
              <header class="hero">
                <h1>Pricing</h1>
                <p>The deterministic toolset — counting, rule verification, projections, risk sizing, the setup scanner — is free and unlimited for every account. Only the AI analyst's narrative reading draws on a quota.</p>
              </header>

              <div class="tiers">
                <div class="tier">
                  <h3>Free</h3>
                  <p class="price">$0</p>
                  <ul>
                    <li>Unlimited wave counting, rule verification, projections, risk sizing, scanner</li>
                    <li>Up to 50 AI-analyst calls per day on our shared key</li>
                    <li>No credit card required</li>
                  </ul>
                  <a class="cta" href="/">Create free account</a>
                </div>

                <div class="tier">
                  <h3>Bring your own key</h3>
                  <p class="price">$0</p>
                  <ul>
                    <li>Everything in Free</li>
                    <li>Unlimited AI-analyst calls using your own Gemini/OpenAI API key</li>
                    <li>Add a key any time from Settings after signing up</li>
                  </ul>
                  <a class="cta" href="/">Create free account</a>
                </div>

                <div class="tier pending">
                  <span class="pending-badge">Coming soon</span>
                  <h3>Pro</h3>
                  <p class="price">TBD</p>
                  <ul>
                    <li>Billing integration is not live yet — nothing is charged today</li>
                    <li>Planned: a higher shared-key quota without needing your own API key</li>
                  </ul>
                </div>
              </div>

              <p><a href="/landing">← Back to the overview</a></p>
            </main>
            {Footer}
            </body>
            </html>
            """;
    }
}
