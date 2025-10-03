# PingPong App – Working Plan

_Last updated: 2025-02-28_

## Vision
Build a lightweight ping pong results tracker that encourages quick match entry, keeps an immutable, append-only event history, and surfaces rich stats (standings, head-to-head, future ELO) using MudBlazor for UI. SQLite backs local dev/tests while SQL Server powers prod/staging. Architecture follows Api ↔ Application ↔ Domain ↔ Infrastructure layering with domain-centric logic. Source of truth is an event log; “matches” and other read models are projections computed by scanning events chronologically.

## Current Vertical Slice Target
Deliver a minimal end-to-end experience for submitting a match via the public API and persisting it in SQLite.

**Status:** _Completed (integration-tested match submission API writing to SQLite)_

### Scope of the Slice
1. **Match submission API**
   - Define a request DTO that includes player identifiers/names, per-set scores, and match date.
   - Endpoint maps DTO to an application command and invokes `IMatchSubmissionService`.
   - Application layer ensures players exist (creating if needed), validates scores (win by two, min 11), and delegates to domain for event creation.
   - Infrastructure saves the event, sets, and match aggregate in a single transaction using SQLite for dev/test.
2. **Integration test**
   - End-to-end test exercises the HTTP endpoint against an in-memory/temp SQLite database, asserting persisted data (match, sets, events, players).
   - Test setup bootstraps the minimal host configuration and tears down cleanly.

### Nice-to-haves (if time permits after slice)
- Basic request logging / diagnostics for the submission endpoint.
- Simple validation error responses with actionable messages.

## Work Breakdown
1. **Domain Enhancements**
   - Implement match aggregate logic (validation, set tallying) focused solely on accepting a new match event.
   - Provide helpers for normalising player names and guarding against duplicate participants.
2. **Application Layer**
   - Build `MatchSubmissionService` (and supporting abstractions) to orchestrate player resolution and domain commands.
   - Add lightweight player lookup/creation logic required for the submission flow only.
3. **Infrastructure**
   - Finalise EF mappings for players, matches, events, and sets; configure DbContext registration for SQLite (tests/dev) and SQL Server (prod).
   - Implement persistence routines the application layer depends on (transaction handling, player lookup/insert, match save).
4. **API Layer**
   - Register DbContext, services, and expose the `/matches` minimal endpoint that accepts the DTO and returns a submission result.
   - Optional: add `/healthz` or simple ping endpoint if useful for integration testing harness.
5. **Testing & Tooling**
   - Unit tests around domain validation and player normalisation.
   - Integration test hitting the HTTP endpoint with SQLite backing store (required for slice).
   - Maintain `dotnet build`, `dotnet test`, and `dotnet format` cleanliness.

## Post-Vertical Slice Backlog
- Standings aggregation service and UI wiring.
- History log endpoint and MudBlazor timeline backed by real data.
- Head-to-head stats and player detail experiences.
- ELO rating calculations and presentation.
- Enhanced fuzzy player directory, alias management, and data hygiene workflows.
- Frontend polish (responsive tweaks, real-time updates) and snackbar feedback.
- Expanded telemetry, logging, and operational readiness items.
- Comprehensive automated testing matrix (bUnit, Playwright, load tests) and CI/CD pipeline setup.
- Deployment hardening: migrations workflow, containerisation, infrastructure automation.

## Event Sourcing Approach
- Single, append-only Event Log is the source of truth. New submissions append events; nothing is deleted or updated in-place.
- Read models (standings, head-to-head, player ledger, optional materialized “matches”) are projections computed by applying events in chronological order.
- “Edits” or “removals” are modeled by adding new events that, when applied later in sequence, change the resulting projection (no explicit supersede flags required). Last write wins within the natural match stream.
- Natural match identity: normalized `PlayerOne`, `PlayerTwo`, `MatchDate`, and an ordinal (1..N) for that day. The last event for the same identity determines the effective state of that match in projections.
- Projections can be computed on demand (queries re-scan) or persisted as materialized views for performance; projections are rebuildable from the log.

