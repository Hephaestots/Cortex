# Cortex: current status

Last updated: 2026-11-11. Update this file when a milestone lands or a decision changes.

## Where things stand
- **AppHost** runs Keycloak (port 6001, `keycloak-data` volume, realm import), Postgres (`postgres` server, `postgres-data` volume, database `questions-db`) and the question service, which references and waits for both. **RabbitMQ is not in the AppHost yet**; it is added in step 4.
- The question service receives its connection string as `ConnectionStrings:questions-db` but does not use it yet.
- **Keycloak** realm `cortex` is imported from `src/Cortex.AppHost/Realms/cortex-realm.json` and working end to end.
- **ServiceDefaults** holds the Aspire defaults plus `AddCortexAuth()` (Keycloak JWT bearer, audience `cortex-api`, `roles` role claim, `AdminOnly` policy).
- **Question service** (`src/Services/Cortex.QuestionService`) is just starting. Wolverine.HTTP 6 is set up as the base for endpoints. No database and no CRUD for questions or answers yet.
- Existing endpoints, both converted from Minimal APIs to Wolverine.HTTP:
  - `GET /api/account/me`: static `MeEndpoint`, `[Authorize]`, returns `MeResponse(UserId, UserName, Email, Roles)` read from claims (`sub`, `Identity.Name`, `email`, flat `roles`).
  - `GET /api/admin/ping`: `[Authorize(Policy = Policies.AdminOnly)]` (the policy requires the `admin` role). Verified 401 without a token, 403 as `user`, 200 as `admin`.
- **Not started:** `Cortex.Contracts`, the gateway, the Profiles service, `web/`, `tests/`.

## Build order progress
| Step | State |
|---|---|
| 1. AppHost infrastructure | Keycloak and Postgres done. RabbitMQ moves to step 4. |
| 2. Gateway plus Next.js auth round trip | Deferred until after step 3. Services validate JWTs themselves, so step 3 does not depend on it. |
| 3. Questions and answers CRUD | In progress (plan below). |
| 4 to 7 | Not started. |

## Step 3 plan
Each item is one small commit.
1. ~~Bring the docs in line with the code and the decisions below.~~ Done.
2. ~~Move the projects to `src/` and `src/Services/` (own commit), and delete the unused `Auth:*` config.~~ Done.
3. ~~Add Postgres to the AppHost: database `questions-db`, referenced and waited on by the question service.~~ Done.
4. EF Core skeleton: `Data/QuestionsDbContext`, `Infrastructure/` (`CurrentUser`, `WolverineSetup`, `ServiceCollectionExtensions`), slimmer `Program.cs`.
5. Domain and first migration: `Question`, `Answer`, their configurations, including `AcceptedAnswerId` with its composite FK. `BookId` is a nullable string.
6. Question slices: `CreateQuestion`, `GetQuestion`, `ListQuestions` (newest first, paged), `UpdateQuestion`, `DeleteQuestion`. Only the author or an admin may change a question.
7. Answer slices: `PostAnswer`, `UpdateAnswer`, `DeleteAnswer`, then `AcceptAnswer` (question author only).
8. A `.http` file that exercises each endpoint with a `cortex-dev-tools` token; update this file.

Tags and votes are out of scope here (step 5 of the build order).

## Next up: item 4, EF Core skeleton
Nothing in the question service touches the database yet. Item 4 adds the plumbing only, with no entities:
- `Data/QuestionsDbContext` registered against the `questions-db` connection string (Aspire's Npgsql EF Core integration), so the service gets a Postgres health check and telemetry with it.
- `Infrastructure/CurrentUser` (reads `sub` and roles from the claims), `Infrastructure/WolverineSetup` (`ConfigureQuestionService`, holding today's `UseRuntimeCompilation()`), `Infrastructure/ServiceCollectionExtensions` (`AddQuestionServiceData()`).
- `Program.cs` reduced to the shape shown in `docs/architecture.md`, and the unused `using` removed.
- Dev-only startup migration hook, which has nothing to apply until item 5 adds the first migration.

Done when: the solution builds, `aspire run` shows the question service healthy with the database health check included, and the two existing endpoints behave as before.

## Repository state
- Items 1 to 3 are on `main`. There are no open branches.
- Run with `aspire run` from the repo root; stop with Ctrl+C (or `aspire start` / `aspire stop`).
- Verified on 2026-11-11: solution builds with no warnings; under the AppHost, Postgres 18.3 starts, `questions-db` exists, the question service is healthy and receives `ConnectionStrings__questions-db`; `GET /api/account/me` returns 401 without a token.
- Not verified since the move to `src/`: a full token round trip (200 on `/api/account/me`, 403/200 on `/api/admin/ping`). The auth code and realm file are unchanged, only their paths.
- Local Docker volumes in use: `keycloak-data`, `postgres-data`.

## Decisions taken for step 3 (2026-11-11)
- **Layout:** projects live in `src/` and `src/Services/` (moved before migrations exist).
- **Routes:** keep the `/api/...` prefix on every endpoint.
- **No events yet:** writes return only the HTTP result and call `SaveChangesAsync` explicitly. Step 4 adds the event tuples and Wolverine's EF Core transaction middleware.
- **Migrations:** applied at startup, in Development only.
- **IDs:** version 7 GUIDs for entities; the Keycloak user id is stored as a string.
- **Auth:** both the gateway and the services validate JWTs. The unused `Auth:Authority` / `Auth:Audience` settings were deleted.

## Gotchas already hit
- **Wolverine 6 wouldn't start** until `WolverineFx.RuntimeCompilation` was added and enabled with `opts.UseRuntimeCompilation()`.
- **Access tokens lacked `sub`** until the `basic` client scope was added to the clients. Now in the realm file.
- **Roles:** the built-in roles scope was removed in favour of a flat `roles` mapper on `cortex-web` and `cortex-dev-tools`, so read `roles`, not `realm_access.roles`.
- **Realm edits need a fresh volume:** Keycloak skips the import when the realm already exists, and the AppHost persists Keycloak in the `keycloak-data` volume. After editing `cortex-realm.json`, delete that volume (or the realm) and restart, otherwise the change is silently ignored.
- **`dotnet run` on the AppHost fails:** with `AspireUseCliBundle`, `dotnet run --project src/Cortex.AppHost` delegates to `aspire run --no-build`, which the CLI rejects while `defaultWatchEnabled` is true in `aspire.config.json` ("The --no-build option cannot be used when watch mode is enabled"). Use `aspire run` from the repo root.
- **Postgres password and volume go together:** Aspire generates the Postgres password once and keeps it in the AppHost's user secrets, while the data lives in the `postgres-data` volume. If one is reset without the other, logins fail; delete the volume to start clean.

## Known leftovers
- `Program.cs` in the question service has an unused `using System.Security.Claims`.
