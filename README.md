# AspireWeb — Quick Reference

.NET 11 / Aspire app: Blazor web front end + minimal-API backend, orchestrated by the Aspire AppHost.

## dotnet commands

| Command | Purpose |
| --- | --- |
| `dotnet restore AspireWeb.slnx` | Restore NuGet packages. |
| `dotnet build AspireWeb.slnx -c Release -warnaserror` | Build everything; fail on any warning. |
| `dotnet test AspireWeb.slnx -c Release` | Run the integration tests. |
| `dotnet format` | Auto-format the code to style rules. |
| `dotnet clean AspireWeb.slnx` | Remove build outputs. |

## aspire commands

> Run from **PowerShell** (the `aspire` CLI isn't on the Git-Bash PATH).

| Command | Purpose |
| --- | --- |
| `aspire run` | Run the whole app locally with the Aspire dashboard. |
| `aspire restore` | Restore + generate AppHost SDK code. |
| `aspire doctor` | Check your Aspire/Docker environment is healthy. |
| `aspire publish -o ./aspire-output` | Generate the Kubernetes Helm chart. |
| `aspire deploy` | Deploy the app to its configured target. |

## Test the web page in your browser

After a successful **`aspire run`**, open the dashboard link printed in the console, then click the
`webfrontend` endpoint (or go straight to it).

For the **Kubernetes deploy**, forward the front-end service and open it:

```powershell
kubectl port-forward -n aspireweb svc/webfrontend-service 8088:8080
```

👉 **[http://localhost:8088](http://localhost:8088)** — the app; the **Weather** page pulls live data from the API service.

See [CLAUDE.md](CLAUDE.md) for the full deploy recipe and project details.
