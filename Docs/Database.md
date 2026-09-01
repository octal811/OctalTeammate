# Database Design & Documentation

This document describes the OctaLTeammate database — the persistent data model behind a team collaboration app for managing materials, documents, tasks, scheduling, and calendars.

## Technology Stack

- **Database Engine:** Microsoft SQL Server
- **ORM / Data Access:** Entity Framework Core
- **Authentication/Authorization:** ASP.NET Core Identity (Microsoft Identity)
- **Architecture:** Clean Architecture / Domain-Driven Design (DDD)

The Domain layer holds the entities designed with **DDD** principles:

- `DomainEntity` — an empty **marker interface** tagging every business entity as part of the domain.
- `CommonEntity : DomainEntity` — adds `Guid Id` and `bool IsDeleted` (used as a **soft delete** trigger; rows are flagged, never hard-deleted).
- `AuditableEntity : CommonEntity` — adds `CreatedDate` and `ModifiedDate` for change tracking.

Entities implement these interfaces, so every entity must explicitly declare `Id`, `IsDeleted`, and (for auditable ones) `CreatedDate`/`ModifiedDate`.

### Base Interface Clarification

| Interface | Extends | Members | Implemented by |
|-----------|---------|---------|----------------|
| `DomainEntity` | — | *(marker)* | `User` (+ all others via inheritance) |
| `CommonEntity` | `DomainEntity` | `Id`, `IsDeleted` | `UserProjectRole` (no audit timestamps needed) |
| `AuditableEntity` | `CommonEntity` | + `CreatedDate`, `ModifiedDate` | `Project`, `ProjectMember`, `Track`, `TrackMember`, `MajorTask`, `MinorTask`, `Event`, `RefreshToken` |
| `User` | `IdentityUser<Guid>, DomainEntity` | identity + business fields | — (cannot be a `CommonEntity`; its key is owned by Identity) |

> `User` is a domain entity in behavior (implements the `DomainEntity` marker) but inherits from `IdentityUser<Guid>` (Microsoft Identity) because its key, hashed password, and login fields are managed by the Identity framework. It already declares `IsDeleted` separately.

### Infrastructure / EF Core Mapping

EF Core configuration lives in the `OctalPulse.Infrastructure` project:

