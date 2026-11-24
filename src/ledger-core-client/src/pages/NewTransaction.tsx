import { useEffect, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, companyStore, type Side } from '../api'
import { useAuth } from '../auth'
import { fmtMoney, parseAmount, todayIso } from '../format'
import { Button, Combobox, ErrorBox, Notice, PageHeader, useLoad, type ComboOption } from '../design'

interface Line { accountId: string; debit: string; credit: string }
const emptyLine = (): Line => ({ accountId: '', debit: '', credit: '' })
const fmtInput = (n: number) => n.toFixed(2).replace('.', ',')

// Ostatnio używane konta (per firma, w przeglądarce) – pokazywane na górze listy podpowiedzi.
const recentKey = () => `lc.recent.${companyStore.id ?? ''}`
function readRecent(): string[] {
  try { return JSON.parse(localStorage.getItem(recentKey()) ?? '[]') } catch { return [] }
}
function pushRecent(ids: string[]) {
  try {
    const next = [...new Set([...ids, ...readRecent()])].slice(0, 8)
    localStorage.setItem(recentKey(), JSON.stringify(next))
  } catch { /* tryb prywatny */ }
}

/** Kwota z pola formularza: pusta = 0, niepoprawna = null. */
const amountOf = (s: string) => (s.trim() ? parseAmount(s) : 0)

