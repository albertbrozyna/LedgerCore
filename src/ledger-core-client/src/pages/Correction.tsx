import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiC, apiX, type InvoiceDetail, type VatRate } from '../api'
import { useAuth } from '../auth'
import { fmtMoney, parseAmount, round2, todayIso, vatPercent, vatRateLabel } from '../format'
import { ErrorBox, Loading, PageHeader, useLoad } from '../ui'

const rates = Object.keys(vatRateLabel) as VatRate[]

interface Row {
  description: string
  beforeNet: number       // kwota netto przed korektą (0 dla nowych pozycji)
  beforeRate: VatRate
  after: string           // kwota netto po korekcie (tekst z pola)
  rate: VatRate           // stawka VAT po korekcie
  isNew: boolean
}

const vatOf = (net: number, rate: VatRate) => round2((net * vatPercent[rate]) / 100)

/** Pozycje korekty to różnice: przy zmianie stawki VAT generowane są dwie pozycje (wycofanie starej + dodanie nowej). */
function toCorrectionLines(rows: Row[], after: (number | null)[]) {
  const out: { description: string; net: number; vatRate: VatRate }[] = []
  rows.forEach((r, i) => {
    const a = after[i] ?? 0
    if (r.isNew) {
      if (a !== 0) out.push({ description: r.description, net: a, vatRate: r.rate })
    } else if (r.rate === r.beforeRate) {
      const diff = round2(a - r.beforeNet)
      if (diff !== 0) out.push({ description: `Korekta: ${r.description}`, net: diff, vatRate: r.rate })
    } else {
      out.push({ description: `Korekta: ${r.description} (stawka ${vatRateLabel[r.beforeRate]})`, net: -r.beforeNet, vatRate: r.beforeRate })
      if (a !== 0) out.push({ description: `Korekta: ${r.description} (stawka ${vatRateLabel[r.rate]})`, net: a, vatRate: r.rate })
    }
  })
  return out
}

export function CorrectionPage() {
  const { id } = useParams()
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const { data, error, loading } = useLoad(() => apiX.invoice(id!), [id])
  const [rows, setRows] = useState<Row[]>([])
  const [issueDate, setIssueDate] = useState(todayIso())
  const [reason, setReason] = useState('')
  const [number, setNumber] = useState('')
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (data) setRows(data.lines.map(l => ({
      description: l.description, beforeNet: l.net, beforeRate: l.vatRate, after: String(l.net).replace('.', ','), rate: l.vatRate, isNew: false,
    })))
  }, [data])

  if (!canWrite) return <><PageHeader title="Korekta faktury" /><ErrorBox message="Twoja rola (Podgląd) nie pozwala na wystawianie korekt." /></>
  if (loading && !data) return <Loading />
  if (!data) return <ErrorBox message={error} />
  const inv: InvoiceDetail = data

  const parsed = rows.map(r => (r.after.trim() === '' ? 0 : parseAmount(r.after)))
  const invalid = parsed.some(p => p === null)
  const lines = invalid ? [] : toCorrectionLines(rows, parsed)
  const net = round2(lines.reduce((s, l) => s + l.net, 0))
  const vat = round2(lines.reduce((s, l) => s + vatOf(l.net, l.vatRate), 0))
  const sales = inv.type === 'Sales'
  const update = (i: number, patch: Partial<Row>) => setRows(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setFormError('')
    if (invalid) { setFormError('Podaj poprawne kwoty netto po korekcie (np. 1234,50)'); return }
    if (lines.length === 0) { setFormError('Korekta nie zmienia żadnej pozycji'); return }
    setBusy(true)
    try {
      const r = await apiC.createCorrection(inv.id, { number: sales ? undefined : number, issueDate, reason, lines })
      nav(`/invoices/${r.invoiceId}`)
    } catch (err) { setFormError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <>
      <PageHeader title={`Korekta faktury ${inv.number}`}>
        <Link className="btn" to={`/invoices/${inv.id}`}>← Faktura</Link>
      </PageHeader>
      <ErrorBox message={formError} />
      <p className="muted">Wpisz kwoty netto <strong>po korekcie</strong>. Aplikacja wyliczy różnice, wystawi fakturę korygującą, zaksięguje ją ze znakiem i zmieni kwotę do zapłaty
        na fakturze {inv.number} (obecnie do zapłaty: {fmtMoney(inv.remaining)}). Zmiana stawki VAT tworzy dwie pozycje korekty.</p>
      <form className="card" onSubmit={submit}>
        <div className="form-row">
          <label>Data wystawienia korekty<input type="date" value={issueDate} onChange={e => setIssueDate(e.target.value)} required /></label>
          {!sales && <label>Numer korekty dostawcy<input value={number} onChange={e => setNumber(e.target.value)} required maxLength={50} /></label>}
          <label className="grow">Przyczyna korekty<input value={reason} onChange={e => setReason(e.target.value)} maxLength={450} placeholder="np. błędna cena, zwrot towaru, rabat" /></label>
        </div>
        <table className="lines">
          <thead><tr><th>Pozycja</th><th className="num">Netto przed</th><th className="num">Netto po korekcie</th><th>VAT po</th><th className="num">Różnica netto</th><th className="num">Różnica VAT</th><th></th></tr></thead>
          <tbody>
            {rows.map((r, i) => {
              const a = parsed[i] ?? 0
              const same = r.rate === r.beforeRate
              const dNet = r.isNew ? a : round2(a - r.beforeNet)
              const dVat = r.isNew || same ? vatOf(dNet, r.rate) : round2(vatOf(a, r.rate) - vatOf(r.beforeNet, r.beforeRate))
              return (
                <tr key={i}>
                  <td>{r.isNew
                    ? <input aria-label={`Opis nowej pozycji ${i + 1}`} value={r.description} onChange={e => update(i, { description: e.target.value })} placeholder="Opis nowej pozycji" required />
                    : r.description}</td>
                  <td className="num">{fmtMoney(r.beforeNet)}</td>
                  <td><input className="num" inputMode="decimal" aria-label={`Netto po korekcie pozycji ${i + 1}`} value={r.after} onChange={e => update(i, { after: e.target.value })} /></td>
                  <td>
                    <select aria-label={`Stawka VAT po korekcie pozycji ${i + 1}`} value={r.rate} onChange={e => update(i, { rate: e.target.value as VatRate })}>
                      {rates.map(x => <option key={x} value={x}>{vatRateLabel[x]}</option>)}
                    </select>
                  </td>
                  <td className="num">{fmtMoney(dNet)}</td>
                  <td className="num">{fmtMoney(dVat)}</td>
                  <td>{r.isNew && <button type="button" className="btn btn-small btn-link" onClick={() => setRows(rows.filter((_, j) => j !== i))}>Usuń</button>}</td>
                </tr>
              )
            })}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={4}><button type="button" className="btn btn-small" onClick={() => setRows([...rows, { description: '', beforeNet: 0, beforeRate: 'R23', after: '', rate: 'R23', isNew: true }])}>+ Dodaj nową pozycję</button></td>
              <td className="num" data-testid="corr-net">{fmtMoney(net)}</td>
              <td className="num" data-testid="corr-vat">{fmtMoney(vat)}</td><td></td>
            </tr>
          </tfoot>
        </table>
        <p className="balance" data-testid="corr-gross">Różnica brutto (zmiana należności): <strong>{fmtMoney(round2(net + vat))}</strong></p>
        <button className="btn btn-primary" disabled={busy || invalid || lines.length === 0}>{busy ? 'Zapisywanie…' : 'Wystaw korektę i zaksięguj'}</button>
      </form>
    </>
  )
}
