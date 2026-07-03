---
description: Build images and deploy AspireWeb to local Docker Desktop Kubernetes via Helm
---

# Deploy to Kubernetes

Deploy AspireWeb to the local Docker Desktop Kubernetes cluster using local images (no registry).
Confirm the active kube context is `docker-desktop` before applying anything.

1. Generate the Helm chart: `aspire publish -o ./aspire-output` (run `aspire` from PowerShell).
2. Build the container images with the exact names the chart expects:
   - `dotnet publish AspireWeb.ApiService/AspireWeb.ApiService.csproj -c Release -t:PublishContainer -p:ContainerRepository=apiservice -p:ContainerImageTag=latest`
   - `dotnet publish AspireWeb.Web/AspireWeb.Web.csproj -c Release -t:PublishContainer -p:ContainerRepository=webfrontend -p:ContainerImageTag=latest`
   - `dotnet publish AspireWeb.MigrationService/AspireWeb.MigrationService.csproj -c Release -t:PublishContainer -p:ContainerRepository=migrationservice -p:ContainerImageTag=latest`
   (Use PowerShell or `-t:` form — Git-Bash mangles `/t:`.)
3. `kubectl create namespace aspireweb` (ignore "already exists").
4. Install with the required secret values (Helm composes the Postgres connection strings from
   them; exact key names are in `aspire-output/values.yaml`). Generate `$PGPW` (any strong
   password) and `$JWTKEY` (base64 of 32 random bytes — reuse the value from
   `dotnet user-secrets list --project AspireWeb.AppHost` if set):

   ```powershell
   helm upgrade --install aspireweb ./aspire-output -n aspireweb `
     --set secrets.postgres.postgres_password=$PGPW `
     --set secrets.migrationservice.postgres_password=$PGPW `
     --set secrets.apiservice.postgres_password=$PGPW `
     --set secrets.webfrontend.postgres_password=$PGPW `
     --set secrets.apiservice.jwt_signing_key=$JWTKEY `
     --set secrets.webfrontend.jwt_signing_key=$JWTKEY
   ```

   Reuse the same `$PGPW` on upgrades — the Postgres PVC keeps the old password.
5. Verify: `kubectl get pods -n aspireweb` (all `Running`; migrationservice logs show
   "Database migrations applied" then it idles), then port-forward
   `svc/webfrontend-service 8088:8080` and confirm `/weather` renders API data; for tenancy,
   register an organisation and check `/todos`.

Report pod status and the port-forward URL. Remind the user of teardown:
`helm uninstall aspireweb -n aspireweb; kubectl delete ns aspireweb` — and that the
postgres PVC survives uninstall and must be deleted explicitly.
