# Blazor Frontend Integration Guide

This document tracks every backend feature synced from upstream that needs a corresponding
Blazor UI. Updated each sync phase. The upstream ships a React + Vite admin/dashboard —
we skip those and build equivalents in `src/Playground/Playground.Blazor/`.

---

## How to read this

- **Status: Pending** — backend API exists, no Blazor page yet
- **Status: Done** — Blazor page/component built
- **Endpoint** — the API route your Blazor component calls
- **Notes** — behaviour differences or Blazor-specific considerations

---

## Phase 7 — SaaS Features

### 1. User Impersonation
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `POST api/v1/identity/impersonation/start`, `POST api/v1/identity/impersonation/end` |
| Permission | `Identity.Impersonation` (root tenant operators only for cross-tenant; tenant admins limited to own tenant) |
| Notes | On start, response is a short-lived access-only token (no refresh). Store separately from the normal session token. Show a persistent banner ("You are impersonating {user}") with an End button. Impersonation token carries `act_sub` / `act_tenant` claims per RFC 8693. |

### 2. File Storage (MinIO / S3)
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | Existing storage endpoints under `api/v1/storage/` |
| Notes | Upload via multipart form. Display public URL from `Storage__S3__PublicBaseUrl`. Quota metering fires automatically on upload/delete — show current storage usage against plan limit. |

### 3. Quota Display
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `GET api/v1/billing/subscriptions/me`, `GET api/v1/billing/usage` |
| Notes | Show a quota strip on the tenant dashboard: API calls used / limit, storage bytes used / limit, user count used / limit. Overage rows should be highlighted. Refresh via SSE live feed (see item 5). |

### 4. Billing — Plans, Subscriptions, Invoices
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `GET api/v1/billing/plans`, `POST api/v1/billing/plans`, `PUT api/v1/billing/plans/{id}`, `POST api/v1/billing/subscriptions/assign`, `GET api/v1/billing/subscriptions/me`, `GET api/v1/billing/invoices`, `GET api/v1/billing/invoices/mine`, `GET api/v1/billing/invoices/{id}`, `POST api/v1/billing/invoices/generate`, `POST api/v1/billing/invoices/{id}/issue`, `POST api/v1/billing/invoices/{id}/mark-paid`, `POST api/v1/billing/invoices/{id}/void`, `POST api/v1/billing/usage/capture` |
| Permission | Plans/Subscriptions: root tenant admin. Invoices: tenant admin can see own. |
| Notes | Root admin pages: plan CRUD, assign subscription to tenant, generate/issue/void invoices, manual usage snapshot. Tenant pages: view current subscription and invoice history. |

### 5. SSE Realtime Feed
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `GET api/v1/sse/token` (get single-use token), then `GET api/v1/sse/stream?token={token}` |
| Notes | **Do NOT use the native `EventSource` API** — it auto-reconnects with the original URL, which replays the already-consumed single-use token and gets a 401. Use `HttpClient` streaming with `ReadAsStreamAsync` instead. Mint a fresh token on each (re)connect. Implement exponential backoff (1s → 30s). Use `X-Tenant-Id` header on the stream request. |

### 6. Rate Limiting — User Feedback
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Notes | No dedicated page needed. Handle HTTP 429 responses globally in `AuthorizationHeaderHandler`. Show a toast with the `Retry-After` header value ("Too many requests — try again in {n}s"). |

---

## Phase 8 — API Hardening

### 7. TOTP Two-Factor Authentication
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `POST api/v1/identity/two-factor/enroll` (returns QR code URI + secret), `POST api/v1/identity/two-factor/verify-enroll` (confirm with TOTP code), `POST api/v1/identity/two-factor/disable` |
| Notes | Add a "Security" section to the user profile page. Enroll: show QR code image (generate from `otpauth://` URI using a JS interop or a .NET QR library), then prompt for the first TOTP code to confirm. Disable: require current TOTP code. Login flow: after password check, if 2FA is enabled the token response signals a `requiresTwoFactor` flag — show a second step prompting for the TOTP code before issuing the JWT. |

### 8. Account Lockout — Admin Unlock
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | Existing user management endpoints — check `LockoutEnd` field on `GET api/v1/identity/users/{id}` |
| Notes | In the user detail/list page, show a "Locked" badge when `LockoutEnd` is in the future. Add an "Unlock" action that calls the toggle-status or a dedicated unlock endpoint. Lockout is automatic after consecutive failed logins (configured via `IdentityOptions.Lockout`). |

### 9. Webhook Subscription Management
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `GET api/v1/webhooks/subscriptions`, `POST api/v1/webhooks/subscriptions`, `DELETE api/v1/webhooks/subscriptions/{id}`, `GET api/v1/webhooks/deliveries` |
| Notes | Tenant admin page: list active subscriptions (URL, event types, active toggle), create new subscription, view delivery history with status (Success/Failed) and payload. Deliveries are retried by Hangfire on transient failure — show retry count. |

---

## Phase 10 — New Modules (Upcoming)

