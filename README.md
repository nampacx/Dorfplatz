# BulletinBoard

A community bulletin board ("schwarzes Brett") web app for a Verein / neighbourhood community.

## Features

- **Events & News** — members submit events (party, Vereins-Event); **moderators** approve or reject (with reason) before they go public.
- **Services / Dienstleistungen** — "ich suche" (seeking) and "ich biete" (offering) posts.
- **Goods** — eBay-Kleinanzeigen-style classifieds with description, images and a price.
- **Direct chat** — members message each other in real time (e.g. to answer a listing).
- **Roles** — `user` (default), `moderator` (assigned by admins, moderates events), `admin` (moderator powers + can promote users to moderator).
- **GDPR** — consent capture, data export (portability), erasure/anonymisation, secrets in Key Vault, no PII in URLs.

## Architecture

| Layer | Technology |
| --- | --- |
| Frontend | React + Vite + TypeScript on **Azure Static Web Apps** (Standard) |
| Auth | SWA built-in auth: **Microsoft Entra ID** (personal MS accounts) + **Google**; roles via `/api/GetRoles` |
| Backend | **Azure Functions** (.NET 8 isolated), linked to SWA as "bring your own" |
| Database | **Azure Database for PostgreSQL** Flexible Server via **EF Core / Npgsql** |
| Files | **Azure Blob Storage** (listing images, SAS uploads) |
| Chat | **Azure SignalR Service** (serverless mode) |
| Secrets / identity | **Key Vault** + **Managed Identity**; **Application Insights** for telemetry |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full picture and [docs/LOCAL_DEV.md](docs/LOCAL_DEV.md) to run it locally.

## Repository layout

```
frontend/   React + Vite SPA + staticwebapp.config.json
api/         Azure Functions (.NET 8 isolated)
infra/       Bicep modules + main.bicep
.github/     GitHub Actions (infra, frontend, api, PR validation)
docs/        Privacy, GDPR, architecture, local dev
```

## Quick start (local)

```bash
# 1. Start local Postgres + Azurite
docker compose up -d

# 2. Backend (Azure Functions)
cd api && cp local.settings.json.example local.settings.json && func start

# 3. Frontend
cd frontend && cp .env.example .env && npm install && npm run dev

# 4. Tie together with the SWA CLI (emulates auth, roles, /api proxy)
npm install -g @azure/static-web-apps-cli
swa start http://localhost:5173 --api-location http://localhost:7071
```

Open http://localhost:4280 and sign in via the mock auth screen.

## Deploy

Fully automated with Bicep + scripts + one GitHub Actions pipeline. Two commands to go live:

```bash
# one-time: deploy identity, RBAC, resource group, and all repo secrets
./scripts/bootstrap-github-oidc.sh -s <subscriptionId> -r <owner>/<repo> -e you@example.com

# provision + deploy infra, API and frontend (or just push to main)
gh workflow run deploy.yml -r <owner>/<repo>
```

Then `./scripts/setup-entra-auth.sh -r <owner>/<repo>` to wire Microsoft sign-in. Prefer no
CI? `./scripts/deploy.ps1 -SubscriptionId <sub>` does the whole thing locally. Full details in
[scripts/README.md](scripts/README.md) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#deployment).
