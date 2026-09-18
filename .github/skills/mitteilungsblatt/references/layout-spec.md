# Measured Layout Specification

This specification was measured from `2026_Mitteilungsblatt_34.pdf`, created by Microsoft Word 2021. Measurements are in PDF points, where 72 pt equals one inch. Small tolerances are acceptable when the visual hierarchy and print result remain equivalent.

## Page Geometry

| Property | Reference value |
| --- | ---: |
| Page format | A4 portrait |
| Page size | 595.32 x 841.92 pt |
| Main left edge | 51 pt |
| Left column | x = 51 to about 303 pt |
| Right column | x = 319 to about 570 pt |
| Column gutter | about 16 pt |
| Main right edge | about 570 pt |
| Page 1 content top | about 162.5 pt |
| Later-page content top | about 45.6 pt |
| Typical content bottom | 803 to 810 pt |

The reference uses a dense newspaper layout rather than cards. Body content reads down the entire left column, then continues at the top of the right column.

## Page 1 Masthead

The masthead occupies approximately y = 20 to 96 pt:

- Coat of arms at the left, approximately x = 63 to 129 pt and y = 24 to 95 pt.
- Center title in three lines: `Mitteilungsblatt`, `der Ortschaft Asch`, `Alb – Donau - Kreis`.
- The first two lines are bold and visually dominant; the district line is smaller and regular weight.
- Official `Blautopfstadt Blaubeuren` logo at the right, approximately x = 404 to 548 pt and y = 28 to 72 pt.
- Preserve clear white space around all three masthead elements.

Use approved supplied artwork. Do not redraw, recolor, distort, crop, or replace either official logo.

## Issue Header

Page 1 uses a bordered block spanning approximately x = 50 to 572 pt and y = 103 to 148 pt.

- First row: bold `Jahrgang {year - 1967}` on the left; `/ Nr. {issueNumber}` immediately after it, using `Nr. –` when no number exists; bold `{publication date} / Seite 1` aligned right.
- Second row: `89143 Blaubeuren-Asch, Bei der Hüle 1, Telefon: 07344 6628, E-Mail: asch@ov.blaubeuren.de`.
- Third row: `Öffnungszeiten Mittwoch 17:30 – 19:00 Uhr und Donnerstag 8:00 – 10:00 Uhr und von 17:30 – 19:00 Uhr`.
- Keep the email clickable.
- Use a compact border with no decorative fill.

Pages 2 and later use a compact bordered running header at approximately y = 24 to 44 pt. It contains only the bold issue label on the left and `{publication date} / Seite {pageNumber}` on the right.

## Typography

The source uses Arial:

| Role | Reference style |
| --- | --- |
| Body and headings | Arial, about 9.96 pt |
| Section/article headings | Arial Bold, about 9.96 pt |
| Small boxed/contact text | Arial, about 9 pt |
| Running header | Arial Bold, about 11.04 pt |
| Masthead title | Arial Bold, approximately 16 to 20 pt |

Use Arial when licensing and runtime availability permit. Otherwise use a metrically compatible sans serif and lock it in tests because font substitution changes pagination. Body line spacing should remain compact, approximately 1.05 to 1.15 times the font size.

## Editorial Components

- **Article:** bold title followed immediately by regular body copy. Multiple introductory lines may be bold.
- **Section bar:** medium-gray horizontal bar with bold white text, used for labels such as `Vereine`, `Kirchliche Nachrichten`, and `Sonstiges`.
- **Contact box:** thin black rectangular border around compact contact details; no rounded corners or shadow.
- **Editorial image:** one-column image with preserved aspect ratio. The page 1 example occupies roughly x = 319 to 567 pt.
- **QR code:** high-contrast square with sufficient quiet zone; never resample so heavily that it becomes unreadable.
- **Links:** blue and underlined when visibly printed, with matching PDF annotations.
- **Closing:** final editorial text and signature may remain in the right column with deliberate white space below.

## Pagination

- Treat the two columns as sequential frames, not independent feeds.
- A section may continue into the next column or page without repeating its heading unless editorial clarity requires it.
- Keep an article title with at least two following lines when space permits.
- Keep a date heading with its associated time/location line.
- Move a whole short item to the next frame instead of splitting it awkwardly.
- Split long articles only at paragraph boundaries where practical.
- Repeat the compact issue header on every page after page 1.
- Compute `Seite` from the final rendered pagination, not from source records.

## Reference Content Observations

The four-page reference demonstrates:

1. Full masthead and large issue header on page 1.
2. Text flowing from the left column into the right column.
3. A large one-column event poster followed by section-bar notices.
4. Plain continuation pages with compact running headers.
5. Section bars, bordered contact blocks, QR codes, logos, links, and a closing signature.
6. Articles and schedules that continue across columns and pages without card containers.

These are structural examples, not fixed content. Generated issues must use current eligible event data.

## Visual Acceptance

A generated issue is acceptable when:

- every page is A4 portrait and printable without clipping;
- masthead elements retain their measured proportions and alignment;
- both columns share consistent edges and gutter width;
- body text remains legible at 100% print scale;
- no glyph is missing, especially `ä`, `ö`, `ü`, `ß`, and en dashes;
- no title, paragraph, image, box, or footer overlaps another element;
- page numbering and publication date are correct on every page;
- URL and email annotations point to the displayed destination;
- a rendered page montage is recognizably equivalent to the reference document's editorial structure.
