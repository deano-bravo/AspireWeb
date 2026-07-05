---
description: Add/regenerate EF Core migrations for the two DbContexts (Identity + App) with correct ordering
argument-hint: <MigrationName> [identity|app|both]
---

# EF Core migration

Add an EF Core migration named `$1` for the requested context(s) (`$2`; infer from the change
if omitted — entity/table changes in `TenantDbContext` → app, Identity/`Tenants` changes → identity).

1. `dotnet tool restore` — `dotnet-ef` is a pinned **local** tool and must stay on the exact
   same `11.0.0-preview.*` build as the EF packages (they move together; see the comments in
   `Directory.Packages.props`).
2. Scaffold (design-time factories make `AspireWeb.Data` self-sufficient — no host boot, no
   DB connection needed; history tables are `__ef_migrations_identity` / `__ef_migrations_app`):

   ```powershell
   dotnet ef migrations add <Name> --project AspireWeb.Data --startup-project AspireWeb.Data --context ApplicationDbContext --output-dir Migrations/Identity
   dotnet ef migrations add <Name> --project AspireWeb.Data --startup-project AspireWeb.Data --context TenantDbContext      --output-dir Migrations/App
   ```

3. Inspect the generated migration — invariants:
   - MigrationService applies the **Identity context first** (it owns the `Tenants` table that
     `TenantDbContext` FKs target).
   - `TenantDbContext` maps `Tenants` with `ExcludeFromMigrations` — an App migration containing
     `CreateTable("Tenants")` is a bug: delete the migration and re-scaffold.
   - `Migrations/**` is `generated_code` — **never hand-edit**; fix the model and regenerate.
4. Migrations are applied at runtime by the MigrationService — `dotnet ef database update` is
   NOT the normal path (hence its `ask` permission).
5. Verify: `dotnet build AspireWeb.slnx -c Release -warnaserror` and the fast test tier
   (`dotnet test --solution AspireWeb.slnx -c Release -- --filter-not-trait Category=Integration`).

At EF 11 RC/GA: **regenerate (don't chain)** the two Initial migrations, bumping every
EF-adjacent package and the `dotnet-ef` local tool together.
