# GDPR compliance notes

This app is built to support the core obligations of the GDPR. This document maps each
requirement to where it is implemented. It is a technical aid, **not legal advice** — have the
final deployment reviewed by someone responsible for data protection.

## Data inventory

| Data | Where | Purpose | Retention |
| --- | --- | --- | --- |
| Identity (name, email, provider userId) | `AppUser` (Postgres) | authentication, attribution | until erasure |
| Consent timestamp | `AppUser.ConsentedAt` | proof of consent | until erasure |
| Content (events/services/goods + images) | Postgres + Blob | the board itself | until author deletes / erasure |
| Messages | `Message` (Postgres) | member-to-member chat | tombstoned on erasure |
| Audit log | `AuditLog` | accountability (Art. 5(2)) | operational |

## Data subject rights

| Right | Implementation |
| --- | --- |
| **Transparency** (Art. 13/14) | Privacy page (`/privacy`) + consent banner |
| **Consent** (Art. 6/7) | Banner records `ConsentedAt`; `/api/account/consent` |
| **Access & portability** (Art. 15/20) | `/api/account/export` returns all user data as JSON |
| **Erasure** (Art. 17) | `/api/account/delete` anonymises profile, removes images, tombstones messages, withdraws listings, strips roles, sets `DeletedAt` |
| **Withdraw consent** | erasure or re-prompt; roles/consent cleared |

## Data protection by design (Art. 25)

- **Minimisation**: only the provider's name/email/userId are stored; no PII in URLs or query strings.
- **Security**: TLS everywhere (SWA default), Postgres `SslMode=Require`, private Blob container
  (no public access), CSP + security headers in `staticwebapp.config.json`.
- **Secrets**: held in Azure Key Vault, accessed via Managed Identity — never in source or app code.
- **Least privilege**: the API identity has only Blob Data Contributor, Key Vault Secrets User and
  SignalR access.
- **Region**: deploy to an EU region (set `location` in Bicep) to keep data in the EU.

## Still to do before go-live

- Add a Data Processing Agreement with Microsoft (Azure) — covered by the Microsoft DPA.
- Fill in the controller identity and contact in `PRIVACY.md` / `/privacy`.
- Define and document a concrete retention policy for inactive accounts and old listings.
- Decide whether image EXIF/location metadata should be stripped on upload.
