import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Ładowanie danych z obsługą błędu i ponownego pobrania (`refetch`, alias `reload`).
 * Spóźnione odpowiedzi (po zmianie deps, kolejnym pobraniu lub odmontowaniu) są ignorowane,
 * więc dane starego `id` nie nadpiszą nowych.
 */
export function useLoad<T>(fn: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const latest = useRef(0) // numer ostatniego żądania; odpowiedzi o innym numerze są odrzucane
  const fnRef = useRef(fn)
  fnRef.current = fn
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(() => fnRef.current(), deps)
  const refetch = useCallback(() => {
    const id = ++latest.current
    setLoading(true)
    run()
      .then(d => { if (latest.current === id) { setData(d); setError('') } })
      .catch(e => { if (latest.current === id) setError(e instanceof Error ? e.message : String(e)) })
      .finally(() => { if (latest.current === id) setLoading(false) })
  }, [run])
  useEffect(() => {
    refetch()
    // eslint-disable-next-line react-hooks/exhaustive-deps
    return () => { latest.current++ } // unieważnia odpowiedzi w locie (zmiana deps / odmontowanie)
  }, [refetch])
  return { data, error, loading, refetch, reload: refetch }
}
