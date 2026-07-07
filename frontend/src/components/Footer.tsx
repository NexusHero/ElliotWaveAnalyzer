/**
 * Persistent footer (#167 AC1): links the legal pages (server-rendered, reachable unauthenticated —
 * see `LegalEndpoints` on the backend) from every screen, including the pre-login screen. Full-page
 * navigation via plain `<a>` (like the Google OAuth link) — these are real backend routes, not SPA
 * views (the frontend is a router-less SPA and has nothing else to route them to).
 */
export default function Footer() {
  return (
    <footer className="app-footer">
      <span className="app-footer-copy">
        Elliott Wave Analyzer — structural analysis, not investment advice.
      </span>
      <nav className="app-footer-links" aria-label="Legal">
        <a href="/legal/impressum" target="_blank" rel="noopener noreferrer">
          Impressum
        </a>
        <a href="/legal/privacy" target="_blank" rel="noopener noreferrer">
          Privacy Policy
        </a>
        <a href="/legal/terms" target="_blank" rel="noopener noreferrer">
          Terms of Service
        </a>
      </nav>
    </footer>
  )
}
