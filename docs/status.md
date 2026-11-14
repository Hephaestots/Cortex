# Cortex: current status

Last updated: 2026-11-13. Update this file when a milestone lands or a decision changes.

## Where things stand
- **AppHost** runs Keycloak (port 6001, `keycloak-data` volume, realm import), Postgres (`postgres` server on fixed port 5432, `postgres-data` volume, database `questions-db`, with pgAdmin) and the question service, which references and waits for both. **RabbitMQ is not in the AppHost yet**; it is added in step 4.
- The question service registers `QuestionDbContext` against `ConnectionStrings:questions-db` (Aspire's Npgsql EF Core integration, snake case naming via `EFCore.NamingConventions`) and applies pending migrations at startup in Development.
- **Keycloak** realm `cortex` is imported from `src/Cortex.AppHost/Realms/cortex-realm.json` and working end to end.
- **ServiceDefaults** holds the Aspire defaults plus `AddCortexAuth()` (Keycloak JWT bearer, audience `cortex-api`, `roles` role claim, `AdminOnly` policy).
- **Question service** (`src/Services/Cortex.QuestionService`) has its domain model, EF Core configurations and the `Initial` migration (see "Data layer" below). Wolverine.HTTP 6 is set up as the base for endpoints. No CRUD endpoints for questions or answers yet.
- Existing endpoints, both converted from Minimal APIs to Wolverine.HTTP:
  - `GET /api/account/me`: static `MeEndpoint`, `[Authorize]`, returns `MeResponse(UserId, UserName, Email, Roles)` built from `CurrentUser`.
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
4. ~~EF Core skeleton: `Data/QuestionDbContext`, `Infrastructure/` (`CurrentUser`, `WolverineSetup`, `ServiceCollectionExtensions`), slimmer `Program.cs`.~~ Done.
5. ~~Domain and first migration: `Question`, `Answer`, their configurations, including `AcceptedAnswerId` with its composite FK. `BookId` is a nullable string.~~ Done, and `Tag`, `QuestionVote` and `AnswerVote` were pulled forward into the same migration.
6. Question slices: `CreateQuestion`, `GetQuestion`, `ListQuestions` (newest first, paged), `UpdateQuestion`, `DeleteQuestion`. Only the author or an admin may change a question.
   - **6.1 Seed the starting tags** (do this first). A question needs 1 to 5 tags and there is no endpoint that creates tags until step 5 of the build order, so `CreateQuestion` cannot succeed against an empty `tags` table. The list lives in `Data/TagSeed.cs` and is inserted through EF Core's `UseSeeding` / `UseAsyncSeeding` hooks, which `MigrateAsync` runs: insert-missing by slug, never updating existing rows.
7. Answer slices: `PostAnswer`, `UpdateAnswer`, `DeleteAnswer`, then `AcceptAnswer` (question author only).
8. A `.http` file that exercises each endpoint with a `cortex-dev-tools` token; update this file.

The tag and vote entities and tables now exist, but their endpoints stay out of scope here (step 5 of the build order).

## Data layer (2026-11-13)
- **Domain** (`Domain/`): `Question`, `Answer`, `Tag`, `QuestionVote`, `AnswerVote`, `VoteValue`. Rules throw `DomainException` carrying a `DomainError` (`Common/`). Length limits are public constants on the entities and reused by the configurations.
- **Tables:** `questions`, `answers`, `tags`, `question_tags`, `question_votes`, `answer_votes`.
- **Naming:** `UseSnakeCaseNamingConvention()` names everything EF generates. Only the id columns are spelled out (`question_id`, `answer_id`, `tag_id`), plus two constraint names: `ak_answers_question_id_answer_id` and `fk_questions_accepted_answer`.
- **Accepted answer:** `Question.AcceptedAnswerId` only, no navigation. Composite FK (`question_id`, `accepted_answer_id`) to `answers` (`question_id`, `answer_id`), with `ON DELETE RESTRICT`. `answers` keeps `answer_id` as its primary key and has (`question_id`, `answer_id`) as an alternate key.
- **Tags:** many-to-many through `question_tags` (`question_id`, `tag_id`), with navigations on both sides.
- **Votes:** primary key (`question_id` or `answer_id`, `voter_id`), `value` is `smallint` with `CHECK (value IN (-1, 1))`.
- **User ids:** `author_id` and `voter_id` are `varchar(255)`, from the shared `Domain/UserIdRules.MaxLength` (the OpenID Connect limit for `sub`).
- **Tags:** unique index `ix_tags_slug`.
- **Domain rules worth knowing:** vote values are validated in `Cast` and `Change` (`*.invalid_value`), a question title is trimmed before its length is checked, and every entity's `Errors` class is private.
- **Migration:** `Data/Migrations/20261113222737_Initial`. It was regenerated once, before the first commit, to fold in the user id length and the slug index; the empty `public` schema of `questions-db` was dropped and recreated for that.

## Infrastructure (2026-11-13)
- `Infrastructure/CurrentUser`: scoped, built from `IHttpContextAccessor`. Exposes `Id` (`sub`, throws when there is no authenticated user), `DisplayName` (`preferred_username`), `Email`, `Roles`, `IsAdmin` and `IsAuthenticated`. Take it as an endpoint method parameter.
- `Infrastructure/WolverineSetup`: `opts.ConfigureQuestionService()`, stage 1 only (`UseRuntimeCompilation()`). It takes no `IConfiguration` yet; add the parameter when RabbitMQ needs it.
- `Infrastructure/ServiceCollectionExtensions`: `builder.AddQuestionServiceData()` (extends the builder, not `IServiceCollection`, because Aspire's `AddNpgsqlDbContext` does), `services.AddCurrentUser()`, and `app.ApplyMigrationsAsync()`.
- `Program.cs` matches the shape in `docs/architecture.md`, plus the Development block (`MapOpenApi`, `ApplyMigrationsAsync`) and `UseHttpsRedirection()`, which is kept on purpose (decided 2026-11-13).
- `MeEndpoint` takes `CurrentUser` as its parameter, so it doubles as a live check that the injection works.

## Next up: item 6, question slices
`CreateQuestion`, `GetQuestion`, `ListQuestions`, `UpdateQuestion`, `DeleteQuestion`, using `CurrentUser` and an explicit `SaveChangesAsync`.

**6.1, tag seeding, is implemented** (`Data/TagSeed.cs` holds the list, `Data/TagSeeder.cs` inserts it, hooked up in `AddQuestionServiceData`). Verified on 2026-11-13 under the AppHost: the first start inserted the 32 tags, a restart left the count at 32, and two tags deleted by hand were re-inserted on the next start. The synchronous `Seed` path (used by `dotnet ef database update`) has not been run. `Tag` now checks the slug length after normalising it.

Three gaps to close as part of it (nothing else is left over from items 4 and 5):
- **Admins cannot edit a question.** The plan says "only the author or an admin may change a question", but `Question.Edit` calls `EnsureAuthor`, which rejects everyone except the author.
- **A `DomainException` becomes a 500.** Nothing maps it to an HTTP response yet, so a rule such as "title too long" would return a server error instead of a 400 carrying the error code.
- **`WolverineFx.Http.FluentValidation` is not installed.** The conventions call for it for request validation; the project references only `WolverineFx.Http` and `WolverineFx.RuntimeCompilation`.

## Repository state
- Items 1 to 5 are on `main` and pushed (item 5 was committed before item 4, because the registration needs the context and its entities). There are no open branches.
- Run with `aspire run` from the repo root; stop with Ctrl+C (or `aspire start` / `aspire stop`).
- Verified on 2026-11-11: solution builds with no warnings; under the AppHost, Postgres 18.3 starts, `questions-db` exists, the question service is healthy and receives `ConnectionStrings__questions-db`; `GET /api/account/me` returns 401 without a token.
- Verified on 2026-11-13: the question service builds with no warnings; under the AppHost it starts healthy, applies `20261113222737_Initial`, and `questions-db` contains the six tables plus `__EFMigrationsHistory`, with `author_id` / `voter_id` at 255 characters and the unique `ix_tags_slug` index.
- Not verified: anything that writes data. In particular, removing an accepted answer relies on EF clearing `accepted_answer_id` before deleting the answer row (the FK is `RESTRICT`).
- Verified on 2026-11-13 with `cortex-dev-tools` tokens: `GET /api/account/me` returns 401 without a token and 200 as `reader`; `GET /api/admin/ping` returns 403 as `reader` and 200 as `admin-user`; `/me` returns the right id, name, email and roles for both users through `CurrentUser`, with no Wolverine service-location warning.
- Local Docker volumes in use: `keycloak-data`, `postgres-data`.

## Decisions taken for step 3 (2026-11-11)
- **Layout:** projects live in `src/` and `src/Services/` (moved before migrations exist).
- **Routes:** keep the `/api/...` prefix on every endpoint.
- **No events yet:** writes return only the HTTP result and call `SaveChangesAsync` explicitly. Step 4 adds the event tuples and Wolverine's EF Core transaction middleware.
- **Migrations:** applied at startup, in Development only.
- **IDs:** version 7 GUIDs for entities; the Keycloak user id is stored as a string.
- **Auth:** both the gateway and the services validate JWTs. The unused `Auth:Authority` / `Auth:Audience` settings were deleted.

## Decisions taken for the data layer (2026-11-13)
- **Tags are seeded with `UseSeeding` / `UseAsyncSeeding`, not `HasData`:** `HasData` needs hard-coded keys (tag ids are generated in the domain) and turns every wording change into a migration. Open point: startup migration is Development-only, so a production deploy must run `Migrate` or `dotnet ef database update` for the seed to apply.
- **Snake case by convention** (`EFCore.NamingConventions`) instead of naming every column by hand; id columns stay explicit so they read the same in every table.
- **Composite FK for the accepted answer** was kept (the handler-validation fallback in `docs/architecture.md` was not needed), with `RESTRICT` so deleting an answer can never delete its question.
- **No `AcceptedAnswer` navigation:** the id is the single source of truth.
- **The context is named `QuestionDbContext`**; `docs/architecture.md` was updated to match.

## Gotchas already hit
- **Wolverine 6 wouldn't start** until `WolverineFx.RuntimeCompilation` was added and enabled with `opts.UseRuntimeCompilation()`.
- **Access tokens lacked `sub`** until the `basic` client scope was added to the clients. Now in the realm file.
- **Roles:** the built-in roles scope was removed in favour of a flat `roles` mapper on `cortex-web` and `cortex-dev-tools`, so read `roles`, not `realm_access.roles`.
- **Realm edits need a fresh volume:** Keycloak skips the import when the realm already exists, and the AppHost persists Keycloak in the `keycloak-data` volume. After editing `cortex-realm.json`, delete that volume (or the realm) and restart, otherwise the change is silently ignored.
- **`dotnet run` on the AppHost fails:** with `AspireUseCliBundle`, `dotnet run --project src/Cortex.AppHost` delegates to `aspire run --no-build`, which the CLI rejects while `defaultWatchEnabled` is true in `aspire.config.json` ("The --no-build option cannot be used when watch mode is enabled"). Use `aspire run` from the repo root.
- **First start logs one EF error:** `Failed executing DbCommand ... FROM "__EFMigrationsHistory"` appears once on an empty database, because EF queries the history table before creating it. It is harmless.
- **Checking the EF model without a database:** from the service folder, `env "ConnectionStrings__questions-db=Host=localhost;Database=x;Username=x;Password=x" dotnet ef dbcontext script` builds the model and prints the schema. Without the dummy connection string the design-time host fails.
- **A collection navigation with no inverse is one-to-many by convention:** `Question.Tags` needed an explicit `HasMany().WithMany()`, and explicit FK names in `UsingEntity` to get `tag_id` instead of `tags_id`.
- **A new endpoint needs a restart:** an endpoint file added while the AppHost was running under `aspire start` returned 404 until the AppHost was restarted.
- **A project resource needs `WithHttpHealthCheck` for `aspire wait` to mean "ready":** without one, "healthy" only means the process started, and a script that queried the database right after `aspire wait question-service` saw the state from before migrations and seeding. The question service now has `.WithHttpHealthCheck("/health")` in the AppHost (verified 2026-11-13: the wait returns after the service is listening). Give every new service the same.
- **Postgres password and volume go together:** Aspire generates the Postgres password once and keeps it in the AppHost's user secrets, while the data lives in the `postgres-data` volume. If one is reset without the other, logins fail; delete the volume to start clean.

## Known leftovers
None beyond the three item 6 gaps listed under "Next up".
