# Local development

The app runs locally with the **Static Web Apps CLI**, which emulates authentication
(`/.auth/*`), custom role assignment, and the same-origin `/api` reverse proxy — so the
local experience matches Azure.

## Prerequisites

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for local Postgres + Azurite)
- SWA CLI: `npm install -g @azure/static-web-apps-cli`

## 1. Start dependencies

```bash
docker compose up -d
```

This starts PostgreSQL (`localhost:5432`, `postgres`/`postgres`) and Azurite (Blob on `10000`).

## 2. Configure the API

```bash
cd api
cp local.settings.json.example local.settings.json
```

Set `Seed__AdminEmail` to your login email so you get the `admin` role on first sign-in.
Migrations are applied automatically on API start-up (`DbInitializer`).

## 3. Run everything with the SWA CLI

From the repo root:

```bash
swa start
```

The CLI reads [`swa-cli.config.json`](../swa-cli.config.json): it launches the Vite dev
server and the Functions host, then serves the whole thing on **http://localhost:4280**.

- Sign in via the mock auth screen (choose a username and add roles, or leave default for `user`).
- The API's `GetRoles` still runs and returns roles from Postgres, so seeded admins work locally too.

## Useful commands

```bash
# Frontend only
cd frontend && npm run dev

# API only
cd api && func start

# Add an EF Core migration
cd api && dotnet ef migrations add <Name> -o Data/Migrations

# Regenerate the migration SQL bundle
cd api && dotnet ef migrations script --idempotent -o migrate.sql
```

## Roles while developing

With the SWA CLI mock login you can assign roles directly in the login form. Against a real
database, promote a user by signing in once (creates the `AppUser`), then either seed
`Seed__AdminEmail` or have an existing admin promote them from the **Admin** page.