### 10. Tickets Module
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `POST api/v1/tickets`, `GET api/v1/tickets` (search: status/priority/assignee/reporter/text), `GET api/v1/tickets/{id}`, `POST api/v1/tickets/{id}/assign`, `POST api/v1/tickets/{id}/resolve`, `POST api/v1/tickets/{id}/reopen`, `POST api/v1/tickets/{id}/comments`, `GET api/v1/tickets/{id}/comments` |
| Permission | `Tickets.View`, `Tickets.Create`, `Tickets.Update`, `Tickets.Delete`, `Tickets.Assign`, `Tickets.Resolve`, `Tickets.Reopen`, `Tickets.Comment` |
| Notes | State machine: `Open → InProgress → Resolved → Closed`. Illegal transitions return 409 — handle gracefully. Sequential numbers (`TK-1`, `TK-2`) are tenant-scoped. No cross-tenant view — root admin must use impersonation to see another tenant's tickets. A ticket assigned at creation starts as `InProgress`. Unassigning an `InProgress` ticket reverts to `Open`. Closed tickets cannot accept comments — show accordingly. |

### 11. Tenant-Wide Session Management
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Endpoints | `GET api/v1/identity/sessions/tenant` (paged, filterable: search by user name/email/IP, `includeInactive` flag) |
| Permission | Tenant admin only |
| Notes | Admin page showing all active sessions across the tenant. Searchable by user name, email, or IP. "Active only" by default. Can revoke individual sessions. Reuses existing per-user session revoke endpoint. |

### 12. System Role / Last-Admin Guards — UI Feedback
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Notes | No new endpoints — existing role management endpoints now return 400/409 when attempting to delete or rename system roles (`Admin`, `Basic`). Update the roles page to handle these errors gracefully with a descriptive message ("System roles cannot be deleted"). Similarly for the last-admin guard — if deleting or deactivating the last admin user returns an error, surface it as a toast. |

---

## Phase 9 — JWT / Identity Fixes (Blazor Impact)

### 13. JWT Claim Changes — Update Blazor Claim Parsing
| Field | Value |
|-------|-------|
| Status | **Pending** |
| Notes | Two JWT changes affect how Blazor reads user identity from tokens: (1) The `sub` claim is now emitted alongside `ClaimTypes.NameIdentifier` (RFC 7519 compliance). (2) RFC short claim names are now emitted — the server sends `sub`, `name`, `email` instead of the long `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/...` URIs. If `Playground.Blazor` uses `ClaimTypes.Name` or `ClaimTypes.Email` to display the username, switch to reading the short-name claims (`name`, `email`, `sub`) or configure `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` in `Program.cs` so the short names are preserved as-is. This fixes the "Unknown" username display seen in the dashboard. |

---

## Phase 10 (Mixed Commits) — Permission Architecture Refactoring

### 14. PermissionConstants Refactoring — SystemPermissions

Backend-only change; Blazor UI action: **none required, but be aware**.

`PermissionConstants._all` is now empty at startup. Modules call `PermissionConstants.Register()` during `ConfigureServices`. The `Web/Extensions.cs` bootstrapper calls `PermissionConstants.Register(SystemPermissions.All)` to seed platform-level permissions (Hangfire, Dashboard, Platform.Tenants, Platform.Plans, etc.).

If any Blazor page hard-codes a permission string from the old `PermissionConstants` initializer (e.g., `Permissions.Tenants.View`), note that these strings changed: tenants permissions are now under `Platform.Tenants` (e.g., `Platform.Tenants.View`). Fetch the effective permission list from the API rather than maintaining a client-side copy.

### 15. X-FSH-App Header on Token Endpoint

`POST api/v1/identity/tokens` now checks for an optional `X-FSH-App` header.  
- If `X-FSH-App: dashboard` with `tenant: root` → **403 Forbidden** (SuperAdmin must use the admin app shell).  
- Blazor never sends this header, so there is **no impact** — the Blazor app can authenticate as SuperAdmin from the root tenant context without any changes.  
- No UI action needed unless you decide to apply the same app-boundary guard in the Blazor shell.

---

## Infrastructure (No UI Needed)

| Feature | Reason |
|---------|--------|
| HybridCache | Transparent to UI — inject `HybridCache` in services |
| Idempotency middleware | Transparent — `Idempotency-Key` header sent by `HttpClient` handler if needed |
| Rate limiting (enforcement) | Handle 429 responses — see item 6 above |
| CORS fix | Transparent |
| Quota metering (API calls, storage) | Metered server-side — only the display (item 3) needs UI |
| Webhook Hangfire dispatcher | Background job — no UI beyond delivery history (item 9) |
| MinIO wiring | Infrastructure — UI is item 2 above |
| Audit recursion guard | Backend interceptor fix — no UI impact |
| AccessTokenMinutes 15→45 | Dev config change — no UI change needed |

---

## Blazor-Specific Implementation Notes

### SSE Token Refresh Pattern
The SSE stream uses single-use opaque tokens. Implement the connection loop in a Blazor service:
```
1. GET /sse/token  → single-use token
2. Open HttpClient stream to /sse/stream?token={token}
3. On disconnect/error → exponential backoff → go to step 1
```
Never reconnect with the same token. Each reconnect mints a fresh one.

### Impersonation Banner
Persist impersonation state in a scoped service. Check `act_sub` claim on the JWT.
When impersonating: disable any "switch tenant" actions, show a fixed top banner, and End button calls `/impersonation/end` then restores the original token.

### 2FA Login Flow
After `POST /tokens` if response has `requiresTwoFactor: true`:
- Do NOT store the token yet
- Show a TOTP code input dialog
- `POST /tokens/two-factor` with the TOTP code to get the real JWT