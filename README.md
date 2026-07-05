# AspireWeb

[![CI](https://github.com/deano-bravo/AspireWeb/actions/workflows/ci.yml/badge.svg)](https://github.com/deano-bravo/AspireWeb/actions/workflows/ci.yml)

A .NET Aspire distributed application scaffolded as a **multi-tenant SaaS**: a Blazor Server
front end with ASP.NET Core Identity, a minimal-API backend, and PostgreSQL with row-level
tenant isolation (named global query filters + a write-side interceptor), orchestrated by an
Aspire AppHost. Shared web↔api DTOs live in `AspireWeb.Contracts`; the tenancy claims/policy
contract lives in `AspireWeb.ServiceDefaults`.

See [CLAUDE.md](CLAUDE.md) for architecture, the multi-tenancy model, conventions, and the
Kubernetes + HTTPS deploy recipe.

## Prerequisites

- The pinned .NET SDK from [global.json](global.json) (a .NET 11 preview build).
- **Docker Desktop** — required by `aspire run` and the integration tests.
- One-time tool + secret setup:

```powershell
dotnet tool restore   # pinned Aspire CLI + dotnet-ef (.config/dotnet-tools.json)
# any base64-encoded 32 random bytes:
dotnet user-secrets set "Parameters:jwt-signing-key" "<base64-32-bytes>" --project AspireWeb.AppHost
```

## Commands

`aspire run` starts the whole app locally with the Aspire dashboard. For the full command
reference — build (`-warnaserror`), the two test tiers, `dotnet format`, `aspire publish`, EF
migrations, and secrets — see **[CLAUDE.md → Commands](CLAUDE.md#commands)** (kept as the single
source so the lists don't drift).

The test suite is two-tier: model/service unit tests run anywhere; tests marked
`Category=Integration` boot the full AppHost (Postgres container + migrations + services).
CI mirrors this split — see [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Open the app

- **Local:** `aspire run`, then click the `webfrontend` endpoint in the dashboard.
- **Kubernetes (HTTPS):** **<https://localhost>** — trusted locally via a dev CA
  (run `./k8s/gen-cert.sh` then `powershell -File k8s/trust-ca.ps1` once, and restart the
  browser). Full deploy runbook: [.claude/commands/deploy-k8s.md](.claude/commands/deploy-k8s.md).

## License

[MIT](LICENSE)
