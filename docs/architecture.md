# Cortex: Architecture (draft v0.5)

Status: working draft. Decisions are listed below; everything else is still a proposal.

## Decisions so far
- **RabbitMQ** is the message transport.
- **Open Library** is the source of book metadata.
- **YARP** is the API gateway.
- **TanStack Query** handles server data on the frontend; Zustand is for UI state only.
- **Questions service owns Answers, Votes and Tags.** No separate Answers or Tags service for now (see "Service boundaries").
- **A question references a book by ID only** (the Open Library key). Questions never manages or validates books.
- **One accepted answer per question**, stored as a nullable `AcceptedAnswerId` on Questions (no flag on Answers).
- **Two vote tables:** `question_votes` and `answer_votes`.
- **Wolverine.HTTP endpoints for all HTTP routes** (reads and writes), not Minimal APIs or controllers, organized per feature (see "Service project layout").
- **Vertical-slice project layout** for every service (see "Service project layout").
- **gRPC is deferred to v1.2** as an optional add-on around Reputation (see "Planned add-ons").

## Principles
- Start with few services and split only where a boundary is clear.
- Each service owns its data. No cross-service database reads or writes.
- Services talk through events (Wolverine), with synchronous HTTP only through the gateway.
- Use the outbox pattern so a database write and the event it publishes succeed or fail together.

## Services
| Service | Owns | Notes |
|---|---|---|
| **Questions** | questions, answers, tags, votes | Core of the Q&A half. Publishes `QuestionPosted`, `AnswerPosted`, `AnswerAccepted`, `VoteCast`. Project: `Services/Cortex.QuestionService`, Aspire resource `question-service`. |
| **Profiles** | user profiles, book catalog entries, shelf entries, reviews | The Fable-style half. Keyed to the Keycloak user id. Talks to Open Library. |
| **Reputation** | reputation scores | Pure event consumer. Can be folded into Questions at first. |
| **Search** | read-optimized search index | Consumes events. Postgres full-text first. |
| **Notifications** | notification records | Consumes events. Added later. |
| **Gateway (YARP)** | none | Single entry point, JWT validation, routing. |

Start with **Questions + Profiles + Gateway**. Add the rest when the MVP works.

## Service boundaries (where to draw the line)
Size is not the measure; consistency is. Tests used when deciding whether to split:
1. **Shared invariants stay together.** If a rule spans two entities and must always hold, they live in one service.
2. **Different reasons to change or scale** (release cadence, load profile, owner) justify a split.
3. **Independence under failure:** can A do something useful when B is down?
4. **Team boundaries:** a solo project has none, so every extra service is pure overhead.

Applied to Questions:
- **Answers stay inside Questions.** They are part of the same aggregate (accepted answer must belong to its question, votes, cascade delete). Splitting would trade real FKs and transactions for sagas.
- **Tags stay inside Questions for now.** A split becomes plausible only if tags grow their own features (descriptions or wiki, synonyms, followed tags, curation). Questions would then keep `QuestionTags` and the Tags service would own tag definitions.
- Good candidates for new services are event-driven, reaction-style features (search, notifications, reputation, analytics) and independent features with their own data (reading challenges, book clubs, curated lists). New services must not write into another service's database; they store references by ID and learn about other data through events or API calls.
- Watch-out: a distributed monolith (services that cannot change or run independently, but carry all the network and ops cost).
- Adding a new consumer service does not touch existing deployments, but a new feature may require an existing service to publish a new event or field. Evolve event contracts compatibly (new optional fields, never rename or remove).

## Service project layout (reusable template)
Applies to every Cortex service, and is the reference layout for other .NET service projects. Feature-based (vertical slice): everything for one use case sits together, rather than layered `Controllers/`, `Services/`, `Dtos/` folders. HTTP is served by **Wolverine.HTTP** (`WolverineFx.Http`), so each use case is a static endpoint class that Wolverine discovers; there are no `MapGroup` wiring files.

Example for the Questions service:
```
Services/Cortex.QuestionService/
  Program.cs
  appsettings.json

  Features/
    Questions/
      CreateQuestion.cs           // request record, validator, endpoint class, response
      GetQuestion.cs
      ListQuestions.cs
      UpdateQuestion.cs
      DeleteQuestion.cs
      AcceptAnswer.cs
    Answers/
      PostAnswer.cs               // [WolverinePost("/questions/{questionId}/answers")]
      UpdateAnswer.cs
      DeleteAnswer.cs
    Votes/
      VoteOnQuestion.cs
      VoteOnAnswer.cs
    Tags/
      ListTags.cs
    Events/                       // in-service handlers for events this service reacts to (optional)

  Domain/
    Question.cs
    Answer.cs
    QuestionVote.cs
    AnswerVote.cs
    Tag.cs

  Data/
    QuestionsDbContext.cs
    Configurations/               // IEntityTypeConfiguration<T> per entity
      QuestionConfiguration.cs    // composite FK for AcceptedAnswerId lives here
      AnswerConfiguration.cs
      ...
    Migrations/

  Infrastructure/
    CurrentUser.cs                // reads the Keycloak user id from claims
    WolverineSetup.cs             // UseWolverine options: discovery, EF Core, outbox, RabbitMQ routing
    ServiceCollectionExtensions.cs // AddQuestionServiceData(), AddQuestionAuth()...
```

