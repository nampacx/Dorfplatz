# Architecture

## Overview

```
Browser (React/Vite SPA)
      │  same-origin /api reverse proxy (no CORS)
      ▼
Azure Static Web Apps (Standard)  ── built-in auth (Entra ID + Google), rolesSource=/api/GetRoles
      │  linked "bring your own" Function App
      ▼
Azure Functions (.NET 8 isolated)
   ├─ HTTP APIs: events, services, goods, chat, admin, account/gdpr
   ├─ GetRoles: role assignment at sign-in (reads Postgres)
   ├─ SignalR negotiate + send  ─────────►  Azure SignalR Service (Serverless)
   ├─ EF Core / Npgsql          ─────────►  Azure Database for PostgreSQL Flexible Server
   └─ Blob SAS issuing          ─────────►  Azure Blob Storage (listing images)
Cross-cutting: User-assigned Managed Identity, Key Vault (secrets), Application Insights
```

## Key decisions

- **Bring-your-own Function App** (not SWA managed functions): managed functions are HTTP-only
  and support neither Managed Identity, Key Vault references, nor the SignalR trigger/output
  bindings — all of which this app relies on.
- **Auth**: SWA built-in authentication with two identity providers — Microsoft Entra ID
  (`common` issuer, so personal Microsoft accounts work) and Google. Custom roles are resolved
  by [`GetRoles`](../api/Functions/GetRolesFunction.cs), which reads `UserRole` rows from Postgres.
  Every protected API additionally re-checks roles server-side via
  [`CurrentUserService`](../api/Services/CurrentUserService.cs) — the client is never trusted.
- **Chat**: Azure SignalR in serverless mode. The client negotiates a user-scoped connection;
  the API persists each message to Postgres and pushes it to the recipient's user group via the
  SignalR output binding.
- **Images**: browsers upload directly to Blob using short-lived SAS URLs issued by the API,
  keeping large payloads off the Functions host.

## Sections & roles

| Section | Write | Moderation |
| --- | --- | --- |
| Events & News | any user (→ Pending) | moderator approves/rejects before publish |
| Services | any user | none |
| Goods | any user | none (author/admin can delete) |

| Role | Capabilities |
| --- | --- |
| `user` | view everything, submit in all sections, chat |
| `moderator` | + review/approve/reject events |
| `admin` | + promote users to moderator/admin, approve/reject/suspend member access |

### Member access

An account is `Pending` until an admin lets it onto the board. `Approved` access can later be
taken away (`Suspended`) and given back, and both of those decisions require a reason, which is
recorded in the audit log (`member.suspend` / `member.reinstate`) and shown to the member.
`Rejected` is the different case of an account that was never let in. Everything is decided
through `POST /members/{id}/approval` with `{ decision, reason }`.

## Data model

See [`api/Data/Entities`](../api/Data/Entities). Enums are stored as strings. Public reads filter
`DeletedAt == null` and the relevant published/active status. GDPR erasure soft-deletes authored
content, tombstones messages, removes images and anonymises the profile.

## Deployment

Provisioned by Bicep ([`infra/main.bicep`](../infra/main.bicep)) and fully automated by
scripts + path-scoped GitHub Actions pipelines. See [scripts/README.md](../scripts/README.md)
for the step-by-step. The short version:

```bash
# 1. one-time: deploy identity + RBAC + resource group + all repo secrets/vars
./scripts/bootstrap-github-oidc.sh -s <sub> -r <owner>/<repo> -e you@example.com
# 2. provision infrastructure, then deploy application areas independently
gh workflow run deploy-infra.yml -r <owner>/<repo>
gh workflow run deploy-api.yml -r <owner>/<repo>
gh workflow run deploy-frontend.yml -r <owner>/<repo>
# 3. wire up Microsoft (and then Google) sign-in
./scripts/setup-entra-auth.sh -r <owner>/<repo>
```

### The pipelines
Deployment is split into three path-scoped workflows:

- [`deploy-infra.yml`](../.github/workflows/deploy-infra.yml) runs for `infra/**` changes.
- [`deploy-api.yml`](../.github/workflows/deploy-api.yml) runs for `api/**` changes.
- [`deploy-frontend.yml`](../.github/workflows/deploy-frontend.yml) runs for `frontend/**` changes.

The API and frontend workflows resolve the existing resource name from Azure using the
environment tags created by Bicep, so they do not need to wait for an infrastructure job.

PR validation is split the same way into `pr-validate-infra.yml`, `pr-validate-api.yml`, and
`pr-validate-frontend.yml`; each runs only for changes in its area.

### Configuration (all set by `bootstrap-github-oidc`)

- **Secrets**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
  `POSTGRES_ADMIN_PASSWORD`, `DEPLOYER_OBJECT_ID`, `SEED_ADMIN_EMAIL`, and the auth secrets
  `ENTRA_CLIENT_ID` / `ENTRA_CLIENT_SECRET` / `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET`
  (filled by `setup-entra-auth` / `set-google-auth`).
- **Variables**: `AZURE_RESOURCE_GROUP`.

No SWA token or Function App name is stored by hand — the pipeline derives them.

### Identity providers

`setup-entra-auth` scripts the Microsoft Entra app (personal + work accounts) end-to-end.
Google requires a one-time manual OAuth client in Google Cloud Console (redirect
`https://<swa-host>/.auth/login/google/callback`), then `set-google-auth` applies it to the
SWA settings and GitHub secrets.
