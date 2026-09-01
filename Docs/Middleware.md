# Middleware Pipeline & Request Flow

This document explains the ASP.NET Core middleware pipeline of **OctalPulse** — the order, the purpose of each middleware, and how a request flows through the system.

## Pipeline Overview

The pipeline is registered in `OctalPulse.API/Program.cs`:

```
Request IN
   │
   ▼
1. ExceptionHandlingMiddleware        ← outermost: catches everything
   │
   ▼
2. HTTPS Redirection                  ← framework (UseHttpsRedirection)
   │
   ▼
3. Authentication                     ← framework: validates the JWT bearer token
   │
   ▼
4. ApiAvailabilityMiddleware          ← our: rejects disabled endpoints (503)
   │
   ▼
5. UserOperationLockMiddleware        ← our: rejects concurrent duplicate requests (409)
   │
   ▼
6. Authorization                      ← framework: enforces [Authorize] / policies
   │
   ▼
7. Endpoint Execution (MapControllers / MVC action)
   │
   ▼
Response OUT
```

## Middleware by Layer

| # | Middleware | Type | Layer | Responsibility |
|---|------------|------|-------|----------------|
| 1 | `ExceptionHandlingMiddleware` | custom | API | Wraps everything; catches exceptions and maps them to consistent JSON error responses. |
| 2 | `UseHttpsRedirection` | framework | — | Redirects HTTP → HTTPS. |
| 3 | `UseAuthentication` | framework | — | Reads the `Authorization: Bearer <token>` header, validates it, populates `HttpContext.User`. |
| 4 | `ApiAvailabilityMiddleware` | custom | API | Reads `[ApiAvailability]` metadata; blocks disabled capabilities with `503 API_DISABLED`. |
| 5 | `UserOperationLockMiddleware` | custom | API | Reads `[UserOperationLock]` metadata; rejects a user's duplicate in-flight request with `409`. |
| 6 | `UseAuthorization` | framework | — | Enforces `[Authorize]`, roles, and policies (e.g. `AdminOnly`). |
| 7 | `MapControllers` | framework | — | Routes to the MVC action matching the endpoint. |

## Why this Order?

### ExceptionHandlingMiddleware must be outermost
It is registered **first**, so it wraps the entire pipeline. Any exception thrown by authentication, another middleware, authorization, or a controller action bubbles up to it, letting it write a single consistent JSON error body and log the failure. If it were placed later, exceptions from earlier middleware would fall through unhandled.

