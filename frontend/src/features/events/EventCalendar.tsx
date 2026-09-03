import { useMemo, useState } from 'react'
import type { EventPost } from '../../types'

/** Monday-first, the way a German wall calendar reads. */
const WEEKDAYS = ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So']

/**
 * Local calendar day of an event, as `YYYY-MM-DD`. Built from the local
 * getters rather than toISOString(), which would shift an evening event into
 * the next day for anyone east of UTC.
 */
function dayKey(d: Date) {
  const m = `${d.getMonth() + 1}`.padStart(2, '0')
  const day = `${d.getDate()}`.padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

function startOfMonth(d: Date) {
  return new Date(d.getFullYear(), d.getMonth(), 1)
}

/** 0 for Monday … 6 for Sunday. JS counts from Sunday, so rotate by one. */
function mondayIndex(d: Date) {
  return (d.getDay() + 6) % 7
}

function fmtLongDate(d: Date) {
  return d.toLocaleDateString('de-DE', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
}

function fmtTime(iso: string) {
  const d = new Date(iso)
  if (d.getHours() === 0 && d.getMinutes() === 0) return ''
  return d.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

interface Props {
  events: EventPost[]
  onSelect: (e: EventPost) => void
}

export default function EventCalendar({ events, onSelect }: Props) {
  const today = useMemo(() => new Date(), [])
  const [month, setMonth] = useState(() => startOfMonth(today))
  const [selectedDay, setSelectedDay] = useState<string | null>(null)

  /** Events grouped by their local day, each group sorted by start time. */
  const byDay = useMemo(() => {
    const map = new Map<string, EventPost[]>()
    for (const e of events) {
      if (!e.eventDate) continue
      const d = new Date(e.eventDate)
      if (Number.isNaN(d.getTime())) continue
      const key = dayKey(d)
      const list = map.get(key)
      if (list) list.push(e)
      else map.set(key, [e])
    }
    for (const list of map.values()) {
      list.sort((a, b) => (a.eventDate ?? '').localeCompare(b.eventDate ?? ''))
    }
    return map
  }, [events])

  const undated = useMemo(() => events.filter((e) => !e.eventDate), [events])

  const cells = useMemo(() => {
    const first = startOfMonth(month)
    const lead = mondayIndex(first)
    const daysInMonth = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate()
    // Whole weeks only, so the grid never ends mid-row.
    const total = Math.ceil((lead + daysInMonth) / 7) * 7
    return Array.from({ length: total }, (_, i) => {
      const date = new Date(month.getFullYear(), month.getMonth(), i - lead + 1)
      return { date, key: dayKey(date), inMonth: date.getMonth() === month.getMonth() }
    })
  }, [month])

  const todayKey = dayKey(today)
  const selectedEvents = selectedDay ? (byDay.get(selectedDay) ?? []) : []

  function shiftMonth(delta: number) {
    setMonth((m) => new Date(m.getFullYear(), m.getMonth() + delta, 1))
    setSelectedDay(null)
  }

  return (
    <div className="calendar">
      <div className="cal-head">
        <button className="btn secondary small" onClick={() => shiftMonth(-1)} aria-label="Voriger Monat">
          ‹
        </button>
        <strong aria-live="polite">
          {month.toLocaleDateString('de-DE', { month: 'long', year: 'numeric' })}
        </strong>
        <button className="btn secondary small" onClick={() => shiftMonth(1)} aria-label="Nächster Monat">
          ›
        </button>
        <button
          className="btn ghost small"
          onClick={() => {
            setMonth(startOfMonth(new Date()))
            setSelectedDay(null)
          }}
        >
          Heute
        </button>
      </div>

      <div className="cal-grid" role="grid" aria-label="Monatsübersicht">
        {WEEKDAYS.map((w) => (
          <div key={w} className="cal-weekday" role="columnheader">
            {w}
          </div>
        ))}
        {cells.map((cell) => {
          const dayEvents = byDay.get(cell.key) ?? []
          const classes = [
            'cal-day',
            cell.inMonth ? '' : 'muted',
            cell.key === todayKey ? 'today' : '',
            cell.key === selectedDay ? 'selected' : '',
          ]
            .filter(Boolean)
            .join(' ')
          return (
            <div
              key={cell.key}
              className={classes}
              role="gridcell"
              tabIndex={0}
              aria-label={`${fmtLongDate(cell.date)} – ${dayEvents.length} Event(s)`}
              onClick={() => setSelectedDay(cell.key === selectedDay ? null : cell.key)}
              onKeyDown={(ev) => {
                if (ev.key === 'Enter' || ev.key === ' ') {
                  ev.preventDefault()
                  setSelectedDay(cell.key === selectedDay ? null : cell.key)
                }
              }}
            >
              <span className="cal-daynum">{cell.date.getDate()}</span>
              {/* Narrow screens have no room for titles: markers stand in, and
                  the day list below carries the detail. */}
              {dayEvents.length > 0 && (
                <span className="cal-dots" aria-hidden="true">
                  {dayEvents.slice(0, 3).map((e) => (
                    <span key={e.id} className="cal-dot" />
                  ))}
                </span>
              )}
              <span className="cal-events">
                {dayEvents.slice(0, 3).map((e) => (
                  <button
                    key={e.id}
                    type="button"
                    className="cal-event"
                    title={e.title}
                    onClick={(ev) => {
                      ev.stopPropagation()
                      onSelect(e)
                    }}
                  >
                    {e.title}
                  </button>
                ))}
                {dayEvents.length > 3 && <span className="cal-more">+{dayEvents.length - 3} weitere</span>}
              </span>
            </div>
          )
        })}
      </div>

      {selectedDay && (
        <div className="cal-daylist">
          <h3>{fmtLongDate(new Date(`${selectedDay}T00:00:00`))}</h3>
          {selectedEvents.length === 0 ? (
            <p className="cal-daylist-empty">Keine Events an diesem Tag.</p>
          ) : (
            <ul>
              {selectedEvents.map((e) => (
                <li key={e.id}>
                  <button type="button" className="cal-daylist-item" onClick={() => onSelect(e)}>
                    <span className="cal-daylist-time">{fmtTime(e.eventDate!) || '–'}</span>
                    <span>
                      <strong>{e.title}</strong>
                      {e.location && <span className="cal-daylist-loc"> · {e.location}</span>}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {undated.length > 0 && (
        <div className="cal-daylist">
          <h3>Ohne Datum</h3>
          <ul>
            {undated.map((e) => (
              <li key={e.id}>
                <button type="button" className="cal-daylist-item" onClick={() => onSelect(e)}>
                  <span className="cal-daylist-time">–</span>
                  <span>
                    <strong>{e.title}</strong>
                    {e.location && <span className="cal-daylist-loc"> · {e.location}</span>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
