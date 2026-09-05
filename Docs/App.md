# OctalTeammate — Application Overview

This document explains **what OctalTeammate is**, **who it targets**, and **how the app flows** end to end. It is the high-level companion to `Database.md` (schema), `API.md` (endpoints), `Middleware.md` (request pipeline), and `Realtime.md` (SignalR collaboration).

## Idea

OctalTeammate is a **team management & collaboration platform** for software teams. It organizes work hierarchically and keeps everyone in sync:

```
Project
   └── Track            (workstream: AI, Backend, Mobile, ...)
          └── MajorTask  (feature / epic-level task)
                 └── MinorTask  (small actionable unit of work)
```

The design is intentionally **hierarchical and role-aware**:

- **Projects** group a team around a goal (e.g. "OctalPulse mobile app").
- **Tracks** are the verticals *inside* a project (e.g. "AI", "Backend", "Mobile"). A track has a **lead** who curates it and gates membership.
- **Major tasks** are the deliverables a track owns.
- **Minor tasks** are the concrete, finishable work items. **Completing them drives progress automatically**:

```
MinorTask done (state = Done)
   → MajorTask progress = done / total × 100
      → marking a MajorTask Done (state = Done)
         → Track progress = Done major tasks / total × 100
            → Project progress = Done major tasks / total × 100
```

Everyone carries a **main role** (their specialty: `BackEnd`, `Mobile`, `Designer`, ...) plus **one or more roles per project**. Members join tracks to receive notifications and log work. The system emails members about task updates (and about verification / password resets), and a calendar of events (meetings, deadlines, milestones) keeps the schedule visible.

## Target

| Audience | Who they are | What they get |
|----------|--------------|---------------|
| **Team members** | Developers, designers, QA, PMs, DevOps | Join projects & tracks, own tasks, complete minor tasks, receive email notifications, see progress |
| **Track leads** | Users who lead a workstream | Curate their track, admit members, guard the track from accidental deletion |
| **Administrators** | App operators (`Rank = Admin`) | Maintain the app: handle problems, and control API availability at runtime |

Administrators are still regular members — they keep their own `MainRole` and project roles; the `Admin` rank only unlocks app-level capabilities (e.g. the API availability panel). Admin-gated routes are authorized against the live `User.Rank` value in the database, not a JWT claim, so a rank change applies immediately.

## Architecture at a Glance

Clean Architecture, four projects:

```
OctalPulse.Domain           domain entities, enums, base interfaces (no dependencies)
OctalPulse.Application      use cases / commands (MediatR), service & repository interfaces
OctalPulse.Infrastructure   EF Core (SQL Server), Identity, repositories, services (email, JWT, cache, OTP)
OctalPulse.API              HTTP layer: controllers, middleware, attributes, DI + pipeline
```

Rules that hold the design together:

- **Controllers are thin.** They only translate HTTP → MediatR commands (folders like `Features/Command/Auth/Register`). Business logic lives in command handlers inside Application.
- **Everything goes through interfaces.** The API depends on Application interfaces (`IApiAvailabilityService`, `IOtpService`, `IUserOperationLock`, repositories) — never `DbContext` directly.
- **Soft delete everywhere.** Rows are flagged `IsDeleted`, never hard-deleted.
- **Guid identity.** Every key is a `Guid`.
- **Auditing.** Auditable tables carry `CreatedDate` / `ModifiedDate`.
- **Progress is auto-calculated.** Never accepted as input and never stored — `IProgressCalculator` computes it **at read time** from child task states (major task = % Done minor tasks; track & project = % Done major tasks). Derived fresh on every response.

## Feature Highlights (implemented so far)

### 1. Authentication & accounts
- JWT access token (15 min) + rotating **refresh token** (7 days, hashed, single-use).
- Register, login, refresh, revoke.
- **Email verification** via 8-digit OTP (`EmailConfirmed` flag).
- **Password reset** via OTP — for users who forgot their password.
- Per-user **concurrent request lock** (see below) so double-submits are rejected cleanly.

### 2. Email notifications
- MailKit SMTP (`EmailSettings` in `appsettings.json`).
- Rendered from embedded HTML templates (`EmailVerification.html`, `PasswordReset.html`, `TaskNotification.html`).
- `EmailVerification` + `PasswordReset` templates both show the 8-digit code with a countdown; `TaskNotification` shows task updates and a conditional "View task" button.

### 3. Runtime API availability (admin control)
- Endpoints opt in with `[ApiAvailability("Key")]`.
- Admins enable/disable keys via `api/admin/ApiAvailability/*`; changes persist to the `ApiAvailability` table and take effect **immediately** (in-memory cache, invalidated on change).
- Disabled calls return `503 API_DISABLED`.

### 4. Anti-both-kinds of abuse middleware
- `ApiAvailabilityMiddleware` — whole capabilities off (503).
- `UserOperationLockMiddleware` — a user can't overlap the same operation (409); e.g., double-clicking "refresh".

