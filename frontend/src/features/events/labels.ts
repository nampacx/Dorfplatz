import type { EventPost } from '../../types'

export const STATUS_LABELS: Record<EventPost['status'], string> = {
  Pending: 'In Prüfung',
  Published: 'Veröffentlicht',
  Rejected: 'Abgelehnt',
}
