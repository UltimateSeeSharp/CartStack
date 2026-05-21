# CartStack — project conventions

> v1 shipped — family in production at https://cartstack.fly.dev/.

These conventions apply only to this repo. See `POST_V1.md` for backlog.

## UI: MudBlazor only

The entire UI is built with MudBlazor. **No raw HTML controls anywhere** — no `<input>`, no `<button>`, no `<select>`, no `<form>`, no Bootstrap, no hand-rolled CSS for things MudBlazor covers. Every input, button, dialog, layout container, alert, snackbar, navigation, card uses the MudBlazor component for that purpose (`MudTextField`, `MudButton`, `MudSelect`, `MudPaper`, `MudAlert`, `MudSnackbar`, `MudAppBar`, `MudFab`, etc.).

If something seems easier with raw HTML (form posting, file upload), find the MudBlazor equivalent or build a thin wrapper that keeps the look. Login is a deliberate case in point — see Auth below.

## Authorization — DO NOT use a fallback policy

The natural-sounding pattern `AddAuthorization(opt => opt.FallbackPolicy = ...RequireAuthenticatedUser())` is **wrong** for Blazor Web Apps. The fallback policy applies to every endpoint that doesn't already have an explicit policy — including the SignalR `_blazor` hub, framework JS, MudBlazor JS/CSS, and CSS-isolation `.razor.js` artifacts. Anonymous browsers get 302-redirected to `/login` for those URLs, then the browser tries to parse the returned HTML as JS or JSON and the page breaks with `Unexpected token '<'`. The page goes blank.

`MapStaticAssets().AllowAnonymous()` only fixes the static-asset half. The `_blazor` hub registered by `AddInteractiveServerComponents` is not covered.

**Correct pattern:** no fallback policy. `@attribute [Authorize]` in `Components/_Imports.razor`, `[AllowAnonymous]` on `Login.razor`. The cookie handler's `LoginPath` redirects unauthenticated users.

```csharp
builder.Services.AddAuthorization();   // no FallbackPolicy
```

## Auth

Family-code shared login + cookie auth. No ASP.NET Core Identity.

- **Login flow** (`Components/Pages/Login.razor`): MudBlazor interactive form. Component validates `Family:Code` and the picked name in-process, mints a 30s data-protected ticket via `Auth/LoginTicketProtector.cs`, then `Nav.NavigateTo("/auth/sign-in?ticket=...", forceLoad: true)`. The static `MapGet("/auth/sign-in")` endpoint unprotects the ticket and calls `SignInAsync`. The ticket bridge exists because interactive Blazor handlers can't call `SignInAsync` — no `HttpContext` over the SignalR circuit.
- **No raw `<form action="...">` anywhere.**
- **Cookie**: `cartstack.auth`, `HttpOnly`, `SameSite=Lax`. `SecurePolicy=Always` in production, `SameAsRequest` in development. Persistent until logout: `IsPersistent=true`, 10-year expiry, sliding.
- **iOS standalone-PWA Safari drops non-`Secure` cookies on app close** — the cookie survives the session in memory but is gone the moment you reopen the PWA. That's why `SecurePolicy=Always` in production is mandatory, not optional.
- **`ForwardedHeaders` middleware** runs in production (`UseForwardedHeaders` before auth) so `Request.IsHttps` reflects the Fly edge's HTTPS termination, not the plain-HTTP hop into the container — otherwise `SecurePolicy=Always` would emit no cookie at all because the framework would think the request is HTTP.
- **`CurrentUserAccessor`** (scoped, `Services/CurrentUserAccessor.cs`) reads claims via `AuthenticationStateProvider`, **not** `IHttpContextAccessor` — the latter is unreliable across SSR/interactive boundaries in Blazor Web App.
- **Logout**: `GET /auth/sign-out` (MudButton with `Href=...`, no form).

## Production runtime (Fly)

- **Data Protection keys persist on the Fly volume**. `Program.cs` reads `DataProtection:KeysPath` from config and configures `PersistKeysToFileSystem` only if it's set. `fly.toml` points it at `/data/dp-keys`. Without this, every machine restart logs the whole family out and invalidates any in-flight `LoginTicketProtector` ticket.
- **`UseHttpsRedirection()` only in Development**. Fly terminates TLS at the edge and forwards plain HTTP; the middleware inside the container has nothing to redirect to and logs a noisy warning. `force_https = true` in `fly.toml` handles HTTPS enforcement at the edge.
- **`Family__Members` is hardfail**. `SeedData.EnsureSeededAsync` throws if no users exist *and* the config value is empty — better than booting with an empty login dropdown.
- **Single Fly machine** (`min_machines_running = 1`, `auto_stop_machines = "off"`). The in-memory `ChangeBroadcaster` only reaches users on the same machine — if we ever scale to ≥2 machines, broadcasting must move to Redis pub/sub or `fly-replay`. Update this section when that happens.
- **CI/CD via Fly's GitHub integration**, not a custom Actions workflow. Push to `main` → Fly's webhook deploys.

## Database migrations

EF Core migrations apply **automatically at startup** via `db.Database.MigrateAsync()` inside `Data/SeedData.cs` (called from `Program.cs`). Seeding runs in the same path and is idempotent.

- Schema change: `dotnet ef migrations add <Name>` (authoring).
- Apply: nothing — runs on next start.
- Do not put `dotnet ef database update` in Dockerfile, `fly.toml` release commands, or README setup steps.

## Configuration

- Public, non-sensitive defaults in `appsettings.json` (logging, connection string template).
- Secrets and family-specific config (family code, member names) in a gitignored `.env` at repo root. Loaded by `Configuration/DotEnvLoader.cs` before `WebApplication.CreateBuilder`; the host's default `AddEnvironmentVariables()` then makes them available via `IConfiguration`.
- `.env.example` is tracked with placeholder values.
- Env var naming: double-underscore (`Family__Code`) maps to the nested config key (`Family:Code`).
- List values (e.g. `Family__Members`) use comma-separated strings and are split at the consumer — env vars don't bind cleanly to `string[]` the way JSON arrays do.
- In production, `Family__Code` and `Family__Members` come from `fly secrets`, not from `.env` (the `.env` file stays out of the Docker image entirely via `.dockerignore`).