export function NewTransactionPage() {
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const [params] = useSearchParams()
  const copyId = params.get('copy')
  const accounts = useLoad(() => api.accounts(true), [])
  const templates = useLoad(() => api.templates(), [])
  const [templateName, setTemplateName] = useState('')
  const [notice, setNotice] = useState('')
  const [description, setDescription] = useState('')
  const [date, setDate] = useState(todayIso())
  const [lines, setLines] = useState<Line[]>([emptyLine(), emptyLine()])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [recent] = useState(readRecent)
  const [before, setBefore] = useState<Record<string, number>>({})
  const formRef = useRef<HTMLFormElement>(null)

  // „Kopiuj jako nowe”: opis i wiersze z istniejącego księgowania, data = dziś
  useEffect(() => {
    if (!copyId) return
    let cancelled = false
    api.transaction(copyId).then(t => {
      if (cancelled) return
      setDescription(t.description)
      setDate(todayIso())
      setLines(t.entries.map(e => ({
        accountId: e.accountId,
        debit: e.side === 'Debit' ? fmtInput(e.amount) : '',
        credit: e.side === 'Credit' ? fmtInput(e.amount) : '',
      })))
      setNotice(`Skopiowano księgowanie ${t.number}: sprawdź opis i kwoty, data ustawiona na dziś.`)
    }).catch(e => { if (!cancelled) setError(e instanceof Error ? e.message : String(e)) })
    return () => { cancelled = true }
  }, [copyId])

  const accountList = accounts.data
  const options: ComboOption[] = useMemo(() => {
    const active = (accountList ?? []).filter(a => a.isActive)
    const byId = new Map(active.map(a => [a.id, a]))
    const rec = recent.map(id => byId.get(id)).filter((a): a is NonNullable<typeof a> => !!a)
    const recIds = new Set(rec.map(a => a.id))
    const opt = (a: (typeof active)[number], group?: string): ComboOption => ({ value: a.id, label: `${a.code} ${a.name}`, group })
    return [
      ...rec.map(a => opt(a, 'Ostatnio używane')),
      ...active.filter(a => !recIds.has(a.id)).sort((x, y) => x.code.localeCompare(y.code, undefined, { numeric: true }))
        .map(a => opt(a, rec.length ? 'Wszystkie konta' : undefined)),
    ]
  }, [accountList, recent])
  const accountById = useMemo(() => new Map((accountList ?? []).map(a => [a.id, a])), [accountList])

  const usedIds = useMemo(() => [...new Set(lines.map(l => l.accountId).filter(Boolean))].sort(), [lines])
  const usedKey = usedIds.join(',')
  useEffect(() => {
    if (!usedKey) { setBefore({}); return }
    let cancelled = false
    const timer = setTimeout(() => {
      api.accountBalances(usedKey.split(',')).then(r => { if (!cancelled) setBefore(Object.fromEntries(r.map(x => [x.accountId, x.balance]))) }).catch(() => {})
    }, 200)
    return () => { cancelled = true; clearTimeout(timer) }
  }, [usedKey])

  if (!canWrite) return <><PageHeader title="Nowe księgowanie" /><ErrorBox message="Twoja rola (Podgląd) nie pozwala na księgowanie." /></>

  const amounts = lines.map(l => ({ d: amountOf(l.debit), c: amountOf(l.credit) }))
  const invalid = amounts.some(a => a.d === null || a.c === null)
  const totalD = amounts.reduce((s, a) => s + (a.d ?? 0), 0)
  const totalC = amounts.reduce((s, a) => s + (a.c ?? 0), 0)
  const diff = Math.round((totalD - totalC) * 100) / 100
  const balanced = !invalid && diff === 0 && totalD > 0

  // podgląd: saldo konta przed → po (w znaku naturalnym konta: aktywa/koszty Wn−Ma, pozostałe Ma−Wn)
  const preview = usedIds.map(id => {
    const a = accountById.get(id)
    const delta = lines.reduce((s, l, i) => (l.accountId === id ? s + (amounts[i].d ?? 0) - (amounts[i].c ?? 0) : s), 0)
    const debitNormal = a?.type === 'Asset' || a?.type === 'Expense'
    const change = Math.round((debitNormal ? delta : -delta) * 100) / 100
    return { id, code: a?.code ?? '?', name: a?.name ?? '', before: before[id], change }
  })

  const update = (i: number, patch: Partial<Line>) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))

  /** Wybór konta w pustym wierszu uzupełnia brakującą kwotę drugiej strony (różnica bilansu). */
  function selectAccount(i: number, accountId: string) {
    setLines(prev => {
      const next = prev.map((l, j) => (j === i ? { ...l, accountId } : l))
      const row = next[i]
      if (row.debit.trim() || row.credit.trim()) return next
      let d = 0, c = 0
      for (const [j, l] of next.entries()) {
        if (j === i) continue
        const a = amountOf(l.debit), b = amountOf(l.credit)
        if (a === null || b === null) return next
        d += a; c += b
      }
      const gap = Math.round((d - c) * 100) / 100
      if (gap > 0) row.credit = fmtInput(gap)
      else if (gap < 0) row.debit = fmtInput(-gap)
      return next
    })
  }

  const focusSoon = (id: string) => requestAnimationFrame(() => document.getElementById(id)?.focus())

  /** Enter w polach kwot: przechodzi dalej; w ostatnim polu ostatniego wiersza dodaje wiersz. */
  function amountKey(e: KeyboardEvent<HTMLInputElement>, i: number, side: 'debit' | 'credit') {
    if (e.key !== 'Enter' || e.ctrlKey || e.metaKey) return
    e.preventDefault()
    if (side === 'debit') return focusSoon(`cre-${i}`)
    if (i < lines.length - 1) return focusSoon(`acc-${i + 1}`)
    setLines(ls => [...ls, emptyLine()])
    focusSoon(`acc-${i + 1}`)
  }

  function formKey(e: KeyboardEvent<HTMLFormElement>) {
    if (e.key !== 'Enter') return
    if (e.ctrlKey || e.metaKey) {
      e.preventDefault()
      if (!balanced) setError('Księgowanie nie jest zbilansowane – popraw kwoty (Winien musi równać się Ma).')
      else if (!busy) formRef.current?.requestSubmit()
    } else if ((e.target as HTMLElement).tagName === 'INPUT') {
      e.preventDefault() // Enter w polu nie wysyła formularza; księguje przycisk lub Ctrl+Enter
    }
  }

  function applyTemplate(id: string) {
    const t = templates.data?.find(x => x.id === id)
    if (!t) return
    if (!description) setDescription(t.description)
    setLines(t.lines.map(l => ({
      accountId: l.accountId,
      debit: l.side === 'Debit' && l.amount ? String(l.amount).replace('.', ',') : '',
      credit: l.side === 'Credit' && l.amount ? String(l.amount).replace('.', ',') : '',
    })))
  }

  async function saveTemplate() {
    setError(''); setNotice('')
    const tl = lines.map((l, i) => ({ l, a: amounts[i] })).filter(x => x.l.accountId)
      .map(({ l, a }) => ({ accountId: l.accountId, side: (l.credit.trim() ? 'Credit' : 'Debit') as Side, amount: ((a.d || a.c) ?? 0) || null }))
    try {
      await api.saveTemplate({ name: templateName, description, lines: tl })
      setNotice(`Zapisano szablon „${templateName}”`); setTemplateName(''); templates.reload()
    } catch (err) { setError((err as Error).message) }
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    const entries: { accountId: string; amount: number; side: Side }[] = []
    for (const [i, l] of lines.entries()) {
      const { d, c } = amounts[i]
      if (!d && !c && !l.accountId) continue
      if (!l.accountId) { setError(`Wiersz ${i + 1}: wybierz konto`); return }
      if (d && c) { setError(`Wiersz ${i + 1}: wpisz kwotę tylko po jednej stronie (Winien albo Ma)`); return }
      if (!d && !c) { setError(`Wiersz ${i + 1}: podaj kwotę`); return }
      entries.push({ accountId: l.accountId, amount: (d || c)!, side: d ? 'Debit' : 'Credit' })
    }
    setBusy(true)
    try {
      const r = await api.createTransaction({ description, date, entries })
      pushRecent(entries.map(x => x.accountId))
      nav(`/transactions/${r.transactionId}`, { state: { created: r.number } })
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <>
      <PageHeader title="Nowe księgowanie" />
      <ErrorBox message={error || accounts.error} />
      <Notice message={notice} />
      <div className="card form-row">
        <label className="grow">Szablon księgowania
          <select aria-label="Szablon" value="" onChange={e => applyTemplate(e.target.value)}>
            <option value="">– wybierz szablon, aby wypełnić formularz –</option>
            {templates.data?.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
        </label>
        <label>Zapisz bieżące jako szablon
          <input aria-label="Nazwa szablonu" value={templateName} onChange={e => setTemplateName(e.target.value)} placeholder="np. Czynsz za biuro" maxLength={100} />
        </label>
        <Button type="button" disabled={!templateName.trim()} onClick={saveTemplate}>Zapisz szablon</Button>
      </div>
      <form className="card" ref={formRef} onSubmit={submit} onKeyDown={formKey}>
        <div className="form-row">
          <label>Data<input type="date" value={date} onChange={e => setDate(e.target.value)} required /></label>
          <label className="grow">Opis<input value={description} onChange={e => setDescription(e.target.value)} required maxLength={500} placeholder="np. Faktura FV 12/2026 – usługi" /></label>
        </div>
        <table className="lines">
          <thead><tr><th>Konto</th><th className="num">Winien</th><th className="num">Ma</th><th></th></tr></thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={i}>
                <td>
                  <Combobox inputId={`acc-${i}`} label={`Konto w wierszu ${i + 1}`} options={options} value={l.accountId} onChange={v => selectAccount(i, v)}
                    placeholder="numer lub nazwa konta" fallbackLabel={v => { const a = accountById.get(v); return a ? `${a.code} ${a.name} (nieaktywne)` : '' }} />
                </td>
                <td><input id={`deb-${i}`} className="num" inputMode="decimal" aria-label={`Winien w wierszu ${i + 1}`} value={l.debit}
                  onKeyDown={e => amountKey(e, i, 'debit')} onChange={e => update(i, { debit: e.target.value, credit: e.target.value ? '' : l.credit })} placeholder="0,00" /></td>
                <td><input id={`cre-${i}`} className="num" inputMode="decimal" aria-label={`Ma w wierszu ${i + 1}`} value={l.credit}
                  onKeyDown={e => amountKey(e, i, 'credit')} onChange={e => update(i, { credit: e.target.value, debit: e.target.value ? '' : l.debit })} placeholder="0,00" /></td>
                <td>{lines.length > 2 && <Button variant="link" small type="button" onClick={() => setLines(lines.filter((_, j) => j !== i))}>Usuń</Button>}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td><Button small type="button" onClick={() => setLines([...lines, emptyLine()])}>+ Dodaj wiersz</Button></td>
              <td className="num" data-testid="sum-debit">{fmtMoney(totalD)}</td>
              <td className="num" data-testid="sum-credit">{fmtMoney(totalC)}</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
        <p className={balanced ? 'balance ok' : 'balance bad'} data-testid="balance-status">
          {invalid ? 'Niepoprawna kwota (użyj formatu 1234,50)' : balanced ? 'Księgowanie zbilansowane ✓' : `Różnica Winien − Ma: ${fmtMoney(diff)}`}
        </p>
        <Button variant="primary" disabled={busy || !balanced}>{busy ? 'Zapisywanie…' : 'Zaksięguj'}</Button>
        <p className="muted small">Enter w polu kwoty przechodzi dalej (w ostatnim – dodaje wiersz) · Ctrl+Enter księguje · po wyborze konta brakująca kwota uzupełnia się sama.</p>
      </form>
      {preview.length > 0 && (
        <div className="card">
          <h2 className="flush">Wpływ na salda</h2>
          <table className="preview-table" aria-label="Wpływ na salda">
            <thead><tr><th>Konto</th><th className="num">Przed</th><th className="num">Zmiana</th><th className="num">Po</th></tr></thead>
            <tbody>
              {preview.map(p => (
                <tr key={p.id}>
                  <td>{p.code} {p.name}</td>
                  <td className="num" data-testid={`preview-before-${p.code}`}>{p.before === undefined ? '…' : fmtMoney(p.before)}</td>
                  <td className="num" data-testid={`preview-change-${p.code}`}>{fmtMoney(p.change)}</td>
                  <td className="num" data-testid={`preview-after-${p.code}`}>{p.before === undefined ? '…' : fmtMoney(p.before + p.change)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="muted small">Salda w znaku naturalnym konta (aktywa i koszty: Winien − Ma; pozostałe: Ma − Winien), łącznie ze wszystkimi dotychczasowymi księgowaniami.</p>
        </div>
      )}
    </>
  )
}
