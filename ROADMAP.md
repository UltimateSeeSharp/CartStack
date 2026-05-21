# CartStack — Family Grocery Web App

A small mobile-first grocery list for the Vinci family (David, Julian, Angelika, Andreas). Everyone adds items from their phone, tags each with which store (Spar, Hofer, Baumarkt), marks bought, sees purchase history. Quick-add favorites + typeahead from history on top of the core list.

**Stack:** Blazor Web App (.NET 10, InteractiveServer) + **MudBlazor** UI + EF Core + SQLite + Fly.io. UI in German labels; app name "CartStack" everywhere.

**Accepted limitation:** Blazor Server's WebSocket drops on phone screen-lock; you see a brief "Verbindung wird wiederhergestellt…" overlay. Fine for family use.

---

## Phase 0 — Foundations (≈2h)

**Goal:** empty project that builds, runs, and shows a "CartStack — folgt" placeholder.

- `dotnet new blazor -int Server -ai -e -o . -n CartStack -f net10.0`
- RootNamespace `CartStack`, target `net10.0`.
- `git init`, `.gitignore` (`bin/`, `obj/`, `*.db`, `*.db-shm`, `*.db-wal`).
- Replace `Home.razor` with "CartStack — folgt".
- Set `de-AT` culture in `Program.cs`.
- **Add MudBlazor**: `dotnet add package MudBlazor`, `AddMudServices()` in `Program.cs`, `@using MudBlazor` in `_Imports.razor`, MudBlazor CSS+JS in `App.razor` head/body, providers (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) + `<MudLayout>` in `MainLayout.razor`. Home page uses `MudContainer` + `MudText` to prove wiring.
- **Default font: Inter.** Custom `MudTheme` in `Components/AppTheme.cs` sets `Typography.*.FontFamily = ["Inter", "ui-sans-serif", "system-ui", …]`. Google Fonts `<link>` for Inter in `App.razor` head. `<MudThemeProvider Theme="AppTheme.Default" />` in `MainLayout.razor`.

**Gate:** `dotnet run` → placeholder renders in Inter via MudBlazor typography. Commit: `Phase 0: scaffold CartStack + MudBlazor + Inter`.

## Phase 1 — Data layer (≈2h)

**Goal:** EF Core + SQLite, schema migrated, seed data in.

