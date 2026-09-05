# Dorfplatz

Dorfplatz is a digital meeting place for the village of Asch: a trusted, respectful
community space for sharing useful information, making local services visible, and
improving services and cooperation in the village.

The platform was initiated by the **Lebenswert** group in Asch. It is intended to
strengthen the village and help neighbours inform one another, find practical help,
and exchange goods locally. Everyone using Dorfplatz is expected to behave
**respectfully, kindly, and fairly**. A considerate, appreciative atmosphere is a
basic principle of the project.

## What Dorfplatz does

- **Events and news** — Members can submit village announcements, meetings,
  Vereinsfeste, and other events. Submissions enter a pending queue; moderators
  approve or reject them, and can provide a reason, before an item is published.
  Event categories include `Verwaltung`, `Vereine`, `Kirchen`, `Stadt`, and
  `Sonstiges`.
- **Services / Dienstleistungen** — Members can publish either `ich suche`
  (seeking help) or `ich biete` (offering help) posts. Service posts can be edited,
  deleted, or marked closed.
- **Goods / Marktplatz** — Members can list items to buy, sell, or give away with a
  description, price, condition, category, location, and images. Listings can be
  marked sold or withdrawn.
- **Member messages** — A member can start a conversation from a service or
  classified listing to exchange contact details and practical details needed for
  that specific exchange. Messages are real-time and persisted, but this is **not
  a general-purpose chat platform**.
- **Member access and roles** — A newly signed-in account is pending until an
  administrator approves it for the village board. Access can later be suspended
  or reinstated with a recorded reason. The `user` role can view and submit board
  content and use member messages; `moderator` adds event review; `admin` adds
  member approval, suspension, and role management.
- **Account and legal features** — Members can edit their display name, record
  consent, export their data as JSON, or delete their account. Account deletion
  anonymises the profile, removes authored listings and images, and tombstones
  messages. The app includes German `Impressum` (§ 5 DDG) and
  `Datenschutzerklärung` pages, linked from the footer on every route.

The GDPR implementation is a technical aid, not legal advice. Before a production
launch, complete the controller details and retention decisions documented in
[docs/GDPR.md](docs/GDPR.md) and [docs/IMPRESSUM.md](docs/IMPRESSUM.md).

## Abgrenzung: Was der Dorfplatz nicht ist

Der Dorfplatz will **keine** bestehenden Werkzeuge ersetzen:

- **Kein Ersatz für WhatsApp oder Signal.** Die Nachrichtenfunktion dient ausschließlich
  konkreten Absprachen zu einem Angebot, einem Gesuch oder einer Marktplatz-Anzeige.
  Sie ist bewusst keine allgemeine Messenger- oder Chat-Plattform für den Alltag.
- **Kein Ersatz für Kleinanzeigen und ähnliche Verkaufsportale.** Der Marktplatz ist für
  die Nachbarschaft in Asch gedacht — nicht als Handelsplattform mit großer Reichweite,
  Versand, Bezahlfunktionen oder gewerblichem Verkauf.
- **Kein Ersatz für Werkzeuge zur Vereinsverwaltung.** Mitgliederlisten, Beiträge, interne
  Termine und die Organisation der Vereine bleiben dort, wo sie heute schon gepflegt
  werden. Der Dorfplatz macht nur sichtbar, was für das ganze Dorf interessant ist.

Das Ziel ist ein anderes: **Der Dorfplatz soll das Leben in unserem Dorf verbessern.**
Informationen an einem Ort bündeln, Hilfe in der Nachbarschaft sichtbar machen und die
Zusammenarbeit vor Ort stärken. Alles andere darf ruhig dort bleiben, wo es heute schon
gut funktioniert.

## Architecture

| Layer | Technology |
| --- | --- |
| Frontend | React, Vite, and TypeScript SPA on Azure Static Web Apps (Standard) |
| Authentication | Static Web Apps built-in auth with Microsoft Entra ID (including personal Microsoft accounts) and Google |
| Roles | `/api/GetRoles` resolves database roles; protected API operations re-check authorization server-side |
| Backend | Azure Functions, .NET 8 isolated worker, linked to Static Web Apps as a separate “bring your own” Function App |
| Database | Azure Database for PostgreSQL Flexible Server through EF Core and Npgsql |
| Images | Private Azure Blob Storage with short-lived SAS upload URLs |
| Real-time messages | Azure SignalR Service in serverless mode |
| Secrets and telemetry | Azure Key Vault, user-assigned Managed Identity, and Application Insights |

