#!/usr/bin/env python3
"""Inspect a PDF's structure and optionally render its pages for visual review."""

from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
import sys
from typing import Any

try:
    import pymupdf
except ImportError:
    sys.exit(
        "PyMuPDF is required. Run setup-python.ps1 or setup-python.sh from this "
        "directory first."
    )


SKILL_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_PDF = SKILL_ROOT / "references" / "2026_Mitteilungsblatt_34.pdf"


def rounded_box(rect: pymupdf.Rect | tuple[float, ...]) -> list[float]:
    return [round(value, 2) for value in rect]


def analyze_fonts(page: pymupdf.Page) -> list[dict[str, Any]]:
    fonts: Counter[tuple[str, float, int]] = Counter()
    for block in page.get_text("dict")["blocks"]:
        if block["type"] != 0:
            continue
        for line in block["lines"]:
            for span in line["spans"]:
                fonts[(span["font"], round(span["size"], 2), span["flags"])] += 1

    return [
        {"font": font, "size_pt": size, "flags": flags, "span_count": count}
        for (font, size, flags), count in fonts.most_common()
    ]


def analyze_text_blocks(page: pymupdf.Page) -> list[dict[str, Any]]:
    blocks = []
    for block in page.get_text("dict")["blocks"]:
        if block["type"] != 0:
            continue
        text = "\n".join(
            "".join(span["text"] for span in line["spans"])
            for line in block["lines"]
        ).strip()
        blocks.append({"bbox": rounded_box(block["bbox"]), "text": text})
    return blocks


def analyze_images(document: pymupdf.Document, page: pymupdf.Page) -> list[dict[str, Any]]:
    images = []
    for image in page.get_image_info(xrefs=True):
        xref = image.get("xref", 0)
        extension = None
        if xref:
            extension = document.extract_image(xref).get("ext")
        images.append(
            {
                "xref": xref,
                "bbox": rounded_box(image["bbox"]),
                "pixel_width": image["width"],
                "pixel_height": image["height"],
                "bits_per_component": image["bpc"],
                "colorspace_components": image["colorspace"],
                "extension": extension,
            }
        )
    return images


def analyze_drawings(page: pymupdf.Page) -> dict[str, Any]:
    drawings = page.get_drawings()
    prominent = []
    for drawing in drawings:
        rect = drawing["rect"]
        if rect.width <= 150 and rect.height <= 20:
            continue
        prominent.append(
            {
                "bbox": rounded_box(rect),
                "stroke": drawing.get("color"),
                "fill": drawing.get("fill"),
                "line_width": drawing.get("width"),
            }
        )
    return {"count": len(drawings), "prominent": prominent}


def analyze_links(page: pymupdf.Page) -> list[dict[str, Any]]:
    return [
        {"bbox": rounded_box(link["from"]), "uri": link["uri"]}
        for link in page.get_links()
        if link.get("uri")
    ]


def render_pages(
    document: pymupdf.Document, output_directory: Path, dpi: int
) -> list[str]:
    output_directory.mkdir(parents=True, exist_ok=True)
    scale = dpi / 72
    rendered = []
    for page_number, page in enumerate(document, start=1):
        output_path = output_directory / f"page-{page_number:02d}.png"
        page.get_pixmap(matrix=pymupdf.Matrix(scale, scale), alpha=False).save(
            output_path
        )
        rendered.append(str(output_path.resolve()))
    return rendered


def analyze_pdf(
    pdf_path: Path, include_text_blocks: bool, render_directory: Path | None, dpi: int
) -> dict[str, Any]:
    with pymupdf.open(pdf_path) as document:
        pages = []
        for page_number, page in enumerate(document, start=1):
            page_data: dict[str, Any] = {
                "page_number": page_number,
                "width_pt": round(page.rect.width, 2),
                "height_pt": round(page.rect.height, 2),
                "rotation": page.rotation,
                "fonts": analyze_fonts(page),
                "images": analyze_images(document, page),
                "drawings": analyze_drawings(page),
                "links": analyze_links(page),
            }
            if include_text_blocks:
                page_data["text_blocks"] = analyze_text_blocks(page)
            pages.append(page_data)

        result: dict[str, Any] = {
            "source": str(pdf_path.resolve()),
            "metadata": document.metadata,
            "page_count": len(document),
            "pages": pages,
        }
        if render_directory is not None:
            result["rendered_pages"] = render_pages(document, render_directory, dpi)
        return result


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Analyze PDF geometry, fonts, images, drawings, links, and text blocks."
    )
    parser.add_argument(
        "pdf", nargs="?", type=Path, default=DEFAULT_PDF, help="PDF to inspect"
    )
    parser.add_argument("--output", type=Path, help="Write JSON to this file")
    parser.add_argument(
        "--include-text-blocks", action="store_true", help="Include extracted text and bounding boxes"
    )
    parser.add_argument(
        "--render-dir", type=Path, help="Render each page as a PNG in this directory"
    )
    parser.add_argument(
        "--dpi", type=int, default=150, help="Preview resolution; default: 150"
    )
    args = parser.parse_args()
    if not args.pdf.is_file():
        parser.error(f"PDF does not exist: {args.pdf}")
    if args.dpi < 72:
        parser.error("--dpi must be at least 72")
    return args


def main() -> int:
    args = parse_args()
    result = analyze_pdf(
        args.pdf, args.include_text_blocks, args.render_dir, args.dpi
    )
    serialized = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized + "\n", encoding="utf-8")
        print(f"Analysis written to {args.output.resolve()}")
    else:
        print(serialized)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())