import { useEffect, useState } from 'react'

/**
 * True from 900px up — the same threshold styles.css uses to switch from the
 * phone shell to the desktop one. Kept in sync by hand, because media queries
 * cannot read custom properties.
 *
 * Reach for this only where a layout decision cannot be expressed in CSS: the
 * chat page needs it to decide whether auto-selecting a conversation makes
 * sense, and whether opening one should push a history entry.
 */
const WIDE = '(min-width: 900px)'

export default function useIsWide() {
  const [wide, setWide] = useState(() => window.matchMedia(WIDE).matches)

  useEffect(() => {
    const mq = window.matchMedia(WIDE)
    const onChange = (e: MediaQueryListEvent) => setWide(e.matches)
    mq.addEventListener('change', onChange)
    // Re-read in case the viewport changed between first render and this effect.
    setWide(mq.matches)
    return () => mq.removeEventListener('change', onChange)
  }, [])

  return wide
}
