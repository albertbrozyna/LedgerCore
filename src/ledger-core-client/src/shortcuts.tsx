import { useEffect, useRef } from 'react'

/** Czy zdarzenie pochodzi z pola, w którym użytkownik pisze (skróty jednoklawiszowe nie mogą wtedy działać). */
export function isTypingTarget(t: EventTarget | null) {
  if (!(t instanceof HTMLElement)) return false
  return t.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(t.tagName)
}

export const shortcutList: { keys: string; description: string }[] = [
  { keys: 'Ctrl + K', description: 'Wyszukiwarka i paleta poleceń (działa także w polach tekstowych)' },
  { keys: 'N', description: 'Nowe księgowanie' },
  { keys: 'F', description: 'Nowa faktura sprzedaży' },
  { keys: 'Alt + ↑ / Alt + ↓', description: 'Poprzednia / następna firma' },
  { keys: '?', description: 'Pokaż tę listę skrótów' },
  { keys: 'Esc', description: 'Zamknij okno lub anuluj' },
]

interface Handlers {
  onPalette: () => void
  onHelp: () => void
  onNewTransaction?: () => void
  onNewInvoice?: () => void
  onCompanyStep: (delta: 1 | -1) => void
}

/**
 * Globalne skróty klawiaturowe. `Ctrl+K` działa zawsze; pozostałe tylko poza polami tekstowymi
 * i gdy nie jest otwarte żadne okno dialogowe.
 */
export function useGlobalShortcuts(h: Handlers) {
  const ref = useRef(h)
  ref.current = h
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const k = ref.current
      if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        k.onPalette()
        return
      }
      if (e.defaultPrevented || isTypingTarget(e.target) || document.querySelector('dialog[open]')) return
      if (e.ctrlKey || e.metaKey) return
      if (e.altKey && !e.shiftKey && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
        e.preventDefault()
        k.onCompanyStep(e.key === 'ArrowDown' ? 1 : -1)
        return
      }
      if (e.altKey) return
      if (e.key === '?') { e.preventDefault(); k.onHelp(); return }
      if (e.shiftKey) return
      const key = e.key.toLowerCase()
      if (key === 'n' && k.onNewTransaction) { e.preventDefault(); k.onNewTransaction() }
      else if (key === 'f' && k.onNewInvoice) { e.preventDefault(); k.onNewInvoice() }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])
}
