---
name: verify-app
description: Verify an AspireWeb change end-to-end — run the app, exercise the affected flow, and prove tenant isolation. Use when a change touches runtime behavior (endpoints, pages, auth, tenancy, data).
---

# Verify an AspireWeb change

## Test tiers (gate for "done")

- Fast tier (seconds, no Docker): `dotnet test --solution AspireWeb.slnx -c Release -- --filter-not-trait Category=Integration`
- Full suite (gates "done", needs a running Docker engine): `dotnet test --solution AspireWeb.slnx -c Release`
  — the `Category=Integration` tier boots the full AppHost once via the `AppFixture` collection
  fixture (~3-minute startup timeout on a cold Postgres pull). Tests pin their own deterministic
  JWT key (`AppFixture.JwtSigningKey`) — no secret setup needed.

## Run the app

- `aspire run` from PowerShell (or `dotnet aspire run` from any shell after `dotnet tool restore`).
- Prerequisite (one-time per machine): the `Parameters:jwt-signing-key` user-secret on the
  AppHost — services **fail fast at startup** without it:
  `dotnet user-secrets set "Parameters:jwt-signing-key" "<base64-32-bytes>" --project AspireWeb.AppHost`
- The dashboard URL is printed to the console; use it to reach the web front end and logs.

## Tenancy verification (required for any tenancy-adjacent change)

1. Register two organisations in two separate browser profiles (registration creates a
   `Tenant` + Owner user per org).
2. Create todos in each and confirm `/todos` isolation **both ways** — neither tenant sees
   the other's rows.
3. Check `/debug/claims` (Development only) shows `tenant_id` / `tenant_role` / `tenant_name`
   in each profile.

## API-level checks

- `GET /weatherforecast` is anonymous → 200 without auth.
- `GET /todos` without a JWT → 401 (fail-closed fallback policy).

## Deployed verification

For the Kubernetes deployment, follow `/deploy-k8s` step 6 (pods Running, `https://localhost`,
`/weather`, tenancy check).
