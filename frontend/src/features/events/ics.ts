import type { EventPost } from '../../types'
import { STATUS_LABELS } from './labels'
import { scheduleHasExplicitTime } from './schedule'

/** RFC 5545 escaping: backslash, semicolon, comma and newlines are special in text values. */
function escapeText(value: string) {
  return value
    .replace(/\\/g, '\\\\')
    .replace(/;/g, '\\;')
    .replace(/,/g, '\\,')
    .replace(/\r\n|\r|\n/g, '\\n')
}

/** Content lines must not exceed 75 octets; continuations start with a single space. */
function fold(line: string) {
  if (line.length <= 75) return line
  const parts = [line.slice(0, 75)]
  let rest = line.slice(75)
  while (rest.length > 74) {
    parts.push(' ' + rest.slice(0, 74))
    rest = rest.slice(74)
  }
  if (rest.length) parts.push(' ' + rest)
  return parts.join('\r\n')
}

function pad(n: number) {
  return String(n).padStart(2, '0')
}

/** Local calendar day of the event, as it is shown in the detail view. */
function dateValue(d: Date) {
  return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}`
}

function utcStamp(d: Date) {
  return (
    `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}` +
    `T${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}Z`
  )
}

function fmtDay(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/**
 * Everything the detail view shows that has no dedicated iCalendar property
 * (category, author, submission date, rejection reason) goes into DESCRIPTION,
 * so the downloaded file carries the full submission.
 */
function buildDescription(item: EventPost) {
  const lines: string[] = []
  if (item.description) lines.push(item.description, '')
  lines.push(`Kategorie: ${item.category}`)
  if (item.location) lines.push(`Ort: ${item.location}`)
  lines.push(`Eingereicht von: ${item.authorName}`)
  lines.push(`Eingereicht am: ${fmtDay(item.createdAt)}`)
  if (item.status !== 'Published') lines.push(`Status: ${STATUS_LABELS[item.status]}`)
  if (item.status === 'Rejected' && item.rejectionReason) {
    lines.push(`Grund der Ablehnung: ${item.rejectionReason}`)
  }
  return lines.join('\n')
}

export function buildEventIcs(item: EventPost, now = new Date()): string {
  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Dorfplatz//Events//DE',
    'CALSCALE:GREGORIAN',
    'METHOD:PUBLISH',
    'BEGIN:VEVENT',
    `UID:${item.id}@dorfplatz`,
    `DTSTAMP:${utcStamp(now)}`,
    `SUMMARY:${escapeText(item.title)}`,
  ]

  if (item.eventDate) {
    const start = new Date(item.eventDate)
    if (scheduleHasExplicitTime(item.eventDate, 'start') || scheduleHasExplicitTime(item.endDate, 'end')) {
      lines.push(`DTSTART:${utcStamp(start)}`)
      if (item.endDate) lines.push(`DTEND:${utcStamp(new Date(item.endDate))}`)
    } else {
      const end = item.endDate ? new Date(item.endDate) : new Date(start)
      end.setDate(end.getDate() + 1)
      lines.push(`DTSTART;VALUE=DATE:${dateValue(start)}`, `DTEND;VALUE=DATE:${dateValue(end)}`)
    }
  } else {
    // No date on the post: anchor the entry on its submission day so the file stays valid.
    const created = new Date(item.createdAt)
    const end = new Date(created)
    end.setDate(end.getDate() + 1)
    lines.push(`DTSTART;VALUE=DATE:${dateValue(created)}`, `DTEND;VALUE=DATE:${dateValue(end)}`)
  }

  if (item.location) lines.push(`LOCATION:${escapeText(item.location)}`)
  lines.push(`CATEGORIES:${escapeText(item.category)}`)
  lines.push(`ORGANIZER;CN=${escapeText(item.authorName)}:MAILTO:noreply@dorfplatz.invalid`)
  lines.push(`DESCRIPTION:${escapeText(buildDescription(item))}`)
  if (item.status !== 'Published') lines.push('STATUS:TENTATIVE')
  else lines.push('STATUS:CONFIRMED')
  lines.push('END:VEVENT', 'END:VCALENDAR')

  return lines.map(fold).join('\r\n') + '\r\n'
}

/** Slug for the download filename; falls back to "event" when nothing survives. */
export function icsFileName(item: EventPost) {
  const slug = item.title
    .toLowerCase()
    .replace(/[äÄ]/g, 'ae')
    .replace(/[öÖ]/g, 'oe')
    .replace(/[üÜ]/g, 'ue')
    .replace(/ß/g, 'ss')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 60)
  return `${slug || 'event'}.ics`
}

export function downloadEventIcs(item: EventPost) {
  const blob = new Blob([buildEventIcs(item)], { type: 'text/calendar;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = icsFileName(item)
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}
