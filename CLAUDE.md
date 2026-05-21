# CartStack — project conventions

These conventions apply only to this repo. See `ROADMAP.md` for the build plan.

## Database migrations

EF Core migrations apply **automatically at app startup** via `db.Database.MigrateAsync()` inside `SeedData.EnsureSeededAsync` (called from `Program.cs`). Seeding runs in the same startup path and is idempotent.

- Authoring a schema change: `dotnet ef migrations add <Name>` by hand. This generates the migration code.
- Applying it: nothing — it runs on next start.
- Do not put `dotnet ef database update` in README setup steps, Dockerfile, or `fly.toml` release commands. The app applies its own schema.
- Single-instance deploy (Fly with one machine), so the parallel-startup migration race isn't a concern. Flag this convention if/when we scale out.

## Configuration

- Public, non-sensitive defaults live in `appsettings.json` (logging, connection string template).
- Secrets and personal/family-specific config (family code, member names) live in a gitignored `.env` at the repo root. Loaded by `Configuration/DotEnvLoader.cs` before `WebApplication.CreateBuilder`, then `AddEnvironmentVariables()` makes them available via `IConfiguration`.
- `.env.example` is tracked with placeholder values so a fresh clone knows what to fill in.
- Env var naming: double-underscore (`Family__Code`) maps to the nested config key (`Family:Code`).
- For list values (e.g. `Family__Members`), use comma-separated strings and split at the consumer — env vars don't bind cleanly to `string[]` the way JSON arrays do.
