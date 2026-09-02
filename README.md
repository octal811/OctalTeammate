<img src="Docs/Assets/OctalPusle.jfif" alt="OctalPulse" width="100%" />

# OctalPulse — Team Collaboration & Project Management Platform

**OctalPulse** is a modern, cross-platform team-collaboration and project-management platform. It helps teams plan work, schedule events, manage tasks across tracks, share documents, and collaborate in real time — all from one place.

It is being built **backend-first**: a feature-complete REST API + real-time layer powers the project today, with dedicated **desktop** and **mobile** clients planned on top of the same API (see [Future Clients](#frontends-desktop--mobile--coming-next)).

---

## What OctalPulse Does

OctalPulse is a full lifecycle tool for managing a team and its work:

- **Projects** — create containers for team efforts; the creator is auto-joined as the Project Manager.
- **Tracks** — workstreams inside a project (e.g. AI, Backend, Mobile). Members join tracks to receive scoped notifications and log work.
- **Major & Minor Tasks** — hierarchical task breakdown. Completing minor tasks drives **automatic progress** up the chain (MinorTask → MajorTask → Track → Project).
- **Events & Calendar** — meetings, deadlines, reminders, and milestones per project, shared with the whole team in real time.
- **Membership & Roles** — project membership with **approval status** (`Approved` / `Pending`); only approved members can read/write and receive real-time events. Users hold a main role plus many per-project roles.
- **Real-time collaboration** — SignalR pushes project, track, task, and event changes instantly to precisely-scoped audiences.
- **Authentication & Security** — JWT access + rotating refresh tokens, email verification, password reset via OTP, and admin-controlled API availability toggles.

### Target Audience

OctalPulse is designed for **software teams and any group that manages projects, workstreams, and calendars together** — from small startups running a single product to larger teams with several concurrent projects across frontend, backend, mobile, DevOps, design, and QA tracks.

---

## What Has Been Done So Far

The **backend is complete and verified end-to-end**. It is a real, tested application, not a skeleton:

### Core Features (Implemented & Working)

| Area | Status |
|---|---|
| **Auth** — register, login, refresh-token rotation, logout/revoke | ✅ |
| **Email OTP flows** — email verification & password reset | ✅ |
| **Security** — JWT access tokens, single-use hashed refresh tokens, per-user concurrent-operation lock | ✅ |
| **Admin control** — runtime API availability toggles (e.g. disable registration/login) | ✅ |
| **Projects** — CRUD + paginated list + members | ✅ |
| **Tracks** — CRUD + list-by-project | ✅ |
| **Major / Minor tasks** — CRUD with **auto-computed progress** cascade | ✅ |
| **Events** — project-scoped CRUD + **year/month retrieval returning all events** (no pagination cap) | ✅ |
| **Membership gates** — every read/write checks **approved project/track membership**; task & event edits are **creator-only** | ✅ |
| **Real-time (SignalR)** — `projectChanged`, `trackChanged`, `majorTaskChanged`, `minorTaskChanged`, `eventChanged` pushed to scoped groups | ✅ |
| **Soft deletes + audit trail** — rows are flagged, never hard-deleted; `CreatedByUserId` / `DeletedByUserId` recorded | ✅ |
| **Documented** — `Docs/API.md`, `Docs/Database.md`, `Docs/Realtime.md` kept up to date | ✅ |

---

## Backend — Technologies, Tools & Packages

### Architecture

**Clean Architecture / Domain-Driven Design**, split into four **.NET 10** projects (C#):

```
OctalPulse.API            → HTTP layer: controllers, SignalR hub, notifier, middleware (Startup)
OctalPulse.Application    → Use cases: MediatR commands/queries/handlers + FluentValidation validators
OctalPulse.Domain         → Core domain entities (DDD), enums, marker/interfaces
OctalPulse.Infrastructure → EF Core DbContext + migrations, Identity, email, JWT services
```

### Core Technologies

| Technology | Role |
|---|---|
| **.NET 10** | Runtime & framework (`net10.0`) |
| **ASP.NET Core Web API** | REST endpoints, middleware pipeline, `[Authorize]` |
| **SignalR** | Real-time collaboration hub (`/hubs/collaboration`) |
| **Entity Framework Core 10** | ORM / data access |
| **Microsoft SQL Server** (LocalDB) | Relational database |
| **ASP.NET Core Identity** | Users, roles, password hashing, 2FA foundation |
| **JWT Bearer** | Stateless access-token authentication |
| **Clean Architecture** | Layered, testable, framework-agnostic core |

### Tools

| Tool | Use |
|---|---|
| **Visual Studio 2022 / .NET CLI** | Build, run, manage the solution (`OctalPulse.slnx`) |
| **`dotnet ef` migrations** | Schema versioning — a dedicated `Persistence/Migrations` folder |
| **SQL Server LocalDB** | Local development database |
| **Swagger / OpenAPI** | Interactive API docs at runtime + OpenAPI spec |
| **Serilog** | Structured file logging under `logs/` |

### NuGet Packages

| Package | Project | Purpose |
|---|---|---|
| `MediatR` | Application | CQRS — commands, queries, handlers |
| `FluentValidation.DependencyInjectionExtensions` | Application | Declarative request validation |
| `Microsoft.EntityFrameworkCore.SqlServer` | Infrastructure | SQL Server EF provider |
| `Microsoft.EntityFrameworkCore.Tools` | Infrastructure | Design-time migrations |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | API / Infra / Domain | Identity + EF storage |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | API | JWT auth middleware |
| `MailKit` | Infrastructure | Sending emails (email verification / password reset) |
| `System.IdentityModel.Tokens.Jwt` | Infrastructure | JWT token generation |
| `Microsoft.Extensions.Caching.Memory` | Infrastructure | In-memory OTP storage |
| `Serilog.AspNetCore` | API | Structured logging |
| `Swashbuckle.AspNetCore` | API | Swagger UI / OpenAPI docs |
| `Microsoft.OpenApi` | API | OpenAPI model generation |

> **HTTP API details** — base URL `http://localhost:5009/api`, authenticated via `Authorization: Bearer <accessToken>`. Every feature, request/response shape, and status code is documented in **[`Docs/API.md`](Docs/API.md)**.

---

## Front-Ends — Desktop & Mobile (Coming Next)

OctalPulse is **API-first**, so the same backend will power every client. Dedicated client documents will be added as each frontend is planned:

- **[`Docs/Client-Desktop.md`](Docs/Client-Desktop.md)** *(planned)* — desktop app technologies, tooling, and packages.
- **[`Docs/Client-Mobile.md`](Docs/Client-Mobile.md)** *(planned)* — mobile app technologies, tooling, and packages.

Each will follow the format above: framework/stack, tools, and packages, so the whole team has one place to read what each layer is built with.

---

## Documentation

| Document | Contents |
|---|---|
| [`Docs/API.md`](Docs/API.md) | Full HTTP API reference — every endpoint, body, and response |
| [`Docs/Database.md`](Docs/Database.md) | Data model, EF mappings, migrations, relationships |
| [`Docs/Realtime.md`](Docs/Realtime.md) | SignalR hub, connection/auth, groups, event names |
| [`Docs/Client-Desktop.md`](Docs/Client-Desktop.md) | *(planned)* Desktop client stack |
| [`Docs/Client-Mobile.md`](Docs/Client-Mobile.md) | *(planned)* Mobile client stack |

---

## Getting Started

1. **Run the application** from the solution folder:

   ```bash
   dotnet build OctalPulse.slnx
   # then run the API project (Development environment)
   ```

2. **Export the framework reference** — the runtime listens on `http://localhost:5009/api`, and the real-time hub on `/hubs/collaboration`.
3. **Apply the latest schema** if the database is out of date:

   ```bash
   dotnet ef database update --project OctalPulse.Infrastructure --startup-project OctalPulse.API
   ```

4. **Register** via `POST /api/auth/register`, then carry the returned `accessToken` as a Bearer header.

---

## License

This project is licensed under the terms in the [`LICENSE`](LICENSE) file.