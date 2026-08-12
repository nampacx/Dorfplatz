import type { ReactNode } from 'react'
import Sheet from './Sheet'

/**
 * Listing detail view. A thin alias over {@link Sheet} so the five call sites
 * (events, moderation, services, goods, my events) keep their existing import
 * and get full-screen presentation, focus trapping and Back-to-close for free.
 */
export default function DetailModal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  return (
    <Sheet title={title} onClose={onClose} wide>
      {children}
    </Sheet>
  )
}
