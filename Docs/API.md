# API Reference

This document describes the public HTTP API of **OctalPulse**. Endpoints are grouped by feature, with request/response shapes and authentication notes.

## Conventions

- **Base URL**: `https://<host>/api`
- **Content-Type**: `application/json` (both request and response)
- **Authentication**: JWT Bearer token. Send `Authorization: Bearer <accessToken>` for protected endpoints. Anonymous endpoints (register, login, e-mail OTP flows) do not require it.
- **Request / Response models**: the API accepts the same JSON shape it returns — there are no DTO wrappers (email/password are sent in the body). Controllers bind the **command records** directly from the body. Each command's handler, response, and FluentValidation validator live together in `Application/Features/Command/Auth/<Feature>/`.
- **Errors**: all failures are normalized by `ExceptionHandlingMiddleware` into:

```json
{
  "error": "Validation Error",
  "message": "The OTP you entered is incorrect.",
  "statusCode": 400
}
```

## Auth Tokens

Register, login, and refresh each return their own response record — `RegisterResponse`, `LoginResponse`, `RefreshTokenResponse` — with the same shape, a JWT access token plus a rotating refresh token:

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<base64url>",
  "accessTokenExpiresAt": "2026-09-01T12:30:00Z",
  "refreshTokenExpiresAt": "2026-09-08T12:15:00Z",
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "user@example.com",
  "name": "Ahmed"
}
```

- Access token: short-lived (default `15` minutes). Contains claims `sub`, `email`, `name`, `mainRole`, `rank`, `jti`, `iat`.
- Refresh token: long-lived (default `7` days), stored **hashed** (SHA-256) in the `RefreshTokens` table and **single-use**. Each refresh rotates: the old token is revoked and a new pair is returned.
- The JWT carries a `rank` claim (`Admin` or `Member`) for client-side hints, but the **`AdminOnly` authorization policy does not trust the claim** — it loads the actual user from the database and requires `User.Rank == Admin`. A demoted/promoted user takes effect immediately, without re-login.

---

## Authentication

### POST `/api/auth/register`

Anonymous. Availability-controlled: `[ApiAvailability("UserRegistration")]`.

Creates a new user with `Rank = Member` and returns a full token pair immediately. The user's email is **not** auto-confirmed (`EmailConfirmed = false`).

```json
// Request
{
  "email": "user@example.com",
  "name": "Ahmed",
  "mainRole": "BackEnd",
  "password": "StrongPass1!"
}
```

- `mainRole`: `FrontEnd | BackEnd | Mobile | DevOps | Designer | QA | ProjectManager`
- `201`-style success → `200` with `RegisterResponse` (see above)
- `400` — email already registered, or password fails Identity rules (min 8 chars, digit, upper & lower)

---

### POST `/api/auth/login`

Anonymous. Availability-controlled: `[ApiAvailability("UserLogin")]`.

Authenticates with email + password and returns a token pair.

```json
// Request
{
  "email": "user@example.com",
  "password": "StrongPass1!"
}
```

- `200` — `LoginResponse`
- `401` — invalid email or password (identical message for both, no account enumeration)

---

### POST `/api/auth/refresh`

Anonymous (uses the refresh token, not the access token). Concurrent-operation-locked: `[UserOperationLock("refresh")]`.

Redeems a refresh token for a **new** token pair (single-use rotation).

```json
// Request
{
  "refreshToken": "<the-refresh-token>"
}
```

- `200` — new `RefreshTokenResponse`
- `401` — invalid/expired/revoked refresh token, or the owning user no longer exists

---

### POST `/api/auth/revoke`

Anonymous. Revokes the given refresh token (logout / sign-out on this device).

```json
// Request
{
  "refreshToken": "<the-refresh-token>"
}
```

- `204 No Content` — revoked (idempotent; unknown tokens are ignored)

---

## Email Verification

### POST `/api/auth/verify-email/request`

Anonymous. Concurrent-operation-locked: `[UserOperationLock("verify-email:request")]`.

Sends an **8-digit OTP** (crypto-random) to the given email for verification. This is also the "resend" flow — calling it again while a code is active is rejected (anti-spam).

```json
// Request
{ "email": "user@example.com" }
```

- `200` — `{ "message": "Verification code sent to your email." }`
- `400` — no account for this email, email already verified, or a code is still pending

---

### POST `/api/auth/verify-email`

Anonymous. Concurrent-operation-locked: `[UserOperationLock("verify-email:confirm")]`.

Confirms an email by submitting the OTP. Success just flips `EmailConfirmed = true` (no tokens issued).

```json
// Request
{
  "email": "user@example.com",
  "otp": "84712039"
}
```

- `200` — `{ "message": "Email verified successfully." }`
- `400` — wrong OTP, expired OTP, too many invalid attempts, or email already verified

---

## Password Reset

### POST `/api/auth/password-reset/request`

Anonymous. Concurrent-operation-locked: `[UserOperationLock("password-reset:request")]`.

Sends an **8-digit OTP** for resetting a forgotten password. Behaves identically whether or not the account exists (no account enumeration) — but only actually sends when the account exists.

```json
// Request
{ "email": "user@example.com" }
```

- `200` — `{ "message": "If an account exists, a reset code has been sent to your email." }`
- `400` — a reset code is already pending

---

### POST `/api/auth/password-reset`

Anonymous. Concurrent-operation-locked: `[UserOperationLock("password-reset:confirm")]`.

Validates the OTP and sets a new password. The user must log in again afterward (existing refresh tokens are **not** invalidated; the user simply signs in).

```json
// Request
{
  "email": "user@example.com",
  "otp": "84712039",
  "newPassword": "NewStrongPass1!"
}
```

- `200` — `{ "message": "Password has been reset. You can now log in with your new password." }`
- `400` — wrong/expired OTP, too many invalid attempts, or the new password fails Identity rules

---

## Projects

All routes in this group require an authenticated user (`[Authorize]`). Any **approved project member** can create, list, view, or update projects (project update/delete are also creator-only on `ProjectsController`). The creator is automatically joined as a `ProjectManager` member — other users join on request and are admitted only after the **creator approves** them (membership is `Pending` until then). Only `Approved` members can read/write and receive real-time project events.

### POST `/api/projects`

Creates a project. The authenticated user becomes its first member (role `ProjectManager`).

```json
// Request
{
  "title": "OctalPulse Web App",
  "description": "Build the member dashboard.",
  "status": "Active"
}
```

- `status`: `Active | Completed | OnHold | Archived` (default `Active`)
- `progress` is **not accepted** — always starts at `0` (auto-calculated from major tasks)
- `200` — `CreateProjectResponse`:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "title": "OctalPulse Web App",
  "description": "Build the member dashboard.",
  "progress": 0,
  "status": "Active",
  "createdDate": "2026-09-02T09:51:37Z"
}
```