Event types (initial cut)
- MatchRecorded: { playerOne, playerTwo, date, ordinal?, sets[], submittedBy, createdAt }
- Optional future events: PlayerAliased, PlayerRenamed, RatingRecomputed (if we externalize rating evolution), CorrectionNoteAdded.

Projection rules (initial)
- Standings: for each natural match identity, include only the most recent MatchRecorded event when tallying wins/losses.
- History: show raw chronological events (no grouping), optionally group in UI by date and identity, label same-day matches with ordinal.
- Head-to-head: derive from effective matches (after last-write-wins grouping), then aggregate by opponent.

## Product Spec (Initial Prompt Details)
- Core workflow
  - Anyone can submit a match without logging in.
  - Every submission appends to an immutable event log; corrections occur by appending a new event for the same match identity (no destructive edits; no explicit supersede markers required).
  - Default to 3-set matches; allow arbitrary number of sets. Users enter per-set scores only; total sets won is auto-calculated.
  - Date defaults to today. If multiple matches occur the same day, display them with an ordinal (e.g., “2025-03-01 (#2)”) instead of requiring time-of-day.

- Players and data hygiene
  - Player autocomplete surfaces known players while typing.
  - Provide an “Add new player” option in the autocomplete when no match is found; everything is open — no admin needed.
  - Prevent duplicates and misspellings (e.g., “Rickard/Richard”) via normalization + aliasing and fuzzy matching. New players create a canonical record; variations map to aliases. Show fuzzy suggestions before adding a new record.

- Standings (scoreboard)
  - Columns: Matches Played, Wins, Losses, Match Win% (sets-based win/loss defines match winner); optionally show Rating when ELO lands.
  - Clicking a player opens a detail view with head-to-head stats (same columns) versus each opponent.

- History
  - Dedicated history view accessible from a top navbar, listing the immutable event timeline in strict chronological order with filters (player, date).

- UI/UX and layout
  - MudBlazor components with built-in theming (no Bootstrap). Use MudThemeProvider + MudDialogProvider + MudSnackbarProvider + MudPopoverProvider in the app shell.
  - Desktop: two columns — left “Register Score”, right “Score Table”.
  - Mobile: stack vertically — “Register Score” first, then “Score Table”.
  - Keep submission friction low; sane defaults; async feedback via snackbars.

- Backend/API
  - Minimal API endpoints: submission, standings, history (raw events), head-to-head, player search/add.
  - Persistence: SQLite for local dev/tests; SQL Server for prod/staging.
  - Event Log is append-only; projections are derived and rebuildable.
  - Migrations maintained in Infrastructure project; Dev may auto-apply.

## Proposed Minimal Endpoints
- POST `/matches` — append a MatchRecorded event (players, date, optional ordinal, per-set scores, submittedBy).
- GET `/standings` — compute standings from effective matches (last event per identity).
- GET `/history` — paged raw event log; filter by player/date; strict chronological order.
- GET `/players` — search players (fuzzy) by query; return canonical and aliases.
- POST `/players` — add new player (normalized; may create alias if near-duplicate detected).
- GET `/headtohead/{playerId}` — opponent breakdown computed from effective matches.

## UX Rules and Validations
- Set validation: non-negative scores, win by ≥2, winner ≥11.
- Players must be distinct; names normalized server-side; deduplicate via normalized key and alias table.
- Submission form defaults to 3 sets; allow add/remove sets inline.
- Error responses contain clear, actionable messages suitable for inline display.

## Open Questions to Nail Down
- ELO specifics: which variant (e.g., table tennis tuned K-factor), whether to seed initial ratings, and if ratings are per-player global or seasonal.
- Match formats: best-of-N vs. exact N sets; should we support “race to X sets won” mode explicitly in the UI?
- Player identity: any need for soft-merge workflows when duplicates slip through (admin-only tool)?
- Rate limiting for anonymous submission to prevent spam.
- History presentation details: grouping by day, showing match ordinal, and how to present superseding/correction events.

## Pending Questions / Decisions
- Exact fuzzy matching strategy library vs. handwritten? (Must stay provider-agnostic.)
- How to flag corrections in standings (ignore superseded event, or annotate row?).
- Rate limiting / abuse prevention not yet considered for anonymous submission.

---
_Keep this document updated when scope or priorities change._