### Rules of the layout
- **`Features/<Resource>/<UseCase>.cs`**: one file per use case, holding its request and response records, an optional validator, and a static endpoint class with one method marked `[WolverineGet]`, `[WolverinePost]`, `[WolverinePut]` or `[WolverineDelete]`. The route lives on the attribute, so the file is self-contained.
- **No `<Resource>Endpoints.cs` files.** Wolverine discovers endpoint classes in the service assembly when `app.MapWolverineEndpoints()` runs. Shared concerns that `MapGroup` used to cover are handled by standard ASP.NET Core attributes on the class or method (`[Authorize]`, `[Tags("Questions")]`) or by policies set in `MapWolverineEndpoints(opts => ...)`.
- **Method injection, no constructors.** Dependencies (`QuestionsDbContext`, `CurrentUser`, `CancellationToken`, route values, the request body) are parameters of the endpoint method.
- **Load / Validate / Handle split** (Wolverine's compound handler conventions) for endpoints that act on an existing entity: a `Load` or `Before` method fetches the entity and can return `NotFound`/`ProblemDetails` to stop the request, so the main method only contains the business logic. Validation through `WolverineFx.Http.FluentValidation` middleware.
- **Writes return their events.** An endpoint that changes state returns a tuple of the HTTP result and the event(s), for example `(Created<AnswerDto>, AnswerPosted)`. Wolverine publishes the returned event as a cascading message; with the outbox enabled it is stored in the same transaction as the EF Core changes. Endpoints do not inject `IMessageBus` unless they need something cascading cannot do (for example scheduling).
- **Reads are plain endpoints.** Queries return `Results<Ok<T>, NotFound>` and publish nothing. Same attribute style as writes, so there is one way of doing HTTP in the service.
- **`Domain/`**: entities and their rules (for example `question.Accept(answer)` checking the answer belongs to it). No EF Core, HTTP or Wolverine concerns.
- **`Data/`**: DbContext, one `IEntityTypeConfiguration<T>` per entity (constraints, check constraints, composite keys), and migrations. Mapping stays out of the entities.
- **`Infrastructure/`**: cross-cutting pieces (current user, DI registration, Wolverine configuration).
- **Events are not in the service.** They live in `Cortex.Contracts` so consumers reference them without referencing the service. Handlers for events consumed by a service go under `Features/` next to the feature they update, or `Features/Events/` when they span features.
- **One project per service.** Folders, not separate Application/Infrastructure projects, until a service genuinely grows.
- Endpoints use `TypedResults` with `Results<...>` return types so OpenAPI output stays accurate.

### Example use-case files
A read:
```csharp
public static class GetQuestionEndpoint
{
    [Tags("Questions")]
    [WolverineGet("/questions/{id}")]
    public static async Task<Results<Ok<QuestionDto>, NotFound>> Get(
        Guid id, QuestionsDbContext db, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([id], ct);
        return question is null ? TypedResults.NotFound() : TypedResults.Ok(question.ToDto());
    }
}
```

A write that loads an entity first and publishes an event:
```csharp
public record PostAnswer(string Body);

public static class PostAnswerEndpoint
{
    // Runs first; returning a non-null IResult stops the request
    public static async Task<(Question?, IResult)> LoadAsync(
        Guid questionId, QuestionsDbContext db, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([questionId], ct);
        return question is null
            ? (null, TypedResults.NotFound())
            : (question, WolverineContinue.Result());
    }

    [Authorize]
    [Tags("Answers")]
    [WolverinePost("/questions/{questionId}/answers")]
    public static (Created<AnswerDto>, AnswerPosted) Post(
        PostAnswer cmd, Question question, CurrentUser user, QuestionsDbContext db)
    {
        var answer = question.AddAnswer(cmd.Body, user.Id);
        db.Answers.Add(answer);   // saved by Wolverine's EF Core transaction middleware

        return (
            TypedResults.Created($"/questions/{question.Id}/answers/{answer.Id}", answer.ToDto()),
            new AnswerPosted(answer.Id, question.Id, user.Id));
    }
}
```
Check the exact `Load` return conventions against the Wolverine.HTTP docs for the version in use; the shape above shows the intent.

### `Program.cs`
Stays short:
```csharp
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddQuestionServiceData(builder.Configuration);

builder.Host.UseWolverine(opts => opts.ConfigureQuestionService(builder.Configuration));
builder.Services.AddWolverineHttp();

var app = builder.Build();

app.MapDefaultEndpoints();     // /health and /alive from ServiceDefaults, needed by Aspire
app.MapWolverineEndpoints();   // discovers every [WolverineGet/Post/...] in the assembly
```

`ConfigureQuestionService` (in `Infrastructure/WolverineSetup.cs`) grows in stages:
1. **Endpoints only:** nothing beyond the defaults. Handler and endpoint discovery covers the service assembly.
2. **Local events:** return events from endpoints and handle them in-process to see cascading work, no broker yet.
3. **Outbox:** EF Core transaction integration, Postgres message persistence, auto-applied transactions.
4. **RabbitMQ:** `WolverineFx.RabbitMQ`, connection from Aspire, publish rules for `Cortex.Contracts` events. Endpoint code does not change at this stage, only routing.

### Why Wolverine.HTTP over Minimal APIs
- It builds on ASP.NET Core endpoint routing, so health checks, auth, OpenAPI and YARP behave the same.
- Endpoints can return events next to the HTTP result, and Wolverine publishes them through the outbox in the same transaction. With Minimal APIs this needs `IMessageBus` injected and an explicit transaction.
- Method injection and the Load/Handle split keep each use case to one small static class, which matches the vertical-slice layout.
- One programming model for HTTP endpoints and message handlers (same middleware, same conventions).
- Trade-off: more convention and "magic" than Minimal APIs (discovery, generated code), and fewer community examples. Minimal APIs remain a valid fallback for a single endpoint if needed, and both can live in the same app.

## Questions service: data model
Entities (all inside the service boundary): `Questions`, `Answers`, `QuestionVotes`, `AnswerVotes`, `Tags`, `QuestionTags` (join table).

**Relationships**
- Questions 1 — N Answers (composition; answers cannot exist without their question).
- Questions 1 — N QuestionVotes, Answers 1 — N AnswerVotes (composition).
- Questions N — N Tags through `QuestionTags`.
- Questions to Books: dashed reference by ID only. Books `0..1` — `0..N` Questions (a question references zero or one book; a book can have zero or many questions). Books live outside the service boundary.
- Questions to Answers, accepted answer: optional association, `0..1` on both ends.

**Votes**
- `question_votes`: `QuestionId` (FK), `UserId` (Keycloak id, no FK), `Value`, `CreatedAt`. PK (`QuestionId`, `UserId`).
- `answer_votes`: `AnswerId` (FK), `UserId`, `Value`, `CreatedAt`. PK (`AnswerId`, `UserId`).
- `Value` is `smallint` with `CHECK (Value IN (-1, 1))`. Retracting a vote deletes the row; flipping a vote updates `Value`.
- Two tables rather than one polymorphic table so each has a real FK with cascade delete.
- Score is `SUM(Value)` at first. If sorting by top gets slow, add a denormalized `Score` column on Questions and Answers, updated when a vote is cast (a natural point to publish `VoteCast`).

**Accepted answer**
- One accepted answer per question: nullable `AcceptedAnswerId` on Questions. No `IsAccepted` flag on Answers (two sources of truth would drift).
- Integrity: composite FK (`Id`, `AcceptedAnswerId`) → Answers (`QuestionId`, `Id`), which requires a unique constraint on Answers (`QuestionId`, `Id`). The column is nullable because the two tables reference each other: insert the question first, set the accepted answer afterward. Validating in the handler is an acceptable fallback if the composite FK feels heavy.
- Reads: load the question's answers and sort the accepted one first (`ORDER BY (Id = q.AcceptedAnswerId) DESC, Score DESC`); compute `isAccepted` per answer in the DTO. Lists use `AcceptedAnswerId IS NOT NULL` for the "answered" check.
- If multiple accepted answers were ever wanted, flip the design: `IsAccepted` on Answers and drop the column on Questions.

**Book reference**
- `Questions.BookId` is nullable and holds the Open Library work key (for example `OL45883W`).
- Questions never fetches or validates the book. The frontend gets book details (title, author, cover) through the gateway from the service that owns them and joins in the UI.
- Optional later: a small denormalized snapshot on the question (`BookTitle`, `BookCoverUrl`) filled at post time, for list views. It is a copy, so slight staleness is fine.
- Any user can ask about any book. The reference does not depend on the asker having the book on a shelf.

## Profiles service: book model (to be detailed)
Two separate concepts, kept as separate entities:
- **Book (catalog):** the global record, identified by the Open Library key. Not owned by any user.
- **ShelfEntry:** links a user to a book with status (reading, want to read, finished), rating and review. This is what belongs to a user's profile.

Questions only needs the first concept (the key).

## Event flow example
1. User posts an answer: Gateway routes to Questions.
2. The `PostAnswer` Wolverine.HTTP endpoint saves the answer and returns `AnswerPosted`; Wolverine writes it to the outbox in the same transaction.
3. Wolverine delivers the event through RabbitMQ to Reputation, Search and Notifications.
4. Each consumer updates its own store. No consumer blocks the original request.

## Messaging
- RabbitMQ, run locally through Aspire.
- Wolverine handles routing, retries, dead-letter queues and the Postgres-backed outbox.
- Events are immutable, versioned contracts in a shared `Cortex.Contracts` project.
- Prefer cascading messages (returning events from endpoints and handlers) over injecting `IMessageBus`.

## Gateway (YARP)
YARP is Microsoft's reverse proxy library. The gateway is a small ASP.NET project where routes and clusters are configured in `appsettings.json`. It gives us one public entry point, JWT validation in one place, and routing to each service without the frontend knowing where they live. It integrates with Aspire service discovery.

## Book data (Open Library)
- Profiles is the only service that calls Open Library, so no other service depends on it.
- Open question: live calls with caching, or copying a book record into our own database the first time a user adds it. Copying keeps pages fast and protects us from API downtime or changes, at the cost of staleness.
- Respect their usage guidelines (identify the app in the User-Agent and cache responses).

## Data
- One PostgreSQL server in dev (Aspire), separate database per service.
- EF Core or Marten for persistence. Marten fits Wolverine well but is optional.

## Auth
- Keycloak issues JWTs. The gateway validates them and services trust the validated claims.
- Roles kept minimal at first: `user` and `admin`.

## Frontend
- Next.js App Router with server components for reading pages.
- Calls go to the gateway only, over REST. Auth via an OIDC library (for example Auth.js with the Keycloak provider).
- TanStack Query for fetched data, caching and mutations. Zustand only for local UI state (modals, drafts, filters).

## Solution layout
```
Cortex/
  src/
    Cortex.AppHost/          # Aspire orchestration
    Cortex.ServiceDefaults/
    Cortex.Contracts/        # shared event types
    Cortex.Gateway/          # YARP
    Services/
      Cortex.QuestionService/
      Cortex.ProfileService/
  web/                       # Next.js app
  tests/
```

AppHost wiring per service: `AddProject<...>("<name>-service")` with `.WithReference(...)` and `.WaitFor(...)` for Keycloak, its database and RabbitMQ.

## Build order
1. AppHost with Postgres, RabbitMQ and Keycloak running
2. Gateway plus auth round trip from Next.js
3. Questions service: CRUD for questions and answers as Wolverine.HTTP endpoints (no messaging yet)
4. Wolverine events and outbox between Questions and one consumer
5. Voting, then Profiles with Open Library lookups
6. Search, Notifications, Reputation
7. Docker images, then Azure deployment

## Planned add-ons
- **v1.2: gRPC around Reputation (optional, learning-driven).** Questions would query Reputation synchronously (for example "does this user have enough reputation to downvote?") over internal gRPC.
  - Alternative to compare against: Questions keeps a local copy of each user's reputation, updated from Reputation's events. This avoids the runtime dependency at the cost of eventual consistency; it is the default choice that fits our principles.
  - Trade-off to explore: if Reputation is down, voting fails unless a fallback is added.
  - gRPC stays internal. The browser keeps using REST through YARP (browsers cannot speak native gRPC without gRPC-Web). Not part of the MVP.
- Other possible later services: reading challenges, book clubs, curated reading lists, a dedicated Tags service if tags grow their own features.

## Risks and watch-points
- Too many services too early: the main cost for a solo learner.
- Eventual consistency in the UI (votes and reputation lag): decide how the frontend handles it.
- Keycloak setup friction: script the realm import so it is repeatable.
- Open Library availability and rate limits: another reason to cache or copy records.
- Circular FK between Questions and Answers (accepted answer): nullable column and insert order matter.
- Forgetting `app.MapDefaultEndpoints()` in a service breaks Aspire health checks and `WaitFor`.
- Wolverine discovery is assembly-scoped: endpoints or handlers in another assembly (for example `Cortex.Contracts`) are not found unless added with `opts.Discovery.IncludeAssembly(...)`.
