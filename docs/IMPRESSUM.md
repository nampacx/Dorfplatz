# Impressum — what the operator must supply

German law requires an Impressum. This document lists every entry the app needs, why it is
required, and where to put it. It is a technical checklist, **not legal advice** — have the filled-in
result reviewed by a lawyer before go-live.

Everything below lives in one file: [`frontend/src/config/legal.ts`](../frontend/src/config/legal.ts).
The Impressum page (`/impressum`) and the privacy page (`/privacy`) both render from it, so the two
cannot drift apart. Until the placeholders are replaced, `/impressum` shows a red banner listing what
is still missing — an unfilled Impressum cannot go live unnoticed.

## Legal basis

| Rule | What it demands |
| --- | --- |
| **§ 5 DDG** (Digitale-Dienste-Gesetz, replaced § 5 TMG on 14.05.2024) | The mandatory provider details listed below |
| **§ 18 Abs. 2 MStV** (Medienstaatsvertrag) | A named natural person responsible for editorial content |
| **§ 36 VSBG** | A statement on willingness to take part in consumer arbitration |
| **Art. 13 DSGVO** | The controller's identity and contact details in the privacy notice |

§ 5 DDG applies to "geschäftsmäßige" digital services — which covers a Verein's website even
without any commercial intent. Assume it applies.

## Required entries

| Field in `legal.ts` | What to enter | Source |
| --- | --- | --- |
| `name` | Full name of the operating association, exactly as registered | Vereinsregister excerpt |
| `legalForm` | e.g. `eingetragener Verein (e. V.)` | Satzung |
| `address` | Street + number, postcode, city. A PO box is **not** sufficient — it must be an address where legal documents can be served | Satzung |
| `representedBy` | The board members authorised to represent the Verein (§ 26 BGB) | Vereinsregister excerpt |
| `email` | A monitored address — § 5 Abs. 1 Nr. 2 DDG makes e-mail mandatory | — |
| `phone` | A second, direct channel. Case law wants e-mail **plus** one further means of immediate contact; a phone number is the safe choice, a monitored callback form can work | — |
| `contentResponsible.name` | A natural person, of legal age, resident in Germany or the EU (§ 18 Abs. 2 MStV). Leave `address` as `null` if it matches the Verein's address | — |

## Optional entries — fill in only if they apply

| Field | When it applies |
| --- | --- |
| `registerCourt` + `registerNumber` | The Verein is entered in the Vereinsregister (`Amtsgericht …`, `VR 1234`). Both are rendered together or not at all |
| `vatId` | A VAT ID under § 27a UStG actually exists. Do not invent one, and do not enter the Steuernummer instead |
| `supervisoryAuthority` | The activity requires official authorisation |
| `dataProtectionOfficer` | A DPO is required — roughly, 20+ people regularly processing personal data (Art. 37 DSGVO, § 38 BDSG) |
| `privacyPolicyDate` | ISO date shown as "Stand: …" on the privacy page |

## Consumer arbitration (`disputeResolution`)

- `'none'` — not willing and not obliged. The usual choice for a small Verein: the § 36 VSBG duty
  only binds businesses with more than ten employees.
- `'voluntary'` — takes part voluntarily. Name the arbitration body in the text if so.
- `'obliged'` — legally obliged to take part.

> The EU Online Dispute Resolution platform was **shut down on 20.07.2025**. Many Impressum
> templates still carry a link to `ec.europa.eu/consumers/odr` — that link must **not** be added; it
> is dead and pointing at it is itself a warning-notice risk.

## Placement — this part is already done

§ 5 DDG requires the Impressum to be "leicht erkennbar, unmittelbar erreichbar und ständig
verfügbar": reachable from every page, within two clicks, under the word **Impressum**.

`SiteFooter` sits in the app shell (`App.tsx`), so the link is on every route, including for signed-out
visitors — the Impressum must never sit behind a login. Do not move these links into a page that
requires authentication.

## Checklist before go-live

- [ ] Every `TODO` in `frontend/src/config/legal.ts` replaced; `/impressum` shows no red banner
- [ ] Impressum and privacy notice reviewed by a lawyer
- [ ] The e-mail address in `email` is actually monitored
- [ ] `/impressum` and `/privacy` reachable while signed out
- [ ] Remaining items in [GDPR.md](GDPR.md#still-to-do-before-go-live) worked through