- `400` — invalid title/description/status

---

### POST `/api/projects/list`

Paginated list. Sends `pageNumber` and `pageSize` in the JSON body. Returns **only** `id`, `title`, `description`, `progress`, `status` (newest first). `pageSize` is capped at 100.

```json
// Request
{
  "pageNumber": 1,
  "pageSize": 10
}

// 200 — GetAllProjectsResponse
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "title": "OctalPulse Web App",
      "description": "Build the member dashboard.",
      "progress": 10,
      "status": "Active"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 10
}
```

---

### GET `/api/projects`

Full project details: basic info, who created it, and its members.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000"
}

// 200 — GetProjectByIdResponse
{
  "id": "00000000-0000-0000-0000-000000000000",
  "title": "OctalPulse Web App",
  "description": "Build the member dashboard.",
  "progress": 10,
  "status": "Active",
  "createdDate": "2026-09-02T09:51:37Z",
  "modifiedDate": null,
  "creator": {
    "userId": "00000000-0000-0000-0000-000000000000",
    "name": "Ahmed",
    "email": "user@example.com"
  },
  "membersCount": 1,
  "members": [
    {
      "userId": "00000000-0000-0000-0000-000000000000",
      "name": "Ahmed",
      "email": "user@example.com"
    }
  ]
}
```

- `404` — project does not exist

---

### PUT `/api/projects`

Updates the title, description, and status of the project.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000",
  "title": "OctalPulse Web App v2",
  "description": "Now with team reports.",
  "status": "Active"
}
```

- `progress` is **not accepted** — auto-calculated from existing major tasks
- `200` — `UpdateProjectResponse` (`id`, `title`, `description`, `progress`, `status`, `modifiedDate`)
- `404` — project not found; `400` — invalid fields

