import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api, type KpirKind, type InvoiceItem, type AttachmentDto, type InvoiceType, type VatRate } from '../api'
import { useAuth } from '../auth'
import {
  addDaysIso, fmtDate, fmtMoney, invoiceTypeLabel, kpirKindLabel, parseAmount, purchaseKinds, ryczaltRates, salesKinds, round2, statusLabel, todayIso, vatPercent, vatRateLabel, yearStartIso,
} from '../format'
import { Button, DataTable, ErrorBox, Loading, Notice, PageHeader, SavedViews, StatusBadge, type Column, type SortState, useDialogs, useLoad } from '../design'

const rates = Object.keys(vatRateLabel) as VatRate[]

export function InvoicesPage() {
  const { canWrite, user } = useAuth()
  const [params] = useSearchParams()
  const [type, setType] = useState<InvoiceType | ''>('')
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [from, setFrom] = useState(yearStartIso())
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortState | null>(null)
  const contractorId = params.get('contractorId') ?? undefined
  const { data, error, loading } = useLoad(
    () => api.invoices({ type: type || undefined, status, search, from, contractorId, page, pageSize: 20, sort: sort?.key, dir: sort?.dir }),
    [type, status, search, from, contractorId, page, sort])

  const columns: Column<InvoiceItem>[] = [
    { key: 'number', header: 'Numer', sortable: true, csv: i => i.number,
      render: i => <><Link to={`/invoices/${i.id}`}>{i.number}</Link>{i.correctedNumber && <div className="muted small">korekta do {i.correctedNumber}</div>}</> },
    { key: 'type', header: 'Rodzaj', sortable: true, render: i => invoiceTypeLabel[i.type], csv: i => invoiceTypeLabel[i.type] },
    { key: 'contractor', header: 'Kontrahent', sortable: true, render: i => i.contractorName, csv: i => i.contractorName },
    { key: 'issue', header: 'Wystawiona', sortable: true, render: i => fmtDate(i.issueDate), csv: i => i.issueDate.slice(0, 10) },
    { key: 'due', header: 'Termin', sortable: true, render: i => fmtDate(i.dueDate), csv: i => i.dueDate.slice(0, 10) },
    { key: 'gross', header: 'Brutto', sortable: true, align: 'right', render: i => fmtMoney(i.gross), csv: i => i.gross },
    { key: 'remaining', header: 'Do zapłaty', sortable: true, align: 'right', csv: i => (i.status === 'canceled' || i.isCorrection ? '' : i.remaining),
      render: i => (i.status === 'canceled' || i.isCorrection ? '' : fmtMoney(i.remaining)) },
    { key: 'status', header: 'Status', sortable: true, render: i => <StatusBadge status={i.status}>{statusLabel[i.status]}</StatusBadge>, csv: i => statusLabel[i.status] },
  ]

  return (
    <>
      <PageHeader title="Faktury">
        {canWrite && <>
          <Link className="btn btn-primary" to="/invoices/new?type=Sales">Nowa faktura sprzedaży</Link>
          <Link className="btn" to="/invoices/new?type=Purchase">Rejestruj fakturę zakupu</Link>
        </>}
      </PageHeader>
      <ErrorBox message={error} />
      <SavedViews storageKey={`invoices.${user?.id}`} state={{ type, status, search, from }}
        onApply={v => { setType(v.type); setStatus(v.status); setSearch(v.search); setFrom(v.from); setPage(1) }} />
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
      {data && (
        <DataTable label="Lista faktur" columns={columns} rows={data.items} rowKey={i => i.id} loading={loading}
          empty="Brak faktur dla wybranych kryteriów." sort={sort} onSort={s => { setSort(s); setPage(1) }} viewKey="invoices" csvName="faktury-widok"
          selectable={{ label: i => `Zaznacz fakturę ${i.number}` }}
          footer={{ label: 'Razem (bez anulowanych)', cells: { gross: fmtMoney(data.totalGross), remaining: <span data-testid="invoices-remaining">{fmtMoney(data.totalRemaining)}</span> } }}
          pager={{ page, pageSize: data.pageSize, total: data.totalCount, onPage: setPage, unit: 'faktur' }} />
      )}
    </>
  )
}

interface Line { description: string; net: string; vatRate: VatRate }
const emptyLine = (): Line => ({ description: '', net: '', vatRate: 'R23' })

