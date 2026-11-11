# Cortex: current status

Last updated: 2026-11-11. Update this file when a milestone lands or a decision changes.

## Where things stand
- Aspire AppHost runs Postgres, RabbitMQ and Keycloak.
- Keycloak realm `cortex` is imported from `realm.json` and working end to end.
- **Question service** (`Cortex.QuestionService`) is just starting. Wolverine.HTTP is set up as the base for endpoints. No CRUD for questions or answers yet.
- Existing endpoints, both converted from Minimal APIs to Wolverine.HTTP:
  - `GET /api/account/me`: static `MeEndpoint`, `[Authorize]`, returns `MeResponse(UserId, UserName, Email, Roles)` read from claims (`sub`, `Identity.Name`, `email`, flat `roles`).
  - `GET /api/admin/ping`: `[Authorize(Roles = "admin")]`. Verified 401 without a token, 403 as `user`, 200 as `admin`.

## Next up (build order step 3)
CRUD for questions and answers as Wolverine.HTTP endpoints in the vertical-slice layout, with EF Core on Postgres. No messaging yet.

## Gotchas already hit
- **Wolverine 6 wouldn't start** until `WolverineFx.RuntimeCompilation` was added (`TypeLoadMode.Dynamic`).
- **Access tokens lacked `sub`** until the `basic` client scope was added to the clients. Now in `realm.json`.
- **Roles:** the built-in roles scope was removed in favour of a flat `roles` mapper on `cortex-web` and `cortex-dev-tools`, so read `roles`, not `realm_access.roles`.

## Note on layout
`docs/architecture.md` shows a `src/Services/Cortex.QuestionService` layout. The project currently sits at the repo root (`Cortex.QuestionService/`). Decide whether to move it before the service grows.
