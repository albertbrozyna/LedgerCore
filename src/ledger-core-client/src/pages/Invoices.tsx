import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api, apiX, apiZ, type AttachmentDto, type InvoiceType, type VatRate } from '../api'
import { useAuth } from '../auth'
import {
  addDaysIso, fmtDate, fmtMoney, invoiceTypeLabel, parseAmount, round2, statusLabel, todayIso, vatPercent, vatRateLabel, yearStartIso,
} from '../format'
import { ErrorBox, Loading, Notice, PageHeader, useLoad } from '../ui'

const rates = Object.keys(vatRateLabel) as VatRate[]

export function InvoicesPage() {
  const { canWrite } = useAuth()
  const [params] = useSearchParams()
  const [type, setType] = useState<InvoiceType | ''>('')
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [from, setFrom] = useState(yearStartIso())
  const [page, setPage] = useState(1)
  const contractorId = params.get('contractorId') ?? undefined
  const { data, error, loading } = useLoad(
    () => apiX.invoices({ type: type || undefined, status, search, from, contractorId, page, pageSize: 20 }),
    [type, status, search, from, contractorId, page])
  const pages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  return (
    <>
      <PageHeader title="Faktury">
        {canWrite && <>
          <Link className="btn btn-primary" to="/invoices/new?type=Sales">Nowa faktura sprzedaży</Link>
          <Link className="btn" to="/invoices/new?type=Purchase">Rejestruj fakturę zakupu</Link>
        </>}
      </PageHeader>
      <ErrorBox message={error} />
      <div className="card form-row">
        <label>Rodzaj
          <select value={type} onChange={e => { setType(e.target.value as InvoiceType | ''); setPage(1) }}>
            <option value="">Wszystkie</option><option value="Sales">Sprzedaż</option><option value="Purchase">Zakup</option>
          </select>
        </label>
        <label>Status
          <select value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}>
            <option value="">Wszystkie</option><option value="unpaid">Do zapłaty</option><option value="overdue">Po terminie</option>
            <option value="paid">Opłacone</option><option value="canceled">Anulowane</option>
          </select>
        </label>
        <label>Od<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1) }} /></label>
        <label className="grow">Szukaj<input placeholder="numer lub kontrahent" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
      </div>
      {loading && !data && <Loading />}
      {data && (data.items.length === 0 ? <p className="muted">Brak faktur dla wybranych kryteriów.</p> : (
        <>
          <table>
            <thead><tr><th>Numer</th><th>Rodzaj</th><th>Kontrahent</th><th>Wystawiona</th><th>Termin</th><th className="num">Brutto</th><th className="num">Do zapłaty</th><th>Status</th></tr></thead>
            <tbody>
              {data.items.map(i => (
                <tr key={i.id}>
                  <td><Link to={`/invoices/${i.id}`}>{i.number}</Link>{i.correctedNumber && <div className="muted small">korekta do {i.correctedNumber}</div>}</td>
                  <td>{invoiceTypeLabel[i.type]}</td><td>{i.contractorName}</td>
                  <td>{fmtDate(i.issueDate)}</td><td>{fmtDate(i.dueDate)}</td>
                  <td className="num">{fmtMoney(i.gross)}</td>
                  <td className="num">{i.status === 'canceled' || i.isCorrection ? '' : fmtMoney(i.remaining)}</td>
                  <td><span className={`badge status-${i.status}`}>{statusLabel[i.status]}</span></td>
                </tr>
              ))}
            </tbody>
            <tfoot><tr><td colSpan={5}>Razem (bez anulowanych)</td>
              <td className="num">{fmtMoney(data.totalGross)}</td><td className="num" data-testid="invoices-remaining">{fmtMoney(data.totalRemaining)}</td><td></td></tr></tfoot>
          </table>
          <div className="pager">
            <button className="btn btn-small" disabled={page <= 1} onClick={() => setPage(page - 1)}>‹ Poprzednia</button>
            <span>Strona {page} z {pages} ({data.totalCount} faktur)</span>
            <button className="btn btn-small" disabled={page >= pages} onClick={() => setPage(page + 1)}>Następna ›</button>
          </div>
        </>
      ))}
    </>
  )
}

interface Line { description: string; net: string; vatRate: VatRate }
const emptyLine = (): Line => ({ description: '', net: '', vatRate: 'R23' })