### 5. Projects
- Any authenticated user can **create** a project (`POST /api/projects`); the creator is auto-joined as a `ProjectManager` member. Progress starts at `0` (auto-calculated from tracks).
- **Paginated list** (`POST /api/projects/list` with `{ "pageNumber": 1, "pageSize": 10 }` in body) — returns only `id`, `title`, `description`, `progress`, `status` (pageSize capped at 100).
- **Update** (`PUT /api/projects/{id}`) and **full details** (`GET /api/projects/{id}` — basic info, creator, member list/count). Progress is auto-calculated from tracks at read time.
- **Soft delete** (`DELETE /api/projects/{id}`) — cascades `IsDeleted` through members (+ roles), tracks, track members, major/minor tasks, and project events; idempotent (`204`). Deleted projects vanish from list/detail.
- Missing projects return `404` (new `NotFoundException` in middleware); requests follow `Features/Command/Project/<Feature>` / `Features/Query/Project/<Feature>` with their handlers, responses, and validators co-located.

### 6. Tracks
- **List tracks by project** (`GET /api/projects/{id}/tracks`) — returns all tracks under the given project (name, description, progress) sorted newest first.
- **Create** (`POST /api/tracks` with `{ projectId, name, description? }` in body) — requires the target project to exist. Progress starts at `0` (auto-calculated from major tasks).
- **Update** (`PUT /api/tracks/{id}`) — updates name and description. Progress is auto-calculated at read time from existing major tasks.
- **Soft delete** (`DELETE /api/tracks/{id}`) — cascades `IsDeleted` through track members, major tasks, and their minor tasks; idempotent (`204`). Parent project's progress updates automatically (computed at read time).
- All parameters sent as JSON body (`[FromBody]`); features live in `Features/Command/Track/<Feature>` / `Features/Query/Track/<Feature>`.
- Repository exposes `GetByIdWithTreeIncludingDeletedAsync` (IgnoreQueryFilters + full Include graph) so the delete handler can walk the entire subtree.

### 7. Real-time collaboration (SignalR)
- Hub at **`/hubs/collaboration`** (`CollaborationHub`), `[Authorize]` — JWT passed as `?access_token=` in the query string (read by `JwtBearerEvents.OnMessageReceived` for `/hubs/*` paths).
- **Project group** `project-{id}` — clients `JoinProject`/`LeaveProject` (verifies project membership).
- **Track group** `track-{id}` — clients `JoinTrack`/`LeaveTrack` (verifies track membership via `TrackMember`).
- After a mutation commits, handlers push via `IRealtimeNotifier`/`SignalRNotifier` → `IHubContext` → group:
  - `projectChanged`, `trackChanged` → project group
  - `majorTaskChanged`, `minorTaskChanged` → **track group** (track-scoped, so members of one track only see their own track's task activity)
- Full details: see **`Docs/Realtime.md`**.

## Main User Flows

### Onboarding & verification
```
1. POST /api/auth/register                     → tokens issued, email unconfirmed
2. POST /api/auth/verify-email/request         → 8-digit OTP emailed
3. POST /api/auth/verify-email {email, otp}    → EmailConfirmed = true
(Resend anytime by re-calling step 2 once the code expires; active codes block re-sends.)
```

### Login & sessions
```
POST /api/auth/login                     → LoginResponse (access + refresh)
→ call protected APIs with Authorization: Bearer <access>
→ when access expires: POST /api/auth/refresh {refreshToken}  → new pair
→ sign out:            POST /api/auth/revoke {refreshToken}
```

### Forgotten password
```
1. POST /api/auth/password-reset/request {email}     → OTP emailed
2. POST /api/auth/password-reset {email, otp, newPassword}
   → password changed; user signs back in
```

### Project work (the core loop)
```
Admin / lead creates a Project  (progress = 0)
   → Tracks are created inside it  (progress = 0)
      → members join tracks (TrackMember) and receive notifications
         → MajorTasks belong to a track
            → MinorTasks belong to a MajorTask, assigned to a user
→ user marks MinorTask Done
                    → Progress derived: % Done minor tasks → MajorTask
                       → Mark a MajorTask Done
                          → % Done major tasks → Track → Project
                  (computed at read time by IProgressCalculator; never stored or manually set)
```

### Runtime admin control
```
Admin (rank=Admin) → POST /api/admin/ApiAvailability/UserRegistration/disable
   → ApiAvailability table updated, cache invalidated
      → next POST /api/auth/register → 503 API_DISABLED
```

## Operational Notes

- **Config**: `appsettings.json` holds `ConnectionStrings:DefaultConnection` (SQL Server LocalDB by default), `EmailSettings` (Gmail SMTP settings — user/password must be filled in real deployments), and `Jwt` (replace `Key` with ≥ 32 random chars in `appsettings.Development.json` or User Secrets; never commit a real key).
- **Migrations**: applied with `dotnet ef database update --project ..\OctalPulse.Infrastructure --startup-project ..\OctalPulse.API`.
- **Single instance today**: the refresh-token rotation, in-memory OTP store, presence cache, and per-user locks are all process-local. Scaling horizontally (multiple API instances behind a load balancer) requires moving those to shared stores (Redis for cache/OTP/locks, distributed invalidation for availability) — the service interfaces are already isolated for that swap.
- **Email in dev**: to send real emails, configure an app password in `EmailSettings`; `EmailTemplateRenderer` is a singleton, so templates render fast from embedded resources.

## What's Next / Roadmap Direction

- Major/Minor Task CRUD, membership & role management, event/calendar CRUD.
- Notifications beyond email (e.g. in-app).
- Distributed deployment support for the in-memory pieces listed above.
- Tests project: middleware unit tests, service unit tests, and integration tests through `WebApplicationFactory` (see `Middleware.md` for the strategy).