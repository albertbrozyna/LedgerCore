import { useCallback, useEffect, useState, type ReactNode } from 'react'

/** Ładowanie danych z obsługą błędu i ponownego pobrania. */
export function useLoad<T>(fn: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fn, deps)
  const reload = useCallback(() => {
    setLoading(true)
    run().then(d => { setData(d); setError('') }).catch(e => setError(e.message)).finally(() => setLoading(false))
  }, [run])
  useEffect(() => { reload() }, [reload])
  return { data, error, loading, reload }
}

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

export function Loading() { return <p className="muted">Ładowanie…</p> }

/** Nagłówek widoczny tylko na wydruku: nazwa firmy, tytuł, okres i data wydruku. */
export function PrintHeader({ title, period }: { title: string; period?: string }) {
  const [name, setName] = useState('')
  useEffect(() => { import('./api').then(m => m.apiY.company()).then(c => setName(c.name)).catch(() => {}) }, [])
  return (
    <div className="print-only print-header" data-testid="print-header">
      <h2>{name || 'LedgerCore'} – {title}</h2>
      <div>{period ? `${period} · ` : ''}wydrukowano {new Date().toLocaleString('pl-PL')}</div>
    </div>
  )
}