- NuGet: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`. Local tool `dotnet-ef`.
- `Models/`: `User`, `Store`, `GroceryItem`, `Favorite`, `ItemStatus` enum.
- `Data/AppDbContext.cs` + `Data/SeedData.cs` (idempotent). `SeedData.EnsureSeededAsync` calls `db.Database.MigrateAsync()` first — migrations apply automatically on startup; no manual `dotnet ef database update` needed (see `CLAUDE.md`).
- `Data/DesignTimeDbContextFactory.cs` so `dotnet ef migrations add` works without booting the app.
- Public config in `appsettings.json` (`ConnectionStrings:Db`). Family-specific values in a gitignored `.env` (`Family__Code`, `Family__Members`) — see `.env.example`. Loader: `Configuration/DotEnvLoader.cs`.
- Seed stores: Spar, Hofer, Baumarkt.
- `dotnet ef migrations add Init` — the migration code is committed; applying it is automatic on next app start.

**Gate (you run the app, I don't):** start the app, confirm `app.db` is created and contains 4 users + 3 stores (any SQLite viewer, or check `Users` / `Stores` table counts). Commit: `Phase 1: data layer`.

## Phase 2 — Auth (≈1.5h)

**Goal:** family-code login with cookie ticket. No ASP.NET Core Identity.

- Cookie auth, 30-day sliding expiration, `RequireAuthenticatedUser` fallback.
- `Components/Pages/Login.razor` at `/login`: Familiencode input + name dropdown + "Angemeldet bleiben". Validates code, issues cookie ticket, redirects to `/`.
- `Services/CurrentUserAccessor.cs` (scoped): exposes `UserId`, `UserName`.
- `/logout` endpoint.

**Gate:** anon → redirect to `/login`; wrong code → error; right code + name → `/` with name visible; logout works. Commit: `Phase 2: auth`.

## Phase 3 — Service layer + live updates (≈2h)

**Goal:** all mutations through one service that broadcasts changes to every connected user.

- `Services/IGroceryService.cs` + `GroceryService.cs` (scoped, EF-backed, raises broadcaster on every mutation).
- `Services/ChangeBroadcaster.cs` (singleton, `event Action<ChangeEvent> Changed`).
- `Services/NameSuggestionCache.cs` (singleton, DISTINCT item names last 180 days, invalidated on Add).
- DI wiring.

**Gate:** temp `/test` page → `AddItem("Milch", 1, sparId)` → other browser tab logs the event. Commit: `Phase 3: service layer`.

## Phase 4 — Liste page + add dialog (≈3.5h)

**Goal:** the core screen — mobile-first list grouped by store, add via FAB, mark bought inline, live across tabs.

- `Components/Pages/List.razor` at `/` (built with MudBlazor):
  - Top: empty horizontal `MudChipSet` placeholder for favorites (populated in Phase 6).
  - Items in `MudList` grouped by store via section headers, each item with a leading `MudCheckBox` and subtle "von David" text. Large enough to tap.
  - `MudFab` "+" bottom-right → opens `<AddItemDialog>` via `IDialogService`.
- `AddItemDialog.razor` (MudDialog): `MudTextField` name, `MudNumericField` qty, `MudSelect` store. Buttons: "Hinzufügen" / "Abbrechen".
- Subscribe to `ChangeBroadcaster` in `OnInitializedAsync`, `InvokeAsync(StateHasChanged)` on Item events, unsubscribe in `Dispose`.

**Gate:** two browser windows, two users → add in A → appears in B within ~1s; mark bought in B → vanishes from A. Commit: `Phase 4: liste page`.

## Phase 5 — Name typeahead (≈0.5h)

**Goal:** typeahead so spellings stay consistent.

- Replace the name `MudTextField` in `AddItemDialog` with `MudAutocomplete<string>` bound to `IGroceryService.GetNameSuggestions(prefix)`.
- `CoerceText="true"`, `ResetValueOnEmptyText="true"`, `MaxItems="5"`.

**Gate:** type "Mi" → "Milch" appears if previously added. Commit: `Phase 5: typeahead`.

## Phase 6 — Favoriten + Verlauf + Geschäfte (≈2.5h)

**Goal:** the three secondary screens. Now the chip row on Liste lights up.

- `Pages/Favorites.razor` at `/favoriten`: CRUD + up/down reorder. Each favorite = name + default store.
- Wire chip row on Liste: tap chip → `AddItem(name, 1, defaultStoreId)`. If that name already exists `Pending`, grey the chip.
- `Pages/History.razor` at `/verlauf`: reverse-chronological bought items, paged 50, grouped by day.
- `Pages/Stores.razor` at `/geschaefte`: CRUD; prevent delete if items reference (show count, offer reassign).

**Gate:** create "Milch → Spar" favorite → chip → item on Liste → mark bought → entry in Verlauf with "von David" + timestamp. Commit: `Phase 6: secondary pages`.

## Phase 7 — Mobile polish + bottom nav (≈1h)

**Goal:** feels like an app on a phone.

- Replace the bottom region of `MainLayout.razor` with a fixed bottom nav. Two options to compare in place: native `<nav>` with `safe-area-inset-bottom` CSS padding, or `MudPaper` + `MudIconButton`s pinned via custom CSS. Choose whichever ends up cleaner.
- Active route highlighted (use `NavLink` `active` class or `NavigationManager` comparison).
- `MudSnackbar` for "Milch hinzugefügt" toasts (`ISnackbar.Add(...)`).
- Strip `app.css` down — MudBlazor provides most styling. Keep one media query for >768px to widen content on desktop.

**Gate:** open on phone via dev tunnel — looks correct, bottom nav usable, snackbar toasts on add. Commit: `Phase 7: mobile polish`.

## Phase 8 — PWA installability (≈1h)

**Goal:** "Zum Home-Bildschirm" installs the app with the right icon and name.

- `wwwroot/manifest.webmanifest`: `name: "CartStack"`, `short_name: "CartStack"`, `start_url: "/"`, `display: "standalone"`, `theme_color`, `lang: "de"`, 192 + 512 PNG icons.
- Manifest link + apple-touch-icon + apple-mobile-web-app-capable meta in `App.razor` head.
- Minimal `service-worker.js`: register only, NO offline caching (Blazor Server can't run offline; stale list is worse than no list).

**Gate:** deferred to Phase 9 (PWA install requires HTTPS, won't work against `localhost` from a phone). Commit: `Phase 8: PWA manifest`.

## Phase 9 — Deploy to Fly.io (≈2h)

**Goal:** family can actually use it.

- `Dockerfile`: multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`, expose 8080, `ENTRYPOINT ["dotnet", "CartStack.dll"]`.
- `fly.toml`: `app = "cartstack"` (or `cartstack-vinci` if taken), `primary_region = "fra"`, `internal_port = 8080`, `force_https = true`, `auto_stop_machines = "off"`, `min_machines_running = 1`.
- `fly volumes create data --size 1 --region fra`, mount at `/data`, override connection string to `Data Source=/data/app.db`.
- `fly launch --no-deploy` → `fly deploy`.

**Gate:**
1. Open Fly URL on phone → login works.
2. "Zum Home-Bildschirm" → standalone app launches.
3. Two phones, two users → add on one → appears on the other within ~1s.
4. `fly machine restart` → data persists (volume mount).
5. Tab idle 10 min → reconnects on return.

Commit: `Phase 9: deploy`. Tag `v1.0`.

---

## Total effort

~17h end-to-end. Each phase ships a verifiable state — any phase is a valid stopping point.

## Future (not in v1)

- Drag-sort for stores and favorites.
- Item edit (currently delete-and-readd).
- Push notifications.
- `litestream` backups to S3/B2.
- Per-user attribution colors.
