import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type NoticeDraft, type NoticeKind, type NoticeListItem, type NoticeRequest, type OverdueItem } from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, todayIso } from '../format'
import { Badge, Button, DataTable, EmptyState, ErrorBox, Loading, Notice, PageHeader, PrintHeader, type Column, useLoad } from '../design'

const kindLabel: Record<NoticeKind, string> = { Reminder: 'Wezwanie do zapłaty', InterestNote: 'Nota odsetkowa' }
const statusText = { Issued: 'wystawiono', Sent: 'wysłano', Failed: 'błąd wysyłki' } as const

/** Przegląd zadłużenia: kontrahenci z fakturami po terminie (na wybrany dzień) wraz z odsetkami. */
export function CollectionsPage() {
  const [asOf, setAsOf] = useState(todayIso())
  const { data, error, loading } = useLoad(() => api.overdue(asOf), [asOf])

  const byContractor = useMemo(() => {
    const m = new Map<string, { id: string; name: string; email?: string | null; count: number; remaining: number; interest: number | null; oldest: string; maxDays: number }>()
    for (const i of data?.items ?? []) {
      const g = m.get(i.contractorId) ?? { id: i.contractorId, name: i.contractorName, email: i.contractorEmail, count: 0, remaining: 0, interest: 0 as number | null, oldest: i.dueDate, maxDays: 0 }
      g.count++; g.remaining += i.remaining; g.maxDays = Math.max(g.maxDays, i.daysOverdue)
      g.interest = g.interest === null || i.interest == null ? null : g.interest + i.interest
      if (i.dueDate < g.oldest) g.oldest = i.dueDate
      m.set(i.contractorId, g)
    }
    return [...m.values()].sort((a, b) => b.remaining - a.remaining)
  }, [data])
  type G = (typeof byContractor)[number]

  const columns: Column<G>[] = [
    { key: 'name', header: 'Kontrahent', sortable: true, sortValue: g => g.name, filterText: g => g.name, render: g => <Link to={`/collections/${g.id}?asOf=${asOf}`}>{g.name}</Link>, csv: g => g.name },
    { key: 'count', header: 'Faktur po terminie', align: 'right', sortable: true, sortValue: g => g.count, render: g => g.count, csv: g => g.count },
    { key: 'oldest', header: 'Najstarszy termin', sortable: true, sortValue: g => g.oldest, render: g => fmtDate(g.oldest), csv: g => g.oldest.slice(0, 10) },
    { key: 'days', header: 'Największa zwłoka (dni)', align: 'right', sortable: true, sortValue: g => g.maxDays, render: g => g.maxDays, csv: g => g.maxDays },
    { key: 'remaining', header: 'Zaległość', align: 'right', sortable: true, sortValue: g => g.remaining, render: g => fmtMoney(g.remaining), csv: g => g.remaining },
    { key: 'interest', header: 'Odsetki', align: 'right', sortable: true, sortValue: g => g.interest ?? -1, render: g => (g.interest === null ? '—' : fmtMoney(g.interest)), csv: g => g.interest ?? '' },
    { key: 'email', header: 'E-mail', render: g => g.email ?? '—', csv: g => g.email ?? '' },
  ]

  return (
    <>
      <PageHeader title="Windykacja" />
      <ErrorBox message={error} />
      <div className="card form-row">
        <label>Na dzień<input type="date" value={asOf} onChange={e => setAsOf(e.target.value)} required /></label>
        <p className="muted grow flush">Faktury sprzedaży po terminie płatności; opłacone, anulowane i korekty są pomijane. Wybierz kontrahenta, aby przygotować wezwanie do zapłaty lub notę odsetkową.</p>
      </div>
      {data?.interestError && <div className="alert alert-error" role="alert">{data.interestError}</div>}
      {loading && !data && <Loading />}
      {data && <DataTable label="Zadłużenie kontrahentów" columns={columns} rows={byContractor} rowKey={g => g.id} loading={loading}
        empty="Brak faktur po terminie na wybrany dzień." viewKey="collections" csvName="windykacja-widok"
        footer={byContractor.length ? { label: 'Razem', cells: { remaining: fmtMoney(byContractor.reduce((s, g) => s + g.remaining, 0)) } } : undefined} />}
    </>
  )
}

