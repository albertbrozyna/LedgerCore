import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, apiZ, type Side } from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, parseAmount, todayIso, yearStartIso } from '../format'
import { ErrorBox, Loading, Notice, PageHeader, PrintHeader, useLoad } from '../ui'

export function JournalPage() {
  const [from, setFrom] = useState(yearStartIso())
  const [to, setTo] = useState('')
  const [search, setSearch] = useState('')
  const [accountId, setAccountId] = useState('')
  const [page, setPage] = useState(1)
  const [exportError, setExportError] = useState('')
  const accounts = useLoad(() => api.accounts(true), [])
  const { data, error, loading } = useLoad(
    () => api.transactions({ from, to, search, accountId, page, pageSize: 20 }), [from, to, search, accountId, page])
  const pages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  return (
    <>
      <PageHeader title="Dziennik księgowań">
        <button className="btn" onClick={() => window.print()}>Drukuj / PDF</button>
        <button className="btn" onClick={() => api.exportJournal(from, to).catch(e => setExportError(e.message))}>Eksport CSV</button>
        <Link className="btn btn-primary" to="/transactions/new">Nowe księgowanie</Link>
      </PageHeader>
      <PrintHeader title="Dziennik księgowań" period={`${from} – ${to || 'dziś'}`} />
      <ErrorBox message={error || exportError} />
      <div className="card form-row no-print">
        <label>Od<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1) }} /></label>
        <label>Do<input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1) }} /></label>
        <label>Konto
          <select value={accountId} onChange={e => { setAccountId(e.target.value); setPage(1) }}>
            <option value="">Wszystkie</option>
            {accounts.data?.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
          </select>
        </label>
        <label className="grow">Szukaj<input placeholder="opis lub numer" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
      </div>
      {loading && !data && <Loading />}
      {data && (data.items.length === 0 ? <p className="muted">Brak księgowań dla wybranych kryteriów.</p> : (
        <>
          <table>
            <thead><tr><th>Numer</th><th>Data</th><th>Opis</th><th className="num">Kwota</th><th></th></tr></thead>
            <tbody>
              {data.items.map(t => (
                <tr key={t.id}>
                  <td><Link to={`/transactions/${t.id}`}>{t.number}</Link></td>
                  <td>{fmtDate(t.date)}</td>
                  <td>{t.description}{t.isReversal && <span className="badge"> storno</span>}{t.isReversed && <span className="badge badge-warn"> stornowane</span>}</td>
                  <td className="num">{fmtMoney(t.amount)}</td>
                  <td></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pager">
            <button className="btn btn-small" disabled={page <= 1} onClick={() => setPage(page - 1)}>‹ Poprzednia</button>
            <span>Strona {page} z {pages} ({data.totalCount} księgowań)</span>
            <button className="btn btn-small" disabled={page >= pages} onClick={() => setPage(page + 1)}>Następna ›</button>
          </div>
        </>
      ))}
    </>
  )
}

interface Line { accountId: string; debit: string; credit: string }
const emptyLine = (): Line => ({ accountId: '', debit: '', credit: '' })

export function NewTransactionPage() {
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const accounts = useLoad(() => api.accounts(), [])
  const templates = useLoad(() => apiZ.templates(), [])
  const [templateName, setTemplateName] = useState('')
  const [notice, setNotice] = useState('')
  const [description, setDescription] = useState('')
  const [date, setDate] = useState(todayIso())
  const [lines, setLines] = useState<Line[]>([emptyLine(), emptyLine()])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (!canWrite) return <><PageHeader title="Nowe księgowanie" /><ErrorBox message="Twoja rola (Podgląd) nie pozwala na księgowanie." /></>

  const amounts = lines.map(l => ({ d: l.debit.trim() ? parseAmount(l.debit) : 0, c: l.credit.trim() ? parseAmount(l.credit) : 0 }))
  const invalid = amounts.some(a => a.d === null || a.c === null)
  const totalD = amounts.reduce((s, a) => s + (a.d ?? 0), 0)
  const totalC = amounts.reduce((s, a) => s + (a.c ?? 0), 0)
  const diff = Math.round((totalD - totalC) * 100) / 100
  const balanced = !invalid && diff === 0 && totalD > 0

  const update = (i: number, patch: Partial<Line>) => setLines(lines.map((l, j) => (j === i ? { ...l, ...patch } : l)))

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
      .map(({ l, a }) => ({ accountId: l.accountId, side: (l.credit.trim() ? 'Credit' : 'Debit') as Side, amount: (a.d || a.c) || null }))
    try {
      await apiZ.saveTemplate({ name: templateName, description, lines: tl })
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
        <button type="button" className="btn" disabled={!templateName.trim()} onClick={saveTemplate}>Zapisz szablon</button>
      </div>
      <form className="card" onSubmit={submit}>
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
                  <select aria-label={`Konto w wierszu ${i + 1}`} value={l.accountId} onChange={e => update(i, { accountId: e.target.value })}>
                    <option value="">– wybierz konto –</option>
                    {accounts.data?.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
                  </select>
                </td>
                <td><input className="num" inputMode="decimal" aria-label={`Winien w wierszu ${i + 1}`} value={l.debit} onChange={e => update(i, { debit: e.target.value, credit: e.target.value ? '' : l.credit })} placeholder="0,00" /></td>
                <td><input className="num" inputMode="decimal" aria-label={`Ma w wierszu ${i + 1}`} value={l.credit} onChange={e => update(i, { credit: e.target.value, debit: e.target.value ? '' : l.debit })} placeholder="0,00" /></td>
                <td>{lines.length > 2 && <button type="button" className="btn btn-small btn-link" onClick={() => setLines(lines.filter((_, j) => j !== i))}>Usuń</button>}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td><button type="button" className="btn btn-small" onClick={() => setLines([...lines, emptyLine()])}>+ Dodaj wiersz</button></td>
              <td className="num" data-testid="sum-debit">{fmtMoney(totalD)}</td>
              <td className="num" data-testid="sum-credit">{fmtMoney(totalC)}</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
        <p className={balanced ? 'balance ok' : 'balance bad'} data-testid="balance-status">
          {invalid ? 'Niepoprawna kwota (użyj formatu 1234,50)' : balanced ? 'Księgowanie zbilansowane ✓' : `Różnica Winien − Ma: ${fmtMoney(diff)}`}
        </p>
        <button className="btn btn-primary" disabled={busy || !balanced}>{busy ? 'Zapisywanie…' : 'Zaksięguj'}</button>
      </form>
    </>
  )
}

export function TransactionPage() {
  const { id } = useParams()
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const { data, error, loading } = useLoad(() => api.transaction(id!), [id])
  const [actionError, setActionError] = useState('')
  const [notice, setNotice] = useState('')

  async function reverse() {
    if (!confirm('Utworzyć storno tego księgowania? Zapis oryginalny pozostanie w dzienniku.')) return
    setActionError('')
    try {
      const r = await api.reverseTransaction(id!)
      setNotice(`Utworzono storno ${r.number}`)
      nav(`/transactions/${r.transactionId}`)
    } catch (e) { setActionError((e as Error).message) }
  }

  return (
    <>
      <PageHeader title={data ? `Księgowanie ${data.number}` : 'Księgowanie'}>
        <Link className="btn" to="/transactions">← Dziennik</Link>
        {canWrite && data && !data.reversesTransactionId && !data.reversedByTransactionId &&
          <button className="btn btn-danger" onClick={reverse}>Storno</button>}
      </PageHeader>
      <ErrorBox message={error || actionError} />
      <Notice message={notice} />
      {loading && !data && <Loading />}
      {data && (
        <div className="card">
          <dl className="meta">
            <dt>Data</dt><dd>{fmtDate(data.date)}</dd>
            <dt>Opis</dt><dd>{data.description}</dd>
            {data.reversesTransactionId && <><dt>Storno dokumentu</dt><dd><Link to={`/transactions/${data.reversesTransactionId}`}>{data.reversesNumber}</Link></dd></>}
            {data.reversedByTransactionId && <><dt>Stornowane przez</dt><dd><Link to={`/transactions/${data.reversedByTransactionId}`}>{data.reversedByNumber}</Link></dd></>}
          </dl>
          <table>
            <thead><tr><th>Konto</th><th className="num">Winien</th><th className="num">Ma</th></tr></thead>
            <tbody>
              {data.entries.map((e, i) => (
                <tr key={i}>
                  <td><Link to={`/accounts/${e.accountId}/ledger`}>{e.accountCode}</Link> {e.accountName}</td>
                  <td className="num">{e.side === 'Debit' ? fmtMoney(e.amount) : ''}</td>
                  <td className="num">{e.side === 'Credit' ? fmtMoney(e.amount) : ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