---

### DELETE `/api/projects`

Soft-deletes the project **and everything under it** — members (and their project roles), tracks, track members, major tasks, minor tasks, and project events. All rows are flagged `IsDeleted`, never hard-deleted. The project disappears from the list and detail endpoints immediately.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000"
}
```

- `204 No Content` — deleted (idempotent; re-deleting an already deleted project also returns `204`)
- `404` — project does not exist

---

### Project Membership & Join flow

A user joins a project on request; the **project creator** approves or rejects the request (single-request endpoint, creator-only review). The `ProjectMember.Status` is `Pending` while waiting, `Approved` once admitted, or `Rejected`. Only `Approved` members can read/write project data and receive real-time events.

#### POST `/api/projects/join`

Submits a join request for the authenticated user **with project roles**. The roles are stored as `UserProjectRole` rows linked to the membership. The existing `ProjectMember` row (if any) is left as-is if not rejected. The requester is identified from the JWT.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "roles": ["FrontEnd", "Mobile"]
}

// 200 — RequestProjectJoinResponse
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "message": "Your request to join has been sent. Please wait for the project creator to accept."
}
```

- `roles`: array of `ProjectRole` enum values (`FrontEnd`, `BackEnd`, `Mobile`, `DevOps`, `Designer`, `QA`, `ProjectManager`, `TechLead`). At least one role is required. Duplicate roles are ignored.
- `404` — project not found; `400` — already a member, or invalid/empty roles

#### POST `/api/projects/approve-join`

**Creator-only.** Approves a pending join request, setting the membership to `Approved`.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "targetUserId": "00000000-0000-0000-0000-000000000000"
}

// 200 — ReviewProjectJoinResponse
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "userId": "00000000-0000-0000-0000-000000000000",
  "status": "Approved"
}
```

- `403` — caller is not the project creator; `404` — project or join request not found; `400` — already approved

#### POST `/api/projects/reject-join`

**Creator-only.** Rejects a pending join request, setting the membership to `Rejected`.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "targetUserId": "00000000-0000-0000-0000-000000000000"
}

// 200 — ReviewProjectJoinResponse
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "userId": "00000000-0000-0000-0000-000000000000",
  "status": "Rejected"
}
```

- `403` — caller is not the project creator; `404` — project or join request not found; `400` — already rejected

#### PUT `/api/projects/roles`

Updates the authenticated user's project roles. The user must be an **approved member** of the project. All existing roles are replaced with the new list.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "roles": ["BackEnd", "TechLead"]
}

// 200 — UpdateProjectRolesResponse
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "userId": "00000000-0000-0000-0000-000000000000",
  "roles": ["BackEnd", "TechLead"]
}
```

- `roles`: array of `ProjectRole` enum values — at least one required. Duplicate roles are ignored.
- `403` — not an approved member; `404` — project not found; `400` — invalid/empty roles

---

## Tracks

All routes in this group require an authenticated user who is an **approved project member** of the track's project (the join/gate rules mirror Projects). Track CRUD is on `TracksController`; list-by-project lives on `ProjectsController`. All parameters are sent as JSON `[FromBody]`. A user joins a track on request; the **track lead** approves or rejects — only `Approved` track members receive that track's notifications.

### GET `/api/projects/tracks`

Returns all tracks in the given project (newest created first).

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000"
}

// 200 — GetTracksByProjectResponse
{
  "tracks": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "name": "Backend Track",
      "description": "API and database work",
      "progress": 15
    }
  ]
}
```

- `404` — project does not exist (via FluentValidation)

---

### POST `/api/tracks`

Creates a track inside a project.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "name": "Backend Track",
  "description": "API and database work"
}
```

- `progress` is **not accepted** — always starts at `0` (auto-calculated from major tasks)
- `200` — `CreateTrackResponse` (id, projectId, name, description, progress)
- `404` — project does not exist; `400` — invalid fields

---

### PUT `/api/tracks`

Updates a track's name and description. Progress is auto-calculated at read time from existing major tasks.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000",
  "projectId": "00000000-0000-0000-0000-000000000000",
  "name": "Backend Track v2",
  "description": "Updated scope"
}
```

