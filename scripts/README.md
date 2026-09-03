# Deployment automation

Everything needed to stand the app up in Azure. PowerShell (`.ps1`) and Bash (`.sh`)
versions are equivalent — use whichever fits your shell.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/cli/azure/) logged in (`az login`) with **Owner**
  (or Contributor + User Access Administrator) on the target subscription.
- [GitHub CLI](https://cli.github.com/) logged in (`gh auth login`) — for the CI path.
- For the local path only: .NET 8 SDK, Node 20+.

## Two ways to deploy

### A) CI/CD (recommended) — GitHub Actions does everything

Run these from inside the cloned repo; the repo slug (`owner/name`) is auto-detected from the
git remote, so `-r` / `-Repo` is optional (pass it only to target a different repo).

```bash
# 1. One-time: create the OIDC deploy identity, RBAC, resource group, and all repo secrets/vars.
./scripts/bootstrap-github-oidc.sh -s <subscriptionId> -e you@example.com
#   PowerShell: ./scripts/bootstrap-github-oidc.ps1 -SubscriptionId <sub> -SeedAdminEmail you@example.com

# 2. Provision infrastructure (or just push infra changes to main).
gh workflow run deploy-infra.yml

# 3. Deploy the API or frontend independently when their paths change.
gh workflow run deploy-api.yml
gh workflow run deploy-frontend.yml

# 4. Configure Microsoft sign-in (after step 2 created the SWA host).
./scripts/setup-entra-auth.sh
#   PowerShell: ./scripts/setup-entra-auth.ps1

# 5. Configure Google sign-in (manual client creation, then apply).
./scripts/set-google-auth.sh -i <clientId> -x <clientSecret>
```

After step 1, the deployment workflows need **no** hand-set resource names: they find the
Function App and SWA by their environment tags, fetch the SWA deploy token at runtime, and
rely on the API applying EF Core migrations on start-up. Pushes only run the workflow for the
changed area.

### B) Local — one command, no CI

```bash
./scripts/deploy.sh -s <subscriptionId> -e you@example.com
#   PowerShell: ./scripts/deploy.ps1 -SubscriptionId <sub> -SeedAdminEmail you@example.com
```

Then run `setup-entra-auth` / `set-google-auth` as in steps 3–4 above.

## What each script does

| Script | Purpose |
| --- | --- |
| `bootstrap-github-oidc` | OIDC app + federated creds (main, PRs, environments) + RBAC + resource group + GitHub secrets/vars |
| `setup-entra-auth` | Entra app for user sign-in (personal + work MS accounts), redirect URI, secret → SWA + GitHub |
| `set-google-auth` | Apply a manually-created Google OAuth client to SWA + GitHub |
| `deploy` | Local end-to-end: Bicep provision + Function App publish + SWA upload |
| `teardown` | Delete the resource group (`-P`/`-PurgeKeyVault` to also purge the vault) |

## Notes

- **Google** can't be fully scripted (no first-class client-creation API), so you create the
  OAuth client in Google Cloud Console once, then `set-google-auth` wires it in.
- **Migrations** are applied automatically by the API on cold start (`DbInitializer`), so no
  migration step runs in CI and the DB never needs to accept connections from the runner.
- **Seed admin**: the email you pass becomes the first `admin` on their initial sign-in.
- **PostgreSQL password** is generated if you don't supply one — store it; re-deploying resets
  the admin password to whatever value is used.
