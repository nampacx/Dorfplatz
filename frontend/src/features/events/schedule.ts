import type { EventPost } from '../../types'

const END_OF_DAY = { hour: 23, minute: 59, second: 59, millisecond: 999 }

function parseIso(iso: string | null) {
  if (!iso) return null
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? null : date
}

function pad(n: number) {
  return String(n).padStart(2, '0')
}

function isMidnight(date: Date) {
  return date.getHours() === 0
    && date.getMinutes() === 0
    && date.getSeconds() === 0
    && date.getMilliseconds() === 0
}

function isEndOfDay(date: Date) {
  return date.getHours() === END_OF_DAY.hour
    && date.getMinutes() === END_OF_DAY.minute
    && date.getSeconds() === END_OF_DAY.second
    && date.getMilliseconds() === END_OF_DAY.millisecond
}

function formatTime(date: Date) {
  return date.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

function formatDate(date: Date, withWeekday = false) {
  return date.toLocaleDateString('de-DE', withWeekday
    ? { weekday: 'long', day: '2-digit', month: 'long', year: 'numeric' }
    : { day: '2-digit', month: 'long', year: 'numeric' })
}

function sameLocalDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate()
}

function hasVisibleTime(date: Date, kind: 'start' | 'end') {
  if (isMidnight(date)) return false
  return kind === 'start' || !isEndOfDay(date)
}

function formatPart(date: Date, kind: 'start' | 'end', withWeekday = false) {
  const day = formatDate(date, withWeekday)
  return hasVisibleTime(date, kind) ? `${day}, ${formatTime(date)} Uhr` : day
}

export function toDateInputValue(iso: string | null) {
  const date = parseIso(iso)
  if (!date) return ''
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export function toTimeInputValue(iso: string | null, kind: 'start' | 'end') {
  const date = parseIso(iso)
  if (!date || !hasVisibleTime(date, kind)) return ''
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function combineDateAndTime(
  dateText: string,
  timeText: string,
  { blankTimeMeansEndOfDay = false }: { blankTimeMeansEndOfDay?: boolean } = {},
) {
  if (!dateText) return null
  const [year, month, day] = dateText.split('-').map(Number)
  if (!year || !month || !day) return null

  const date = new Date(year, month - 1, day)
  if (timeText) {
    const [hours, minutes] = timeText.split(':').map(Number)
    if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return null
    date.setHours(hours, minutes, 0, 0)
  } else if (blankTimeMeansEndOfDay) {
    date.setHours(END_OF_DAY.hour, END_OF_DAY.minute, END_OF_DAY.second, END_OF_DAY.millisecond)
  } else {
    date.setHours(0, 0, 0, 0)
  }

  return date.toISOString()
}

export function formatSchedule(
  startIso: string | null,
  endIso: string | null,
  { withWeekday = false }: { withWeekday?: boolean } = {},
) {
  const start = parseIso(startIso)
  if (!start) return null

  const end = parseIso(endIso)
  if (!end) return formatPart(start, 'start', withWeekday)

  const startTime = hasVisibleTime(start, 'start') ? formatTime(start) : null
  const endTime = hasVisibleTime(end, 'end') ? formatTime(end) : null
  const day = formatDate(start, withWeekday)

  if (sameLocalDay(start, end)) {
    if (startTime && endTime) return `${day}, ${startTime}–${endTime} Uhr`
    if (startTime) return `${day}, ab ${startTime} Uhr`
    if (endTime) return `${day}, bis ${endTime} Uhr`
    return day
  }

  return `${formatPart(start, 'start', withWeekday)} – ${formatPart(end, 'end', withWeekday)}`
}

export function eventEndsAt(item: Pick<EventPost, 'eventDate' | 'endDate'>) {
  return item.endDate ?? item.eventDate
}

export function formatCalendarTime(startIso: string | null, endIso: string | null) {
  const start = parseIso(startIso)
  if (!start) return '–'

  const end = parseIso(endIso)
  const startTime = hasVisibleTime(start, 'start') ? formatTime(start) : null
  if (!end) return startTime ?? '–'

  const endTime = hasVisibleTime(end, 'end') ? formatTime(end) : null
  if (!sameLocalDay(start, end)) return startTime ? `ab ${startTime}` : 'mehrtägig'
  if (startTime && endTime) return `${startTime}–${endTime}`
  if (startTime) return `ab ${startTime}`
  if (endTime) return `bis ${endTime}`
  return '–'
}

export function scheduleHasExplicitTime(iso: string | null, kind: 'start' | 'end') {
  const date = parseIso(iso)
  return !!date && hasVisibleTime(date, kind)
}