export function NewInvoicePage() {
  const { canWrite, mode, fullBooks } = useAuth()
  const nav = useNavigate()
  const [params] = useSearchParams()
  const type: InvoiceType = params.get('type') === 'Purchase' ? 'Purchase' : 'Sales'
  const sales = type === 'Sales'
  const contractors = useLoad(() => api.contractors(), [])
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
  const [kind, setKind] = useState('')
  const [rate, setRate] = useState('')
  const resultAccounts = accounts.data?.filter(a => (sales ? a.type === 'Income' : a.type === 'Expense')) ?? []
  const update = (i: number, patch: Partial<Line>) => setLines(lines.map((l, j) => (j === i ? { ...l, ...patch } : l)))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (invalid) { setError('Podaj poprawne kwoty netto we wszystkich pozycjach (np. 1234,50)'); return }
    setBusy(true)
    try {
      const r = await api.createInvoice({
        type, number: sales ? undefined : number, contractorId, issueDate, dueDate, notes,
        resultAccountId: fullBooks ? resultAccountId || undefined : undefined,
        kind: mode === 'Kpir' && kind ? (kind as KpirKind) : undefined,
        ryczaltRate: mode === 'Ryczalt' && sales && rate ? Number(rate) : undefined,
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
          {fullBooks && <label>{sales ? 'Konto przychodu' : 'Konto kosztu'}
            <select value={resultAccountId} onChange={e => setResultAccountId(e.target.value)}>
              <option value="">domyślne ({sales ? '700' : '402'})</option>
              {resultAccounts.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
            </select>
          </label>}
          {mode === 'Kpir' && <label className="grow">Rodzaj wpisu w KPiR
            <select value={kind} onChange={e => setKind(e.target.value)}>
              <option value="">domyślny ({kpirKindLabel[sales ? 'Revenue' : 'OtherExpense']})</option>
              {(sales ? salesKinds : purchaseKinds).map(k => <option key={k} value={k}>{kpirKindLabel[k]}</option>)}
            </select>
          </label>}
          {mode === 'Ryczalt' && sales && <label>Stawka ryczałtu
            <select value={rate} onChange={e => setRate(e.target.value)} required>
              <option value="">– wybierz stawkę –</option>
              {ryczaltRates.map(r => <option key={r} value={r}>{String(r).replace('.', ',')}%</option>)}
            </select>
          </label>}
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
                  <td>{lines.length > 1 && <Button variant="link" small type="button"  onClick={() => setLines(lines.filter((_, j) => j !== i))}>Usuń</Button>}</td>
                </tr>
              )
            })}
          </tbody>
          <tfoot>
            <tr>
              <td><Button small type="button"  onClick={() => setLines([...lines, emptyLine()])}>+ Dodaj pozycję</Button></td>
              <td className="num" data-testid="inv-net">{fmtMoney(net)}</td><td></td>
              <td className="num" data-testid="inv-vat">{fmtMoney(vat)}</td>
              <td className="num" data-testid="inv-gross">{fmtMoney(round2(net + vat))}</td><td></td>
            </tr>
          </tfoot>
        </table>
        <div className="form-row">
          <label className="grow">Uwagi<input value={notes} onChange={e => setNotes(e.target.value)} maxLength={500} /></label>
        </div>
        <p className="muted small">{fullBooks
          ? `Faktura zostanie zaksięgowana automatycznie (${sales ? 'Wn 201 / Ma przychód + 220 VAT' : 'Wn koszt + 220 VAT / Ma 202'}).`
          : `Faktura trafi do ewidencji ${mode === 'Kpir' ? 'KPiR' : 'ryczałtu'} (kwoty netto) – bez księgowania na konta.`}</p>
        <Button variant="primary"  disabled={busy || invalid || !contractorId || (mode === 'Ryczalt' && sales && !rate)}>{busy ? 'Zapisywanie…' : !fullBooks ? (sales ? 'Wystaw fakturę' : 'Zarejestruj fakturę') : sales ? 'Wystaw i zaksięguj' : 'Zarejestruj i zaksięguj'}</Button>
      </form>
    </>
  )
}

