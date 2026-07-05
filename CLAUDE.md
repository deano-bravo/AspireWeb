# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository.

## What this is

**AspireWeb** is a .NET Aspire distributed application scaffolded as a **multi-tenant SaaS**:
a Blazor Server front end with ASP.NET Core Identity (register / login / account profile,
passkey-capable) that calls a minimal-API backend, PostgreSQL + EF Core with row-level
tenant isolation, orchestrated by an Aspire AppHost with shared ServiceDefaults and
Contracts libraries, a migration worker, and a two-tier test project (fast unit tier +
AppHost-backed integration tier).

- **Stack:** .NET 11 (preview) · Aspire 13.4.6 · **EF Core 11 preview** · PostgreSQL ·
  C# with `Nullable` and `ImplicitUsings` enabled.
- **SDK is pinned** in [global.json](global.json) — use that exact SDK; do not bump it casually.
- Solution file is [AspireWeb.slnx](AspireWeb.slnx) (the new XML solution format).

## Projects

| Project | Path | Role |
| --- | --- | --- |
| AppHost | [AspireWeb.AppHost/](AspireWeb.AppHost/) | Aspire orchestrator & entry point. Wires resources in [AppHost.cs](AspireWeb.AppHost/AppHost.cs). |
| ApiService | [AspireWeb.ApiService/](AspireWeb.ApiService/) | Minimal-API backend. Anonymous `/weatherforecast`; tenant-scoped `/todos` behind JWT bearer auth. |
| Web | [AspireWeb.Web/](AspireWeb.Web/) | Blazor Server front end. ASP.NET Core Identity (cookie auth, `Components/Account/**`), tenant-aware registration, calls the API via typed clients in `Clients/`. |
| Contracts | [AspireWeb.Contracts/](AspireWeb.Contracts/) | Shared web↔api wire contracts (`TodoItemDto`, `CreateTodoRequest`, `WeatherForecast`). Deliberately dependency-free. |
| Data | [AspireWeb.Data/](AspireWeb.Data/) | Entities, the two DbContexts (+ shared registration extensions `AddAppDbContext`/`AddIdentityDbContext`), tenancy primitives, EF migrations, design-time factories. |
| MigrationService | [AspireWeb.MigrationService/](AspireWeb.MigrationService/) | Applies EF migrations for both contexts at startup; AppHost gates web/api on `WaitForCompletion`. |
| ServiceDefaults | [AspireWeb.ServiceDefaults/](AspireWeb.ServiceDefaults/) | Shared OpenTelemetry, health checks, service discovery, HTTP resilience — plus the shared tenancy contract (`TenantClaimTypes`, `TenantPolicies`, `ApiJwtDefaults`, `AddTenantPolicies`). |
| Tests | [AspireWeb.Tests/](AspireWeb.Tests/) | xUnit v3 on the Microsoft.Testing.Platform runner. Fast unit tier (model/interceptor/token-service; no Docker) + `Category=Integration` tests over the full AppHost (`AppFixture` as a collection fixture — deliberately NOT an assembly fixture, which xunit creates eagerly even for filtered runs). References Web to unit-test its services. |

**Topology** (see [AppHost.cs](AspireWeb.AppHost/AppHost.cs)): `postgres` (container; `appdb`
database; pgweb UI in run mode; data volume only when publishing) → `migrationservice`
(runs to completion) → `apiservice` and `webfrontend` (`WaitForCompletion(migrations)`),
with `webfrontend` also referencing `apiservice`. Cross-service calls use Aspire **service
discovery** (`https+http://apiservice`) — never hardcode host/port.

## Multi-tenancy model (understand before touching data or endpoints)

- **Shared database, row-level isolation.** Every tenant-owned entity implements
  `ITenantOwned { Guid TenantId }`. [AppDbContext](AspireWeb.Data/AppDbContext.cs) applies a
  **named global query filter** (`AppDbContext.TenantFilterName`) to every `ITenantOwned`
  entity **by convention** — never hand-code per-entity filters.
  [TenantSaveChangesInterceptor](AspireWeb.Data/Tenancy/TenantSaveChangesInterceptor.cs)
  stamps `TenantId` on inserts and throws on cross-tenant writes.
- **Bare `IgnoreQueryFilters()` is banned** — it drops tenant isolation. Use
  `IgnoreQueryFilters([AppDbContext.TenantFilterName])` only where crossing tenants is
  explicitly justified, and expect it to be challenged in review.