- `projectId` is required in the body
- `progress` is **not accepted** — auto-calculated from existing major tasks
- `200` — `UpdateTrackResponse` (id, projectId, name, description, progress, modifiedDate)
- `404` — track not found; `400` — invalid fields

---

### DELETE `/api/tracks`

Soft-deletes the track **and everything under it** — track members, major tasks, and their minor tasks. All rows are flagged `IsDeleted`, never hard-deleted.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000"
}
```

- `204 No Content` — deleted (idempotent; re-deleting an already deleted track returns `204`). Parent project's progress updates automatically (computed at read time).
- `404` — track does not exist

---

### Track Membership & Join flow

A user joins a track on request; the **track lead** (`TrackLeadUserId`) approves or rejects the request (single-request endpoint). The `TrackMember.Status` is `Pending` while waiting, `Approved` once admitted, or `Rejected`. Only `Approved` track members are added to the track's real-time group.

#### POST `/api/tracks/join`

Submits a join request for the authenticated user. The requester is identified from the JWT.

```json
// Request
{
  "trackId": "00000000-0000-0000-0000-000000000000"
}

// 200 — RequestTrackJoinResponse
{
  "trackId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "message": "Join request submitted."
}
```

- `404` — track not found; `400` — already a member

#### POST `/api/tracks/approve-join`

**Track-lead only.** Approves a pending join request, setting the membership to `Approved`.

```json
// Request
{
  "trackId": "00000000-0000-0000-0000-000000000000",
  "targetUserId": "00000000-0000-0000-0000-000000000000"
}

// 200 — ReviewTrackJoinResponse
{
  "trackId": "00000000-0000-0000-0000-000000000000",
  "userId": "00000000-0000-0000-0000-000000000000",
  "status": "Approved"
}
```

- `403` — caller is not the track lead; `404` — track or join request not found; `400` — already approved

#### POST `/api/tracks/reject-join`

**Track-lead only.** Rejects a pending join request, setting the membership to `Rejected`.

```json
// Request
{
  "trackId": "00000000-0000-0000-0000-000000000000",
  "targetUserId": "00000000-0000-0000-0000-000000000000"
}

// 200 — ReviewTrackJoinResponse
{
  "trackId": "00000000-0000-0000-0000-000000000000",
  "userId": "00000000-0000-0000-0000-000000000000",
  "status": "Rejected"
}
```

- `403` — caller is not the track lead; `404` — track or join request not found; `400` — already rejected

---

## Events

Every project has its own events (`EventsController`). All routes require an authenticated user who is an **approved project member** of the event's project. **Any project member can create** an event, but **only the event creator** can update or delete it. All mutations are real-time (SignalR `eventChanged` to the project group).

### POST `/api/events`

Creates an event for a project. An **approved project member** of that project may create it.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "title": "Sprint Review",
  "description": "Weekly demo",
  "type": "Meeting",
  "startDate": "2026-09-15T10:00:00Z",
  "endDate": "2026-09-15T11:00:00Z",
  "startTime": null,
  "endTime": null,
  "isAllDay": false,
  "trackId": null,
  "majorTaskId": null
}
```

- `type`: `Meeting | Deadline | Task | Reminder | Milestone`
- The caller (`createdByUserId`) is recorded from the JWT — it cannot be spoofed from the body.
- `200` — `CreateEventResponse`

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "projectId": "00000000-0000-0000-0000-000000000000",
  "title": "Sprint Review",
  "description": "Weekly demo",
  "type": "Meeting",
  "startDate": "2026-09-15T10:00:00Z",
  "endDate": "2026-09-15T11:00:00Z",
  "startTime": null,
  "endTime": null,
  "isAllDay": false,
  "trackId": null,
  "majorTaskId": null,
  "createdByUserId": "00000000-0000-0000-0000-000000000000",
  "isDeleted": false,
  "createdDate": "2026-09-02T18:04:54Z"
}
```

- `403` — caller is not an approved member of the project; `404` — project/track/major task not found; `400` — invalid fields

---

### PUT `/api/events`

Updates an event. **Only the event creator** (the user in `CreatedByUserId`) may update it.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000",
  "title": "Sprint Review (rescheduled)",
  "description": "Weekly demo",
  "type": "Meeting",
  "startDate": "2026-09-16T10:00:00Z",
  "endDate": "2026-09-16T11:00:00Z",
  "isAllDay": false
}
```

