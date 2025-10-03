# PingPong App – Working Plan

_Last updated: 2025-02-28_

## Vision
Build a lightweight ping pong results tracker that encourages quick match entry, keeps an immutable event history, and surfaces rich stats (standings, head-to-head, future ELO) using MudBlazor for UI. SQLite backs local dev/tests while SQL Server powers prod/staging. Architecture follows Api ↔ Application ↔ Domain ↔ Infrastructure layering with domain-centric logic.

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

## Pending Questions / Decisions
- Exact fuzzy matching strategy library vs. handwritten? (Must stay provider-agnostic.)
- How to flag corrections in standings (ignore superseded event, or annotate row?).
- Rate limiting / abuse prevention not yet considered for anonymous submission.

---
_Keep this document updated when scope or priorities change._
