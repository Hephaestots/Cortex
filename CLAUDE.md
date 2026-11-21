# Cortex

Stack Overflow-style Q&A for readers, plus a Fable-style book profile (shelves, ratings, reviews).
Learning project: the goal is a realistic distributed .NET system and understanding its trade-offs, not the simplest app.

Read these before larger changes:
- `docs/overview.md`: product brief, MVP, milestones, open questions
- `docs/architecture.md`: decisions, service boundaries, service layout template, data model
- `docs/status.md`: where the work stands right now, and gotchas already hit

## Stack
.NET 10 / C# · .NET Aspire (dev orchestration) · WolverineFx (handlers, outbox) on RabbitMQ · WolverineFx.Http for **all** HTTP endpoints · PostgreSQL (one DB per service) · YARP gateway · Keycloak (OIDC/JWT) · Next.js App Router + TypeScript + Tailwind, TanStack Query (server data), Zustand (UI state only) · Open Library for book metadata · Docker, then Azure.

## Environment
- WSL2 (distro `Ubuntu-new`), repo at `~/dev/Cortex`, solution `Cortex.slnx`. Edited in JetBrains Rider via Remote Development.
- Run everything through the Aspire AppHost with the Aspire CLI from the repo root: `aspire run` (or `aspire start` / `aspire stop` for a background run, `aspire describe <resource>` to inspect one). Watch mode is off: `AddDotnetProject` (Aspire 17) doesn't support it yet, so restart the AppHost after changing a service. Today it starts Keycloak, Postgres (one server, database `questions-db`) and the question service; RabbitMQ is added in build step 4 (see `docs/status.md`).
- Layout: `src/Cortex.AppHost`, `src/Cortex.ServiceDefaults`, services under `src/Services/`, tests under `tests/` (see "Solution layout" in `docs/architecture.md`).
- Tests: `dotnet test --project tests/Cortex.QuestionService.Tests` (Docker must be running; the new `dotnet test` needs `--project`). Endpoint tests boot the real `Program.cs` against a Testcontainers Postgres, with a test auth scheme instead of Keycloak (see "Tests" in `docs/status.md`).
- GitHub: `Hephaestots/Cortex`, default branch `main`.

## Conventions (must follow)
- **Endpoints are Wolverine.HTTP**, not Minimal APIs or controllers: one static endpoint class per use case with `[WolverineGet/Post/Put/Delete("...")]`. No `MapGroup` or `<Resource>Endpoints.cs` files; `app.MapWolverineEndpoints()` discovers them.
- **Routes start with `/api/`**, e.g. `/api/questions/{id}`.
- **Vertical slices:** `Features/<Resource>/<UseCase>.cs` holds the request/response records, validator and endpoint. Plus `Domain/` (entities and rules, no EF/HTTP/Wolverine), `Data/` (DbContext, one `IEntityTypeConfiguration<T>` per entity, migrations), `Infrastructure/` (CurrentUser, Wolverine setup, DI extensions). One project per service.
- **Method injection**, no constructors. Use the Load/Before + Handle split for endpoints that act on an existing entity. Validation via `WolverineFx.Http.FluentValidation`.
- **Writes return their events** as a tuple, e.g. `(Created<AnswerDto>, AnswerPosted)`; don't inject `IMessageBus` unless cascading can't do it. Reads return `Results<Ok<T>, NotFound>` via `TypedResults`.
  - **Until build step 4** (events and outbox) there is no messaging: writes return only the HTTP result and call `SaveChangesAsync` explicitly. Step 4 adds the event tuples and replaces the explicit save with Wolverine's EF Core transaction middleware.
- **IDs:** entity IDs are `Guid.CreateVersion7()`, generated in the domain. The Keycloak user id (`sub`) is stored as a `string`, with no FK.
- **Migrations** are applied at startup in Development only.
- **Events live in `Cortex.Contracts`**, are immutable, and evolve compatibly (add optional fields; never rename or remove).
- **Map entities to events at the edge:** extension methods in `Features/<Resource>/<Resource>Events.cs` (e.g. `question.ToQuestionCreated()`), never on the domain entity (the domain doesn't know contracts) and never inline in the endpoint. Requests map to new entities with a `ToX(...)` method on the request record, as responses map with `From(entity)`.
- **Question and answer bodies are Markdown** (CommonMark), stored exactly as written; titles are plain text. The frontend renders bodies with raw HTML disabled, and search indexes them as plain text through Markdig (`MarkdownText.ToPlainText` in the Search service, also with HTML disabled), so the index matches what a reader sees.
- **Message handlers have an explicit error policy** in their service's `WolverineSetup`: transient failures retry with cooldown, permanent ones go straight to the dead-letter queue, anything else retries twice and then dead-letters. Nothing is dropped silently.
- **Each service owns its data.** No cross-service DB reads/writes; reference other services' data by ID only (e.g. `Questions.BookId` = Open Library work key, never validated by Questions).
- Every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` (Aspire health checks and `WaitFor` depend on it).
- Wolverine discovery is assembly-scoped: endpoints/handlers in another assembly need `opts.Discovery.IncludeAssembly(...)`.

## Auth
- Keycloak realm `cortex`, imported from `src/Cortex.AppHost/Realms/cortex-realm.json`, which is the **source of truth** (edit it, don't click-configure).
- Keycloak only imports a realm that does not exist yet, and the AppHost keeps Keycloak's data in the `keycloak-data` volume. After editing the realm file, delete that volume (or the realm) so the next start re-imports it.
- **Both the gateway and each service validate JWTs.** Services call `builder.AddCortexAuth()` (ServiceDefaults): audience `cortex-api`, authority resolved through Aspire service discovery. There is no `Auth:*` configuration to set.
- Realm roles: `user`, `admin`. Clients: `cortex-web` (Next.js, confidential + PKCE), `cortex-api` (bearer-only, the audience), `cortex-dev-tools` (public, for Postman). Test users: `reader`, `hephaestots`, `admin-user`.
- Tokens carry `sub` (via the `basic` client scope) and a flat `roles` claim (custom mapper); the built-in roles scope is removed, so there is no `realm_access`.

## Working style
- This is a learning project: explain the why behind non-obvious choices, and flag trade-offs instead of silently picking.
- Prefer small, verifiable steps that follow the build order in `docs/architecture.md`.
- Check Wolverine APIs against the docs for the installed version (Wolverine 6); conventions changed between versions.
- Keep `docs/status.md` current when a milestone lands or a decision changes.