- **Configurations/** — one `IEntityTypeConfiguration<T>` per entity defining table names, key constraints, column lengths, enum-as-string conversions, indexes, delete behaviors, and the soft-delete query filter (`HasQueryFilter(e => !e.IsDeleted)`).
- **Persistence/ApplicationDbContext** — `IdentityDbContext<User, IdentityRole<Guid>, Guid>` exposing a `DbSet` per entity. Configurations are auto-discovered via `ApplyConfigurationsFromAssembly`.
- **DependencyInjection** — `AddInfrastructure()` registers the `ApplicationDbContext` bound to SQL Server using the `DefaultConnection` connection string.
- **Persistence/Migrations** — the EF Core migration folder. The initial schema is `InitialCreate`; migrations are added with `dotnet ef migrations add <Name>` and applied with `dotnet ef database update`.

### Creating / Applying Migrations

```bash
# from the Infrastructure project directory, using the API as startup project
dotnet ef migrations add InitialCreate --project ..\OctalPulse.Infrastructure --startup-project ..\OctalPulse.API --output-dir Persistence\Migrations
dotnet ef database update --project ..\OctalPulse.Infrastructure --startup-project ..\OctalPulse.API
```

The connection string is read from `appsettings.json` -> `ConnectionStrings:DefaultConnection` (SQL Server). The API project is used as the startup project because it registers the `DbContext` and owns the configuration.



## Conventions & Global Rules

| Rule | Description |
|------|-------------|
| **Primary Key** | Every entity uses `Guid` as its ID data type, providing globally unique, non-sequential identifiers. |
| **Soft Delete** | Every table has an `IsDeleted` boolean column. Deleting a record sets it to `true` instead of removing the row. |
| **Created / Modified** | Auditable tables include `CreatedDate` and optional `ModifiedDate` (`DateTime`). |
| **Identity** | The `User` entity extends `IdentityUser<Guid>`, so all identity fields (UserName, PasswordHash, Email, etc.) come from Microsoft Identity. |
| **Progress Calculation** | `Progress` values on `MajorTask`, `Track`, and `Project` are **auto-calculated** from their child completion ratios and are never stored as meaningful manual input. |

## Table Reference

| Entity | Table Name | Purpose |
|--------|-----------|---------|
| User | `Users` | Team members, their main role and rank (custom Identity user) |
| Project | `Projects` | A managed project container |
| ProjectMember | `ProjectMembers` | User ↔ Project membership |
| UserProjectRole | `UserProjectRoles` | Many roles a user can hold within one project |
| Track | `Tracks` | Workstream within a project (e.g., AI, Backend, Mobile) |
| TrackMember | `TrackMembers` | User ↔ Track membership (notifications + work logging) |
| MajorTask | `MajorTasks` | High-level task belonging to a track |
| MinorTask | `MinorTasks` | Small actionable task belonging to a major task |
| Event | `Events` | Calendar/scheduling items (meetings, deadlines, etc.) |
| RefreshToken | `RefreshTokens` | Login/refresh session tokens (hashed, revocable) for JWT auth |
| ApiAvailability | `ApiAvailability` | Admin-controlled enable/disable toggle for API endpoints (by key) |

> Identity also creates supporting tables: `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`.

---

## 1. User — `Users`

Represents a team member. Uses **Microsoft Identity** so login (email/password), password hashing, and roles are handled by the framework.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key (Identity key). |
| UserName | string | Unique login name (usually the email). |
| Email | string | User email. |
| EmailConfirmed | bool | Whether the email is verified (Identity). |
| PasswordHash | string | Hashed password — handled by Identity. |
| PhoneNumber | string? | Identity phone number. |
| TwoFactorEnabled | bool | Whether 2FA is enabled (Identity). |
| ... | (Identity fields) | Additional standard Microsoft Identity properties. |
| Name | string | Display name shown across the app. |
| MainRole | UserRole (enum) | The user's primary role in their profile (e.g., `BackEnd`, `Mobile`). |
| Rank | UserRank (enum) | Whether the user is an `Admin` or a regular `Member`. An Admin only maintains the app (error/trouble handling) but is still a member with his own MainRole and ProjectRoles. |
| ProfilePictureUrl | string? | Optional profile picture URL. |
| IsDeleted | bool | Soft delete flag. |

**Enums used:**
- `UserRole`: `FrontEnd`, `BackEnd`, `Mobile`, `DevOps`, `Designer`, `QA`, `ProjectManager`
- `UserRank`: `Admin`, `Member`

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `ProjectMemberships` | ProjectMember | 1 → Many |
| `TrackMemberships` | TrackMember | 1 → Many |
| `CreatedEvents` | Event | 1 → Many |
| `AssignedMajorTasks` | MajorTask | 1 → Many |
| `AssignedMinorTasks` | MinorTask | 1 → Many |
| `CreatedProjects` | Project | 1 → Many |
| `RefreshTokens` | RefreshToken | 1 → Many |

---

## 2. Project — `Projects`

A top-level container for a managed team project.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| Title | string | Project name. |
| Description | string? | Short description of the project. |
| Progress | int | Auto-calculated 0–100 from the progress of its tracks. |
| Status | ProjectStatus (enum) | e.g., `Active`, `Completed`, `OnHold`, `Archived`. |
| CreatedDate | DateTime | When the project was created. |
| ModifiedDate | DateTime? | Last update timestamp. |
| CreatedByUserId | Guid | FK → User.Id — the user who created the project. |
| IsDeleted | bool | Soft delete flag. |

**Enums used:** `ProjectStatus`: `Active`, `Completed`, `OnHold`, `Archived`

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `CreatedByUser` | User | Many → 1 |
| `Members` | ProjectMember | 1 → Many |
| `Tracks` | Track | 1 → Many |
| `Events` | Event | 1 → Many |

---

## 3. ProjectMember — `ProjectMembers`

**Junction table** linking a User to a Project. Represents that the user is a member of the project.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| ProjectId | Guid | FK → Project.Id. |
| UserId | Guid | FK → User.Id. |
| CreatedDate | DateTime | When the user joined the project. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `Project` | Project | Many → 1 |
| `User` | User | Many → 1 |
| `Roles` | UserProjectRole | 1 → Many |

---

## 4. UserProjectRole — `UserProjectRoles`

Stores the **one or more roles a user holds within a specific project**. Because it is a separate table, a user can have *many* roles in the same project (e.g., Mobile AND Web AND Backend), unlike the single `MainRole` on the User profile.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| ProjectMemberId | Guid | FK → ProjectMember.Id. |
| Role | ProjectRole (enum) | A role held in this project, e.g., `Mobile`, `Web`, `BackEnd`. |
| IsDeleted | bool | Soft delete flag. |

**Enums used:** `ProjectRole`: `FrontEnd`, `BackEnd`, `Mobile`, `DevOps`, `Designer`, `QA`, `ProjectManager`, `TechLead`

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `ProjectMember` | ProjectMember | Many → 1 |

---

## 5. Track — `Tracks`

A workstream *inside* a project (e.g., "AI", "Backend", "Mobile"). A project is created with its specific tracks and tracks may be added or removed later. Users join tracks to receive notifications and to log their work.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| Name | string | Track name (e.g., `AI`, `Backend`, `Mobile`). |
| Description | string? | What this track covers. |
| Progress | int | Auto-calculated 0–100 from the completion of its major tasks. |
| ProjectId | Guid | FK → Project.Id. |
| TrackLeadUserId | Guid? | FK → User.Id (optional). The lead is responsible for guarding the track from accidental deletion and for letting users join. |
| CreatedDate | DateTime | When the track was created. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `Project` | Project | Many → 1 |
| `TrackLeadUser` | User | Many → 1 (zero/optional) |
| `Members` | TrackMember | 1 → Many |
| `MajorTasks` | MajorTask | 1 → Many |

---

## 6. TrackMember — `TrackMembers`

**Junction table** linking a User to a Track. A user who joins a track receives notifications about it and can write/record their work in it.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| TrackId | Guid | FK → Track.Id. |
| UserId | Guid | FK → User.Id. |
| CreatedDate | DateTime | When the user joined the track. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `Track` | Track | Many → 1 |
| `User` | User | Many → 1 |

---

## 7. MajorTask — `MajorTasks`

A high-level task belonging to a track. Contains an ordered list of minor tasks. Its progress is derived from its minor tasks.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| Title | string | Task title. |
| Description | string? | Short description. |
| Details | string? | Longer/rich details (e.g., markdown). |
| Progress | int | Auto-calculated 0–100 from finished minor tasks. |
| Link | string? | External reference link. |
| State | MajorTaskState (enum) | `Todo`, `InProgress`, `OnHold`, `Done`. |
| Priority | Priority (enum) | `Low`, `Medium`, `High`, `Critical`. |
| DueDate | DateTime? | Optional deadline. |
| Order | int | Display/ordering index within its track. |
| CompletedDate | DateTime? | Set when the task reaches state `Done`. |
| TrackId | Guid | FK → Track.Id. |
| AssignedUserId | Guid? | FK → User.Id (nullable — can be a shared task). |
| CreatedDate | DateTime | When created. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Enums used:** `MajorTaskState`, `Priority`

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `Track` | Track | Many → 1 |
| `AssignedUser` | User | Many → 1 (zero/optional) |
| `MinorTasks` | MinorTask | 1 → Many |

---

## 8. MinorTask — `MinorTasks`

A small, actionable task under a major task. Completing minor tasks drives progress upward (MajorTask → Track → Project) and counts as an achievement for the assigned user.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| Title | string | Task title. |
| Description | string? | Short description. |
| Target | string? | What the completion of this task achieves. |
| State | MinorTaskState (enum) | `Todo`, `InProgress`, `Done`, `Canceled`, `Failed`. |
| Notes | string? | Notes recorded (e.g., after completion). |
| Link | string? | External reference link. |
| Order | int | Display/ordering index within its major task. |
| CompletedDate | DateTime? | Set when the task reaches state `Done`. |
| MajorTaskId | Guid | FK → MajorTask.Id. |
| AssignedUserId | Guid? | FK → User.Id (nullable). |
| CreatedDate | DateTime | When created. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Enums used:** `MinorTaskState`: `Todo`, `InProgress`, `Done`, `Canceled`, `Failed`

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `MajorTask` | MajorTask | Many → 1 |
| `AssignedUser` | User | Many → 1 (zero/optional) |

---

## 9. Event — `Events`

Calendar/scheduling entries used to remind the team about tasks, jobs, meetings, milestones, etc.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| Title | string | Event title. |
| Description | string? | Event description. |
| Type | EventType (enum) | `Meeting`, `Deadline`, `Task`, `Reminder`, `Milestone`. |
| StartDate | DateTime | When the event starts (date portion). |
| EndDate | DateTime? | When the event ends (for multi-day items). |
| StartTime | TimeOnly? | Optional start time-of-day. |
| EndTime | TimeOnly? | Optional end time-of-day. |
| IsAllDay | bool | Whether the event spans a full day. |
| ProjectId | Guid? | FK → Project.Id (optional scoping). |
| TrackId | Guid? | FK → Track.Id (optional scoping). |
| MajorTaskId | Guid? | FK → MajorTask.Id (optional scoping). |
| CreatedByUserId | Guid | FK → User.Id — who created the event. |
| CreatedDate | DateTime | When the event was created. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Enums used:** `EventType`: `Meeting`, `Deadline`, `Task`, `Reminder`, `Milestone`

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `Project` | Project | Many → 1 (zero/optional) |
| `Track` | Track | Many → 1 (zero/optional) |
| `MajorTask` | MajorTask | Many → 1 (zero/optional) |
| `CreatedByUser` | User | Many → 1 |

---

## 10. RefreshToken — `RefreshTokens`

Stores login/refresh session tokens for **JWT authentication**. The refresh token itself is never stored raw — only its **SHA-256 hash**, so a leaked database cannot be used to mint new sessions. Each login/refresh issues a new single-use token and revokes the previous one.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| TokenHash | string (128) | SHA-256 hex hash of the refresh token (unique). |
| ExpiresAt | DateTime | When the refresh token expires (default 7 days). |
| RevokedAt | DateTime? | When the token was revoked/replaced (single-use rotation). |
| UserId | Guid | FK → User.Id — the owner of the session. |
| CreatedDate | DateTime | When the token was issued. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

**Relationships**
| Navigation | Type | Cardinality |
|-----------|------|-------------|
| `User` | User | Many → 1 |

---

## 11. ApiAvailability — `ApiAvailability`

Lets an administrator enable or disable a **named API capability at runtime** (e.g. `UserRegistration`, `UserLogin`) without redeploying. Endpoints declare an `[ApiAvailability("Key")]` attribute; an API middleware layer consults this table on each request and rejects disabled capabilities with `503 Service Unavailable`.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key. |
| Key | string (128, unique) | The availability key referenced by `[ApiAvailability("Key")]`. |
| IsEnabled | bool | Whether the capability is currently enabled. |
| UpdatedAt | DateTime | Last time the state was changed. |
| UpdatedBy | Guid? | FK → User.Id (nullable) — the admin who changed it. |
| CreatedDate | DateTime | When the row was first created. |
| ModifiedDate | DateTime? | Last update timestamp. |
| IsDeleted | bool | Soft delete flag. |

Rows are created lazily when an administrator first enables/disables a key. A key with **no row is treated as enabled** (default allow).

---

## Relationships & Cardinality Summary

```
User 1 ──── * ProjectMember * ──── 1 Project
                        │
                        * UserProjectRole * (many roles per user in one project)

User 1 ──── * TrackMember * ──── 1 Track
                        │
                        │ TrackLeadUserId (optional, 0..1)
                        │
Project 1 ──── * Track ──── * MajorTask ──── * MinorTask

Project 1 ──── * Event
User 1 ──── * Event (CreatedByUserId)
```

| From | To | Type | Cardinality | Notes |
|------|-----|------|-------------|-------|
| User | ProjectMember | Has Many | 1 → N | A user can belong to many projects. |
| Project | ProjectMember | Has Many | 1 → N | A project has many members. |
| ProjectMember | UserProjectRole | Has Many | 1 → N | One membership can hold many roles. |
| User | TrackMember | Has Many | 1 → N | A user can join many tracks. |
| Track | TrackMember | Has Many | 1 → N | A track has many members. |
| Project | Track | Has Many | 1 → N | A project has many tracks. |
| Track | User (Lead) | Has One | N → 0..1 | A track optionally has one lead user. |
| Track | MajorTask | Has Many | 1 → N | A track contains many major tasks. |
| MajorTask | MinorTask | Has Many | 1 → N | A major task contains many minor tasks. |
| User | MajorTask | Has Many | N → 0..1 | A user is optionally assigned many major tasks. |
| User | MinorTask | Has Many | N → 0..1 | A user is optionally assigned many minor tasks. |
| Project | Event | Has Many | 1 → N | A project may have many events. |
| Track | Event | Has Many | N → 0..1 | An event may be scoped to a track. |
| MajorTask | Event | Has Many | N → 0..1 | An event may be scoped to a major task. |
| User | Event | Has Many | 1 → N | A user creates many events. |
| Project | User | Has One | N → 1 | `CreatedByUserId` creator relationship. |
| User | RefreshToken | Has Many | 1 → N | A user owns many (rotated) refresh tokens. |

## Progress Cascade

Progress is automatically propagated bottom-up whenever a minor task is finished:

```
MinorTask completed (state = Done)
   → MajorTask.Progress = (Done minor tasks / total minor tasks) × 100
      → Track.Progress = average of its MajorTasks' progress
         → Project.Progress = average of its Tracks' progress
```

Completing a minor task also records an **achievement** for its assigned user.
