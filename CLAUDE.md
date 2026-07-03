# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository.

## What this is

**AspireWeb** is a .NET Aspire distributed application scaffolded as a **multi-tenant SaaS**:
a Blazor Server front end with ASP.NET Core Identity (register / login / account profile,
passkey-capable) that calls a minimal-API backend, PostgreSQL + EF Core with row-level
tenant isolation, orchestrated by an Aspire AppHost with a shared ServiceDefaults library,
a migration worker, and an integration test project.

- **Stack:** .NET 11 (preview) · Aspire 13.4.6 · **EF Core 11 preview** · PostgreSQL ·
  C# with `Nullable` and `ImplicitUsings` enabled.
- **SDK is pinned** in [global.json](global.json) — use that exact SDK; do not bump it casually.
- Solution file is [AspireWeb.slnx](AspireWeb.slnx) (the new XML solution format).

## Projects

| Project | Path | Role |
| --- | --- | --- |
| AppHost | [AspireWeb.AppHost/](AspireWeb.AppHost/) | Aspire orchestrator & entry point. Wires resources in [AppHost.cs](AspireWeb.AppHost/AppHost.cs). |
| ApiService | [AspireWeb.ApiService/](AspireWeb.ApiService/) | Minimal-API backend. Anonymous `/weatherforecast`; tenant-scoped `/todos` behind JWT bearer auth. |
| Web | [AspireWeb.Web/](AspireWeb.Web/) | Blazor Server front end. ASP.NET Core Identity (cookie auth, `Components/Account/**`), tenant-aware registration, calls the API via typed clients. |
| Data | [AspireWeb.Data/](AspireWeb.Data/) | Entities, the two DbContexts, tenancy primitives, EF migrations, design-time factories. |
| MigrationService | [AspireWeb.MigrationService/](AspireWeb.MigrationService/) | Applies EF migrations for both contexts at startup; AppHost gates web/api on `WaitForCompletion`. |
| ServiceDefaults | [AspireWeb.ServiceDefaults/](AspireWeb.ServiceDefaults/) | Shared OpenTelemetry, health checks, service discovery, HTTP resilience — plus the shared tenancy contract (`TenantClaimTypes`, `TenantPolicies`, `ApiJwtDefaults`, `AddTenantPolicies`). |
| Tests | [AspireWeb.Tests/](AspireWeb.Tests/) | xUnit v3. Fast model/interceptor unit tests + integration tests over the full AppHost (shared `AppFixture`). |

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
  mirroring [TodoApiClient.cs](AspireWeb.Web/TodoApiClient.cs) (authenticated) or
  [WeatherApiClient.cs](AspireWeb.Web/WeatherApiClient.cs) (anonymous) — do not hardcode URLs.
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
dotnet test  AspireWeb.slnx -c Release        # unit + integration tests (needs Docker)
aspire restore                                # restore + generate AppHost SDK code
aspire doctor                                 # diagnose the Aspire/container environment
dotnet tool restore                           # restores dotnet-ef (and aspire) local tools
```

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

## Deploy to local Kubernetes (Docker Desktop)

The AppHost registers a Kubernetes compute environment (`builder.AddKubernetesEnvironment("k8s")`
via the `Aspire.Hosting.Kubernetes` package). Deploy uses **local images + Helm, no registry**
(Docker Desktop's containerd image store shares built images with its k8s node):

```powershell
aspire publish -o ./aspire-output                    # generate the Helm chart
dotnet publish AspireWeb.ApiService/AspireWeb.ApiService.csproj             -c Release -t:PublishContainer -p:ContainerRepository=apiservice       -p:ContainerImageTag=latest
dotnet publish AspireWeb.Web/AspireWeb.Web.csproj                           -c Release -t:PublishContainer -p:ContainerRepository=webfrontend      -p:ContainerImageTag=latest
dotnet publish AspireWeb.MigrationService/AspireWeb.MigrationService.csproj -c Release -t:PublishContainer -p:ContainerRepository=migrationservice -p:ContainerImageTag=latest
kubectl create namespace aspireweb
# Helm templates compose connection strings from these values; supply the same PG password
# to all four consumers plus the JWT key to both services (exact keys: aspire-output/values.yaml):
helm upgrade --install aspireweb ./aspire-output -n aspireweb `
  --set secrets.postgres.postgres_password=$PGPW `
  --set secrets.migrationservice.postgres_password=$PGPW `
  --set secrets.apiservice.postgres_password=$PGPW `
  --set secrets.webfrontend.postgres_password=$PGPW `
  --set secrets.apiservice.jwt_signing_key=$JWTKEY `
  --set secrets.webfrontend.jwt_signing_key=$JWTKEY
```

Notes: postgres renders as a StatefulSet; the data volume (publish mode only) becomes a PVC
that **survives `helm uninstall`** — delete it explicitly on teardown. The migration worker
renders as a Deployment, so outside Development it idles after migrating instead of exiting
(an exiting pod would restart-loop). DataProtection keys live in the database, so cookies
survive pod restarts.

### HTTPS access (locally-trusted cert via ingress)

The front end is reached over HTTPS through an nginx ingress that terminates TLS. A local root CA
issues the `localhost` leaf cert; trusting that CA once means the browser shows **no warning**. The
app itself serves plain HTTP inside the cluster (`ASPNETCORE_FORWARDEDHEADERS_ENABLED` in the
generated config makes `UseHttpsRedirection` and the `Secure` auth cookie honour the ingress
`X-Forwarded-Proto`). Files live under [k8s/](k8s/): `ingress.yaml` and `gen-cert.sh`/`trust-ca.ps1`
(committed); the CA + keys in `k8s/.certs/` are git-ignored. One-time per cluster/machine:

```powershell
helm upgrade --install ingress-nginx ingress-nginx --repo https://kubernetes.github.io/ingress-nginx -n ingress-nginx --create-namespace
```

```bash
./k8s/gen-cert.sh                        # CA + localhost cert + aspireweb-tls secret (Git Bash)
powershell -File k8s/trust-ca.ps1        # trust the CA in CurrentUser\Root (then restart browser)
kubectl apply -f k8s/ingress.yaml
```

Verify: `kubectl get pods -n aspireweb` (all `Running`), then open <https://localhost> — no cert
warning; register an organisation, and the `/todos` page shows tenant-scoped data from
`apiservice`. (`curl` from Git Bash uses its own CA bundle, so verify trust with
`Invoke-WebRequest https://localhost/` in PowerShell instead.)
Plain-HTTP alternative: `kubectl port-forward -n aspireweb svc/webfrontend-service 8088:8080`.
Teardown: `helm uninstall aspireweb -n aspireweb; kubectl delete ns aspireweb` (+ PVC).

Notes: generated Helm services are **ClusterIP** (use port-forward, or patch to NodePort/LoadBalancer);
`aspire-output/` is generated and git-ignored. `aspire deploy` also exists but its pipeline includes
an image **push** step that assumes a registry — the Helm path above is preferred for local dev.

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

## Verifying a change

Before considering a change done: `dotnet build AspireWeb.slnx -c Release -warnaserror` **and**
`dotnet test AspireWeb.slnx -c Release` must both pass (tests boot the AppHost and require a
running **Docker** engine; first run pulls the Postgres image). For changes to runtime behavior,
also run the app (`aspire run`, or deploy per above) and exercise the affected page/endpoint —
for tenancy changes, register two organisations in two browser profiles and confirm `/todos`
isolation plus `/debug/claims` (Development only) showing the tenant claims.
