# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository.

## What this is

**AspireWeb** is a .NET Aspire distributed application: a Blazor Server web front end that
calls a minimal-API backend, orchestrated by an Aspire AppHost, with a shared
ServiceDefaults library and an integration test project.

- **Stack:** .NET 11 (preview) · Aspire 13.4.6 · C# with `Nullable` and `ImplicitUsings` enabled.
- **SDK is pinned** in [global.json](global.json) — use that exact SDK; do not bump it casually.
- Solution file is [AspireWeb.slnx](AspireWeb.slnx) (the new XML solution format).

## Projects

| Project | Path | Role |
| --- | --- | --- |
| AppHost | [AspireWeb.AppHost/](AspireWeb.AppHost/) | Aspire orchestrator & entry point. Wires resources in [AppHost.cs](AspireWeb.AppHost/AppHost.cs). |
| ApiService | [AspireWeb.ApiService/](AspireWeb.ApiService/) | Minimal-API backend (`/weatherforecast`, `/health`). |
| Web | [AspireWeb.Web/](AspireWeb.Web/) | Blazor Server front end. Calls the API via `WeatherApiClient`. |
| ServiceDefaults | [AspireWeb.ServiceDefaults/](AspireWeb.ServiceDefaults/) | Shared OpenTelemetry, health checks, service discovery, HTTP resilience. Referenced by every service. |
| Tests | [AspireWeb.Tests/](AspireWeb.Tests/) | xUnit v3 integration tests using `Aspire.Hosting.Testing`. |

**Topology** (see [AppHost.cs](AspireWeb.AppHost/AppHost.cs)): `webfrontend` references and
`WaitFor`s `apiservice`; both expose `/health`. Cross-service calls use Aspire **service
discovery** (`https+http://apiservice`) — never hardcode host/port.

## Conventions (follow these)

- Every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`.
- New service-to-service HTTP must go through service discovery + a typed `HttpClient`, mirroring
  [WeatherApiClient.cs](AspireWeb.Web/WeatherApiClient.cs) — do not hardcode URLs.
- New resources (databases, cache, queues, projects) are added in [AppHost.cs](AspireWeb.AppHost/AppHost.cs)
  via `aspire add <integration>` + the corresponding `builder.Add*` call, then `WithReference`/`WaitFor` wired.
- Keep builds warning-clean: they must pass `dotnet build -warnaserror`.
- No central package management yet — package versions live per-`.csproj`; keep the Aspire /
  OpenTelemetry / .NET-preview versions internally consistent when changing one.

## Commands

The **Aspire CLI** is installed as a global tool. On this Windows machine it is exposed as
`aspire.CMD` and is **only on the PowerShell PATH, not Git-Bash** — invoke `aspire ...` from
PowerShell. `clean`/`build`/`test` use `dotnet` (Aspire CLI covers restore/run/publish/deploy).

```powershell
aspire run                                    # run locally with the Aspire dashboard
dotnet build AspireWeb.slnx -c Release -warnaserror   # build, warnings are errors
dotnet test  AspireWeb.slnx -c Release        # run the integration test(s)
aspire restore                                # restore + generate AppHost SDK code
aspire doctor                                 # diagnose the Aspire/container environment
```

MSBuild `/t:` switches get mangled by Git-Bash path conversion — use `-t:` or run in PowerShell.

## Deploy to local Kubernetes (Docker Desktop)

The AppHost registers a Kubernetes compute environment (`builder.AddKubernetesEnvironment("k8s")`
via the `Aspire.Hosting.Kubernetes` package). Deploy uses **local images + Helm, no registry**
(Docker Desktop's containerd image store shares built images with its k8s node):

```powershell
aspire publish -o ./aspire-output                    # generate the Helm chart
dotnet publish AspireWeb.ApiService/AspireWeb.ApiService.csproj -c Release -t:PublishContainer -p:ContainerRepository=apiservice  -p:ContainerImageTag=latest
dotnet publish AspireWeb.Web/AspireWeb.Web.csproj         -c Release -t:PublishContainer -p:ContainerRepository=webfrontend -p:ContainerImageTag=latest
kubectl create namespace aspireweb
helm upgrade --install aspireweb ./aspire-output -n aspireweb
```

### HTTPS access (locally-trusted cert via ingress)

The front end is reached over HTTPS through an nginx ingress that terminates TLS. A local root CA
issues the `localhost` leaf cert; trusting that CA once means the browser shows **no warning**. The
app itself serves plain HTTP inside the cluster (`ASPNETCORE_FORWARDEDHEADERS_ENABLED` in the
generated config makes `UseHttpsRedirection` honour the ingress `X-Forwarded-Proto`). Files live
under [k8s/](k8s/): `ingress.yaml` and `gen-cert.sh`/`trust-ca.ps1` (committed); the CA + keys in
`k8s/.certs/` are git-ignored. One-time per cluster/machine:

```powershell
helm upgrade --install ingress-nginx ingress-nginx --repo https://kubernetes.github.io/ingress-nginx -n ingress-nginx --create-namespace
```

```bash
./k8s/gen-cert.sh                        # CA + localhost cert + aspireweb-tls secret (Git Bash)
powershell -File k8s/trust-ca.ps1        # trust the CA in CurrentUser\Root (then restart browser)
kubectl apply -f k8s/ingress.yaml
```

Verify: `kubectl get pods -n aspireweb` (all `Running`), then open <https://localhost> — no cert
warning, and the `/weather` page renders data fetched from `apiservice`. (`curl` from Git Bash uses
its own CA bundle, so verify trust with `Invoke-WebRequest https://localhost/` in PowerShell instead.)
Plain-HTTP alternative: `kubectl port-forward -n aspireweb svc/webfrontend-service 8088:8080`.
Teardown: `helm uninstall aspireweb -n aspireweb; kubectl delete ns aspireweb`.

Notes: generated Helm services are **ClusterIP** (use port-forward, or patch to NodePort/LoadBalancer);
`aspire-output/` is generated and git-ignored. `aspire deploy` also exists but its pipeline includes
an image **push** step that assumes a registry — the Helm path above is preferred for local dev.

## Known tech debt

- `Microsoft.OpenApi` is deliberately pinned at **2.7.5** in
  [AspireWeb.ApiService.csproj](AspireWeb.ApiService/AspireWeb.ApiService.csproj) — 3.x is
  incompatible with the source generator. Revisit when that's fixed; don't bump blindly.
- Test coverage is a single smoke test — add unit tests for the API endpoint and `WeatherApiClient`
  when touching that code.

## Verifying a change

Before considering a change done: `dotnet build AspireWeb.slnx -c Release -warnaserror` **and**
`dotnet test AspireWeb.slnx -c Release` must both pass. For changes to runtime behavior, also
run the app (`aspire run`, or deploy per above) and exercise the affected page/endpoint.
