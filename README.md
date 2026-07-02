# AspireWeb — Quick Reference

.NET 11 / Aspire app: a Blazor front end + minimal-API backend orchestrated by the Aspire AppHost.
See [CLAUDE.md](CLAUDE.md) for architecture, conventions, and the full Kubernetes + HTTPS deploy recipe.

## Setup (once)

```powershell
dotnet tool restore   # installs the pinned Aspire CLI (.config/dotnet-tools.json)
```

## dotnet commands

| Command | Purpose |
| --- | --- |
| `dotnet restore AspireWeb.slnx` | Restore NuGet packages. |
| `dotnet build AspireWeb.slnx -c Release -warnaserror` | Build; fail on any warning. |
| `dotnet test AspireWeb.slnx -c Release` | Run the integration tests. |
| `dotnet format` | Auto-format to style rules. |

## aspire commands (run from PowerShell)

| Command | Purpose |
| --- | --- |
| `aspire run` | Run the whole app locally with the dashboard. |
| `aspire publish -o ./aspire-output` | Generate the Kubernetes Helm chart. |
| `aspire deploy` | Deploy to the configured target. |
| `aspire doctor` | Check the environment is healthy. |

## Open the app

- **Local:** `aspire run`, then click the `webfrontend` endpoint in the dashboard.
- **Kubernetes (HTTPS):** **[https://localhost](https://localhost)** — trusted locally via a dev CA (run `./k8s/gen-cert.sh` then `powershell -File k8s/trust-ca.ps1` once, and restart the browser). Full setup in [CLAUDE.md](CLAUDE.md).
