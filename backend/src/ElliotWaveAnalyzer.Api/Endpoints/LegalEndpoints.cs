using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Publicly reachable legal pages (#167 AC1, AC5) — Impressum, Privacy Policy, Terms of Service.
/// Plain server-rendered HTML: the frontend is a router-less SPA and the backend serves no static
/// files today (confirmed by reading <c>Program.cs</c>/<c>vite.config.ts</c>), so these are the
/// simplest way to make the pages independently reachable without logging in and without inventing
/// SPA routing or static-file hosting for three pages. Every operator-identifying field below is a
/// clearly marked placeholder — see the "OPERATOR: REPLACE BEFORE LAUNCH" banner on every page — the
/// same partial-scaffold pattern already used for #167's sibling legal-scaffolding tickets.
/// </summary>
internal static class LegalEndpoints
{
    private const string OperatorPlaceholderBanner =
        """
        <p class="placeholder-banner">
          ⚠ OPERATOR: REPLACE BEFORE LAUNCH — the company/contact details on this page are
          placeholders, not real registration data.
        </p>
        """;

    internal static IEndpointRouteBuilder MapLegalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/legal").WithTags("Legal").AllowAnonymous();

        group.MapGet("/impressum", () => Results.Content(Page("Impressum", Impressum), "text/html"))
            .WithName("Impressum")
            .WithSummary("Legally required operator identification (§5 TMG)");

        group.MapGet("/privacy", () => Results.Content(Page("Privacy Policy", Privacy), "text/html"))
            .WithName("PrivacyPolicy")
            .WithSummary("Datenschutzerklärung — what data is processed and why");

        group.MapGet("/terms", () => Results.Content(Page("Terms of Service", Terms), "text/html"))
            .WithName("TermsOfService")
            .WithSummary("AGB — terms of use, including the not-investment-advice stance");

        return app;
    }

    // A plain (non-interpolated) raw string — its braces are literal CSS, not interpolation holes,
    // which sidesteps needing extra '$' prefixes in Page() below just to escape them.
    private const string PageStyle =
        """
        body { font-family: system-ui, sans-serif; max-width: 720px; margin: 40px auto; padding: 0 20px; line-height: 1.6; color: #1a1a1a; }
        h1 { font-size: 1.6em; }
        .placeholder-banner { background: #fff3cd; border: 1px solid #ffe08a; padding: 10px 14px; border-radius: 6px; font-size: 0.9em; }
        .doc-meta { color: #666; font-size: 0.85em; }
        a { color: #2563eb; }
        """;

    private static string Page(string title, string body) =>
        $"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{title} — Elliott Wave Analyzer</title>
        <style>
        {PageStyle}
        </style>
        </head>
        <body>
        <h1>{title}</h1>
        {OperatorPlaceholderBanner}
        {body}
        <p><a href="/">← Back to Elliott Wave Analyzer</a></p>
        </body>
        </html>
        """;

    private const string Impressum =
        $"""
        <p class="doc-meta">Angaben gemäß § 5 TMG — placeholder data.</p>
        <p>
          [Operator legal name] · [Street address] · [Postal code, city] · [Country]<br>
          Represented by: [Managing director / owner]<br>
          Contact: [email address] · [phone number]<br>
          Commercial register: [register court], [register number] (if applicable)<br>
          VAT ID (§27a UStG): [VAT ID] (if applicable)
        </p>
        <p>Responsible for content per § 18 (2) MStV: [Name, same address as above].</p>
        """;

    private const string Privacy =
        $"""
        <p class="doc-meta">Version {LegalDocuments.PrivacyVersion} · effective {LegalDocuments.PrivacyEffectiveDate}</p>
        <p>
          This is a placeholder Privacy Policy (Datenschutzerklärung) outline — the operator must
          replace it with a policy reviewed for the actual data processing this deployment performs
          before accepting real users.
        </p>
        <h2>What is processed</h2>
        <p>
          Account data (email, hashed password), the analyses and depots you save, and — only if
          you opt in via the cookie banner — analytics/marketing identifiers. See the in-app cookie
          preferences for the current opt-in state at any time.
        </p>
        <h2>Your rights (DSGVO Art. 15–21)</h2>
        <p>
          You can export all personal data tied to your account (<code>GET /api/auth/export</code>)
          or permanently delete your account and everything tied to it
          (<code>POST /api/auth/delete-account</code>) at any time from within the app.
        </p>
        <h2>Contact</h2>
        <p>[Operator contact / data protection officer, if required] — [email address].</p>
        """;

    private const string Terms =
        $"""
        <p class="doc-meta">Version {LegalDocuments.TermsVersion} · effective {LegalDocuments.TermsEffectiveDate}</p>
        <p>
          This is a placeholder Terms of Service (AGB) outline — the operator must replace it with
          terms reviewed for the jurisdiction(s) this deployment actually serves before accepting
          real users.
        </p>
        <h2>Not investment advice</h2>
        <p>
          Elliott Wave Analyzer performs structural analysis of price action — not investment advice.
          Nothing it produces — wave counts, risk sizing, projections, or AI-generated narrative — is
          a recommendation or a solicitation to trade. Trading involves risk of loss; you are solely
          responsible for your own decisions.
        </p>
        <h2>Account and acceptable use</h2>
        <p>
          You are responsible for the credentials and any API keys you store, and for using the
          service lawfully. [Placeholder — expand with the operator's actual usage terms, liability
          limitations, and termination conditions.]
        </p>
        """;
}
