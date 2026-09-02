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

All routes in this group require an authenticated user (`[Authorize]`); any member can create, list, view, or update projects. The creator is automatically joined as a `ProjectManager` member.

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
- `progress` is **not accepted** — always starts at `0` (auto-calculated from tracks)
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

### GET `/api/projects/{id}`

Full project details: basic info, who created it, and its members.

```json
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

### PUT `/api/projects/{id}`

Updates title, description, progress, and status of the project.

```json
// Request
{
  "title": "OctalPulse Web App v2",
  "description": "Now with team reports.",
  "status": "Active"
}
```

- `progress` is **not accepted** — auto-calculated from existing tracks
- `200` — `UpdateProjectResponse` (`id`, `title`, `description`, `progress`, `status`, `modifiedDate`)
- `404` — project not found; `400` — invalid fields

---

### DELETE `/api/projects/{id}`

Soft-deletes the project **and everything under it** — members (and their project roles), tracks, track members, major tasks, minor tasks, and project events. All rows are flagged `IsDeleted`, never hard-deleted. The project disappears from the list and detail endpoints immediately.

- `204 No Content` — deleted (idempotent; re-deleting an already deleted project also returns `204`)
- `404` — project does not exist

---

## Tracks

All routes in this group require an authenticated user. Track CRUD is on `TracksController`; list-by-project lives on `ProjectsController`. All parameters are sent as JSON `[FromBody]`.

### GET `/api/projects/{id}/tracks`

Returns all tracks in the given project (newest created first).

```json
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
  "description": "API and database work",
  "progress": 10
}
```

- `progress`: 0–100 (default `0`)
- `200` — `CreateTrackResponse` (id, projectId, name, description, progress)
- `404` — project does not exist; `400` — invalid fields

---

### PUT `/api/tracks/{id}`

Updates a track's name, description, and progress.

```json
// Request
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "name": "Backend Track v2",
  "description": "Updated scope",
  "progress": 60
}
```

- `projectId` is required in the body
- `200` — `UpdateTrackResponse` (id, projectId, name, description, progress, modifiedDate)
- `404` — track not found; `400` — invalid fields

---

### DELETE `/api/tracks/{id}`

Soft-deletes the track **and everything under it** — track members, major tasks, and their minor tasks. All rows are flagged `IsDeleted`, never hard-deleted.

- `204 No Content` — deleted (idempotent; re-deleting an already deleted track returns `204`)
- `404` — track does not exist

---

## Admin — API Availability

All routes in this group are protected by the `AdminOnly` policy, which requires the caller to be an authenticated user whose **database record** has `Rank = Admin` (`ApiAvailabilityController` is `[Authorize(Policy = "AdminOnly")]`). A normal member gets `403 Forbidden`.

### POST `/api/admin/ApiAvailability/{key}/enable`

Enables a named capability. Creates the row on first use; unknown keys default to enabled anyway.

- `200` — `{ "key": "UserRegistration", "isEnabled": true }`

### POST `/api/admin/ApiAvailability/{key}/disable`

Disables a named capability. Takes effect on the next request (the in-memory cache is invalidated immediately).

```json
// 200
{ "key": "UserRegistration", "isEnabled": false }
```

### GET `/api/admin/ApiAvailability/{key}`

Returns the current status. A key never touched by an admin reports the default (`isEnabled: true`).

```json
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