### Authentication before everything that inspects the user
`ApiAvailabilityMiddleware` and `UserOperationLockMiddleware` both need `HttpContext.User` (the availability admin-styled checks don't today, but the lock does). `UseAuthentication` must run first — otherwise the user claims (`sub`, `rank`) are empty.

### ApiAvailabilityMiddleware vs UserOperationLockMiddleware
- **Availability (503)** is checked first because it's the broader gate: if the whole endpoint is disabled, there is no point even acquiring a per-user lock.
- **User operation lock (409)** runs after, so a disabled endpoint short-circuits before lock bookkeeping.

Both run **before `UseAuthorization`**. This is deliberate:
- They only *short-circuit* on explicit metadata; a request for a disabled endpoint returns 503 regardless of who is calling (consistent, no info leak).
- `UseAuthorization` still runs afterward to reject unauthenticated/unauthorized callers before the controller executes.

> Note: `ApiAvailabilityMiddleware` does **not** depend on being authenticated — a disabled endpoint returns the same 503 to anonymous and authenticated callers, which is the correct "feature is off" signal.

### Endpoint routing
Endpoint routing is wired implicitly by the minimal hosting model (`WebApplication`). The `EndpointRoutingMiddleware` runs at the start of the pipeline so `HttpContext.GetEndpoint()` is populated by the time our middlewares read endpoint metadata. `GetEndpoint()` returns the matched route and its metadata collection, which is where `[ApiAvailability]` / `[UserOperationLock]` attributes surface.

## Middleware Details

### 1. ExceptionHandlingMiddleware

File: `OctalPulse.API/Middleware/ExceptionHandlingMiddleware.cs`

Catches exceptions and writes a JSON body. Default ASP.NET Core would return a plain-text 500; this middleware makes every error a structured, client-consistent response.

| Exception | HTTP Status | Error code |
|-----------|-------------|------------|
| `UnauthorizedException` (Application) | `401 Unauthorized` | `Unauthorized` |
| `ValidationException` (Application) | `400 Bad Request` | `Validation Error` |
| any other `Exception` | `500 Internal Server Error` | `Internal Server Error` (generic, no internals leaked) |

Body shape (all responses):

```json
{
  "error": "Validation Error",
  "message": "The OTP you entered is incorrect.",
  "statusCode": 400
}
```

The global handler logs unhandled exceptions via `ILogger` so the caller never sees stack traces or internal details.

### 2. ApiAvailabilityMiddleware

File: `OctalPulse.API/Middleware/ApiAvailabilityMiddleware.cs`

Business goal: **let an admin disable a whole API capability at runtime** without redeploying (see `ApiAvailability` table and the admin controller).

```
GetEndpoint()
   → metadata contains [ApiAvailability("Key")]?
       no  → next()                                   // endpoint not controlled
       yes → IApiAvailabilityService.IsEnabledAsync(Key)
                enabled  → next()
                disabled → 503 {"code":"API_DISABLED",
                                 "message":"This operation is currently unavailable."}
```

- The middleware depends **only on the Application-layer interface** `IApiAvailabilityService`; it never touches `DbContext` or Infrastructure directly.
- The attribute is pure metadata — it only carries the availability key.
- Reads are cached in `IMemoryCache` by the service; admin enable/disable invalidates the cache so changes take effect immediately.
- A key with no database row is treated as **enabled** (default allow).

HTTP status choice: **503 Service Unavailable** (`StatusCodes.Status503ServiceUnavailable`) — the operation is *temporarily unavailable*, not a permission problem (403), not a missing route (404), not a request conflict (409), and not an obscure `423 Locked`.

### 3. UserOperationLockMiddleware

File: `OctalPulse.API/Middleware/UserOperationLockMiddleware.cs`

Business goal: **prevent a user from accidentally firing the same request twice while the first is still executing** (double-click / slow network). This is *not* idempotency — once the first request finishes, the same request is allowed again.

```
GetEndpoint()
   → metadata contains [UserOperationLock("op")]?   (or none)
       no  → next()
       yes → read UserId from authenticated claims (sub / NameIdentifier)
               not authenticated / no id → next()    // e.g. anonymous endpoints
               → IUserOperationLock.TryAcquire(userId, operation)
                    acquired → next(), finally release (via IDisposable lease)
                    rejected → 409 {"title":"Conflict",
                                    "detail":"A request for this operation is already in progress..."}
```

Key points:

- **Operation scoping**: the lock key is `(UserId, Operation)`. The `Operation` is the explicit string passed to `[UserOperationLock("refresh")]`, or defaults to `METHOD:path`. Two different operations for the same user run concurrently; the same operation cannot overlap.
- **Concurrency primitive**: `UserOperationLock` (Infrastructure, singleton) keeps a `ConcurrentDictionary<LockKey, LockEntry>` where each entry is a tiny `bool IsActive` guarded by `lock`. `TryAcquire` atomically checks-and-sets; idle entries are evicted past a size cap to bound memory.
- **Release guarantee**: the middleware uses `using var lease = ...` inside the request scope, so the `finally`-style release fires on success, exception, or cancellation — a user can never be permanently locked.
- **Status code**: `409 Conflict` — the request conflicts with an already-executing request for the same user+operation.

### Pipeline placement rules (summary)

1. Custom user/threat checks that read `HttpContext.User` must come **after** `UseAuthentication`.
2. Cheap pre-checks (availability) should run before heavier/stateful ones (per-user locks).
3. All custom middleware that short-circuits must run **before** the controller action executes; nothing custom belongs *after* `MapControllers`.
4. `UseAuthorization` sits after our custom gates so the final permission decision is enforced before the action runs.

## Request Flow Example — Disabled Registration

```
POST /api/auth/register
   │
   ▼
ExceptionHandlingMiddleware        → try { ... }
   ▼
UseAuthentication                  → no bearer token? stays anonymous
   ▼
ApiAvailabilityMiddleware          → [ApiAvailability("UserRegistration")] found
                                     IsEnabledAsync("UserRegistration") = false (cached)
                                     → 503 {"code":"API_DISABLED", ...}
                                     (pipeline short-circuits here)
   ▼
(UseAuthorization / controller never reached)
```

## Request Flow Example — Concurrent Refresh

```
POST /api/auth/refresh  (bearer token present)
   ▼
UseAuthentication                  → user populated
   ▼
ApiAvailabilityMiddleware          → no [ApiAvailability] attribute → next()
   ▼
UserOperationLockMiddleware        → [UserOperationLock("refresh")] found
                                     TryAcquire(userId, "refresh")
                                       1st request → lease acquired → action runs
                                       2nd request (concurrent) → rejected 409
   ▼
UseAuthorization                   → user is authenticated → allowed
   ▼
RefreshTokenCommandHandler         → rotates refresh token, issues new pair
```

## Testing Middleware

- **Unit**: instantiate each middleware with a mocked `RequestDelegate` + mocked service dependencies; assert skipped endpoints call `next`, gated endpoints write the expected status/body.
- **Integration**: spin up `WebApplicationFactory<Program>` and call the endpoints described in the examples above; verify status codes and that enabling/disabling via the admin API takes effect on the next request (cache invalidation covered end-to-end).