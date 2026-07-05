# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository.

## What this is

**AspireWeb** — a .NET Aspire distributed app scaffolded as a **multi-tenant SaaS**: Blazor Server
front end (ASP.NET Core Identity) calling a minimal-API backend, PostgreSQL + EF Core with
row-level tenant isolation, an Aspire AppHost with shared ServiceDefaults/Contracts, a migration
worker, and a two-tier test project.

- **Stack:** .NET 11 (preview) · Aspire 13.4.6 · **EF Core 11 preview** · PostgreSQL · `Nullable` + `ImplicitUsings`.
- **SDK is pinned** in [global.json](global.json) — use that exact SDK; do not bump it casually.
- Solution file: [AspireWeb.slnx](AspireWeb.slnx) (the new XML solution format).

## Projects

| Project | Path | Role |
| --- | --- | --- |
| AppHost | [AspireWeb.AppHost/](AspireWeb.AppHost/) | Aspire orchestrator & entry point ([AppHost.cs](AspireWeb.AppHost/AppHost.cs)). |
| ApiService | [AspireWeb.ApiService/](AspireWeb.ApiService/) | Minimal-API backend. Anonymous `/weatherforecast`; tenant-scoped `/todos` behind JWT bearer auth. |
| Web | [AspireWeb.Web/](AspireWeb.Web/) | Blazor Server front end. Identity cookie auth (`Components/Account/**`), tenant-aware registration, typed API clients in `Clients/`. |
| Contracts | [AspireWeb.Contracts/](AspireWeb.Contracts/) | Shared web↔api wire contracts. Deliberately dependency-free. |
| Data | [AspireWeb.Data/](AspireWeb.Data/) | Entities, both DbContexts + shared registration extensions, tenancy primitives, EF migrations, design-time factories. |
| MigrationService | [AspireWeb.MigrationService/](AspireWeb.MigrationService/) | Applies EF migrations for both contexts at startup; AppHost gates web/api on `WaitForCompletion`. |
| ServiceDefaults | [AspireWeb.ServiceDefaults/](AspireWeb.ServiceDefaults/) | Shared OTel/health/service-discovery/resilience + the shared tenancy contract under [`Tenancy/`](AspireWeb.ServiceDefaults/Tenancy/) (`TenantClaimTypes`, `TenantPolicies`, `ApiJwtDefaults`). |
| Tests | [AspireWeb.Tests/](AspireWeb.Tests/) | xUnit v3 on Microsoft.Testing.Platform. Fast unit tier (no Docker) + `Category=Integration` over the full AppHost. `AppFixture` is a collection fixture — deliberately NOT an assembly fixture (xunit creates those eagerly even for filtered runs). |

**Topology** ([AppHost.cs](AspireWeb.AppHost/AppHost.cs)): `postgres` (container; `appdb`; pgweb in
run mode; data volume only when publishing) → `migrationservice` (runs to completion) → `apiservice`
and `webfrontend` (`WaitForCompletion`), with `webfrontend` also referencing `apiservice`.
Cross-service calls use Aspire **service discovery** (`https+http://apiservice`) — never hardcode host/port.

## Multi-tenancy model (understand before touching data or endpoints)

- **Shared database, row-level isolation.** Every tenant-owned entity implements
  `ITenantOwned { Guid TenantId }`. [TenantDbContext](AspireWeb.Data/TenantDbContext.cs) applies a
  **named global query filter** (`TenantDbContext.TenantFilterName`) to every `ITenantOwned` entity
  **by convention** — never hand-code per-entity filters.
  [TenantSaveChangesInterceptor](AspireWeb.Data/Tenancy/TenantSaveChangesInterceptor.cs) stamps
  `TenantId` on inserts and throws on cross-tenant writes.
- **Bare `IgnoreQueryFilters()` is banned** — it drops tenant isolation. Use
  `IgnoreQueryFilters([TenantDbContext.TenantFilterName])` only where crossing tenants is
  explicitly justified, and expect it to be challenged in review.
- **Raw SQL must include a `tenant_id` predicate** (none exists today; keep it that way unless unavoidable).
- Tenant comes from claims only: registration creates a `Tenant` + Owner user;
  [TenantClaimsPrincipalFactory](AspireWeb.Web/Identity/TenantClaimsPrincipalFactory.cs) bakes
  `tenant_id` / `tenant_role` / `tenant_name` into the auth cookie.