/** Wezwanie / nota dla jednego kontrahenta: wybór faktur, podgląd, wydruk albo wysyłka e-mail, historia. */
export function ContractorCollectionsPage() {
  const { contractorId } = useParams()
  const nav = useNavigate()
  const { canWrite } = useAuth()
  const [asOf, setAsOf] = useState(() => new URLSearchParams(window.location.search).get('asOf') ?? todayIso())
  const [days, setDays] = useState('7')
  const [kind, setKind] = useState<NoticeKind>('Reminder')
  const [withInterest, setWithInterest] = useState(true)
  const [excluded, setExcluded] = useState<Set<string>>(new Set())
  const [draft, setDraft] = useState<NoticeDraft | null>(null)
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [to, setTo] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  const overdue = useLoad(() => api.overdue(asOf, contractorId), [asOf, contractorId])
  const history = useLoad(() => api.notices({ contractorId }), [contractorId])
  const items = overdue.data?.items ?? []
  const name = items[0]?.contractorName ?? history.data?.[0]?.contractorName ?? 'Kontrahent'
  const selected = items.filter(i => !excluded.has(i.invoiceId))

  const request = (): NoticeRequest => ({
    contractorId: contractorId!, invoiceIds: selected.map(i => i.invoiceId), kind, asOf, paymentDays: Number(days) || 0, includeInterest: kind === 'InterestNote' || withInterest,
  })

  async function preview() {
    setError(''); setNotice(''); setBusy(true)
    try {
      const d = await api.previewNotice(request())
      setDraft(d); setSubject(d.subject); setBody(d.body); setTo(d.contractorEmail ?? '')
    } catch (e) { setError((e as Error).message); setDraft(null) } finally { setBusy(false) }
  }

  async function issue(channel: 'Print' | 'Email') {
    setError(''); setNotice(''); setBusy(true)
    try {
      const r = await api.issueNotice({ request: request(), channel, to: to || undefined, subject, body })
      history.reload()
      if (channel === 'Print') { nav(`/collections/notices/${r.id}`); return }
      if (r.status === 'Failed') setError(`Nie udało się wysłać wiadomości: ${r.error ?? 'nieznany błąd'}`)
      else setNotice(`Wysłano ${kindLabel[kind].toLowerCase()} na adres ${to || draft?.contractorEmail}`)
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }

  const toggle = (id: string) => { setDraft(null); setExcluded(x => { const n = new Set(x); if (n.has(id)) n.delete(id); else n.add(id); return n }) }
  const invoiceCols: Column<OverdueItem>[] = [
    { key: 'include', header: 'Uwzględnij', fixed: true, render: i => <input type="checkbox" aria-label={`Uwzględnij fakturę ${i.number}`} checked={!excluded.has(i.invoiceId)} onChange={() => toggle(i.invoiceId)} /> },
    { key: 'number', header: 'Faktura', render: i => <Link to={`/invoices/${i.invoiceId}`}>{i.number}</Link>, csv: i => i.number },
    { key: 'due', header: 'Termin', render: i => fmtDate(i.dueDate), csv: i => i.dueDate.slice(0, 10) },
    { key: 'days', header: 'Dni zwłoki', align: 'right', render: i => i.daysOverdue, csv: i => i.daysOverdue },
    { key: 'remaining', header: 'Do zapłaty', align: 'right', render: i => fmtMoney(i.remaining), csv: i => i.remaining },
    { key: 'interest', header: 'Odsetki', align: 'right', render: i => (i.interest == null ? '—' : fmtMoney(i.interest)), csv: i => i.interest ?? '' },
  ]
  const histCols: Column<NoticeListItem>[] = [
    { key: 'date', header: 'Data', render: n => fmtDate(n.createdAt), csv: n => n.createdAt.slice(0, 10) },
    { key: 'kind', header: 'Dokument', render: n => kindLabel[n.kind], csv: n => kindLabel[n.kind] },
    { key: 'channel', header: 'Sposób', render: n => (n.channel === 'Email' ? `e-mail (${n.sentTo ?? ''})` : 'wydruk'), csv: n => n.channel },
    { key: 'status', header: 'Status', render: n => <Badge tone={n.status === 'Failed' ? 'danger' : n.status === 'Sent' ? 'ok' : 'neutral'}>{statusText[n.status]}</Badge>, csv: n => statusText[n.status] },
    { key: 'sum', header: 'Kwota', align: 'right', render: n => fmtMoney(n.kind === 'InterestNote' ? n.interest : n.principal + n.interest), csv: n => n.principal + n.interest },
    { key: 'by', header: 'Wystawił', render: n => n.createdBy, csv: n => n.createdBy },
    { key: 'actions', header: 'Akcje', fixed: true, render: n => <Link to={`/collections/notices/${n.id}`}>pokaż</Link> },
  ]

  return (
    <>
      <PageHeader title={`Windykacja: ${name}`}>
        <Link className="btn" to="/collections">← Windykacja</Link>
      </PageHeader>
      <ErrorBox message={error || overdue.error} />
      <Notice message={notice} />
      <div className="card form-row">
        <label>Na dzień<input type="date" value={asOf} onChange={e => { setAsOf(e.target.value); setDraft(null) }} required /></label>
        <label>Termin zapłaty (dni)<input type="number" min={1} max={90} value={days} onChange={e => { setDays(e.target.value); setDraft(null) }} className="w-6" /></label>
        <label>Rodzaj dokumentu
          <select value={kind} onChange={e => { setKind(e.target.value as NoticeKind); setDraft(null) }}>
            <option value="Reminder">Wezwanie do zapłaty</option>
            <option value="InterestNote">Nota odsetkowa</option>
          </select>
        </label>
        {kind === 'Reminder' && <label className="inline"><input type="checkbox" checked={withInterest} onChange={e => { setWithInterest(e.target.checked); setDraft(null) }} /> Doliczyć odsetki</label>}
      </div>
      {overdue.data?.interestError && (kind === 'InterestNote' || withInterest) && <div className="alert alert-error" role="alert">{overdue.data.interestError}</div>}
      {overdue.loading && !overdue.data && <Loading />}
      {overdue.data && (items.length === 0
        ? <EmptyState>Ten kontrahent nie ma faktur po terminie na wybrany dzień.</EmptyState>
        : <>
          <DataTable label="Faktury po terminie" columns={invoiceCols} rows={items} rowKey={i => i.invoiceId} loading={overdue.loading} />
          {canWrite && <Button variant="primary" loading={busy} disabled={selected.length === 0} onClick={preview}>Przygotuj dokument</Button>}
        </>)}

      {draft && (
        <div className="card" data-testid="notice-draft">
          <h2 className="flush">{kindLabel[draft.kind]}</h2>
          <table aria-label="Pozycje dokumentu">
            <thead><tr><th>Faktura</th><th>Termin</th><th className="num">Do zapłaty</th><th className="num">Dni zwłoki</th><th className="num">Odsetki</th></tr></thead>
            <tbody>
              {draft.lines.map(l => <tr key={l.invoiceId}><td>{l.number}</td><td>{fmtDate(l.dueDate)}</td><td className="num">{fmtMoney(l.principal)}</td>
                <td className="num">{l.days}</td><td className="num">{fmtMoney(l.interest)}</td></tr>)}
            </tbody>
            <tfoot><tr><td colSpan={2}>Razem do zapłaty do {fmtDate(draft.deadline)}</td><td className="num">{fmtMoney(draft.principal)}</td><td></td>
              <td className="num">{fmtMoney(draft.interest)}</td></tr>
              <tr><td colSpan={4}><strong>Kwota do zapłaty</strong></td><td className="num" data-testid="notice-total"><strong>{fmtMoney(draft.total)}</strong></td></tr></tfoot>
          </table>
          <div className="form-row">
            <label className="grow">Temat wiadomości<input value={subject} onChange={e => setSubject(e.target.value)} maxLength={300} /></label>
            <label className="grow">Adres e-mail odbiorcy<input type="email" value={to} onChange={e => setTo(e.target.value)} placeholder="brak adresu w kartotece" /></label>
          </div>
          <label className="mt-3">Treść<textarea rows={9} value={body} onChange={e => setBody(e.target.value)} maxLength={4000} /></label>
          <div className="stack mt-3">
            <Button variant="primary" loading={busy} onClick={() => issue('Print')}>Zapisz i drukuj</Button>
            <Button loading={busy} onClick={() => issue('Email')}>Wyślij e-mail</Button>
          </div>
        </div>
      )}

      <h2>Historia wezwań i not</h2>
      {history.data && <DataTable label="Historia windykacji" columns={histCols} rows={history.data} rowKey={n => n.id} empty="Dla tego kontrahenta nie wystawiono jeszcze wezwań ani not." />}
    </>
  )
}

/** Zapisany dokument w układzie do wydruku (A4) – treść z chwili wystawienia. */
export function NoticePage() {
  const { id } = useParams()
  const nav = useNavigate()
  const { data, error, loading } = useLoad(() => api.notice(id!), [id])
  return (
    <>
      <PageHeader title={data ? kindLabel[data.kind] : 'Dokument windykacyjny'}>
        <Button onClick={() => nav(-1)}>← Wróć</Button>
        <Button variant="primary" onClick={() => window.print()}>Drukuj / PDF</Button>
      </PageHeader>
      <ErrorBox message={error} />
      {loading && !data && <Loading />}
      {data && (
        <div className="invoice-sheet" data-testid="notice-sheet">
          <PrintHeader title={kindLabel[data.kind]} />
          <div className="invoice-parties">
            <div><h3>Wierzyciel</h3><p>{data.issuer.name}{data.issuer.nip ? `\nNIP ${data.issuer.nip}` : ''}{data.issuer.address ? `\n${data.issuer.address}` : ''}</p></div>
            <div><h3>Dłużnik</h3><p>{data.recipient.name}{data.recipient.nip ? `\nNIP ${data.recipient.nip}` : ''}{data.recipient.address ? `\n${data.recipient.address}` : ''}</p></div>
          </div>
          <p>Data wystawienia: <strong>{fmtDate(data.createdAt)}</strong> · stan na dzień <strong>{fmtDate(data.calculationDate)}</strong></p>
          <h2>{data.subject}</h2>
          <p className="pre-wrap">{data.body}</p>
          <table aria-label="Pozycje dokumentu">
            <thead><tr><th>Faktura</th><th>Termin</th><th className="num">Do zapłaty</th><th className="num">Dni zwłoki</th><th className="num">Stopa</th><th className="num">Odsetki</th></tr></thead>
            <tbody>
              {data.lines.map(l => <tr key={l.invoiceId}><td>{l.number}</td><td>{fmtDate(l.dueDate)}</td><td className="num">{fmtMoney(l.principal)}</td>
                <td className="num">{l.days}</td><td className="num">{l.rateInfo}</td><td className="num">{fmtMoney(l.interest)}</td></tr>)}
            </tbody>
            <tfoot><tr><td colSpan={2}>Razem</td><td className="num">{fmtMoney(data.principal)}</td><td></td><td></td><td className="num">{fmtMoney(data.interest)}</td></tr></tfoot>
          </table>
          <p className="invoice-total">Do zapłaty do <strong>{fmtDate(data.paymentDeadline)}</strong>: <strong data-testid="notice-sheet-total">{fmtMoney(data.total)} zł</strong></p>
          {data.issuer.bankAccount && <p>Rachunek do wpłaty: <strong>{data.issuer.bankAccount}</strong></p>}
        </div>
      )}
    </>
  )
}