export function InvoicePage() {
  const { confirm } = useDialogs()
  const { id } = useParams()
  const { canWrite, fullBooks } = useAuth()
  const { data, error, loading, reload } = useLoad(() => api.invoice(id!), [id])
  const accounts = useLoad(() => api.accounts(), [])
  const [actionError, setActionError] = useState('')
  const [notice, setNotice] = useState('')
  const [payDate, setPayDate] = useState(todayIso())
  const [payAmount, setPayAmount] = useState('')
  const [payAccount, setPayAccount] = useState('')

  const cash = accounts.data?.filter(a => a.type === 'Asset') ?? []
  const attachments = useLoad(() => api.attachments(id!), [id])
  const notices = useLoad(() => api.notices({ invoiceId: id }), [id])

  async function upload(file: File | undefined) {
    if (!file) return
    setActionError(''); setNotice('')
    try { await api.uploadAttachment(id!, file); setNotice(`Dodano załącznik ${file.name}`); attachments.reload() }
    catch (err) { setActionError((err as Error).message) }
  }

  async function removeAttachment(a: AttachmentDto) {
    if (!await confirm({ title: 'Usunąć załącznik?', message: `Plik „${a.fileName}” zostanie usunięty.`, confirmLabel: 'Usuń', danger: true })) return
    setActionError('')
    try { await api.deleteAttachment(a.id); attachments.reload() } catch (err) { setActionError((err as Error).message) }
  }

  async function pay(e: FormEvent) {
    e.preventDefault()
    setActionError(''); setNotice('')
    const amount = parseAmount(payAmount)
    if (amount === null || amount <= 0) { setActionError('Podaj poprawną kwotę płatności'); return }
    try {
      await api.addPayment(id!, { date: payDate, amount, accountId: fullBooks ? payAccount : '00000000-0000-0000-0000-000000000000' })
      setNotice(`Zarejestrowano płatność ${fmtMoney(amount)}`)
      setPayAmount('')
      reload()
    } catch (err) { setActionError((err as Error).message) }
  }

  async function cancel() {
    if (!await confirm({ title: 'Anulować fakturę?', message: 'Zostanie zaksięgowane storno. Tej operacji nie można cofnąć.', confirmLabel: 'Anuluj fakturę', danger: true })) return
    setActionError(''); setNotice('')
    try { await api.cancelInvoice(id!); setNotice('Faktura została anulowana'); reload() }
    catch (err) { setActionError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title={data ? `Faktura ${data.number}` : 'Faktura'}>
        <Link className="btn" to="/invoices">← Faktury</Link>
        <Link className="btn" to={`/invoices/${id}/print`}>Drukuj / PDF</Link>
        {canWrite && data && !data.isCorrection && data.status !== 'canceled' && <Link className="btn" to={`/invoices/${id}/correction`}>Wystaw korektę</Link>}
        {canWrite && data && data.status !== 'canceled' && data.paidAmount === 0 && <Button variant="danger"  onClick={cancel}>Anuluj fakturę</Button>}
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
              <dt>Status</dt><dd data-testid="invoice-status"><StatusBadge status={data.status}>{statusLabel[data.status]}</StatusBadge></dd>
              {data.kpirKind && <><dt>Wpis w KPiR</dt><dd data-testid="invoice-kpir-kind">{kpirKindLabel[data.kpirKind]}</dd></>}
              {data.ryczaltRate != null && <><dt>Stawka ryczałtu</dt><dd data-testid="invoice-ryczalt-rate">{String(data.ryczaltRate).replace('.', ',')}%</dd></>}
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
                    <td>{p.transactionId ? <Link to={`/transactions/${p.transactionId}`}>zobacz</Link> : '—'}</td></tr>
                ))}
              </tbody>
            </table>
          )}
          {notices.data && notices.data.length > 0 && <>
            <h2>Historia windykacji</h2>
            <table aria-label="Historia windykacji">
              <thead><tr><th>Data</th><th>Dokument</th><th>Sposób</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {notices.data.map(n => (
                  <tr key={n.id}><td>{fmtDate(n.createdAt)}</td><td>{n.kind === 'Reminder' ? 'Wezwanie do zapłaty' : 'Nota odsetkowa'}</td>
                    <td>{n.channel === 'Email' ? `e-mail (${n.sentTo ?? ''})` : 'wydruk'}</td><td>{n.status === 'Sent' ? 'wysłano' : n.status === 'Failed' ? 'błąd wysyłki' : 'wystawiono'}</td>
                    <td><Link to={`/collections/notices/${n.id}`}>pokaż</Link></td></tr>
                ))}
              </tbody>
            </table>
          </>}
          <h2>Załączniki</h2>
          {attachments.data && attachments.data.length === 0 && <p className="muted">Brak załączników (np. skan faktury dostawcy).</p>}
          <ul className="attachments" aria-label="Załączniki">
            {attachments.data?.map(a => (
              <li key={a.id}>
                <Button variant="link"  onClick={() => api.downloadAttachment(a).catch(e => setActionError(e.message))}>{a.fileName}</Button>
                <span className="muted small">{(a.size / 1024).toFixed(0)} KB</span>
                {canWrite && <Button variant="link" small  onClick={() => removeAttachment(a)}>Usuń</Button>}
              </li>
            ))}
          </ul>
          {canWrite && <label className="no-print">Dodaj załącznik (PDF, PNG, JPG do 10 MB)
            <input type="file" accept=".pdf,.png,.jpg,.jpeg" aria-label="Plik załącznika" onChange={e => { upload(e.target.files?.[0]); e.target.value = '' }} /></label>}

          {canWrite && data.status !== 'canceled' && data.remaining > 0 && (
            <form className="card form-row" onSubmit={pay} aria-label="Nowa płatność">
              <label>Data<input type="date" value={payDate} onChange={e => setPayDate(e.target.value)} required /></label>
              <label>Kwota<input inputMode="decimal" value={payAmount} onChange={e => setPayAmount(e.target.value)} placeholder={fmtMoney(data.remaining)} required /></label>
              {fullBooks && <label className="grow">{data.type === 'Sales' ? 'Wpłynęło na konto' : 'Zapłacono z konta'}
                <select value={payAccount} onChange={e => setPayAccount(e.target.value)} required>
                  <option value="">– wybierz kasę/bank –</option>
                  {cash.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
                </select>
              </label>}
              <Button small type="button"  onClick={() => setPayAmount(String(data.remaining).replace('.', ','))}>Cała kwota</Button>
              <Button variant="primary">Zarejestruj płatność</Button>
            </form>
          )}
        </>
      )}
    </>
  )
}
