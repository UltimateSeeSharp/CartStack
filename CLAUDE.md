# CartStack — project conventions

These conventions apply only to this repo. See `ROADMAP.md` for the build plan.

## Database migrations

EF Core migrations apply **automatically at app startup** via `db.Database.MigrateAsync()` inside `SeedData.EnsureSeededAsync` (called from `Program.cs`). Seeding runs in the same startup path and is idempotent.

- Authoring a schema change: `dotnet ef migrations add <Name>` by hand. This generates the migration code.
- Applying it: nothing — it runs on next start.
- Do not put `dotnet ef database update` in README setup steps, Dockerfile, or `fly.toml` release commands. The app applies its own schema.
- Single-instance deploy (Fly with one machine), so the parallel-startup migration race isn't a concern. Flag this convention if/when we scale out.

## UI: MudBlazor only

The entire UI is built with MudBlazor. **No raw HTML controls anywhere** — no `<input>`, no `<button>`, no `<select>`, no `<form>`, no Bootstrap, no hand-rolled CSS for things MudBlazor covers. Every input, button, dialog, layout container, alert, snackbar, navigation, card, etc. uses the MudBlazor component for that purpose (`MudTextField`, `MudButton`, `MudSelect`, `MudForm`/`EditForm`-with-MudBlazor-children, `MudPaper`, `MudAlert`, `MudSnackbar`, `MudAppBar`, `MudFab`, etc.).

This is a hard rule. If something seems easier with raw HTML (form posting, file upload, etc.), find the MudBlazor equivalent or build a thin wrapper that keeps the MudBlazor look. Auth/login is no exception — login form is MudTextField + MudSelect + MudButton inside MudPaper, submitted via Blazor interactivity, never via a raw `<form action="...">`.

## Authorization in this app — DO NOT use a fallback policy

The natural-sounding pattern `AddAuthorization(opt => opt.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())` is **wrong** for Blazor Web Apps. The fallback policy applies to every endpoint that doesn't already have an explicit policy — which includes the SignalR `_blazor` hub, static assets, framework JS files, and CSS-isolation `.razor.js` artifacts. Anonymous browsers get 302-redirected to `/login` for those URLs, then the browser tries to parse the returned HTML as JS or JSON and the page breaks with `Unexpected token '<'` (sometimes "not valid JS module", sometimes "not valid JSON" depending on which fetch failed). The page goes blank.

`MapStaticAssets().AllowAnonymous()` only fixes the static-asset half. The `_blazor` SignalR hub registered by `AddInteractiveServerComponents` is not covered.

**The correct pattern:** no fallback policy. Apply `[Authorize]` per-page (or globally via `@attribute [Authorize]` in `Components/_Imports.razor`), and `[AllowAnonymous]` on the login page. Framework/asset URLs are then unaffected by auth.

```csharp
builder.Services.AddAuthorization();  // no FallbackPolicy

// _Imports.razor:
@attribute [Authorize]

// Login.razor:
@attribute [AllowAnonymous]
```

The cookie handler's `LoginPath` redirects unauthenticated users to `/login` when they hit a protected page — that's the right place for the redirect, not the global fallback.

## Auth

Family-code login + cookie auth. Not ASP.NET Core Identity.

- One shared `Family:Code` from `.env` (never in `appsettings.json`). Login form posts `code` + `name` to `/auth/login` (minimal API). Endpoint validates the code, looks up the active `User` row by name, signs in via cookie auth.
- Cookie name: `cartstack.auth`. `HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest` (so HTTP dev still works; Fly is always HTTPS in prod).
- Persistent until logout: `IsPersistent=true`, `ExpiresUtc=now+10y`. Sliding expiration enabled. There is no idle timeout — family stays logged in indefinitely.
- Fallback authorization policy is `RequireAuthenticatedUser()`. Unauth users hitting any page get redirected to `/login` via `<AuthorizeRouteView>` + `RedirectToLogin`. The `/login` Razor page is marked `[AllowAnonymous]`.
- Auth endpoints (`/auth/login`, `/auth/logout`) use `.DisableAntiforgery()`. Justification: the shared family code is itself the secret, the cookie isn't a value an attacker can forge into a form, and these are unauthenticated POSTs by design. If the threat model ever changes, add antiforgery tokens via `<AntiforgeryToken />` in the form.
- `CurrentUserAccessor` (scoped) reads the user id / name from `AuthenticationStateProvider`. Components should depend on this, not on `IHttpContextAccessor` (which is unreliable across SSR/interactive boundaries in Blazor Web App).

## Barcode data sources (for Phase 4.5)

- Free, usable: **Open Food Facts** (`world.openfoodfacts.org/api/v2/product/<ean>.json`). Good for branded goods, weak on Austrian private labels (Hofer "Zurück zum Ursprung", Spar "S-Budget"/"Clever", Baumarkt SKUs).
- Authoritative but unusable here: GS1 Austria (`gs1.at`) is the registrar of EANs starting with `90`/`91` but bulk access is paid B2B. GEPIR portal returns brand owner only.
- **Do not** scrape `spar.at` / `hofer.at` / `billa.at` — ToS violation, structures change, fragile.
- Do not buy commercial barcode-lookup APIs (UPCitemdb, EAN-Search, Barcodelookup) expecting AT private-label coverage — they're US/UK-centric.
- The real database is the family-local `BarcodeLookup` cache populated by first scans; OFF is a one-time bootstrap per EAN.

## Configuration

- Public, non-sensitive defaults live in `appsettings.json` (logging, connection string template).
- Secrets and personal/family-specific config (family code, member names) live in a gitignored `.env` at the repo root. Loaded by `Configuration/DotEnvLoader.cs` before `WebApplication.CreateBuilder`, then `AddEnvironmentVariables()` makes them available via `IConfiguration`.
- `.env.example` is tracked with placeholder values so a fresh clone knows what to fill in.
- Env var naming: double-underscore (`Family__Code`) maps to the nested config key (`Family:Code`).
- For list values (e.g. `Family__Members`), use comma-separated strings and split at the consumer — env vars don't bind cleanly to `string[]` the way JSON arrays do.
