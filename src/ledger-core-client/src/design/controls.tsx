import { useId, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from 'react'

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'link'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  small?: boolean
  /** Pokazuje stan „w toku” i blokuje przycisk. */
  loading?: boolean
}

export function Button({ variant = 'secondary', small, loading, className, disabled, children, ...rest }: ButtonProps) {
  const cls = ['btn', variant !== 'secondary' && `btn-${variant}`, small && 'btn-small', className].filter(Boolean).join(' ')
  return (
    <button {...rest} className={cls} disabled={disabled || loading} aria-busy={loading || undefined}>
      {children}
    </button>
  )
}

/** Etykieta + kontrolka + podpowiedź + błąd pola. Etykieta (<label>) jest kontraktem z testami E2E. */
export function Field({ label, hint, error, grow, children, inline }: {
  label: string; hint?: string; error?: string; grow?: boolean; inline?: boolean; children: ReactNode
}) {
  const hid = useId()
  return (
    <label className={[grow && 'grow', inline && 'inline'].filter(Boolean).join(' ') || undefined} aria-describedby={hint || error ? hid : undefined}>
      {label}
      {children}
      {(hint || error) && <span id={hid} className={`field-note ${error ? 'bad-text' : 'muted'}`} role={error ? 'alert' : undefined}>{error ?? hint}</span>}
    </label>
  )
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) { return <input {...props} /> }

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) { return <select {...props} /> }

/** „1 234,50” / „1234.5” → 1234.5; zwraca null dla niepoprawnego zapisu. */
export function parseMoney(s: string): number | null {
  const t = s.replace(/[\s  ]/g, '').replace(',', '.')
  if (!/^-?\d+(\.\d{1,2})?$/.test(t)) return null
  return Number(t)
}

/** Pole kwoty: akceptuje „1 234,50”; po opuszczeniu pola normalizuje zapis do „1234,50”. */
export function MoneyInput({ value, onChange, ...rest }: Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> & {
  value: string; onChange: (v: string) => void
}) {
  return (
    <input {...rest} className={['num', rest.className].filter(Boolean).join(' ')} inputMode="decimal" autoComplete="off" value={value}
      onChange={e => onChange(e.target.value)}
      onBlur={e => {
        const n = parseMoney(e.target.value)
        if (n !== null) onChange(n.toFixed(2).replace('.', ','))
        rest.onBlur?.(e)
      }} />
  )
}

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** Skróty daty: „t” = dziś, „+7” / „-3” = dziś ± dni. Zwraca ISO (RRRR-MM-DD) albo null. */
export function parseDateShortcut(s: string, today = new Date()): string | null {
  const t = s.trim().toLowerCase()
  if (t === 't') return iso(today)
  const m = /^([+-])(\d{1,4})$/.exec(t)
  if (!m) return null
  const d = new Date(today.getFullYear(), today.getMonth(), today.getDate() + (m[1] === '-' ? -1 : 1) * Number(m[2]))
  return iso(d)
}

/** Pole daty (natywny wybór), z obsługą skrótów wpisanych w polu tekstowym: `t`, `+7`. */
export function DateInput({ value, onChange, ...rest }: Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> & {
  value: string; onChange: (v: string) => void
}) {
  return (
    <input {...rest} type="date" value={value} onChange={e => onChange(e.target.value)}
      onKeyDown={e => {
        if (e.key === 't' && !e.ctrlKey && !e.metaKey) { e.preventDefault(); onChange(iso(new Date())) }
        rest.onKeyDown?.(e)
      }} />
  )
}
