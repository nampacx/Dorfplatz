import { useRef, useState } from 'react'
import type { GoodsImage } from '../../types'

/**
 * Listing images as one horizontally snapping strip.
 *
 * On a phone you swipe it and dots mark the position; from 900px up it is
 * sized like the old hero and a row of thumbnails scrolls it. Same markup for
 * both, so there is one source of truth for which image is showing.
 *
 * The snapping is pure CSS — no carousel library, which the CSP would block
 * anyway. Only the position indicator needs script, and because the slides are
 * exactly one strip wide, that is a division rather than an observer.
 */
export default function Gallery({ images, title }: { images: GoodsImage[]; title: string }) {
  const stripRef = useRef<HTMLDivElement>(null)
  const [active, setActive] = useState(0)

  function onScroll() {
    const strip = stripRef.current
    if (!strip || strip.clientWidth === 0) return
    const index = Math.round(strip.scrollLeft / strip.clientWidth)
    setActive((prev) => (prev === index ? prev : index))
  }

  function goTo(index: number) {
    const strip = stripRef.current
    if (strip) strip.scrollTo({ left: index * strip.clientWidth, behavior: 'smooth' })
  }

  if (images.length === 0) return null

  return (
    <div className="gallery-wrap">
      <div
        className="gallery"
        ref={stripRef}
        onScroll={onScroll}
        // Focusable so the strip can be scrolled with the arrow keys, which is
        // what replaces the click-only <img> thumbnails this used to have.
        tabIndex={0}
        role="group"
        aria-roledescription="Bildergalerie"
        aria-label={`${images.length} Bilder von ${title}`}
      >
        {images.map((img, i) => (
          <img
            key={img.id}
            data-index={i}
            src={img.url}
            alt={`${title} – Bild ${i + 1} von ${images.length}`}
            loading={i === 0 ? undefined : 'lazy'}
            decoding="async"
          />
        ))}
      </div>

      {images.length > 1 && (
        <>
          <div className="gallery-dots">
            {images.map((img, i) => (
              <button
                key={img.id}
                type="button"
                className={i === active ? 'active' : undefined}
                aria-label={`Bild ${i + 1} von ${images.length}`}
                aria-current={i === active}
                onClick={() => goTo(i)}
              />
            ))}
          </div>
          <div className="thumbrow">
            {images.map((img, i) => (
              <button
                key={img.id}
                type="button"
                className={i === active ? 'active' : undefined}
                aria-label={`Bild ${i + 1} anzeigen`}
                aria-current={i === active}
                onClick={() => goTo(i)}
              >
                <img src={img.url} alt="" loading="lazy" decoding="async" />
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  )
}
