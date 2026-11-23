import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'

export function ErrorBox({ message }: { message?: string }) {
  return message ? <div className="alert alert-error" role="alert">{message}</div> : null
}

export function Notice({ message }: { message?: string }) {
  return message ? <div className="alert alert-ok" role="status">{message}</div> : null
}

export function PageHeader({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="page-header">
      <h1>{title}</h1>
      <div className="page-actions no-print">{children}</div>
    </div>
  )
}

export function Breadcrumbs({ items }: { items: { label: string; to?: string }[] }) {
  return (
    <nav aria-label="Ścieżka" className="breadcrumbs no-print">
      <ol>
        {items.map((it, i) => (
          <li key={i} aria-current={i === items.length - 1 ? 'page' : undefined}>
            {it.to && i < items.length - 1 ? <Link to={it.to}>{it.label}</Link> : it.label}
          </li>
        ))}
      </ol>
    </nav>
  )
}

/** Zakładki (role tablist/tab); przyciski mają tekst etykiety, więc nadają się do `getByRole('tab')`. */
export function Tabs<T extends string>({ label, value, onChange, tabs }: {
  label: string; value: T; onChange: (v: T) => void; tabs: { value: T; label: string }[]
}) {
  return (
    <div className="tabs" role="tablist" aria-label={label}>
      {tabs.map(t => (
        <button key={t.value} type="button" role="tab" aria-selected={value === t.value}
          className={value === t.value ? 'active' : ''} onClick={() => onChange(t.value)}>{t.label}</button>
      ))}
    </div>
  )
}

export type BadgeTone = 'neutral' | 'info' | 'ok' | 'danger' | 'warn'
export function Badge({ tone = 'neutral', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={`badge badge-${tone}`}>{children}</span>
}

const statusTone: Record<string, BadgeTone> = {
  unpaid: 'info', overdue: 'danger', paid: 'ok', canceled: 'neutral', correction: 'warn',
}
/** Status zawsze ma tekst (nie tylko kolor). `canceled` jest dodatkowo przekreślony. */
export function StatusBadge({ status, children }: { status: string; children: ReactNode }) {
  return <span className={`badge badge-${statusTone[status] ?? 'neutral'} status-${status}`}>{children}</span>
}

export function EmptyState({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return <div className="empty-state"><p className="muted">{children}</p>{action}</div>
}

export function Skeleton({ lines = 3 }: { lines?: number }) {
  return (
    <div className="skeleton">
      <span className="sr-only">Ładowanie…</span>
      {Array.from({ length: lines }, (_, i) => <div key={i} className="skeleton-line" aria-hidden="true" />)}
    </div>
  )
}

export function Loading() { return <Skeleton /> }

/** Nagłówek widoczny tylko na wydruku: nazwa firmy, tytuł, okres i data wydruku. */
export function PrintHeader({ title, period }: { title: string; period?: string }) {
  const [name, setName] = useState('')
  useEffect(() => { import('../api').then(m => m.api.company()).then(c => setName(c.name)).catch(() => {}) }, [])
  return (
    <div className="print-only print-header" data-testid="print-header">
      <h2>{name || 'LedgerCore'} – {title}</h2>
      <div>{period ? `${period} · ` : ''}wydrukowano {new Date().toLocaleString('pl-PL')}</div>
    </div>
  )
}
