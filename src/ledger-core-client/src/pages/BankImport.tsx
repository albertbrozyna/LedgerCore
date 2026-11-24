import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  api, type BankFormat, type BankReconciliationDto, type BankRowDto, type BankRuleDto, type BankStatementDto, type CsvProfile, type InvoiceItem, type RuleDirection,
} from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, parseAmount } from '../format'
import { matchRow, type Confidence } from '../bankImport'
import { Badge, Button, EmptyState, ErrorBox, Notice, PageHeader, Tabs, useDialogs, useLoad } from '../design'

interface Row extends BankRowDto { invoiceId: string; confidence: Confidence; selected: boolean; result?: string; ok?: boolean }
type Tab = 'import' | 'rules' | 'reconcile'

const confLabel: Record<Confidence, string> = { exact: 'pewne', number: 'wg numeru', amount: 'wg kwoty', none: 'brak' }
const formatLabel: Record<BankFormat, string> = { Csv: 'CSV', Mt940: 'MT940', Elixir: 'Elixir-0' }
const profileLabel: Record<CsvProfile, string> = { Auto: 'Automatyczny', Mbank: 'mBank', Pko: 'PKO BP', Ing: 'ING Bank Śląski' }
const directionLabel: Record<RuleDirection, string> = { Any: 'Dowolny', In: 'Wpływ', Out: 'Wypłata' }

/** Pliki z banków bywają w UTF-8 albo w Windows-1250 – wybieramy kodowanie, które nie psuje polskich znaków. */
async function readText(f: File): Promise<string> {
  const buf = await f.arrayBuffer()
  try { return new TextDecoder('utf-8', { fatal: true }).decode(buf) } catch { return new TextDecoder('windows-1250').decode(buf) }
}

function guessFormat(text: string): BankFormat {
  if (/^\s*:20:/m.test(text)) return 'Mt940'
  if (/^\s*110,\d{8},/m.test(text)) return 'Elixir'
  return 'Csv'
}

