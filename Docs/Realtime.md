# Real-time Collaboration (SignalR)

This document describes the real-time layer of **OctalPulse** — the SignalR hub, how clients connect/authenticate, the group model that scopes notifications, and the exact event names clients subscribe to.

## Overview

Real-time push is built on **SignalR** (`Microsoft.AspNetCore.SignalR`) and is used **only for things that genuinely need real-time delivery** — no polling is added where it isn't needed. Notifications are scoped so users only see what they are allowed to see:

- **Project-level events** go to the **project group** `project-{projectId}`.
- **Track-level task events** go to the **track group** `track-{trackId}` — a user who joins Track 2/3 only sees that track's major/minor task activity, not other tracks' task events, even though everyone in the project is part of the project group.

## Architecture / Flow

```
Command handler (Application)
   └─ after commit → IRealtimeNotifier (Application interface)
                        └─ SignalRNotifier (API) wraps IHubContext<CollaborationHub>
                             └─ Clients.Group("project-<id>" | "track-<id>")
                                  └─ SendAsync("projectChanged" | "trackChanged" | "majorTaskChanged" | "minorTaskChanged" | "eventChanged")
```

- The **Application** layer only knows `IRealtimeNotifier` — it never references SignalR directly.
- The **API** layer implements it with `SignalRNotifier` (`OctalPulse.API/Notifications`), which delegates to `IHubContext<CollaborationHub>`.
- Registered in `Program.cs`: `AddSignalR()`, the notifier DI mapping, and `app.MapHub<CollaborationHub>("/hubs/collaboration")`.

## Hub — `CollaborationHub`

Location: `OctalPulse.API/Hubs/CollaborationHub.cs`. It is `[Authorize]`.

### Connection & authentication

- Connect to: `http(s)://<host>/hubs/collaboration`
- The JWT is passed as a **query-string parameter** because SignalR (WebSockets/SSE) browsers can't set the `Authorization` header:
  `?access_token=<jwt>`
- `JwtBearerEvents.OnMessageReceived` in `Program.cs` reads `access_token` for any path starting with `/hubs/`.
- Unauthenticated connections are rejected (401 on connect).

### Hub methods (invoked by the client)

| Method | Signature | Behavior |
|---|---|---|
| `JoinProject` | `(Guid projectId)` | Adds the connection to `project-{projectId}` **only if** the caller is a non-deleted `ProjectMember` of that project. |
| `LeaveProject` | `(Guid projectId)` | Removes the connection from `project-{projectId}`. |
| `JoinTrack` | `(Guid trackId)` | Adds the connection to `track-{trackId}` **only if** the caller is a non-deleted `TrackMember` of that track. |
| `LeaveTrack` | `(Guid trackId)` | Removes the connection from `track-{trackId}`. |

Group naming helpers (internal to the hub): `ProjectGroupName(id) => $"project-{id}"`, `TrackGroupName(id) => $"track-{id}"`.

Membership is validated through `IUnitOfWork` (`ProjectMembers.AnyAsync(...)`, `TrackMembers.AnyAsync(...)`) using the authenticated user's `sub` claim.

## Notifier — events pushed to clients

`IRealtimeNotifier` (`OctalPulse.Application/Interface/Services`) declares track/project/task change notifications. `SignalRNotifier` maps each to a SignalR method on a group:

| Interface method | SignalR event sent | Group | Triggered by |
|---|---|---|---|
| `ProjectChangedAsync(projectId)` | `projectChanged` | `project-{id}` | project created / updated / deleted |
| `TrackChangedAsync(trackId, projectId)` | `trackChanged` | `project-{id}` | track created / updated / deleted (a track-list change visible to all project members) |
| `MajorTaskChangedAsync(trackId, majorTaskId)` | `majorTaskChanged` | `track-{id}` | major task created / updated / deleted (track-scoped) |
| `MinorTaskChangedAsync(trackId, minorTaskId)` | `minorTaskChanged` | `track-{id}` | minor task created / updated / deleted (track-scoped) |
| `EventChangedAsync(projectId, eventId)` | `eventChanged` | `project-{id}` | event created / updated / deleted (project-scoped) |

The handlers in `Features/Command/{Project,Track}/` call the notifier **after** the unit of work commits, so clients only learn about successful mutations.

## Client subscription summary

1. Connect to `/hubs/collaboration?access_token=<jwt>`.
2. `On("<event>", handler)` for the events you care about (e.g. `majorTaskChanged`).
3. `InvokeAsync("JoinProject", projectId)` to receive project-level events.
4. `InvokeAsync("JoinTrack", trackId)` to receive that track's task events.

Only members of a track are admitted to its group, so task events never leak across tracks.