- **Web→API propagation:** the Web front end mints a 5-minute HS256 JWT
  ([TenantTokenService](AspireWeb.Web/Identity/TenantTokenService.cs), contract in
  [ApiJwtDefaults](AspireWeb.ServiceDefaults/Tenancy/ApiJwtDefaults.cs)); the API resolves the tenant from
  the validated token (`ClaimsTenantContext`) — **never from headers**. Inject the token service
  into typed clients directly; do NOT use a `DelegatingHandler` (HttpClientFactory caches handlers
  outside the circuit scope).
- API endpoints are **fail-closed**: an authenticated-user fallback policy applies; anonymous
  endpoints opt out via `.AllowAnonymous()`. Tenant-scoped groups use `TenantPolicies.RequireTenant`
  (+ `RequireTenantAdmin`/`RequireTenantOwner` for privileged verbs) and the `RequireActiveTenantFilter`.
- **Never output-cache tenant-scoped pages/endpoints** (cross-tenant cache leak). The Weather
  page's `OutputCache` pattern must not be copied to tenant data.
- One tenant per user for now; documented upgrade paths (not built): `TenantMembers` join table +
  switcher, `TenantInvite` invites, symmetric → asymmetric JWT (JWKS) → external IdP.

## Conventions (follow these)

- Every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` — except the
  MigrationService, a `BackgroundService` worker with no `WebApplication` (it calls
  `AddServiceDefaults()` only; there are no endpoints to map).
- New service-to-service HTTP = service discovery + a typed `HttpClient`, mirroring
  [TodoApiClient.cs](AspireWeb.Web/Clients/TodoApiClient.cs) (authenticated) or
  [WeatherApiClient.cs](AspireWeb.Web/Clients/WeatherApiClient.cs) (anonymous) — no hardcoded URLs.
- Wire DTOs shared by Web and ApiService live in [AspireWeb.Contracts/](AspireWeb.Contracts/) —
  never duplicate a shape per project or post anonymous objects for a typed contract.
- Hosts register DbContexts via the shared
  [AddTenantDbContext / AddIdentityDbContext](AspireWeb.Data/ServiceCollectionExtensions.cs)
  extensions (history tables, retry policy, tenancy interceptor live there) — don't hand-roll
  `AddDbContext`. Each host keeps its own `builder.AddNpgsqlDataSource("appdb")`.
- API endpoints live in `Endpoints/*.cs` modules (`MapXEndpoints()` per feature), return RFC 7807
  (`Results.Problem`/`ValidationProblem`) for every non-2xx, and declare `.WithName`/`.Produces*`
  — mirror [TodoEndpoints.cs](AspireWeb.ApiService/Endpoints/TodoEndpoints.cs).
- New resources: `aspire add <integration>` + the `builder.Add*` call in
  [AppHost.cs](AspireWeb.AppHost/AppHost.cs), then `WithReference`/`WaitFor` wired.
- **Central Package Management**: versions live in [Directory.Packages.props](Directory.Packages.props);
  a `Version=` attribute on a PackageReference fails restore (NU1008).
- **EF Core 11 preview pins move together**: every EF-adjacent package **and the `dotnet-ef` local
  tool** stay on the same `11.0.0-preview.*` build; at RC/GA regenerate the migrations from scratch
  (don't chain onto the previews) — and fold in the App context's follow-up migration
  (`DropRedundantTodoTenantIndex`), which already sits on top of its initial one, rather than
  assuming one migration per context. The Aspire EF integration is deliberately NOT used (compiled against EF 10) —
  DB access goes through `Aspire.Npgsql` + plain `UseNpgsql`. Detail: `/ef-migration` and the
  comments in Directory.Packages.props.
- Keep builds warning-clean: they must pass `dotnet build -warnaserror`.

## Code quality (Roslyn analyzers)

- Solution-wide analyzers via CPM `GlobalPackageReference`: **Meziantou.Analyzer, Roslynator,
  SonarAnalyzer.CSharp** (xunit.analyzers transitively). `AnalysisLevel latest-recommended` +
  `EnforceCodeStyleInBuild` in [Directory.Build.props](Directory.Build.props); style in [.editorconfig](.editorconfig).
- Two trees are `generated_code = true`: **`AspireWeb.Web/Components/Account/**`** (grafted Identity
  template — do not restyle; new `.cs` files there need a leading `#nullable enable`) and
  **`AspireWeb.Data/Migrations/**`** (EF-generated — never hand-edit; regenerate via `/ef-migration`).
- [AspireWeb.Web/.globalconfig](AspireWeb.Web/.globalconfig) disables five Sonar rules that misfire
  on Razor-generated code — active everywhere else. Suppressions are per-ID with a comment; never blanket-disable.
- `-warnaserror` is enforced at the CLI gate, not in props. MSBuild caches promoted-warning
  failures — after fixing analyzer errors, verify with `--no-incremental`.

## Commands

The Aspire CLI is a global tool (`aspire.CMD`, **PowerShell PATH only, not Git-Bash**) and a pinned
**local tool** — after `dotnet tool restore`, `dotnet aspire …` works from any shell.

```powershell
aspire run                                    # run locally with the Aspire dashboard
dotnet build AspireWeb.slnx -c Release -warnaserror   # build, warnings are errors
dotnet test --solution AspireWeb.slnx -c Release      # full suite: unit + integration (needs Docker)
dotnet test --solution AspireWeb.slnx -c Release -- --filter-not-trait Category=Integration   # fast tier, no Docker, seconds
dotnet format AspireWeb.slnx                  # auto-format to the .editorconfig style rules
dotnet tool restore                           # restores dotnet-ef + aspire local tools
```

`dotnet test` runs through **Microsoft.Testing.Platform** (opted in via [global.json](global.json)):
the solution goes after `--solution` (the old positional form errors), MTP arguments after `--`.
MSBuild `/t:` switches get mangled by Git-Bash path conversion — use `-t:` or PowerShell.

**Secrets (one-time per machine):** the web→api JWT signing key is an AppHost secret parameter;
`aspire run` and the services fail fast without it:
`dotnet user-secrets set "Parameters:jwt-signing-key" "<base64-32-bytes>" --project AspireWeb.AppHost`.
Tests pin their own deterministic key (`TestTokens.JwtSigningKey`) — no setup needed.

### EF Core migrations

Two contexts in [AspireWeb.Data/](AspireWeb.Data/), two folders (`Migrations/Identity`,
`Migrations/App`), separate history tables; the Identity context applies **first** (it owns the
`Tenants` table the App context FKs target). Use `/ef-migration` for the exact commands and
invariants — never hand-edit `Migrations/**`.

## Continuous integration

[ci.yml](.github/workflows/ci.yml): two tiers on pushes to `main` and PRs — build + fast tests,
then `Category=Integration` (ubuntu runners ship Docker; DCP comes from the Aspire NuGet packages).
The `-warnaserror` gate runs in the shared [dotnet-setup action](.github/actions/dotnet-setup/action.yml).

## Deploy to local Kubernetes (Docker Desktop)

`/deploy-k8s` ([.claude/commands/deploy-k8s.md](.claude/commands/deploy-k8s.md)) is the single
source of truth — the runnable recipe (local images + Helm, no registry) plus architecture notes
(PVC survives uninstall, migration worker idles as a Deployment, TLS at the nginx ingress with
forwarded headers, ClusterIP services, why `aspire deploy` is skipped). The AppHost registers the
compute environment via `builder.AddKubernetesEnvironment("k8s")`.

## Known tech debt

**Package pins and their rationales live as comments in [Directory.Packages.props](Directory.Packages.props)
(Microsoft.OpenApi 2.7.5, CodeCoverage 17.14.x, the EF 11 preview anchor) — never bump a pinned
package without reading its comment.**

- `IdentityNoOpEmailSender` only logs confirmation links and `RequireConfirmedAccount = false` — flip both for production.
- Web→api JWT is a shared symmetric key — go asymmetric (JWKS)/external IdP when a second client
  appears; `apiservice` staying cluster-internal is defense in depth, not the security boundary.
- DataProtection keys sit unencrypted in the DB — add `ProtectKeysWith*` when it matters.
- `/health`/`/alive` map in **all** environments (k8s probes need them): `webfrontend`'s `/health`
  is ingress-reachable and runs an unauthenticated Postgres round-trip — protect before production.
- The generated Helm chart wires no liveness/readiness probes — add them when self-healing matters.
- `ActiveTenantGate` caches tenant-active checks in a per-instance `IMemoryCache` — no cross-pod
  invalidation; swap to distributed/HybridCache when replicas matter.
- The tenancy layer keeps Finbuckle-compatible shapes (`Tenant` ≈ `ITenantInfo`) so a swap stays
  mechanical once an EF-11-compatible Finbuckle ships.

## Verifying a change

Before considering a change done: `dotnet build AspireWeb.slnx -c Release -warnaserror` **and**
`dotnet test --solution AspireWeb.slnx -c Release` must both pass (the integration tier boots the
AppHost and needs a running **Docker** engine; first run pulls the Postgres image). The `/validate`
command runs this exact restore → build → test gate. Inner loop: the
fast tier runs in seconds without Docker — but the full suite gates "done". For runtime-behavior
changes, also exercise the affected page/endpoint — the `verify-app` skill has the recipe (run the
app, two-tenant `/todos` isolation check, `/debug/claims`).