export function BankImportPage() {
  const { canWrite } = useAuth()
  const accounts = useLoad(() => api.accounts(), [])
  const [tab, setTab] = useState<Tab>('import')
  const [accountId, setAccountId] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  if (!canWrite) return <><PageHeader title="Import wyciągu bankowego" /><ErrorBox message="Twoja rola (Podgląd) nie pozwala na rejestrowanie płatności." /></>

  const bankAccounts = accounts.data?.filter(a => a.type === 'Asset') ?? []

  return (
    <>
      <PageHeader title="Import wyciągu bankowego">
        <Link className="btn" to="/invoices">← Faktury</Link>
      </PageHeader>
      <ErrorBox message={error || accounts.error} />
      <Notice message={notice} />
      <Tabs label="Wyciągi bankowe" value={tab} onChange={t => { setTab(t); setError(''); setNotice('') }}
        tabs={[{ value: 'import', label: 'Import wyciągu' }, { value: 'rules', label: 'Reguły księgowania' }, { value: 'reconcile', label: 'Uzgodnienie z bankiem' }]} />
      <div className="card">
        <label>Konto bankowe / kasa
          <select value={accountId} onChange={e => setAccountId(e.target.value)} aria-label="Konto bankowe">
            <option value="">– wybierz konto –</option>
            {bankAccounts.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
          </select>
        </label>
      </div>
      {tab === 'import' && <ImportTab accountId={accountId} onError={setError} onNotice={setNotice} />}
      {tab === 'rules' && <RulesTab accountId={accountId} accounts={accounts.data ?? []} onError={setError} onNotice={setNotice} />}
      {tab === 'reconcile' && <ReconcileTab accountId={accountId} onError={setError} />}
    </>
  )
}

interface TabProps { accountId: string; onError: (m: string) => void; onNotice?: (m: string) => void }

function ImportTab({ accountId, onError, onNotice }: TabProps & { onNotice: (m: string) => void }) {
  const [open, setOpen] = useState<InvoiceItem[]>([])
  const [format, setFormat] = useState<BankFormat>('Csv')
  const [profile, setProfile] = useState<CsvProfile>('Auto')
  const [ownAccount, setOwnAccount] = useState('')
  const [applyRules, setApplyRules] = useState(true)
  const [text, setText] = useState('')
  const [fileName, setFileName] = useState('')
  const [rows, setRows] = useState<Row[]>([])
  const [skipped, setSkipped] = useState(0)
  const [busy, setBusy] = useState(false)

  async function analyze(content: string, name: string, fmt: BankFormat) {
    onError(''); onNotice('')
    if (!accountId) { onError('Wybierz konto bankowe, na które zaimportować wyciąg'); return }
    setBusy(true)
    try {
      const r = await api.importBank({ accountId, format: fmt, profile, content, fileName: name || undefined, ownAccount: ownAccount || undefined, applyRules })
      const list = (await api.invoices({ status: 'unpaid', pageSize: 200 })).items
      setOpen(list)
      setSkipped(r.skipped)
      setRows(r.rows.map(x => {
        const open = x.status === 'New' && !x.isDuplicate
        const m = open ? matchRow(x, list) : { invoice: null, confidence: 'none' as Confidence }
        return { ...x, invoiceId: m.invoice?.id ?? '', confidence: m.confidence, selected: open && (m.confidence === 'exact' || m.confidence === 'number') }
      }))
      const parts = [`Zaimportowano operacje: ${r.imported}`]
      if (r.duplicates > 0) parts.push(`pominięto duplikaty: ${r.duplicates}`)
      if (r.rulesPosted > 0) parts.push(`zaksięgowano wg reguł: ${r.rulesPosted}`)
      onNotice(parts.join(', '))
      if (r.ruleErrors.length) onError(`Nie zaksięgowano wg reguł: ${r.ruleErrors.join('; ')}`)
    } catch (e) { setRows([]); onError((e as Error).message) } finally { setBusy(false) }
  }

  async function onFile(f: File | undefined) {
    if (!f) return
    const t = await readText(f)
    const fmt = guessFormat(t)
    setText(t); setFileName(f.name); setFormat(fmt)
    await analyze(t, f.name, fmt)
  }

  const update = (i: number, patch: Partial<Row>) => setRows(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  async function post() {
    setBusy(true); onError(''); onNotice('')
    let okCount = 0
    const next = [...rows]
    for (const [i, r] of rows.entries()) {
      if (!r.selected || !r.invoiceId || r.ok) continue
      const inv = open.find(x => x.id === r.invoiceId)!
      try {
        const { amount } = await api.postBankToInvoice(r.id, inv.id)
        next[i] = { ...r, ok: true, selected: false, status: 'Posted', result: `Zaksięgowano ${fmtMoney(amount)} do ${inv.number}` }
        inv.remaining = Math.round((inv.remaining - amount) * 100) / 100
        okCount++
      } catch (e) { next[i] = { ...r, ok: false, result: (e as Error).message } }
    }
    setRows(next); setBusy(false)
    if (okCount) onNotice(`Zaksięgowano płatności: ${okCount}`)
  }

  async function toggleSkip(i: number) {
    const r = rows[i]
    onError('')
    try {
      await api.skipBankTransaction(r.id, r.status !== 'Skipped')
      update(i, { status: r.status === 'Skipped' ? 'New' : 'Skipped', selected: false })
    } catch (e) { onError((e as Error).message) }
  }

  const selectedCount = rows.filter(r => r.selected && r.invoiceId && !r.ok).length
  const statusText = (r: Row) => r.isDuplicate ? `duplikat (${r.status === 'Posted' ? 'zaksięgowana' : r.status === 'Skipped' ? 'pominięta' : 'czeka'})`
    : r.status === 'Posted' ? 'zaksięgowana' : r.status === 'Skipped' ? 'pominięta' : confLabel[r.confidence]

  return (
    <>
      <div className="card">
        <p className="muted">Wczytaj wyciąg (CSV, MT940 lub Elixir-0). Każda operacja jest zapisywana raz – ponowny import tego samego pliku nie zdubluje operacji ani płatności.
          Wpływy są dopasowywane do faktur sprzedaży, wypłaty – do faktur zakupu. Przed księgowaniem możesz poprawić dopasowanie.</p>
        <div className="form-row">
          <label>Format wyciągu
            <select value={format} onChange={e => setFormat(e.target.value as BankFormat)}>
              {(Object.keys(formatLabel) as BankFormat[]).map(f => <option key={f} value={f}>{formatLabel[f]}</option>)}
            </select>
          </label>
          {format === 'Csv' && <label>Profil CSV
            <select value={profile} onChange={e => setProfile(e.target.value as CsvProfile)}>
              {(Object.keys(profileLabel) as CsvProfile[]).map(p => <option key={p} value={p}>{profileLabel[p]}</option>)}
            </select>
          </label>}
          {format === 'Elixir' && <label className="grow">Numer rachunku firmy (26 cyfr)
            <input value={ownAccount} onChange={e => setOwnAccount(e.target.value)} placeholder="np. 11 1140 2004 0000 3002 0135 5387" maxLength={40} />
          </label>}
          <label>Plik wyciągu<input type="file" accept=".csv,.txt,.sta,.mt940,.940,.pli,.eli" aria-label="Plik wyciągu" onChange={e => onFile(e.target.files?.[0])} /></label>
          <label className="inline"><input type="checkbox" checked={applyRules} onChange={e => setApplyRules(e.target.checked)} /> Zastosuj reguły księgowania</label>
        </div>
        <label className="mt-3">…albo wklej treść wyciągu
          <textarea rows={4} value={text} onChange={e => setText(e.target.value)} aria-label="Treść wyciągu" />
        </label>
        <Button className="mt-2" onClick={() => analyze(text, fileName, format)} disabled={!text.trim() || busy}>Analizuj</Button>
      </div>

      {rows.length > 0 && (
        <>
          <table aria-label="Operacje z wyciągu">
            <thead><tr><th></th><th>Data</th><th>Opis operacji</th><th className="num">Kwota</th><th>Dopasowanie</th><th>Faktura</th><th>Wynik</th><th></th></tr></thead>
            <tbody>
              {rows.map((r, i) => {
                const candidates = open.filter(x => x.type === (r.amount > 0 ? 'Sales' : 'Purchase'))
                const frozen = r.isDuplicate || r.status !== 'New' || r.ok
                return (
                  <tr key={r.id + i}>
                    <td><input type="checkbox" aria-label={`Zaznacz operację ${i + 1}`} checked={r.selected} disabled={!r.invoiceId || frozen} onChange={e => update(i, { selected: e.target.checked })} /></td>
                    <td>{fmtDate(r.date)}</td>
                    <td>{r.title}{r.counterparty && <div className="muted small">{r.counterparty}</div>}</td>
                    <td className="num">{fmtMoney(r.amount)}</td>
                    <td><Badge>{statusText(r)}</Badge></td>
                    <td>
                      <select aria-label={`Faktura dla operacji ${i + 1}`} value={r.invoiceId} disabled={frozen}
                        onChange={e => update(i, { invoiceId: e.target.value, selected: !!e.target.value })}>
                        <option value="">– brak –</option>
                        {candidates.map(x => <option key={x.id} value={x.id}>{x.number} · {x.contractorName} · {fmtMoney(x.remaining)}</option>)}
                      </select>
                    </td>
                    <td className={r.ok ? 'ok-text' : 'bad-text'}>{r.result}</td>
                    <td>{(r.status === 'New' || r.status === 'Skipped') && !r.ok &&
                      <Button small variant="link" onClick={() => toggleSkip(i)} aria-label={`${r.status === 'Skipped' ? 'Przywróć' : 'Pomiń'} operację ${i + 1}`}>{r.status === 'Skipped' ? 'Przywróć' : 'Pomiń'}</Button>}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          <p className="muted small">{skipped > 0 && `Pominięto wierszy bez poprawnej daty lub kwoty: ${skipped}. `}Płatność nie przekroczy kwoty pozostałej do zapłaty na fakturze.</p>
          <Button variant="primary" disabled={busy || !accountId || selectedCount === 0} onClick={post}>
            {busy ? 'Księgowanie…' : `Zaksięguj zaznaczone (${selectedCount})`}
          </Button>
        </>
      )}
    </>
  )
}

function RulesTab({ accountId, accounts, onError, onNotice }: TabProps & { accounts: { id: string; code: string; name: string; isActive: boolean }[]; onNotice: (m: string) => void }) {
  const { confirm } = useDialogs()
  const rules = useLoad(() => api.bankRules(), [])
  const [name, setName] = useState('')
  const [matchText, setMatchText] = useState('')
  const [direction, setDirection] = useState<RuleDirection>('Any')
  const [target, setTarget] = useState('')
  const [description, setDescription] = useState('')

  async function save(e: FormEvent) {
    e.preventDefault(); onError(''); onNotice('')
    try {
      await api.saveBankRule(null, { name, matchText, direction, targetAccountId: target, description: description || undefined })
      setName(''); setMatchText(''); setDescription(''); onNotice(`Dodano regułę „${name}”`); rules.reload()
    } catch (err) { onError((err as Error).message) }
  }
  async function toggle(r: BankRuleDto) {
    onError(''); onNotice('')
    try { await api.saveBankRule(r.id, { name: r.name, matchText: r.matchText, direction: r.direction, targetAccountId: r.targetAccountId, description: r.description ?? undefined, isActive: !r.isActive }); rules.reload() }
    catch (err) { onError((err as Error).message) }
  }
  async function remove(r: BankRuleDto) {
    if (!await confirm({ title: `Usunąć regułę „${r.name}”?`, message: 'Zaksięgowane już operacje zostają.', confirmLabel: 'Usuń regułę' })) return
    onError(''); onNotice('')
    try { await api.deleteBankRule(r.id); onNotice(`Usunięto regułę „${r.name}”`); rules.reload() } catch (err) { onError((err as Error).message) }
  }
  async function apply() {
    onError(''); onNotice('')
    if (!accountId) { onError('Wybierz konto bankowe, dla którego zastosować reguły'); return }
    try {
      const r = await api.applyBankRules(accountId)
      onNotice(`Zaksięgowano wg reguł: ${r.posted}`)
      if (r.errors.length) onError(`Nie zaksięgowano: ${r.errors.join('; ')}`)
    } catch (err) { onError((err as Error).message) }
  }

  return (
    <>
      <form className="card" onSubmit={save} aria-label="Nowa reguła">
        <p className="muted">Reguła księguje operację z wyciągu bezpośrednio na wskazane konto (bez faktury), np. opłaty bankowe lub odsetki: wpływ Wn bank / Ma konto, wypłata Wn konto / Ma bank.
          Przy imporcie stosowana jest pierwsza pasująca reguła (w kolejności dodania).</p>
        <div className="form-row">
          <label>Nazwa reguły<input value={name} onChange={e => setName(e.target.value)} required maxLength={100} /></label>
          <label className="grow">Tekst w opisie lub u kontrahenta<input value={matchText} onChange={e => setMatchText(e.target.value)} required minLength={3} maxLength={100} placeholder="np. Opłata za prowadzenie rachunku" /></label>
          <label>Kierunek
            <select value={direction} onChange={e => setDirection(e.target.value as RuleDirection)}>
              {(Object.keys(directionLabel) as RuleDirection[]).map(d => <option key={d} value={d}>{directionLabel[d]}</option>)}
            </select>
          </label>
          <label>Konto księgowania
            <select value={target} onChange={e => setTarget(e.target.value)} required>
              <option value="">– wybierz konto –</option>
              {accounts.filter(a => a.isActive).map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
            </select>
          </label>
        </div>
        <div className="form-row mt-3">
          <label className="grow">Opis księgowania (opcjonalnie)<input value={description} onChange={e => setDescription(e.target.value)} maxLength={300} /></label>
          <Button variant="primary">Zapisz regułę</Button>
        </div>
      </form>
      {rules.data && rules.data.length === 0 && <EmptyState>Brak reguł księgowania.</EmptyState>}
      {rules.data && rules.data.length > 0 && (
        <table aria-label="Reguły księgowania">
          <thead><tr><th>Nazwa</th><th>Tekst</th><th>Kierunek</th><th>Konto</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {rules.data.map(r => (
              <tr key={r.id}>
                <td>{r.name}</td><td>{r.matchText}</td><td>{directionLabel[r.direction]}</td><td>{r.targetAccount}</td>
                <td>{r.isActive ? <Badge tone="ok">Aktywna</Badge> : <Badge tone="warn">Wyłączona</Badge>}</td>
                <td className="row-actions">
                  <Button small variant="link" onClick={() => toggle(r)} aria-label={`${r.isActive ? 'Wyłącz' : 'Włącz'} regułę ${r.name}`}>{r.isActive ? 'Wyłącz' : 'Włącz'}</Button>
                  <Button small variant="link" onClick={() => remove(r)} aria-label={`Usuń regułę ${r.name}`}>Usuń</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="card form-row">
        <p className="grow flush muted">Zastosuj reguły do operacji już zaimportowanych, które czekają na zaksięgowanie (dla wybranego konta).</p>
        <Button onClick={apply}>Zastosuj reguły do niezaksięgowanych</Button>
      </div>
    </>
  )
}

function ReconcileTab({ accountId, onError }: TabProps) {
  const [asOf, setAsOf] = useState('')
  const [closing, setClosing] = useState('')
  const [res, setRes] = useState<BankReconciliationDto | null>(null)
  const [statements, setStatements] = useState<BankStatementDto[]>([])

  async function run() {
    onError('')
    if (!accountId) { setRes(null); return }
    const manual = closing.trim() === '' ? undefined : parseAmount(closing)
    if (manual === null) { onError('Podaj poprawne saldo końcowe z wyciągu (np. 11014,50)'); return }
    try {
      setRes(await api.bankReconciliation(accountId, asOf || undefined, manual))
      setStatements(await api.bankStatements(accountId))
    } catch (e) { setRes(null); onError((e as Error).message) }
  }
  useEffect(() => { setRes(null); setStatements([]); if (accountId) void run() }, [accountId]) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
      <div className="card form-row">
        <label>Na dzień<input type="date" value={asOf} onChange={e => setAsOf(e.target.value)} /></label>
        <label>Saldo końcowe z wyciągu (opcjonalnie)
          <input className="num" inputMode="decimal" value={closing} onChange={e => setClosing(e.target.value)} placeholder="z pliku MT940 lub wpisz" />
        </label>
        <Button onClick={run} disabled={!accountId}>Sprawdź uzgodnienie</Button>
      </div>
      {!accountId && <EmptyState>Wybierz konto bankowe, aby uzgodnić saldo.</EmptyState>}
      {res && (
        <div className="card" aria-label="Wynik uzgodnienia">
          <p>Saldo konta w księdze na {fmtDate(res.asOf)}: <strong data-testid="rec-ledger">{fmtMoney(res.ledgerBalance)}</strong></p>
          <p>Saldo końcowe z wyciągu ({res.statementSource}{res.statementDate ? `, ${fmtDate(res.statementDate)}` : ''}):{' '}
            <strong data-testid="rec-statement">{res.statementBalance == null ? 'brak' : fmtMoney(res.statementBalance)}</strong></p>
          {res.difference != null && (res.matches
            ? <p><Badge tone="ok">Zgodne</Badge> Saldo w księdze zgadza się z saldem z wyciągu.</p>
            : <p><Badge tone="danger">Różnica</Badge> <strong data-testid="rec-diff">{fmtMoney(res.difference)}</strong> (księga − wyciąg).
              {res.unposted.length > 0 && <> Niezaksięgowane operacje z wyciągu: {fmtMoney(res.unpostedSum)}; po ich zaksięgowaniu różnica wyniesie{' '}
                <strong data-testid="rec-diff-after">{fmtMoney(res.differenceAfterUnposted ?? 0)}</strong>.</>}</p>)}
          {res.difference == null && <p className="muted">Zaimportuj wyciąg MT940 (zawiera saldo końcowe) albo wpisz saldo końcowe ręcznie.</p>}
          {res.unposted.length > 0 && (
            <table aria-label="Niezaksięgowane operacje">
              <thead><tr><th>Data</th><th>Opis operacji</th><th className="num">Kwota</th></tr></thead>
              <tbody>{res.unposted.map(u => <tr key={u.id}><td>{fmtDate(u.date)}</td><td>{u.title}{u.counterparty && <div className="muted small">{u.counterparty}</div>}</td><td className="num">{fmtMoney(u.amount)}</td></tr>)}</tbody>
            </table>
          )}
        </div>
      )}
      {statements.length > 0 && (
        <>
          <h2>Zaimportowane wyciągi</h2>
          <table aria-label="Zaimportowane wyciągi">
            <thead><tr><th>Plik</th><th>Format</th><th>Zaimportowano</th><th className="num">Nowe</th><th className="num">Duplikaty</th><th className="num">Saldo końcowe</th></tr></thead>
            <tbody>{statements.map(s => (
              <tr key={s.id}><td>{s.fileName}</td><td>{s.format}</td><td>{fmtDate(s.importedAt)}</td><td className="num">{s.importedCount}</td><td className="num">{s.duplicateCount}</td>
                <td className="num">{s.closingBalance == null ? '' : fmtMoney(s.closingBalance)}</td></tr>))}</tbody>
          </table>
        </>
      )}
    </>
  )
}
