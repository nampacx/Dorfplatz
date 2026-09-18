---
name: mitteilungsblatt
description: 'Create, implement, review, or test German Mitteilungsblatt PDF generation from BulletinBoard event data. Use when working on bulletin PDFs, print layouts, event exports, A4 two-column publication, Asch masthead/header, Jahrgang calculation, page flow, or visual PDF regression checks.'
argument-hint: 'Describe the PDF generation or review task and the event-data source.'
---

# Mitteilungsblatt PDF

Use this skill when code must generate a printable Mitteilungsblatt der Ortschaft Asch from system event data. Treat the supplied 2026 issue as the visual contract, while keeping publication values data-driven.

## Workflow

1. Inspect the event model, publication date, caller-supplied start and end dates, and existing PDF stack before choosing an implementation.
2. Load [the measured layout specification](./references/layout-spec.md).
3. Compare against [the original four-page issue](./references/2026_Mitteilungsblatt_34.pdf) when visual fidelity matters. Use [the PDF analyzer](./scripts/analyze_reference_pdf.py) when exact geometry or structure needs inspection.
4. Include only events eligible for publication. Preserve submitted wording except for explicit editorial normalization.
5. Sort and group records deterministically, then map groups to editorial sections.
6. Render the masthead and issue metadata, followed by two-column content flow.
7. Generate the PDF with selectable text, working links, embedded or reliably available fonts, and print-safe images.
8. Validate semantics and pagination, then render every page to images and compare against the reference structure.

## PDF Analysis Tool

Set up an isolated environment from the repository root:

```powershell
./.github/skills/mitteilungsblatt/scripts/setup-python.ps1
```

```bash
bash .github/skills/mitteilungsblatt/scripts/setup-python.sh
```

The setup scripts create `.github/skills/mitteilungsblatt/.venv` and install the pinned packages from [requirements.txt](./scripts/requirements.txt).

Run a compact structural analysis of the bundled PDF:

```powershell
./.github/skills/mitteilungsblatt/.venv/Scripts/python.exe ./.github/skills/mitteilungsblatt/scripts/analyze_reference_pdf.py
```

On Linux or macOS, use `.github/skills/mitteilungsblatt/.venv/bin/python`. To include text bounding boxes and render page previews:

```text
analyze_reference_pdf.py <pdf> --include-text-blocks --output <analysis.json> --render-dir <preview-directory> --dpi 150
```

Use the JSON output to investigate page dimensions, font spans, image boxes, prominent drawings, links, and text flow. Generated analysis and previews belong in the ignored `analysis-output` directory unless they are intentional fixtures.

## Bundled Assets

- [Asch coat of arms](./assets/asch-coat-of-arms.png)
- [Blautopfstadt Blaubeuren logo](./assets/blautopfstadt-blaubeuren-logo.jpg)
- [Rendered masthead reference](./assets/masthead-reference.png)

Use the two separate logo files in generated documents so the center title remains selectable text. Use the masthead reference only for comparison, not as the final header bitmap.

## Data Rules

- Derive `Jahrgang` from the publication year: `publicationDate.Year - 1967`. For example, 2026 produces `59`.
- Do not use the machine's current year when generating a historical or scheduled issue.
- Require an explicit event date range from the caller. Do not silently substitute a rolling 7-day or 90-day window.
- Issue number (`Nr.`) is not implemented yet. Render ` / Nr. –` when no explicit issue number is supplied; never invent a numeric value.
- Format dates and times in German. Use German month names in the running header, for example `19. August 2026`.
- Include only records whose publication status permits public display. In this repository that normally means `EventPost.status === 'Published'`.
- Use a stable order: editorial section, event date ascending with undated notices last, creation time ascending, then title.
- Map current categories as follows: `Verwaltung` to municipal notices, `Vereine` to `Vereine`, `Kirchen` to `Kirchliche Nachrichten`, `Stadt` to city/community notices, and `Sonstiges` to `Sonstiges`. Do not create empty headings.
- Render title prominently, followed by date/time, location when present, description, and author/organization only when publication policy requires it.
- Preserve URLs and email addresses as clickable PDF links.

## Layout Rules

- Produce A4 portrait pages.
- Page 1 has the full masthead above a boxed metadata header. Later pages have only the compact bordered running header.
- Flow content down the left column and then the right column before creating a new page.
- Keep section headings with the first content lines. Avoid widows, orphaned headings, clipped text, overlaps, and accidental blank pages.
- Allow editorial images or notices to occupy one column. Preserve aspect ratio and continue text below them.
- Use the reference's restrained black, white, and gray newspaper styling. Blue is reserved for links and official brand artwork.
- Do not recreate the official coat of arms or city logo with generated shapes. The bundled extracted assets are approved for generated PDFs.

## Completion Checks

- Confirm `Jahrgang` with a boundary test for at least two publication years.
- Confirm the header shows `Nr. –` when no issue number is supplied and the supplied number otherwise.
- Confirm all published events appear exactly once and unpublished events do not appear.
- Extract PDF text and check headings, dates, locations, page numbers, and German characters.
- Render every page at 150 DPI or higher and inspect column alignment, masthead proportions, page breaks, image scaling, and overflow.
- Test sparse, one-page, multi-page, long-title, long-description, missing-date, missing-location, and URL/email cases.
- Keep a deterministic fixture so visual snapshots do not vary with the current date or database order.

## Output

Report the implementation files changed, the generated PDF path, the input fixture used, page count, and validation results. Call out any deliberate difference from the reference issue.