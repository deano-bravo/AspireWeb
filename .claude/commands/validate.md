---
description: Validate the solution — restore, build (warnings as errors), and run tests
---

Validate the AspireWeb solution end-to-end and report the results concisely.

1. `aspire restore` (fall back to `dotnet restore AspireWeb.slnx` if the Aspire CLI is unavailable).
2. `dotnet build AspireWeb.slnx -c Release -warnaserror` — must be warning-clean.
3. `dotnet test AspireWeb.slnx -c Release` — all tests must pass.

If any step fails, stop and show the relevant error output; do not proceed to the next step.
Report a one-line pass/fail summary for each step at the end.
