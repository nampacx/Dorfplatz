import { useCallback, useEffect, useRef, type ReactNode } from 'react'

/**
 * Overlay shell: a full-screen sheet on phones, a centred dialog from 900px up.
 *
 * Built on a native <dialog> opened with showModal(), which is what puts it in
 * the top layer, traps focus inside it, makes the rest of the page inert and
 * handles Escape. The hand-rolled backdrop this replaces did none of those.
 *
 * It also takes over a history entry while it is open, so the phone's Back
 * gesture closes the sheet instead of leaving the page — which is what a
 * mobile user expects from anything occupying the whole screen.
 */
export default function Sheet({
  title,
  onClose,
  wide = false,
  children,
}: {
  title: string
  onClose: () => void
  wide?: boolean
  children: ReactNode
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const closedByPop = useRef(false)
  // Callers pass an inline arrow, so onClose is a new function every render.
  // Held in a ref to keep it out of the history effect's deps — re-running
  // that effect would push a second entry on every parent render.
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    const dialog = ref.current
    if (dialog && !dialog.open) dialog.showModal()
    // showModal makes the background inert but does not stop it scrolling.
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previous
    }
  }, [])

  useEffect(() => {
    window.history.pushState({ bbOverlay: true }, '')
    const onPop = () => {
      closedByPop.current = true
      onCloseRef.current()
    }
    window.addEventListener('popstate', onPop)
    return () => {
      window.removeEventListener('popstate', onPop)
      // Closed some other way — a form saved itself, or the parent dropped the
      // state. Drop the entry we pushed so the next Back is not silently
      // swallowed. Skipped once the app has navigated on (ContactButton jumps
      // to /chat from inside a sheet), where going back would undo that.
      if (!closedByPop.current && window.history.state?.bbOverlay) window.history.back()
    }
  }, [])

  /** Every dismissal goes through Back, so there is one path, not two. */
  const requestClose = useCallback(() => {
    if (closedByPop.current) return
    if (window.history.state?.bbOverlay) window.history.back()
    else onCloseRef.current()
  }, [])

  return (
    <dialog
      ref={ref}
      className={wide ? 'sheet wide' : 'sheet'}
      aria-label={title}
      onCancel={(e) => {
        // Escape: cancel the native close so dismissal still runs through Back.
        e.preventDefault()
        requestClose()
      }}
      onClick={(e) => {
        // A click on ::backdrop is reported against the dialog itself, so the
        // only way to tell them apart is the hit point. On a phone the sheet
        // fills the screen, which makes this unreachable — no stray closes.
        const r = e.currentTarget.getBoundingClientRect()
        const outside =
          e.clientY < r.top || e.clientY > r.bottom || e.clientX < r.left || e.clientX > r.right
        if (outside) requestClose()
      }}
    >
      <div className="sheet-content">
        <button className="sheet-close" onClick={requestClose} aria-label="Schließen">×</button>
        {children}
      </div>
    </dialog>
  )
}
