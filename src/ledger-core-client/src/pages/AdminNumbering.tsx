import { useEffect, useState, type FormEvent } from 'react'
import { api, type NumberReset } from '../api'
import { Button, ErrorBox, useLoad } from '../design'

const resetLabel: Record<NumberReset, string> = { Yearly: 'Co rok', Monthly: 'Co miesiąc', Never: 'Nigdy (numeracja ciągła)' }

/** Wzór numeru faktur sprzedaży bieżącej firmy z podglądem następnego numeru. */
// Błędy tej sekcji są lokalne: wspólny komunikat strony nie może być kasowany przez podgląd na żywo.
export function NumberingSection({ onNotice }: { onNotice: (m: string) => void }) {
  const { data, reload } = useLoad(() => api.numbering(), [])
  const [pattern, setPattern] = useState('')
  const [reset, setReset] = useState<NumberReset>('Yearly')
  const [example, setExample] = useState('')
  const [error, setError] = useState('')

  useEffect(() => { if (data) { setPattern(data.pattern); setReset(data.reset); setExample(data.nextExample) } }, [data])

  // podgląd na żywo; błędny wzór pokazuje komunikat zamiast przykładu
  useEffect(() => {
    if (!data) return
    let cancelled = false
    const timer = setTimeout(() => {
      api.previewNumbering(pattern, reset)
        .then(r => { if (!cancelled) { setExample(r.example); setError('') } })
        .catch(err => { if (!cancelled) { setExample(''); setError((err as Error).message) } })
    }, 250)
    return () => { cancelled = true; clearTimeout(timer) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pattern, reset, data])

  async function save(e: FormEvent) {
    e.preventDefault()
    setError(''); onNotice('')
    try { await api.saveNumbering(pattern, reset); onNotice('Zapisano wzór numeracji faktur sprzedaży'); reload() } catch (err) { setError((err as Error).message) }
  }

  if (!data) return null
  return (
    <>
      <h2>Numeracja faktur sprzedaży</h2>
      <ErrorBox message={error} />
      <form className="card" onSubmit={save} aria-label="Numeracja faktur">
        <div className="form-row">
          <label className="grow">Wzór numeru<input value={pattern} onChange={e => setPattern(e.target.value)} required maxLength={60} /></label>
          <label>Zerowanie numeracji
            <select value={reset} onChange={e => setReset(e.target.value as NumberReset)}>
              {(Object.keys(resetLabel) as NumberReset[]).map(r => <option key={r} value={r}>{resetLabel[r]}</option>)}
            </select>
          </label>
          <Button variant="primary">Zapisz wzór</Button>
        </div>
        <p className="muted small mt-3">Znaczniki: <code>{'{RRRR}'}</code> rok, <code>{'{RR}'}</code> rok dwucyfrowy, <code>{'{MM}'}</code> miesiąc, <code>{'{NNNN}'}</code> numer kolejny
          (liczba liter N = minimalna liczba cyfr). Przykłady: <code>{'FV/{RRRR}/{NNNN}'}</code>, <code>{'{NNN}/{MM}/{RR}'}</code>. Zmiana wzoru nie zmienia wystawionych faktur.</p>
        <p className="flush">Następny numer: <strong data-testid="numbering-example">{example || '—'}</strong></p>
      </form>
    </>
  )
}
