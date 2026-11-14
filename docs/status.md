# Cortex: current status

Last updated: 2026-11-14. Update this file when a milestone lands or a decision changes.

## Where things stand
- **AppHost** runs Keycloak (port 6001, `keycloak-data` volume, realm import), Postgres (`postgres` server on fixed port 5432, `postgres-data` volume, database `questions-db`, with pgAdmin) and the question service, which references and waits for both. **RabbitMQ is not in the AppHost yet**; it is added in step 4.
- The question service registers `QuestionDbContext` against `ConnectionStrings:questions-db` (plain `AddDbContext` plus Aspire's `EnrichNpgsqlDbContext`, see "Gotchas already hit"; snake case naming via `EFCore.NamingConventions`) and applies pending migrations at startup in Development.
- **Keycloak** realm `cortex` is imported from `src/Cortex.AppHost/Realms/cortex-realm.json` and working end to end.
- **ServiceDefaults** holds the Aspire defaults plus `AddCortexAuth()` (Keycloak JWT bearer, audience `cortex-api`, `roles` role claim, `AdminOnly` policy).
- **Question service** (`src/Services/Cortex.QuestionService`) has its domain model, EF Core configurations and the `Initial` migration (see "Data layer" below). Wolverine.HTTP 6 is set up as the base for endpoints, with FluentValidation request validation and a `DomainException` handler. `POST /api/questions` is the first CRUD endpoint (see "Question slices" below).
- Existing endpoints, all Wolverine.HTTP:
  - `GET /api/account/me`: static `MeEndpoint`, `[Authorize]`, returns `MeResponse(UserId, UserName, Email, Roles)` built from `CurrentUser`.
  - `GET /api/admin/ping`: `[Authorize(Policy = Policies.AdminOnly)]` (the policy requires the `admin` role). Verified 401 without a token, 403 as `user`, 200 as `admin`.
  - `POST /api/questions`: `[Authorize]`, creates a question for the caller and returns 201 with a `Location` header.
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
6. Question slices: ~~`CreateQuestion`~~ (done), `GetQuestion`, `ListQuestions` (newest first, paged), `UpdateQuestion`, `DeleteQuestion`. Only the author or an admin may change a question.
   - ~~**6.1 Seed the starting tags.**~~ Done. A question needs 1 to 5 tags and there is no endpoint that creates tags until step 5 of the build order, so `CreateQuestion` cannot succeed against an empty `tags` table. The list lives in `Data/TagSeed.cs` and is inserted through EF Core's `UseSeeding` / `UseAsyncSeeding` hooks, which `MigrateAsync` runs: insert-missing by slug, never updating existing rows.
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
- **Domain rules worth knowing:** vote values are validated in `Cast` and `Change` (`*.invalid_value`), a question title is trimmed before its length is checked. The `Errors` classes of `Question` and `Tag` are `internal` so validators can reuse their codes and messages; the others are still private.
- **Migration:** `Data/Migrations/20261113222737_Initial`. It was regenerated once, before the first commit, to fold in the user id length and the slug index; the empty `public` schema of `questions-db` was dropped and recreated for that.

## Infrastructure (2026-11-13)
- `Infrastructure/CurrentUser`: scoped, built from `IHttpContextAccessor`. Exposes `Id` (`sub`, throws when there is no authenticated user), `DisplayName` (`preferred_username`), `Email`, `Roles`, `IsAdmin` and `IsAuthenticated`. Take it as an endpoint method parameter.
- `Infrastructure/WolverineSetup`: `opts.ConfigureQuestionService()`, stage 1 only (`UseRuntimeCompilation()`, plus `UseFluentValidation()` since 2026-11-14). It takes no `IConfiguration` yet; add the parameter when RabbitMQ needs it.
- `Infrastructure/ServiceCollectionExtensions`: `builder.AddQuestionServiceData()` (extends the builder, not `IServiceCollection`, because it needs the configuration and Aspire's `EnrichNpgsqlDbContext` does), `services.AddCurrentUser()`, and `app.ApplyMigrationsAsync()`.
- `Program.cs` matches the shape in `docs/architecture.md`, plus the Development block (`MapOpenApi`, `ApplyMigrationsAsync`) and `UseHttpsRedirection()`, which is kept on purpose (decided 2026-11-13).
- `MeEndpoint` takes `CurrentUser` as its parameter, so it doubles as a live check that the injection works.

## Question slices (2026-11-14)
`CreateQuestion` is written, tested by hand and on `main` (commit `51f91e6`).

- **Slice:** `Features/Questions/CreateQuestion.cs` holds `CreateQuestionRequest(Title, Body, BookId?, TagSlugs)`, its nested `CreateQuestionValidator`, `CreateQuestionResponse` (with a `From(Question)` mapper) and the endpoint. The author id and display name come from `CurrentUser`, never from the body. Rules take their code and message from a domain error through `WithError(DomainError)` in `Infrastructure/ValidationExtensions.cs`, shared by every slice.
- **Tags are sent as slugs.** The endpoint normalises them with `Tag.Normalize` (now public), loads the matching tags (tracked, so EF only writes the join rows) and returns a 400 `ValidationProblem` naming each unknown slug.
- **Validation:** `WolverineFx.Http.FluentValidation` 6.64.0, enabled by `opts.UseFluentValidation()` in `WolverineSetup` and `opts.UseFluentValidationProblemDetailMiddleware()` on `MapWolverineEndpoints`. Rules reuse the domain's length constants and `DomainError` messages. Existence checks stay in the endpoint, because a validator with injected services pushes Wolverine towards service location.
- **Domain errors over HTTP:** `DomainError` has a `Kind` (`DomainErrorKind`: `Validation`, the default, `Forbidden`, `Conflict`). `Infrastructure/DomainExceptionHandler` (an `IExceptionHandler`) maps it to 400 / 403 / 409 and writes a `ProblemDetails` with the message in `detail` and the code in the `code` extension. Registered by `services.AddErrorHandling()` (which also calls `AddProblemDetails()`), with `app.UseExceptionHandler()` first in the pipeline. Only `question.invalid_user` is `Forbidden` so far; nothing uses `Conflict` yet.
- **Who may edit:** `Question.CanBeEditedBy(userId, isModerator)` answers the question and `Question.Edit` no longer takes the editor or checks the author. The edit endpoint must call `CanBeEditedBy` in its `Before` step and return 403. Accepted trade-off: `Edit` does not protect itself, so each mutating endpoint needs a test for the 403.
- **Clock:** endpoints take `TimeProvider` (registered in `Program.cs` with `TryAddSingleton(TimeProvider.System)`) and pass `GetUtcNow()` to the domain.
- **DbContext registration** changed to `AddDbContext` plus `EnrichNpgsqlDbContext`; see "Gotchas already hit".

## Next up: the rest of item 6
`GetQuestion`, `ListQuestions` (newest first, paged), `UpdateQuestion` (with the `CanBeEditedBy` check) and `DeleteQuestion`. `CreateQuestionResponse` will probably become a shared `QuestionResponse` once `GetQuestion` returns the same shape.

## Repository state
- Items 1 to 5, item 6.1 (tag seeding, with the question service health check in the AppHost) and the `CreateQuestion` slice of item 6 are on `main` and pushed (item 5 was committed before item 4, because the registration needs the context and its entities). There are no open branches.
- Run with `aspire run` from the repo root; stop with Ctrl+C (or `aspire start` / `aspire stop`).
- Verified on 2026-11-11: solution builds with no warnings; under the AppHost, Postgres 18.3 starts, `questions-db` exists, the question service is healthy and receives `ConnectionStrings__questions-db`; `GET /api/account/me` returns 401 without a token.
- Verified on 2026-11-13: the question service builds with no warnings; under the AppHost it starts healthy, applies `20261113222737_Initial`, and `questions-db` contains the six tables plus `__EFMigrationsHistory`, with `author_id` / `voter_id` at 255 characters and the unique `ix_tags_slug` index.
- Verified by hand on 2026-11-14 (Bruno, `cortex-dev-tools` token) against `POST /api/questions`: 401 without a token; 201 for a valid request; 400 for an empty body, an unknown tag and six tags, the last returning "A question needs between 1 and 5 tags." under `TagSlugs`.
- Verified by hand on 2026-11-14: with the `NotEmpty` rule on `Title` commented out, an empty title reaches the domain and `DomainExceptionHandler` returns 400 `Bad Request` with `detail` "The question title must not be null or empty." and `code` `question.empty_title`. The 403 and 409 mappings have not been exercised.
- Not verified: that `/health` still reports Postgres after the switch to `EnrichNpgsqlDbContext`.
- Not verified: updates and deletes. In particular, removing an accepted answer relies on EF clearing `accepted_answer_id` before deleting the answer row (the FK is `RESTRICT`).
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
- **Aspire's `AddNpgsqlDbContext` breaks Wolverine 6 endpoints that take the `DbContext`** (hit 2026-11-14 on `POST /api/questions`): the request fails with `InvalidServiceLocationException ... 'opaque' lambda factory with the Scoped lifetime`. Wolverine generates the endpoint code and wants to construct the context inline; Aspire registers it through a factory lambda, which Wolverine cannot see into, so it would have to ask the container at runtime (service location). Wolverine 5 allowed that with a warning; Wolverine 6 defaults to `ServiceLocationPolicy.NotAllowed`. Fix: register the context with `builder.Services.AddDbContext<T>(..., optionsLifetime: ServiceLifetime.Singleton)` (the lifetime the Wolverine EF Core docs recommend) and call `builder.EnrichNpgsqlDbContext<T>()` after it for Aspire's health check, retries and telemetry. Trade-off: no context pooling. Rejected alternative: `opts.CodeGeneration.AlwaysUseServiceLocationFor<T>()`, one line but it keeps service location, which the docs warn can break scoping once the outbox is in. Step 4 replaces the registration with `AddDbContextWithWolverineIntegration`, which has the same shape. Do the same in every new service that has a `DbContext`.
- **FluentValidation's `Must` has a useless default message** ("The specified condition was not met for 'Tag Slugs'."). Always give it one; `WithError(DomainError)` does.
- **FluentValidation error codes do not reach the client:** Wolverine's problem-details middleware emits only property names and messages, so the message is all a client gets from a validator failure. Codes only appear in `DomainExceptionHandler` responses.
- **FluentValidation keeps running a rule chain after a failure:** `NotNull().Must(x => x.Count ...)` throws on null. Use `.Cascade(CascadeMode.Stop)` or a null-safe predicate.
- **Validation error keys are the C# property names** (`TagSlugs`, not `tagSlugs`). A hand-built `ValidationProblem` should use `nameof(...)` so both 400s agree.
- **The validator answers before the domain is reached:** the FluentValidation middleware short-circuits the request, so a request that breaks both a validator rule and a rule only the domain checks returns only the validator's errors (seen 2026-11-14: six tags plus an empty title, with the title rule disabled, reported only the tag count). The domain error appears once the validator passes.

## Known leftovers
- An unused `using Microsoft.Extensions.DependencyInjection.Extensions;` in `Infrastructure/ServiceCollectionExtensions.cs`.
- `docs/architecture.md` still shows the `Program.cs` shape without `AddErrorHandling()`, `UseExceptionHandler()` and the FluentValidation middleware option.
