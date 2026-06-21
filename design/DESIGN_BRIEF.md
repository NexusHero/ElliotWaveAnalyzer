# Elliott Wave Analyzer — Design Brief

## What this product is (and is NOT)
This is a **learning & reflection tool for Elliott Wave analysis**, not a trading terminal.
**Do not redesign it to look like TradingView.** The chart is a means to an end: users place
wave labels (1–5, A/B/C, W/X/Y) on price and get **objective rule checks + an AI coach that
reflects with them** so they get *better at Elliott Wave over time*. The hero of every screen
is the **wave annotation + coaching loop**, not the candlesticks.

Tone: focused, calm, "study/practice" feeling — closer to a learning app than a Bloomberg
terminal. Works in **dark and light** (tokens already exist; keep both first-class).

## The frames (see /screenshots)
- `01-login.png` — login
- `02-register.png` — register (currently **looks identical to login** — must differ)
- `03-workspace-dark.png` — main workspace, dark, no labels
- `04-workspace-annotated-dark.png` — workspace with wave labels 1/2/3 placed + side panel
- `05-workspace-light.png` — main workspace, light

## Problems to fix per frame

### Login / Register (01, 02)
- Login and Register are **visually indistinguishable** — give Register a clearly different
  state (heading, helper text, maybe a 2-step or a segmented Login|Register switch at the top).
- The whole auth screen feels like a bare form on an empty page. Give it an identity: product
  name + one-line value prop ("Master Elliott Waves with an AI coach"), a calm branded panel,
  proper spacing. Keep it accessible (labels, focus states) in dark + light.

### Workspace (03, 04, 05)
- Layout is a generic chart + a thin right panel — **rebalance toward the Elliott workflow**.
  The right panel (annotations + rule checks + coach reflection) is the product; give it room
  and hierarchy. The chart can be a bit more contained.
- **Surface the AI/coach feedback prominently.** Today the result is a plain list. Users must
  immediately see: ✅/❌ per canonical rule WITH the reason ("Wave 3 is the shortest of 1/3/5"),
  the Fibonacci ratios, and the coach's reflection (why the count is questionable + an
  alternative count + a reflective question). Make "objective rule checks" vs "AI coach
  reflection" two clearly separate, well-styled sections.
- Wave-label markers on the chart are tiny blue dots — make labels legible (numbered chips).

## New things the design must include (features being built)
1. **Timeframe selector** — 4H / 1D / 1W toggle near the symbol. Important and currently missing.
2. **Settings / API-keys page** — a dedicated, clearly-secured settings screen where the user
   enters their LLM API keys (Gemini / Claude / OpenAI). Must *feel* secure: masked inputs,
   "stored encrypted, never shown again" affordance, per-provider rows. (Backend stores them
   encrypted, never plaintext.)
3. **"Analyze for me" button** — alongside "Validate wave count", a button where the **AI does
   the full wave count itself** from the data; the user then compares/learns from it. Design the
   two actions so it's clear: *my count* vs *AI's count*.
4. **Empty / loading / error states** for the coach (e.g. "Add at least 2 labels", "no API key
   configured → go to Settings", "analyzing…").

## Constraints
- Theme tokens live in `frontend/src/index.css` (CSS custom properties, dark = `:root`,
  light = `:root[data-theme="light"]`). Reuse/extend these tokens; component styling is in
  CSS Modules per component. Keep everything themeable from the tokens.
- React 18 + TypeScript + Vite. Chart is TradingView **Lightweight Charts** (rendering only).
- Deliver per-frame redesigns (login, register, workspace, settings, results panel) that we can
  translate back into the CSS-module components.
