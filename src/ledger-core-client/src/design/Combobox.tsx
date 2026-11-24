import { useId, useMemo, useRef, useState } from 'react'

export interface ComboOption { value: string; label: string; group?: string }

interface Props {
  /** Nazwa dostępna dla czytników ekranu i testów (aria-label pola). */
  label: string
  options: ComboOption[]
  value: string
  onChange: (value: string) => void
  /** Id elementu <input> (do ustawiania fokusu z zewnątrz). */
  inputId?: string
  placeholder?: string
  /** Etykieta wartości spoza listy opcji (np. konto nieaktywne). */
  fallbackLabel?: (value: string) => string
  maxResults?: number
}

const norm = (s: string) => s.toLowerCase().normalize('NFD').replace(/\p{Diacritic}/gu, '').replace(/ł/g, 'l')

/**
 * Pole z autouzupełnianiem (wzorzec ARIA combobox + listbox). Filtruje po kodzie i nazwie bez względu na wielkość liter
 * i polskie znaki; kody zaczynające się od wpisanego tekstu są wyżej. Strzałki wybierają, Enter lub Tab zatwierdza
 * podświetloną pozycję, Esc zamyka i przywraca poprzednią wartość.
 */
export function Combobox({ label, options, value, onChange, inputId, placeholder, fallbackLabel, maxResults = 40 }: Props) {
  const listId = useId()
  const ref = useRef<HTMLInputElement>(null)
  const selectedLabel = options.find(o => o.value === value)?.label ?? (value ? fallbackLabel?.(value) ?? '' : '')
  const [text, setText] = useState<string | null>(null) // null = pokazuj etykietę wybranej wartości
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)

  const shown = useMemo(() => {
    const q = norm((text ?? '').trim())
    if (!q) return options.slice(0, maxResults)
    const scored = options.map(o => {
      const l = norm(o.label)
      const code = l.split(' ')[0]
      const rank = code === q ? 0 : code.startsWith(q) ? 1 : l.startsWith(q) ? 2 : l.includes(q) ? 3 : -1
      return { o, rank }
    }).filter(x => x.rank >= 0)
    scored.sort((a, b) => a.rank - b.rank) // sort stabilny – zachowuje kolejność wejściową (ostatnio używane najpierw)
    return scored.map(x => x.o).slice(0, maxResults)
  }, [options, text, maxResults])

  function choose(o: ComboOption) {
    onChange(o.value)
    setText(null); setOpen(false)
  }

  function onKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setActive(a => Math.min(a + 1, shown.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(a => Math.max(a - 1, 0)) }
    else if (e.key === 'Enter' && !e.ctrlKey && !e.metaKey && open && shown[active]) { e.preventDefault(); e.stopPropagation(); choose(shown[active]) }
    else if (e.key === 'Tab' && open && text !== null && shown[active]) choose(shown[active]) // fokus przechodzi dalej, wybór zostaje
    else if (e.key === 'Escape' && open) { e.preventDefault(); e.stopPropagation(); setText(null); setOpen(false) }
  }

  const activeId = open && shown[active] ? `${listId}-${active}` : undefined
  return (
    <div className="combobox">
      <input ref={ref} id={inputId} role="combobox" aria-label={label} aria-expanded={open} aria-controls={listId} aria-autocomplete="list"
        aria-activedescendant={activeId} autoComplete="off" placeholder={placeholder}
        value={text ?? selectedLabel}
        onFocus={e => { e.target.select(); setActive(0); setOpen(true) }}
        onClick={() => setOpen(true)}
        onChange={e => { setText(e.target.value); setOpen(true); setActive(0) }}
        onKeyDown={onKeyDown}
        onBlur={() => { setText(null); setOpen(false) }} />
      {open && (
        <ul id={listId} role="listbox" aria-label={`${label} – podpowiedzi`} className="combobox-list">
          {shown.length === 0 && <li role="presentation" className="muted combobox-empty">Brak pasujących pozycji</li>}
          {shown.map((o, i) => (
            <li key={o.value}>
              {o.group && (i === 0 || shown[i - 1].group !== o.group) && <div className="palette-group" aria-hidden="true">{o.group}</div>}
              <div id={`${listId}-${i}`} role="option" aria-selected={i === active} className={`palette-item${i === active ? ' active' : ''}`}
                onMouseDown={e => { e.preventDefault(); choose(o) }} onMouseMove={() => setActive(i)}>{o.label}</div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