- `200` — `UpdateEventResponse` (same fields as the create response but with `modifiedDate` instead of `isDeleted`/`createdDate`)
- `403` — caller is not the creator (or not an approved member); `404` — event not found; `400` — invalid fields

---

### DELETE `/api/events`

Soft-deletes an event. **Only the event creator** may delete it; the deletion records `DeletedByUserId` and `ModifiedDate` (rows are never hard-deleted). Concurrent-operation-locked: `[UserOperationLock("events:delete")]`.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000"
}
```

- `204 No Content` — deleted (idempotent for an already-deleted event)
- `403` — caller is not the creator (or not an approved member); `404` — event not found

---

### GET `/api/events/month`

Returns **all** events in the given project for the given **calendar year and month** (`1..12`). This endpoint intentionally bypasses pagination and returns **every** event in that month — it is **not** capped at the 10-item default limit. An approved project member of that project may read it.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "year": 2026,
  "month": 9
}

// 200 — GetEventsByMonthResponse
{
  "events": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "projectId": "00000000-0000-0000-0000-000000000000",
      "title": "Sprint Review",
      "description": "Weekly demo",
      "type": "Meeting",
      "startDate": "2026-09-15T10:00:00Z",
      "endDate": "2026-09-15T11:00:00Z",
      "startTime": null,
      "endTime": null,
      "isAllDay": false,
      "trackId": null,
      "majorTaskId": null,
      "createdByUserId": "00000000-0000-0000-0000-000000000000",
      "deletedByUserId": null,
      "isDeleted": false,
      "createdDate": "2026-09-02T18:04:54Z",
      "modifiedDate": null
    }
  ],
  "year": 2026,
  "month": 9,
  "totalCount": 12
}
```

- `totalCount` equals the number of `events` returned (all matching events in the month). An event is included if its `StartDate` year and month match the query — irrespective of count.
- Each `events` entry is an `EventItem` — like the create response but also including `projectId`, `deletedByUserId`, `isDeleted`, and `modifiedDate`.
- `403` — caller is not an approved member; `404` — project not found; `400` — invalid `year`/`month`

---

## Tasks

Tasks are read/write-gated like the rest of the app: the caller must be an **approved project member** of the task's track/project. A **major task** belongs to a *track*; a **minor task** belongs to a *major task*. Completing minor tasks drives a major task's **auto-computed progress**; marking a major task `Done` drives track and project progress (MinorTask → MajorTask → Track → Project).

Many clients rely on the rule **"progress is never accepted as input"** — it is always derived from child task states at read time, so no task endpoint accepts a `progress` field.

All task mutations are **creator-only** for update/delete and are real-time (`majorTaskChanged` / `minorTaskChanged` to the track group).

## Major Tasks

`MajorTasksController` — `[Authorize]`, routed at `/api/majortasks`. Create and delete are concurrent-operation-locked (`majortasks:create` / `majortasks:delete`). Member gate on every read/write; update/delete are **creator-only**.

### GET `/api/majortasks/track`

Returns all non-deleted major tasks in the given track (creator-only restriction applies to edits, not reads — any approved member can list).

```json
// Request
{
  "trackId": "00000000-0000-0000-0000-000000000000"
}

// 200 — GetMajorTasksByTrackResponse
{
  "majorTasks": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "trackId": "00000000-0000-0000-0000-000000000000",
      "title": "Build /api/login",
      "description": "Implement JWT login",
      "details": null,
      "link": null,
      "state": "InProgress",
      "priority": "High",
      "dueDate": "2026-09-20T00:00:00Z",
      "order": 1,
      "assignedUserId": "00000000-0000-0000-0000-000000000000",
      "progress": 50,
      "createdByUserId": "00000000-0000-0000-0000-000000000000",
      "deletedByUserId": null,
      "createdDate": "2026-09-02T09:51:37Z"
    }
  ]
}
```

- `403` — caller is not an approved member; `404` — track not found

### POST `/api/majortasks`

Creates a major task. An **approved project member** may create. `progress` is **not accepted** — it is derived from minor tasks.

