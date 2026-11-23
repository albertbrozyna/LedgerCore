import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type InvoiceItem } from '../api'
import { useAuth } from '../auth'
import { fmtMoney } from '../format'
import { matchRow, parseBankCsv, type BankRow, type Confidence } from '../bankImport'
import { Badge, Button, ErrorBox, Notice, PageHeader, useLoad } from '../design'

interface Row extends BankRow { invoiceId: string; confidence: Confidence; selected: boolean; result?: string; ok?: boolean }

const confLabel: Record<Confidence, string> = { exact: 'pewne', number: 'wg numeru', amount: 'wg kwoty', none: 'brak' }

export function BankImportPage() {
  const { canWrite } = useAuth()
  const accounts = useLoad(() => api.accounts(), [])
  const [open, setOpen] = useState<InvoiceItem[]>([])
  const [accountId, setAccountId] = useState('')
  const [text, setText] = useState('')
  const [rows, setRows] = useState<Row[]>([])
  const [skipped, setSkipped] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  if (!canWrite) return <><PageHeader title="Import wyciągu bankowego" /><ErrorBox message="Twoja rola (Podgląd) nie pozwala na rejestrowanie płatności." /></>

  const bankAccounts = accounts.data?.filter(a => a.type === 'Asset') ?? []

  async function analyze(csv: string) {
    setError(''); setNotice('')
    try {
      const parsed = parseBankCsv(csv)
      if (parsed.rows.length === 0) { setRows([]); setError('Nie znaleziono żadnych operacji. Plik powinien zawierać kolumny: data, kwota, tytuł (opcjonalnie kontrahent).'); return }
      const list = (await api.invoices({ status: 'unpaid', pageSize: 200 })).items
      setOpen(list)
      setSkipped(parsed.skipped)
      setRows(parsed.rows.map(r => {
        const m = matchRow(r, list)
        return { ...r, invoiceId: m.invoice?.id ?? '', confidence: m.confidence, selected: m.confidence === 'exact' || m.confidence === 'number' }
      }))
    } catch (e) { setError((e as Error).message) }
  }

  async function onFile(f: File | undefined) {
    if (!f) return
    const t = await f.text()
    setText(t)
    await analyze(t)
  }

  const update = (i: number, patch: Partial<Row>) => setRows(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  async function post() {
    setBusy(true); setError(''); setNotice('')
    let okCount = 0
    const next = [...rows]
    for (const [i, r] of rows.entries()) {
      if (!r.selected || !r.invoiceId || r.ok) continue
      const inv = open.find(x => x.id === r.invoiceId)!
      const amount = Math.min(Math.abs(r.amount), inv.remaining)
      try {
        await api.addPayment(inv.id, { date: r.date, amount, accountId })
        next[i] = { ...r, ok: true, selected: false, result: `Zaksięgowano ${fmtMoney(amount)} do ${inv.number}` }
        inv.remaining = Math.round((inv.remaining - amount) * 100) / 100
        okCount++
      } catch (e) { next[i] = { ...r, ok: false, result: (e as Error).message } }
    }
    setRows(next); setBusy(false)
    if (okCount) setNotice(`Zaksięgowano płatności: ${okCount}`)
  }

  const selectedCount = rows.filter(r => r.selected && r.invoiceId && !r.ok).length

  return (
    <>
      <PageHeader title="Import wyciągu bankowego">
        <Link className="btn" to="/invoices">← Faktury</Link>
      </PageHeader>
      <ErrorBox message={error || accounts.error} />
      <Notice message={notice} />
      <div className="card">
        <p className="muted">Wczytaj wyciąg w formacie CSV (kolumny: data, kwota, tytuł, opcjonalnie kontrahent; separator „;”, „,” lub tabulator).
          Wpływy są dopasowywane do faktur sprzedaży, wypłaty – do faktur zakupu. Przed księgowaniem możesz poprawić dopasowanie.</p>
        <div className="form-row">
          <label className="grow">Konto bankowe / kasa
            <select value={accountId} onChange={e => setAccountId(e.target.value)} aria-label="Konto bankowe">
              <option value="">– wybierz konto –</option>
              {bankAccounts.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
            </select>
          </label>
          <label>Plik CSV<input type="file" accept=".csv,.txt" aria-label="Plik wyciągu" onChange={e => onFile(e.target.files?.[0])} /></label>
        </div>
        <label className="mt-3">…albo wklej treść wyciągu
          <textarea rows={4} value={text} onChange={e => setText(e.target.value)} aria-label="Treść wyciągu" />
        </label>
        <Button  className="mt-2" onClick={() => analyze(text)} disabled={!text.trim()}>Analizuj</Button>
      </div>

      {rows.length > 0 && (
        <>
          <table aria-label="Operacje z wyciągu">
            <thead><tr><th></th><th>Data</th><th>Opis operacji</th><th className="num">Kwota</th><th>Dopasowanie</th><th>Faktura</th><th>Wynik</th></tr></thead>
            <tbody>
              {rows.map((r, i) => {
                const candidates = open.filter(x => x.type === (r.amount > 0 ? 'Sales' : 'Purchase'))
                return (
                  <tr key={i}>
                    <td><input type="checkbox" aria-label={`Zaznacz operację ${i + 1}`} checked={r.selected} disabled={!r.invoiceId || r.ok} onChange={e => update(i, { selected: e.target.checked })} /></td>
                    <td>{r.date}</td>
                    <td>{r.title}{r.counterparty && <div className="muted small">{r.counterparty}</div>}</td>
                    <td className="num">{fmtMoney(r.amount)}</td>
                    <td><Badge>{confLabel[r.confidence]}</Badge></td>
                    <td>
                      <select aria-label={`Faktura dla operacji ${i + 1}`} value={r.invoiceId} disabled={r.ok}
                        onChange={e => update(i, { invoiceId: e.target.value, selected: !!e.target.value })}>
                        <option value="">– brak –</option>
                        {candidates.map(x => <option key={x.id} value={x.id}>{x.number} · {x.contractorName} · {fmtMoney(x.remaining)}</option>)}
                      </select>
                    </td>
                    <td className={r.ok ? 'ok-text' : 'bad-text'}>{r.result}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          <p className="muted small">{skipped > 0 && `Pominięto wierszy bez poprawnej daty lub kwoty: ${skipped}. `}Płatność nie przekroczy kwoty pozostałej do zapłaty na fakturze.</p>
          <Button variant="primary"  disabled={busy || !accountId || selectedCount === 0} onClick={post}>
            {busy ? 'Księgowanie…' : `Zaksięguj zaznaczone (${selectedCount})`}
          </Button>
        </>
      )}
    </>
  )
}
