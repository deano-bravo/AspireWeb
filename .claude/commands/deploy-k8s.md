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
   (Use PowerShell or `-t:` form — Git-Bash mangles `/t:`.)
3. `kubectl create namespace aspireweb` (ignore "already exists").
4. `helm upgrade --install aspireweb ./aspire-output -n aspireweb`.
5. Verify: `kubectl get pods -n aspireweb` (all `Running`), then port-forward
   `svc/webfrontend-service 8088:8080` and confirm `/weather` renders API data.

Report pod status and the port-forward URL. Remind the user of teardown:
`helm uninstall aspireweb -n aspireweb; kubectl delete ns aspireweb`.