```json
// Request
{
  "trackId": "00000000-0000-0000-0000-000000000000",
  "title": "Build /api/login",
  "description": "Implement JWT login",
  "details": null,
  "link": null,
  "state": "Todo",
  "priority": "High",
  "dueDate": "2026-09-20T00:00:00Z",
  "order": 1,
  "assignedUserId": null
}
```

- `state`: `Todo | InProgress | OnHold | Done`; `priority`: `Low | Medium | High | Critical`
- `progress` is **not accepted** — computed at read time from done minor tasks (returns `0` for a new major task with no minor tasks)
- `createdByUserId` is recorded from the JWT
- `200` — `CreateMajorTaskResponse` (same shape as the list item; `createdByUserId`, `progress`)
- `403` — not an approved member; `404` — track not found; `400` — invalid fields

### PUT `/api/majortasks`

Updates a major task. **Creator-only.** `progress` is **not accepted** — recomputed from minor tasks at read time.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000",
  "title": "Build /api/login (v2)",
  "description": "Add refresh token rotation",
  "state": "InProgress",
  "priority": "Critical"
}
```

- `200` — `UpdateMajorTaskResponse` (adds `modifiedDate`); `progress` recomputed
- `403` — caller is not the creator (or not an approved member); `404` — task not found; `400` — invalid fields

### DELETE `/api/majortasks`

Soft-deletes a major task **and its minor tasks**. **Creator-only.** Records `DeletedByUserId`/`ModifiedDate` (never hard-deleted). Concurrent-operation-locked.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000"
}
```

- `204 No Content` — deleted (idempotent; re-deleting returns `204`)
- `403` — caller is not the creator (or not an approved member); `404` — task not found

---

## Minor Tasks

`MinorTasksController` — `[Authorize]`, routed at `/api/minortasks`. Create and delete are concurrent-operation-locked (`minortasks:create` / `minortasks:delete`). Member gate on every read/write; update/delete are **creator-only**. Completing a `Done` minor task raises the major task's progress and records an achievement for the assigned user.

### GET `/api/minortasks/major`

Returns all non-deleted minor tasks in the given major task.

```json
// Request
{
  "majorTaskId": "00000000-0000-0000-0000-000000000000"
}

// 200 — GetMinorTasksByMajorTaskResponse
{
  "minorTasks": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "majorTaskId": "00000000-0000-0000-0000-000000000000",
      "title": "Add refresh flow",
      "description": "Rotate the refresh token",
      "target": "Single-use tokens",
      "state": "InProgress",
      "notes": null,
      "link": null,
      "order": 1,
      "assignedUserId": "00000000-0000-0000-0000-000000000000",
      "createdByUserId": "00000000-0000-0000-0000-000000000000",
      "isDeleted": false,
      "createdDate": "2026-09-02T09:51:37Z"
    }
  ]
}
```

- `403` — caller is not an approved member; `404` — major task not found

### POST `/api/minortasks`

Creates a minor task. An **approved project member** may create.

```json
// Request
{
  "majorTaskId": "00000000-0000-0000-0000-000000000000",
  "title": "Add refresh flow",
  "description": "Rotate the refresh token",
  "target": "Single-use tokens",
  "state": "Todo",
  "notes": null,
  "link": null,
  "order": 1,
  "assignedUserId": "00000000-0000-0000-0000-000000000000"
}
```

- `state`: `Todo | InProgress | Done | Canceled | Failed`
- `createdByUserId` is recorded from the JWT
- `200` — `CreateMinorTaskResponse` (same shape as the list item; `createdByUserId`, `isDeleted`)
- `403` — not an approved member; `404` — major task not found; `400` — invalid fields

### PUT `/api/minortasks`