The frontend uses the same-origin `/api` path. The API stores events, services,
goods, member accounts, conversations, and audit records in PostgreSQL. EF Core
migrations are applied by the API during startup. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for data flow, authorization, and
deployment decisions.

## Repository layout

```text
frontend/   React + Vite SPA and Static Web Apps configuration
api/        Azure Functions .NET 8 isolated API, EF Core model, and migrations
infra/      Bicep modules and the main Azure deployment template
scripts/    Bash and PowerShell deployment/bootstrap helpers
docs/       Architecture, local development, GDPR, privacy, and legal guidance
.github/    GitHub Actions for deployment and pull-request validation
```

## Local development

### Prerequisites

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Node.js 20 or later](https://nodejs.org/)
- [Docker](https://www.docker.com/)
- Static Web Apps CLI: `npm install -g @azure/static-web-apps-cli`

Start the local PostgreSQL database and Azurite Blob emulator:

```bash
docker compose up -d
```

Configure the API and choose the first local administrator:

```bash
cd api
cp local.settings.json.example local.settings.json
# Set Seed__AdminEmail to the email used by your local sign-in.
```

From the repository root, start the complete local stack through the Static Web
Apps CLI:

```bash
swa start
```

This reads [`swa-cli.config.json`](swa-cli.config.json), starts Vite and the
Functions host, emulates `/.auth/*` and the `/api` proxy, and serves the app at
**http://localhost:4280**. The mock sign-in screen lets you choose a username and
roles. Database migrations run automatically when the API starts.

For individual processes:

```bash
cd frontend && npm install && npm run dev
cd api && func start
```

More detail, including EF Core migration commands and role setup, is in
[docs/LOCAL_DEV.md](docs/LOCAL_DEV.md).

## Deployment

Azure resources are provisioned with Bicep and deployed through path-scoped GitHub
Actions workflows. The recommended CI/CD path is:

```bash
# One-time: create the OIDC identity, RBAC, resource group, and GitHub settings.
./scripts/bootstrap-github-oidc.sh -s <subscriptionId> -e you@example.com

# Provision infrastructure, then deploy the API and frontend independently.
gh workflow run deploy-infra.yml
gh workflow run deploy-api.yml
gh workflow run deploy-frontend.yml
```

The workflows can also run automatically: `deploy-infra.yml` for changes to `infra/`,
`deploy-api.yml` for changes to `api/`, and `deploy-frontend.yml` for changes to
`frontend/` on `main`. The API and frontend deployments resolve their existing Azure
resources by the environment tags created by Bicep, so infrastructure must be
provisioned at least once before they can run independently. The API applies EF Core
migrations on cold start, so CI does not need a separate database migration
connection.

After the first deployment, configure Microsoft sign-in with
`setup-entra-auth` and configure Google sign-in with a manually created Google
OAuth client and `set-google-auth`. A local, non-CI deployment is also available:

```bash
./scripts/deploy.sh -s <subscriptionId> -e you@example.com
```

PowerShell equivalents are provided beside the Bash scripts. See
[scripts/README.md](scripts/README.md) and the deployment section of
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for prerequisites, configuration,
and teardown.

## Contributing

Dorfplatz is an **open-source project**, and everyone is welcome to contribute.
Useful contributions include product improvements, accessibility and UX work,
documentation, translations, tests, and privacy or security review.

To contribute:

1. Fork the repository and create a focused branch.
2. Run the local stack and make the smallest coherent change.
3. Run the relevant checks before opening a pull request:
   `cd frontend && npm run lint && npm run build`, and
   `dotnet build api/BulletinBoard.Api.csproj`.
4. Explain the user-facing behavior, implementation details, and any deployment
   or data-protection implications in the pull request.

Please keep changes focused, do not commit secrets or local settings, and preserve
the purpose-bound nature of member messages. Pull requests are validated by the
path-scoped `pr-validate-infra.yml`, `pr-validate-api.yml`, and
`pr-validate-frontend.yml` workflows.

## Further documentation

- [Architecture and deployment](docs/ARCHITECTURE.md)
- [Local development](docs/LOCAL_DEV.md)
- [GDPR implementation notes](docs/GDPR.md)
- [Privacy policy guidance](docs/PRIVACY.md)
- [Impressum guidance](docs/IMPRESSUM.md)
- [Deployment scripts](scripts/README.md)