export function NewInvoicePage() {
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const [params] = useSearchParams()
  const type: InvoiceType = params.get('type') === 'Purchase' ? 'Purchase' : 'Sales'
  const sales = type === 'Sales'
  const contractors = useLoad(() => apiX.contractors(), [])
  const accounts = useLoad(() => api.accounts(), [])
  const [contractorId, setContractorId] = useState('')
  const [number, setNumber] = useState('')
  const [issueDate, setIssueDate] = useState(todayIso())
  const [dueDate, setDueDate] = useState(addDaysIso(todayIso(), 14))
  const [resultAccountId, setResultAccountId] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<Line[]>([emptyLine()])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (!canWrite) return <><PageHeader title="Nowa faktura" /><ErrorBox message="Twoja rola (Podgląd) nie pozwala na wystawianie faktur." /></>

  const parsed = lines.map(l => ({ net: parseAmount(l.net), rate: l.vatRate }))
  const invalid = parsed.some(p => p.net === null || p.net <= 0)
  const net = round2(parsed.reduce((s, p) => s + (p.net ?? 0), 0))
  const vat = round2(parsed.reduce((s, p) => s + round2(((p.net ?? 0) * vatPercent[p.rate]) / 100), 0))
  const resultAccounts = accounts.data?.filter(a => (sales ? a.type === 'Income' : a.type === 'Expense')) ?? []
  const update = (i: number, patch: Partial<Line>) => setLines(lines.map((l, j) => (j === i ? { ...l, ...patch } : l)))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (invalid) { setError('Podaj poprawne kwoty netto we wszystkich pozycjach (np. 1234,50)'); return }
    setBusy(true)
    try {
      const r = await apiX.createInvoice({
        type, number: sales ? undefined : number, contractorId, issueDate, dueDate, notes,
        resultAccountId: resultAccountId || undefined,
        lines: lines.map((l, i) => ({ description: l.description, net: parsed[i].net!, vatRate: l.vatRate })),
      })
      nav(`/invoices/${r.invoiceId}`)
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <>
      <PageHeader title={sales ? 'Nowa faktura sprzedaży' : 'Rejestracja faktury zakupu'}>
        <Link className="btn" to="/invoices">← Faktury</Link>
      </PageHeader>
      <ErrorBox message={error || contractors.error || accounts.error} />
      <form className="card" onSubmit={submit}>
        <div className="form-row">
          <label className="grow">{sales ? 'Nabywca' : 'Dostawca'}
            <select value={contractorId} onChange={e => setContractorId(e.target.value)} required>
              <option value="">– wybierz kontrahenta –</option>
              {contractors.data?.map(c => <option key={c.id} value={c.id}>{c.name}{c.nip ? ` (NIP ${c.nip})` : ''}</option>)}
            </select>
          </label>
          {!sales && <label>Numer faktury dostawcy<input value={number} onChange={e => setNumber(e.target.value)} required maxLength={50} /></label>}
          <label>Data wystawienia<input type="date" value={issueDate} onChange={e => { setIssueDate(e.target.value); setDueDate(addDaysIso(e.target.value, 14)) }} required /></label>
          <label>Termin płatności<input type="date" value={dueDate} onChange={e => setDueDate(e.target.value)} required /></label>
          <label>{sales ? 'Konto przychodu' : 'Konto kosztu'}
            <select value={resultAccountId} onChange={e => setResultAccountId(e.target.value)}>
              <option value="">domyślne ({sales ? '700' : '402'})</option>
              {resultAccounts.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
            </select>
          </label>
        </div>
        <table className="lines">
          <thead><tr><th>Opis pozycji</th><th className="num">Netto</th><th>VAT</th><th className="num">Kwota VAT</th><th className="num">Brutto</th><th></th></tr></thead>
          <tbody>
            {lines.map((l, i) => {
              const n = parsed[i].net ?? 0
              const v = round2((n * vatPercent[l.vatRate]) / 100)
              return (
                <tr key={i}>
                  <td><input aria-label={`Opis pozycji ${i + 1}`} value={l.description} onChange={e => update(i, { description: e.target.value })} required maxLength={300} /></td>
                  <td><input className="num" inputMode="decimal" aria-label={`Netto pozycji ${i + 1}`} value={l.net} onChange={e => update(i, { net: e.target.value })} placeholder="0,00" /></td>
                  <td>
                    <select aria-label={`Stawka VAT pozycji ${i + 1}`} value={l.vatRate} onChange={e => update(i, { vatRate: e.target.value as VatRate })}>
                      {rates.map(r => <option key={r} value={r}>{vatRateLabel[r]}</option>)}
                    </select>
                  </td>
                  <td className="num">{fmtMoney(v)}</td>
                  <td className="num">{fmtMoney(n + v)}</td>
                  <td>{lines.length > 1 && <button type="button" className="btn btn-small btn-link" onClick={() => setLines(lines.filter((_, j) => j !== i))}>Usuń</button>}</td>
                </tr>
              )
            })}
          </tbody>
          <tfoot>
            <tr>
              <td><button type="button" className="btn btn-small" onClick={() => setLines([...lines, emptyLine()])}>+ Dodaj pozycję</button></td>
              <td className="num" data-testid="inv-net">{fmtMoney(net)}</td><td></td>
              <td className="num" data-testid="inv-vat">{fmtMoney(vat)}</td>
              <td className="num" data-testid="inv-gross">{fmtMoney(round2(net + vat))}</td><td></td>
            </tr>
          </tfoot>
        </table>
        <div className="form-row">
          <label className="grow">Uwagi<input value={notes} onChange={e => setNotes(e.target.value)} maxLength={500} /></label>
        </div>
        <p className="muted small">Faktura zostanie zaksięgowana automatycznie ({sales ? 'Wn 201 / Ma przychód + 220 VAT' : 'Wn koszt + 220 VAT / Ma 202'}).</p>
        <button className="btn btn-primary" disabled={busy || invalid || !contractorId}>{busy ? 'Zapisywanie…' : sales ? 'Wystaw i zaksięguj' : 'Zarejestruj i zaksięguj'}</button>
      </form>
    </>
  )
}

export function InvoicePage() {
  const { id } = useParams()
  const { canWrite } = useAuth()
  const { data, error, loading, reload } = useLoad(() => apiX.invoice(id!), [id])
  const accounts = useLoad(() => api.accounts(), [])
  const [actionError, setActionError] = useState('')
  const [notice, setNotice] = useState('')
  const [payDate, setPayDate] = useState(todayIso())
  const [payAmount, setPayAmount] = useState('')
  const [payAccount, setPayAccount] = useState('')

  const cash = accounts.data?.filter(a => a.type === 'Asset') ?? []
  const attachments = useLoad(() => apiZ.attachments(id!), [id])

  async function upload(file: File | undefined) {
    if (!file) return
    setActionError(''); setNotice('')
    try { await apiZ.uploadAttachment(id!, file); setNotice(`Dodano załącznik ${file.name}`); attachments.reload() }
    catch (err) { setActionError((err as Error).message) }
  }

  async function removeAttachment(a: AttachmentDto) {
    if (!confirm(`Usunąć załącznik „${a.fileName}”?`)) return
    setActionError('')
    try { await apiZ.deleteAttachment(a.id); attachments.reload() } catch (err) { setActionError((err as Error).message) }
  }

  async function pay(e: FormEvent) {
    e.preventDefault()
    setActionError(''); setNotice('')
    const amount = parseAmount(payAmount)
    if (amount === null || amount <= 0) { setActionError('Podaj poprawną kwotę płatności'); return }
    try {
      await apiX.addPayment(id!, { date: payDate, amount, accountId: payAccount })
      setNotice(`Zarejestrowano płatność ${fmtMoney(amount)}`)
      setPayAmount('')
      reload()
    } catch (err) { setActionError((err as Error).message) }
  }

  async function cancel() {
    if (!confirm('Anulować fakturę? Zostanie zaksięgowane storno.')) return
    setActionError(''); setNotice('')
    try { await apiX.cancelInvoice(id!); setNotice('Faktura została anulowana'); reload() }
    catch (err) { setActionError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title={data ? `Faktura ${data.number}` : 'Faktura'}>
        <Link className="btn" to="/invoices">← Faktury</Link>
        <Link className="btn" to={`/invoices/${id}/print`}>Drukuj / PDF</Link>
        {canWrite && data && !data.isCorrection && data.status !== 'canceled' && <Link className="btn" to={`/invoices/${id}/correction`}>Wystaw korektę</Link>}
        {canWrite && data && data.status !== 'canceled' && data.paidAmount === 0 && <button className="btn btn-danger" onClick={cancel}>Anuluj fakturę</button>}
      </PageHeader>
      <ErrorBox message={error || actionError} />
      <Notice message={notice} />
      {loading && !data && <Loading />}
      {data && (
        <>
          <div className="card">
            <dl className="meta">
              <dt>Rodzaj</dt><dd>{invoiceTypeLabel[data.type]}</dd>
              <dt>Kontrahent</dt><dd>{data.contractorName}{data.contractorNip ? ` (NIP ${data.contractorNip})` : ''}</dd>
              <dt>Data wystawienia</dt><dd>{fmtDate(data.issueDate)}</dd>
              <dt>Termin płatności</dt><dd>{fmtDate(data.dueDate)}</dd>
              <dt>Status</dt><dd data-testid="invoice-status"><span className={`badge status-${data.status}`}>{statusLabel[data.status]}</span></dd>
              {data.transactionId && <><dt>Księgowanie</dt><dd><Link to={`/transactions/${data.transactionId}`}>{data.transactionNumber}</Link></dd></>}
              {data.notes && <><dt>Uwagi</dt><dd>{data.notes}</dd></>}
            </dl>
            <table>
              <thead><tr><th>Pozycja</th><th className="num">Netto</th><th>VAT</th><th className="num">Kwota VAT</th><th className="num">Brutto</th></tr></thead>
              <tbody>
                {data.lines.map((l, i) => (
                  <tr key={i}><td>{l.description}</td><td className="num">{fmtMoney(l.net)}</td><td>{vatRateLabel[l.vatRate]}</td>
                    <td className="num">{fmtMoney(l.vat)}</td><td className="num">{fmtMoney(l.gross)}</td></tr>
                ))}
              </tbody>
              <tfoot><tr><td>Razem</td><td className="num">{fmtMoney(data.net)}</td><td></td>
                <td className="num">{fmtMoney(data.vat)}</td><td className="num" data-testid="invoice-gross">{fmtMoney(data.gross)}</td></tr></tfoot>
            </table>
            {data.isCorrection && data.correctedInvoiceId && (
              <p data-testid="correction-info">Korekta do faktury <Link to={`/invoices/${data.correctedInvoiceId}`}>{data.correctedNumber}</Link>. Pozycje pokazują różnice względem faktury korygowanej.</p>
            )}
            {data.corrections.length > 0 && (
              <div data-testid="corrections">
                <p>Wartość brutto po korektach: <strong data-testid="invoice-after-corrections">{fmtMoney(data.grossAfterCorrections)}</strong> (korekty: {fmtMoney(data.correctedAmount)})</p>
                <ul>{data.corrections.map(c => (
                  <li key={c.id}><Link to={`/invoices/${c.id}`}>{c.number}</Link> z {fmtDate(c.issueDate)}: {fmtMoney(c.gross)}{c.isCanceled ? ' (anulowana)' : ''}</li>
                ))}</ul>
              </div>
            )}
            <p>Zapłacono: <strong data-testid="invoice-paid">{fmtMoney(data.paidAmount)}</strong> · Do zapłaty: <strong data-testid="invoice-remaining">{fmtMoney(data.remaining)}</strong></p>
          </div>

          <h2>Płatności</h2>
          {data.payments.length === 0 ? <p className="muted">Brak zarejestrowanych płatności.</p> : (
            <table>
              <thead><tr><th>Data</th><th>Konto</th><th className="num">Kwota</th><th>Księgowanie</th></tr></thead>
              <tbody>
                {data.payments.map(p => (
                  <tr key={p.id}><td>{fmtDate(p.date)}</td><td>{p.accountCode} {p.accountName}</td><td className="num">{fmtMoney(p.amount)}</td>
                    <td><Link to={`/transactions/${p.transactionId}`}>zobacz</Link></td></tr>
                ))}
              </tbody>
            </table>
          )}
          <h2>Załączniki</h2>
          {attachments.data && attachments.data.length === 0 && <p className="muted">Brak załączników (np. skan faktury dostawcy).</p>}
          <ul className="attachments" aria-label="Załączniki">
            {attachments.data?.map(a => (
              <li key={a.id}>
                <button className="btn btn-link" onClick={() => apiZ.downloadAttachment(a).catch(e => setActionError(e.message))}>{a.fileName}</button>
                <span className="muted small">{(a.size / 1024).toFixed(0)} KB</span>
                {canWrite && <button className="btn btn-small btn-link" onClick={() => removeAttachment(a)}>Usuń</button>}
              </li>
            ))}
          </ul>
          {canWrite && <label className="no-print">Dodaj załącznik (PDF, PNG, JPG do 10 MB)
            <input type="file" accept=".pdf,.png,.jpg,.jpeg" aria-label="Plik załącznika" onChange={e => { upload(e.target.files?.[0]); e.target.value = '' }} /></label>}

          {canWrite && data.status !== 'canceled' && data.remaining > 0 && (
            <form className="card form-row" onSubmit={pay} aria-label="Nowa płatność">
              <label>Data<input type="date" value={payDate} onChange={e => setPayDate(e.target.value)} required /></label>
              <label>Kwota<input inputMode="decimal" value={payAmount} onChange={e => setPayAmount(e.target.value)} placeholder={fmtMoney(data.remaining)} required /></label>
              <label className="grow">{data.type === 'Sales' ? 'Wpłynęło na konto' : 'Zapłacono z konta'}
                <select value={payAccount} onChange={e => setPayAccount(e.target.value)} required>
                  <option value="">– wybierz kasę/bank –</option>
                  {cash.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
                </select>
              </label>
              <button type="button" className="btn btn-small" onClick={() => setPayAmount(String(data.remaining).replace('.', ','))}>Cała kwota</button>
              <button className="btn btn-primary">Zarejestruj płatność</button>
            </form>
          )}
        </>
      )}
    </>
  )
}