Updates a minor task. **Creator-only.** Setting `state` to `Done` calls up to the major task's **auto-computed progress** (% Done minor tasks — recomputed at read time).

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000",
  "state": "Done",
  "notes": "Shipped to prod",
  "order": 1
}
```

- `200` — `UpdateMinorTaskResponse` (adds `modifiedDate`)
- `403` — caller is not the creator (or not an approved member); `404` — task not found; `400` — invalid fields

### DELETE `/api/minortasks`

Soft-deletes a minor task. **Creator-only.** Records `DeletedByUserId`/`ModifiedDate` (never hard-deleted). Concurrent-operation-locked.

```json
// Request
{
  "id": "00000000-0000-0000-0000-000000000000"
}
```

- `204 No Content` — deleted (idempotent; re-deleting returns `204`)
- `403` — caller is not the creator (or not an approved member); `404` — task not found

---

## Admin — API Availability

All routes in this group are protected by the `AdminOnly` policy, which requires the caller to be an authenticated user whose **database record** has `Rank = Admin` (`ApiAvailabilityController` is `[Authorize(Policy = "AdminOnly")]`). A normal member gets `403 Forbidden`.

### POST `/api/admin/ApiAvailability/enable`

Enables a named capability. Creates the row on first use; unknown keys default to enabled anyway.

```json
// Request
{
  "key": "UserRegistration"
}
```

- `200` — `{ "key": "UserRegistration", "isEnabled": true }`

### POST `/api/admin/ApiAvailability/disable`

Disables a named capability. Takes effect on the next request (the in-memory cache is invalidated immediately).

```json
// Request
{
  "key": "UserRegistration"
}

// 200
{ "key": "UserRegistration", "isEnabled": false }
```

### GET `/api/admin/ApiAvailability/status`

Returns the current status. A key never touched by an admin reports the default (`isEnabled: true`).

```json
// Request
{
  "key": "UserRegistration"
}

// 200 — ApiAvailabilityStatus
{
  "key": "UserRegistration",
  "isEnabled": true,
  "updatedAt": "2026-09-01T16:32:12Z",
  "updatedBy": "00000000-0000-0000-0000-000000000000"
}
```

When a capability is **disabled**, the *middleware* rejects calls to it before it reaches the controller:

```json
// 503 Service Unavailable
{
  "code": "API_DISABLED",
  "message": "This operation is currently unavailable."
}
```

---

## Cross-cutting Behavior

### Progress auto-calculation

Progress is never accepted as input on any endpoint and is **never stored** — it is **computed at read time** by `IProgressCalculator` from child task states (only non-deleted rows count):

- **Major task progress** = (minor tasks with state `Done` / total minor tasks) × 100 (0 if no minor tasks).
- **Track progress** = (major tasks with state `Done` / total major tasks in the track) × 100 (0 if no major tasks).
- **Project progress** = (major tasks with state `Done` / total major tasks in the project) × 100 (0 if no major tasks).

Progress is derived fresh on every response, so it is always consistent with the current children without any write-time recalculation or progress race condition.

### Real-time notifications (SignalR)

The API exposes a real-time collaboration hub at **`/hubs/collaboration`** (see `Realtime.md`). After mutations are committed, command handlers notify connected clients:

| Event (SignalR method) | Audience | Triggered by |
|---|---|---|
| `projectChanged` | `project-{id}` group | project created / updated / deleted |
| `trackChanged` | `project-{id}` group | track created / updated / deleted (a track-list change visible to all project members) |
| `majorTaskChanged` | `track-{id}` group | major task created / updated / deleted (track-scoped) |
| `minorTaskChanged` | `track-{id}` group | minor task created / updated / deleted (track-scoped) |
| `eventChanged` | `project-{id}` group | event created / updated / deleted (project-scoped) |

Project events go to the project group; task events are **scoped to the track group** so members of one track only see their own track's major/minor task activity.

### Concurrent-operation lock (409)

Endpoints marked `[UserOperationLock]` reject a second in-flight request from the **same user for the same operation** with:

```json
// 409 Conflict
{
  "type": "https://tools.ietf.org/html/rfc7231",
  "title": "Conflict",
  "status": 409,
  "detail": "A request for this operation is already in progress. Please wait for it to finish and try again."
}
```

Applicable to: `refresh`, `verify-email/request`, `verify-email`, `password-reset/request`, `password-reset`.

### OTP behavior

- Always **8 numeric digits** (may include leading zeros), generated with a cryptographic RNG.
- Valid for **10 minutes**, single-use, and locked out after **5 failed attempts**.
- Values are stored **in-memory** (`IMemoryCache`), never persisted — restarting the API while a code is active invalidates it.

---

## Availability Keys (current)

| Key | Endpoint(s) |
|-----|-------------|
| `UserRegistration` | `POST /api/auth/register` |
| `UserLogin` | `POST /api/auth/login` |

Any endpoint can opt in with `[ApiAvailability("Key")]`; control lives in the `ApiAvailability` table and is managed through the admin routes above.