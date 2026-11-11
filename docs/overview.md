# Cortex: Project Brief

## What it is
Cortex is a Stack Overflow-style Q&A platform for readers. People ask and answer questions about books, authors and reading, and each user also keeps a book profile (shelves, reading status, reviews) in the spirit of the Fable app.

It is also a learning project. The goal is to build a realistic distributed system with the current .NET stack and understand the trade-offs, rather than ship the simplest possible app.

## Two product halves
1. **Q&A (the "Stack Overflow" side):** questions, answers, tags, votes, accepted answers, reputation and popularity ranking.
2. **Library (the "Fable" side):** a user profile with books, shelves (reading / want to read / finished), ratings and short reviews.

The link between them: a question can reference a book, and a book page shows the questions asked about it.

## Decisions
| Topic | Decision |
|---|---|
| Message transport | **RabbitMQ** (via Wolverine) |
| HTTP endpoints | **Wolverine.HTTP** (not Minimal APIs or controllers) |
| Book metadata | **Open Library** API |
| API gateway | **YARP** |
| Frontend server data | **TanStack Query** (Zustand for UI state only) |

## Tech stack
| Area | Choice |
|---|---|
| Backend | .NET 10, C# |
| Orchestration (dev) | .NET Aspire |
| HTTP endpoints | WolverineFx.Http |
| Messaging / handlers | WolverineFx on RabbitMQ, with a durable Postgres outbox |
| Database | PostgreSQL (one database or schema per service) |
| Gateway | YARP |
| Auth | Keycloak (OIDC) |
| Frontend | Next.js (App Router), TypeScript, Tailwind |
| Client state | Zustand (UI state), TanStack Query (server data) |
| Book data | Open Library API |
| Deployment | Docker, then Azure |

## MVP (first milestone)
- Sign in through Keycloak
- Ask a question, post an answer, vote on both
- Basic tags and a question list sorted by newest and top
- Minimal profile page

## Later milestones
- Book profile: shelves, ratings, reviews, with books looked up from Open Library
- Link questions to books
- Search
- Notifications
- Reputation, plus Azure deployment

## Non-goals (for now)
- Moderation tooling and an admin back office
- Recommendation engine
- Mobile app
- Multi-region or heavy scale work

## Open questions
- Is search Postgres full-text first, or a dedicated engine?
- Does reputation live in its own service or inside Questions?
- Open Library: call it live and cache, or copy book records into our own database when a user first adds a book?
