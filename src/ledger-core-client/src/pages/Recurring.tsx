import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type KpirKind, type RecurrenceFrequency, type RecurringGenerateResult, type RecurringItem, type VatRate } from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, kpirKindLabel, parseAmount, round2, ryczaltRates, salesKinds, todayIso, vatPercent, vatRateLabel } from '../format'
import { Badge, Button, EmptyState, ErrorBox, Loading, Notice, PageHeader, useDialogs, useLoad } from '../design'

const frequencyLabel: Record<RecurrenceFrequency, string> = { Monthly: 'Co miesiąc', Quarterly: 'Co kwartał', Yearly: 'Co rok' }
const rates = Object.keys(vatRateLabel) as VatRate[]

interface Line { description: string; net: string; vatRate: VatRate }
const emptyLine = (): Line => ({ description: '', net: '', vatRate: 'R23' })
const amountText = (n: number) => String(n).replace('.', ',')

/** Szablony faktur sprzedaży wystawianych cyklicznie (abonamenty) i ręczne generowanie zaległych faktur. */
export function RecurringPage() {
  const { canWrite, mode, fullBooks } = useAuth()
  const { confirm } = useDialogs()
  const list = useLoad(() => api.recurringInvoices(), [])
  const contractors = useLoad(() => api.contractors(), [])
  const accounts = useLoad(() => api.accounts(), [])
  const [editing, setEditing] = useState<RecurringItem | 'new' | null>(null)
  const [asOf, setAsOf] = useState(todayIso())
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [result, setResult] = useState<RecurringGenerateResult | null>(null)

  async function generate(templateId?: string) {
    setError(''); setNotice(''); setResult(null); setBusy(true)
    try {
      const r = await api.generateRecurring(asOf, templateId)
      setResult(r)
      setNotice(r.generated.length === 0 ? 'Brak zaległych faktur do wygenerowania' : `Wygenerowano faktur: ${r.generated.length}`)
      list.reload()
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  async function toggle(t: RecurringItem) {
    setError(''); setNotice('')
    try { await api.setRecurringActive(t.id, !t.isActive); list.reload() } catch (err) { setError((err as Error).message) }
  }

  async function remove(t: RecurringItem) {
    if (!await confirm({ title: `Usunąć szablon „${t.name}”?`, message: 'Wystawione już faktury zostają. Szablon przestanie generować nowe.', confirmLabel: 'Usuń szablon' })) return
    setError(''); setNotice('')
    try { await api.deleteRecurring(t.id); setNotice(`Usunięto szablon „${t.name}”`); list.reload() } catch (err) { setError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title="Faktury cykliczne">
        <Link className="btn" to="/invoices">← Faktury</Link>
        {canWrite && <Button variant="primary" onClick={() => { setEditing('new'); setNotice(''); setResult(null) }}>Nowy szablon</Button>}
      </PageHeader>
      <ErrorBox message={error || list.error || contractors.error} />
      <Notice message={notice} />
      {result && result.generated.length > 0 && (
        <ul className="card" aria-label="Wygenerowane faktury">
          {result.generated.map(g => <li key={g.number}>{g.number} – {g.template}, {fmtDate(g.issueDate)}, brutto {fmtMoney(g.gross)}</li>)}
        </ul>
      )}
      {result && result.skipped.length > 0 && (
        <div className="card" role="alert" aria-label="Pominięte szablony">
          <strong>Nie wygenerowano wszystkich faktur:</strong>
          <ul>{result.skipped.map((s, i) => <li key={i}>{s.template} (okres {fmtDate(s.date)}): {s.reason}</li>)}</ul>
        </div>
      )}

      {canWrite && (
        <div className="card form-row">
          <label>Generuj do dnia<input type="date" value={asOf} onChange={e => setAsOf(e.target.value)} /></label>
          <Button disabled={busy} onClick={() => generate()}>{busy ? 'Generowanie…' : 'Wygeneruj zaległe'}</Button>
          <p className="muted small grow flush">Wystawia faktury za wszystkie okresy do wskazanego dnia (z datą wystawienia z harmonogramu). Ponowne uruchomienie nie tworzy duplikatów.</p>
        </div>
      )}

      {editing && canWrite && (
        <TemplateForm key={editing === 'new' ? 'new' : editing.id} item={editing === 'new' ? null : editing}
          contractors={contractors.data ?? []} accounts={accounts.data ?? []} mode={mode} fullBooks={fullBooks}
          onCancel={() => setEditing(null)} onSaved={msg => { setEditing(null); setNotice(msg); list.reload() }} />
      )}

      {list.loading && <Loading />}
      {list.data && list.data.length === 0 && <EmptyState>Brak szablonów faktur cyklicznych. Dodaj szablon, aby abonamenty wystawiały się według harmonogramu.</EmptyState>}
      {list.data && list.data.length > 0 && (
        <div className="table-wrap">
          <table aria-label="Faktury cykliczne">
            <thead><tr><th>Nazwa</th><th>Nabywca</th><th>Częstotliwość</th><th>Następna faktura</th><th className="num">Brutto</th><th>Wystawione</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {list.data.map(t => (
                <tr key={t.id}>
                  <td>{t.name}</td>
                  <td>{t.contractorName}</td>
                  <td>{frequencyLabel[t.frequency]}, dzień {t.dayOfMonth}</td>
                  <td>{fmtDate(t.nextDate)}{t.endDate ? <span className="muted"> (do {fmtDate(t.endDate)})</span> : null}</td>
                  <td className="num">{fmtMoney(t.gross)}</td>
                  <td>{t.generatedCount}</td>
                  <td>{t.isActive ? <Badge tone="ok">Aktywny</Badge> : <Badge tone="warn">Wstrzymany</Badge>}</td>
                  <td className="row-actions">
                    {canWrite && <>
                      <Button small variant="link" onClick={() => { setEditing(t); setNotice(''); setResult(null) }} aria-label={`Edytuj szablon ${t.name}`}>Edytuj</Button>
                      <Button small variant="link" onClick={() => toggle(t)} aria-label={`${t.isActive ? 'Wstrzymaj' : 'Wznów'} szablon ${t.name}`}>{t.isActive ? 'Wstrzymaj' : 'Wznów'}</Button>
                      {t.isActive && <Button small variant="link" onClick={() => generate(t.id)} aria-label={`Wygeneruj z szablonu ${t.name}`}>Wygeneruj</Button>}
                      <Button small variant="link" onClick={() => remove(t)} aria-label={`Usuń szablon ${t.name}`}>Usuń</Button>
                    </>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}

interface FormProps {
  item: RecurringItem | null
  contractors: { id: string; name: string; nip?: string | null }[]
  accounts: { id: string; code: string; name: string; type: string }[]
  mode: string; fullBooks: boolean
  onCancel: () => void; onSaved: (message: string) => void
}

function TemplateForm({ item, contractors, accounts, mode, fullBooks, onCancel, onSaved }: FormProps) {
  const [name, setName] = useState(item?.name ?? '')
  const [contractorId, setContractorId] = useState(item?.contractorId ?? '')
  const [frequency, setFrequency] = useState<RecurrenceFrequency>(item?.frequency ?? 'Monthly')
  const [nextDate, setNextDate] = useState(item?.nextDate.slice(0, 10) ?? todayIso())
  const [day, setDay] = useState(String(item?.dayOfMonth ?? Number(todayIso().slice(8, 10))))
  const [dueDays, setDueDays] = useState(String(item?.dueDays ?? 14))
  const [endDate, setEndDate] = useState(item?.endDate?.slice(0, 10) ?? '')
  const [resultAccountId, setResultAccountId] = useState(item?.resultAccountId ?? '')
  const [kind, setKind] = useState<string>(item?.kind ?? '')
  const [rate, setRate] = useState(item?.ryczaltRate != null ? String(item.ryczaltRate) : '')
  const [notes, setNotes] = useState(item?.notes ?? '')
  const [lines, setLines] = useState<Line[]>(item ? item.lines.map(l => ({ description: l.description, net: amountText(l.net), vatRate: l.vatRate })) : [emptyLine()])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const parsed = lines.map(l => parseAmount(l.net))
  const invalid = parsed.some(p => p === null || p <= 0)
  const net = round2(parsed.reduce<number>((s, p) => s + (p ?? 0), 0))
  const vat = round2(lines.reduce((s, l, i) => s + round2(((parsed[i] ?? 0) * vatPercent[l.vatRate]) / 100), 0))
  const update = (i: number, patch: Partial<Line>) => setLines(lines.map((l, j) => (j === i ? { ...l, ...patch } : l)))
  const incomeAccounts = accounts.filter(a => a.type === 'Income')

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (invalid) { setError('Podaj poprawne kwoty netto we wszystkich pozycjach (np. 1234,50)'); return }
    setBusy(true)
    try {
      await api.saveRecurring(item?.id ?? null, {
        name, contractorId, frequency, nextDate, dayOfMonth: Number(day), dueDays: Number(dueDays), endDate: endDate || null,
        resultAccountId: fullBooks ? resultAccountId || null : null,
        kind: mode === 'Kpir' && kind ? (kind as KpirKind) : null,
        ryczaltRate: mode === 'Ryczalt' && rate ? Number(rate) : null,
        notes: notes || null,
        lines: lines.map((l, i) => ({ description: l.description, net: parsed[i]!, vatRate: l.vatRate })),
      })
      onSaved(item ? `Zapisano szablon „${name}”` : `Dodano szablon „${name}”`)
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <form className="card" onSubmit={submit} aria-label={item ? 'Edycja szablonu' : 'Nowy szablon'}>
      <h2 className="flush">{item ? 'Edycja szablonu' : 'Nowy szablon faktury cyklicznej'}</h2>
      <ErrorBox message={error} />
      <div className="form-row">
        <label className="grow">Nazwa szablonu<input value={name} onChange={e => setName(e.target.value)} required maxLength={200} /></label>
        <label className="grow">Nabywca
          <select value={contractorId} onChange={e => setContractorId(e.target.value)} required>
            <option value="">– wybierz kontrahenta –</option>
            {contractors.map(c => <option key={c.id} value={c.id}>{c.name}{c.nip ? ` (NIP ${c.nip})` : ''}</option>)}
          </select>
        </label>
      </div>
      <div className="form-row mt-3">
        <label>Częstotliwość
          <select value={frequency} onChange={e => setFrequency(e.target.value as RecurrenceFrequency)}>
            {(Object.keys(frequencyLabel) as RecurrenceFrequency[]).map(f => <option key={f} value={f}>{frequencyLabel[f]}</option>)}
          </select>
        </label>
        <label>Następna faktura<input type="date" value={nextDate} onChange={e => { setNextDate(e.target.value); if (e.target.value) setDay(String(Number(e.target.value.slice(8, 10)))) }} required /></label>
        <label>Dzień wystawienia<input type="number" min={1} max={31} value={day} onChange={e => setDay(e.target.value)} className="w-6" required /></label>
        <label>Termin płatności (dni)<input type="number" min={0} max={365} value={dueDays} onChange={e => setDueDays(e.target.value)} className="w-6" required /></label>
        <label>Zakończenie (opcjonalnie)<input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} /></label>
        {fullBooks && <label>Konto przychodu
          <select value={resultAccountId} onChange={e => setResultAccountId(e.target.value)}>
            <option value="">domyślne (700)</option>
            {incomeAccounts.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
          </select>
        </label>}
        {mode === 'Kpir' && <label>Rodzaj wpisu w KPiR
          <select value={kind} onChange={e => setKind(e.target.value)}>
            <option value="">domyślny ({kpirKindLabel.Revenue})</option>
            {salesKinds.map(k => <option key={k} value={k}>{kpirKindLabel[k]}</option>)}
          </select>
        </label>}
        {mode === 'Ryczalt' && <label>Stawka ryczałtu
          <select value={rate} onChange={e => setRate(e.target.value)} required>
            <option value="">– wybierz stawkę –</option>
            {ryczaltRates.map(r => <option key={r} value={r}>{String(r).replace('.', ',')}%</option>)}
          </select>
        </label>}
      </div>
      <table className="lines mt-3">
        <thead><tr><th>Opis pozycji</th><th className="num">Netto</th><th>VAT</th><th></th></tr></thead>
        <tbody>
          {lines.map((l, i) => (
            <tr key={i}>
              <td><input aria-label={`Opis pozycji ${i + 1}`} value={l.description} onChange={e => update(i, { description: e.target.value })} required maxLength={300} /></td>
              <td><input className="num" inputMode="decimal" aria-label={`Netto pozycji ${i + 1}`} value={l.net} onChange={e => update(i, { net: e.target.value })} placeholder="0,00" /></td>
              <td>
                <select aria-label={`Stawka VAT pozycji ${i + 1}`} value={l.vatRate} onChange={e => update(i, { vatRate: e.target.value as VatRate })}>
                  {rates.map(r => <option key={r} value={r}>{vatRateLabel[r]}</option>)}
                </select>
              </td>
              <td>{lines.length > 1 && <Button variant="link" small type="button" onClick={() => setLines(lines.filter((_, j) => j !== i))}>Usuń</Button>}</td>
            </tr>
          ))}
        </tbody>
        <tfoot><tr>
          <td><Button small type="button" onClick={() => setLines([...lines, emptyLine()])}>+ Dodaj pozycję</Button></td>
          <td className="num" data-testid="rec-net">{fmtMoney(net)}</td><td data-testid="rec-gross">brutto {fmtMoney(round2(net + vat))}</td><td></td>
        </tr></tfoot>
      </table>
      <div className="form-row mt-3">
        <label className="grow">Uwagi na fakturze<input value={notes} onChange={e => setNotes(e.target.value)} maxLength={500} /></label>
      </div>
      <div className="form-row mt-3">
        <Button variant="primary" disabled={busy || invalid || !contractorId || (mode === 'Ryczalt' && !rate)}>{busy ? 'Zapisywanie…' : 'Zapisz szablon'}</Button>
        <Button type="button" onClick={onCancel}>Anuluj</Button>
      </div>
    </form>
  )
}