- **Raw SQL must include a `tenant_id` predicate** (none exists today; keep it that way
  unless unavoidable).
- The tenant comes from claims only: registration creates a `Tenant` + Owner user;
  [TenantClaimsPrincipalFactory](AspireWeb.Web/Identity/TenantClaimsPrincipalFactory.cs) bakes
  `tenant_id` / `tenant_role` / `tenant_name` into the auth cookie.
- **Web→API propagation:** the Web front end mints a 5-minute HS256 JWT
  ([TenantTokenService](AspireWeb.Web/Identity/TenantTokenService.cs), contract in
  [ApiJwtDefaults](AspireWeb.ServiceDefaults/ApiJwtDefaults.cs)); the API validates it and
  resolves the tenant from the token (`ClaimsTenantContext`) — **never from headers**.
  Inject the token service into typed clients directly; do NOT use a `DelegatingHandler`
  (HttpClientFactory caches handlers outside the circuit scope).
- API endpoints are **fail-closed**: an authenticated-user fallback policy applies;
  anonymous endpoints must opt out via `.AllowAnonymous()`. Tenant-scoped groups use
  `TenantPolicies.RequireTenant` (+ `RequireTenantAdmin`/`RequireTenantOwner` for
  privileged verbs) and the `RequireActiveTenantFilter`.
- **Never output-cache tenant-scoped pages/endpoints** (cross-tenant cache leak). The
  Weather page's `OutputCache` pattern must not be copied to tenant data.
- One tenant per user for now. Upgrade paths (documented, not built): `TenantMembers`
  join table + tenant switcher; invites via a `TenantInvite` entity;
  symmetric JWT → asymmetric (JWKS) → external IdP.

## Conventions (follow these)

