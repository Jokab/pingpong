# Repository Guidelines

## Project Structure & Module Organization
- Repo root contains `pingpong.sln` and the Razor app at `PingPong/PingPong.csproj`.
- UI resides in `PingPong/Components`: routes (`Routes.razor`), pages (`Components/Pages`), and shared layout/nav (`Components/Layout`).
- Static assets live under `PingPong/wwwroot`; configs live in `appsettings.json` with local overrides in `appsettings.Development.json`.

## Build, Test, and Development Commands
- `dotnet restore` — pull NuGet dependencies before building.
- `dotnet build PingPong/PingPong.csproj` — compile and validate the Razor component project.
- `dotnet watch run --project PingPong/PingPong.csproj` — run the development server with hot reload.

## Coding Style & Naming Conventions
- Standard .NET naming: PascalCase types/members, camelCase locals/parameters, UPPER_CASE constants, interfaces prefixed with `I`.
- One type per `.cs`; mirror namespaces to folders; pair components with optional `.razor.css` for scoped styling.
- Use file-scoped namespaces, `var` when clear, explicit access modifiers, expression-bodied members, and LINQ; nullable is enabled—fix warnings, guard inputs with `ArgumentNullException.ThrowIfNull`, and favor records for immutable view models. Run `dotnet format PingPong/PingPong.csproj` before committing.
- Domain objects should have zero public setters and must expose methods to modify. Prefer to implement as much logic on domain objects as possible.

## Razor Component Practices
- Compose focused components and push heavy or shared logic into injected services rather than expanding markup files.
- Register services in `Program.cs`, rely on dependency injection, use async/await for I/O, and separate data gathering from pure rendering helpers to keep call chains shallow.

## Testing Guidelines
- Create a sibling `PingPong.Tests` project (xUnit or bUnit) that mirrors component namespaces and sticks to the Arrange/Act/Assert pattern.
- Run `dotnet test` from the solution root; add `--collect:"XPlat Code Coverage"` when coverage insights matter, and use lightweight fakes at layer boundaries.

## Comments & Documentation
- Prefer self-explanatory names; comment only on intent or edge cases, and avoid XML documentation on obvious members.

## UI Text Language
- Skriv all UI‑text på svenska.

## Commit & Pull Request Guidelines
- Write imperative, scoped commit subjects (e.g., `Add weather data service`) and bundle related changes together.
- Record testing evidence, configuration updates, linked issues, and attach UI screenshots or GIFs for visual changes.

## Configuration & Security Tips
- Keep secrets out of source control; rely on `dotnet user-secrets` or environment variables when extending `appsettings*.json`.
- Middleware in `Program.cs` enforces HTTPS, antiforgery, and static assets—review it when adding endpoints or forms, and validate environment overrides before deployment.