- Every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`.
- New service-to-service HTTP goes through service discovery + a typed `HttpClient`,
  mirroring [TodoApiClient.cs](AspireWeb.Web/Clients/TodoApiClient.cs) (authenticated) or
  [WeatherApiClient.cs](AspireWeb.Web/Clients/WeatherApiClient.cs) (anonymous) — do not hardcode URLs.
- Wire DTOs shared by Web and ApiService live in
  [AspireWeb.Contracts/](AspireWeb.Contracts/) — never duplicate a request/response shape
  per project, and never post anonymous objects for a typed contract.
- Hosts register DbContexts via the shared
  [AddAppDbContext / AddIdentityDbContext](AspireWeb.Data/ServiceCollectionExtensions.cs)
  extensions (history table, retry policy, tenancy interceptor, Identity schema-version pin
  live there) — don't hand-roll `AddDbContext` blocks. Each host keeps its own
  `builder.AddNpgsqlDataSource("appdb")`.
- API endpoints live in `Endpoints/*.cs` modules (`MapXEndpoints()` extension per feature),
  return RFC 7807 (`Results.Problem`/`ValidationProblem`) for every non-2xx, and declare
  `.WithName`/`.Produces*` metadata — mirror [TodoEndpoints.cs](AspireWeb.ApiService/Endpoints/TodoEndpoints.cs).
- New resources are added in [AppHost.cs](AspireWeb.AppHost/AppHost.cs) via
  `aspire add <integration>` + the corresponding `builder.Add*` call, then
  `WithReference`/`WaitFor` wired.
- **Central Package Management is enabled**: versions live in
  [Directory.Packages.props](Directory.Packages.props); `.csproj` files use versionless
  `PackageReference`. A `Version=` attribute on a PackageReference fails restore (NU1008).
- **EF Core 11 preview pins move together**: the Npgsql provider pins
  `Microsoft.EntityFrameworkCore` exactly, so every EF-adjacent package **and the dotnet-ef
  local tool** must stay on the same `11.0.0-preview.*` build. At EF 11 RC/GA, regenerate
  (don't chain) the two Initial migrations. The Aspire EF integration
  (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`) is deliberately NOT used (compiled
  against EF 10) — DB access goes through `Aspire.Npgsql` (`AddNpgsqlDataSource`) + plain
  `UseNpgsql`. Re-adopt `EnrichNpgsqlDbContext` when Aspire ships an EF-11-aligned build.
- Keep builds warning-clean: they must pass `dotnet build -warnaserror`.

## Code quality (Roslyn analyzers)

- Solution-wide analyzers ride as CPM `GlobalPackageReference`: **Meziantou.Analyzer,
  Roslynator.Analyzers, SonarAnalyzer.CSharp** (xunit.analyzers flows transitively via
  xunit.v3). `AnalysisLevel latest-recommended` + `EnforceCodeStyleInBuild` in
  [Directory.Build.props](Directory.Build.props).
- Style lives in [.editorconfig](.editorconfig). Two trees are deliberately relaxed via
  `generated_code = true`: **`AspireWeb.Web/Components/Account/**`** (grafted Identity
  template code — do not restyle it; new `.cs` files there need a leading `#nullable enable`)
  and **`AspireWeb.Data/Migrations/**`** (EF-generated — never hand-edit; regenerate).
- [AspireWeb.Web/.globalconfig](AspireWeb.Web/.globalconfig) disables five Sonar rules that
  misfire on Razor-generated/template code (path-scoped editorconfig sections cannot reach
  Razor's generated syntax trees) — they stay active everywhere else.
- Rule suppressions are per-ID with a comment explaining why; never blanket-disable.
- Warnings-as-errors is enforced at the CLI gate (`-warnaserror`), not in the props file,
  so the inner loop stays unblocked. Note MSBuild caches promoted-warning failures:
  after fixing analyzer errors, verify with `--no-incremental`.

## Commands

The **Aspire CLI** is installed as a global tool. On this Windows machine it is exposed as
`aspire.CMD` and is **only on the PowerShell PATH, not Git-Bash** — invoke `aspire ...` from
PowerShell. `clean`/`build`/`test` use `dotnet` (Aspire CLI covers restore/run/publish/deploy).

```powershell
aspire run                                    # run locally with the Aspire dashboard
dotnet build AspireWeb.slnx -c Release -warnaserror   # build, warnings are errors
dotnet test --solution AspireWeb.slnx -c Release      # full suite: unit + integration (needs Docker)
dotnet test --solution AspireWeb.slnx -c Release -- --filter-not-trait Category=Integration   # fast tier, no Docker, seconds
aspire restore                                # restore + generate AppHost SDK code
aspire doctor                                 # diagnose the Aspire/container environment
dotnet tool restore                           # restores dotnet-ef (and aspire) local tools
```

`dotnet test` runs through the **Microsoft.Testing.Platform** runner (opted in via the
`"test"` section of [global.json](global.json)) — the solution goes after `--solution`
(the old positional form errors), and MTP arguments go after `--`.

MSBuild `/t:` switches get mangled by Git-Bash path conversion — use `-t:` or run in PowerShell.

### Secrets (one-time per machine)

The web→api JWT signing key is an AppHost secret parameter; `aspire run` and the services
fail fast without it:

```powershell
# any base64-encoded 32 random bytes
dotnet user-secrets set "Parameters:jwt-signing-key" "<base64-32-bytes>" --project AspireWeb.AppHost
```

Tests pin their own deterministic key (`AppFixture.JwtSigningKey`) — no setup needed.

### EF Core migrations

Both contexts live in [AspireWeb.Data/](AspireWeb.Data/) with separate history tables
(`__ef_migrations_identity` / `__ef_migrations_app`); design-time factories make the
classlib self-sufficient (no host boot, no DB connection needed for `migrations add`):

```powershell
dotnet ef migrations add <Name> --project AspireWeb.Data --startup-project AspireWeb.Data --context ApplicationDbContext --output-dir Migrations/Identity
dotnet ef migrations add <Name> --project AspireWeb.Data --startup-project AspireWeb.Data --context AppDbContext         --output-dir Migrations/App
```

Migrations are applied at runtime by the MigrationService (Identity context first — it owns
the `Tenants` table that `AppDbContext` FKs target; `AppDbContext` maps `Tenants` with
`ExcludeFromMigrations`, so an App migration containing `CreateTable("Tenants")` is a bug).

## Continuous integration

[.github/workflows/ci.yml](.github/workflows/ci.yml) runs on pushes to `main` and on PRs,
in two tiers: **build-and-fast-tests** (restore → `build -warnaserror` → the Docker-free
tier with coverage as a Cobertura artifact) and **integration-tests** (the
`Category=Integration` suite; ubuntu-latest ships Docker, and DCP comes from the Aspire
NuGet packages — no Aspire CLI needed). `setup-dotnet` installs the exact preview SDK from
`global.json`. Both jobs upload TRX results as artifacts. The `-warnaserror` gate and the
tests are enforced here — locally they remain the pre-done checklist below.

## Deploy to local Kubernetes (Docker Desktop)

The AppHost registers a Kubernetes compute environment (`builder.AddKubernetesEnvironment("k8s")`
via the `Aspire.Hosting.Kubernetes` package). Deploy uses **local images + Helm, no registry**
(Docker Desktop's containerd image store shares built images with its k8s node).

**The runnable recipe (chart generation, image builds, Helm secret values, HTTPS ingress,
verification, teardown) lives in [.claude/commands/deploy-k8s.md](.claude/commands/deploy-k8s.md)
(`/deploy-k8s`) — that file is the single source of truth for the commands; don't duplicate
them here.** What matters architecturally:

- postgres renders as a StatefulSet; the data volume (publish mode only) becomes a PVC that
  **survives `helm uninstall`** — delete it explicitly on teardown.
- The migration worker renders as a Deployment, so outside Development it idles after
  migrating instead of exiting (an exiting pod would restart-loop).
- DataProtection keys live in the database, so cookies survive pod restarts.
- TLS terminates at an nginx ingress; the app serves plain HTTP in-cluster
  (`ASPNETCORE_FORWARDEDHEADERS_ENABLED` in the generated config makes `UseHttpsRedirection`
  and the `Secure` auth cookie honour `X-Forwarded-Proto`). A local root CA (git-ignored
  under `k8s/.certs/`, created by `k8s/gen-cert.sh`, trusted once via `k8s/trust-ca.ps1`)
  signs the `localhost` leaf, so the browser shows no warning.
- Generated Helm services are **ClusterIP** (reach the app via the ingress or port-forward);
  `aspire-output/` is generated and git-ignored. `aspire deploy` also exists but its pipeline
  includes an image **push** step that assumes a registry — the Helm path is preferred for
  local dev.

## Known tech debt

- `Microsoft.OpenApi` is deliberately pinned at **2.7.5** in
  [Directory.Packages.props](Directory.Packages.props) — 3.x is incompatible with the source
  generator. Revisit when that's fixed; don't bump blindly.
- **EF Core 11 preview everywhere DB-adjacent** — see the pin-ripple note under Conventions;
  Finbuckle.MultiTenant was skipped for the same reason (compiled against EF 10). The
  hand-rolled tenancy layer keeps Finbuckle-compatible shapes (`Tenant` ≈ `ITenantInfo`)
  so a swap stays mechanical when a Finbuckle v11 ships.
- `IdentityNoOpEmailSender` only logs confirmation links, and
  `RequireConfirmedAccount = false` — flip both (real `IEmailSender`, `true`) for production.
- The web→api JWT uses a shared symmetric key; upgrade to asymmetric (JWKS) or an external
  IdP when a second client appears. `apiservice` staying cluster-internal is defense in
  depth, not the security boundary.
- DataProtection keys are stored unencrypted in the database — add `ProtectKeysWith*`
  when it matters.
- `/health` and `/alive` are mapped in **all** environments (k8s probes need them), which
  makes `webfrontend`'s `/health` reachable through the `/` ingress rule — and it runs the
  Postgres check registered by `AddNpgsqlDataSource` (an unauthenticated DB round-trip).
  Exclude it at the ingress or protect it before real production.
- The generated Helm chart wires **no liveness/readiness probes** even though the health
  endpoints exist — add probes (or an Aspire-side customization) when k8s self-healing matters.
- `ActiveTenantGate` caches tenant-active checks in a per-instance `IMemoryCache`: with
  multiple API replicas each pod ages out independently and there is no cross-pod
  invalidation on deactivation. Swap to a distributed/HybridCache when replicas matter.
- `Microsoft.Testing.Extensions.CodeCoverage` is pinned to **17.14.x**: the 18.x line forces
  Microsoft.Testing.Platform 2.x, which breaks xunit.v3 3.2.2's MTP-v1 integration
  (TypeLoadException at run start). Bump together with an xunit.v3 release that targets MTP v2.

## Verifying a change

Before considering a change done: `dotnet build AspireWeb.slnx -c Release -warnaserror` **and**
`dotnet test --solution AspireWeb.slnx -c Release` must both pass (the integration tier boots
the AppHost and requires a running **Docker** engine; first run pulls the Postgres image). For
the inner loop, the fast tier
(`dotnet test --solution AspireWeb.slnx -c Release -- --filter-not-trait Category=Integration`)
runs in seconds without Docker — but the full suite gates "done". For changes to runtime
behavior, also run the app (`aspire run`, or deploy per above) and exercise the affected
page/endpoint — for tenancy changes, register two organisations in two browser profiles and
confirm `/todos` isolation plus `/debug/claims` (Development only) showing the tenant claims.
