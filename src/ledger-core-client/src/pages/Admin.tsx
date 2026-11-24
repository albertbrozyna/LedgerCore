import { useEffect, useState } from 'react'
import { useAuth } from '../auth'
import { InterestRatesSection, MailSettingsSection } from './AdminCollections'
import { accountingModeLabel } from '../format'
import { api, type AccountingMode, type AuditItem, type CompanyDto } from '../api'
import { fmtDate } from '../format'
import { Button, DataTable, ErrorBox, Loading, Notice, PageHeader, type Column, type SortState, useDialogs, useLoad } from '../design'

export function AdminPage() {
  const { fullBooks, mode } = useAuth()
  const [newMode, setNewMode] = useState<AccountingMode>(mode)
  const { confirm } = useDialogs()
  const lock = useLoad(() => api.periodLock(), [])
  const [date, setDate] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortState | null>(null)
  const audit = useLoad(() => api.audit({ search, page, pageSize: 25, sort: sort?.key, dir: sort?.dir }), [search, page, sort])
  const auditColumns: Column<AuditItem>[] = [
    { key: 'at', header: 'Kiedy', sortable: true, render: a => new Date(a.at + 'Z').toLocaleString('pl-PL'), csv: a => a.at },
    { key: 'user', header: 'Użytkownik', sortable: true, render: a => a.userName, csv: a => a.userName },
    { key: 'action', header: 'Akcja', sortable: true, render: a => a.action, csv: a => a.action },
    { key: 'details', header: 'Szczegóły', render: a => a.details, csv: a => a.details },
  ]
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const company = useLoad(() => api.company(), [])
  const [c, setC] = useState<CompanyDto>({ name: '' })
  useEffect(() => { if (company.data) setC(company.data) }, [company.data])
  const [year, setYear] = useState(String(new Date().getFullYear() - 1))
  const backups = useLoad(() => api.backups(), [])

  async function saveCompany(e: React.FormEvent) {
    e.preventDefault()
    setError(''); setNotice('')
    try { await api.saveCompany(c); setNotice('Zapisano dane firmy'); audit.reload() } catch (err) { setError((err as Error).message) }
  }

  async function createBackup() {
    setError(''); setNotice('')
    try { const r = await api.createBackup(); setNotice(`Utworzono kopię zapasową ${r.name}`); backups.reload(); audit.reload() } catch (err) { setError((err as Error).message) }
  }

  async function changeMode() {
    if (newMode === mode) return
    if (!await confirm({ title: `Zmienić tryb ewidencji na „${accountingModeLabel[newMode]}”?`, message: 'Zmiana jest możliwa tylko w firmie bez faktur i księgowań. Zmieni się menu i sposób rejestracji faktur (KPiR i ryczałt nie księgują na konta).', confirmLabel: 'Zmień tryb' })) return
    setError(''); setNotice('')
    try { await api.setAccountingMode(newMode); window.location.assign('/admin') } catch (err) { setError((err as Error).message) }
  }

  async function closeYear() {
    if (!await confirm({ title: `Zamknąć rok obrotowy ${year}?`, message: `Wynik zostanie przeniesiony na konto 860, a księgowanie do 31.12.${year} zablokowane.`, confirmLabel: 'Zamknij rok', danger: true })) return
    setError(''); setNotice('')
    try {
      const r = await api.closeYear(Number(year))
      setNotice(`Zamknięto rok ${year} (${r.number}), wynik finansowy: ${r.result.toFixed(2).replace('.', ',')}`)
      lock.reload(); audit.reload()
    } catch (err) { setError((err as Error).message) }
  }

  async function save(value: string | null) {
    setError(''); setNotice('')
    try {
      await api.setPeriodLock(value)
      setNotice(value ? `Okres zamknięty do ${value}` : 'Odblokowano wszystkie okresy')
      lock.reload(); audit.reload()
    } catch (e) { setError((e as Error).message) }
  }

  return (
    <>
      <PageHeader title="Administracja" />
      <ErrorBox message={error || lock.error || audit.error} />
      <Notice message={notice} />

      <h2>Dane firmy</h2>
      <form className="card" onSubmit={saveCompany} aria-label="Dane firmy">
        <div className="form-row">
          <label className="grow">Nazwa firmy<input value={c.name} onChange={e => setC({ ...c, name: e.target.value })} required maxLength={200} /></label>
          <label>NIP<input value={c.nip ?? ''} onChange={e => setC({ ...c, nip: e.target.value })} /></label>
          <label className="grow">Adres<input value={c.address ?? ''} onChange={e => setC({ ...c, address: e.target.value })} /></label>
        </div>
        <div className="form-row mt-3">
          <label>E-mail<input type="email" value={c.email ?? ''} onChange={e => setC({ ...c, email: e.target.value })} /></label>
          <label>Telefon<input value={c.phone ?? ''} onChange={e => setC({ ...c, phone: e.target.value })} /></label>
          <label className="grow">Numer rachunku bankowego<input value={c.bankAccount ?? ''} onChange={e => setC({ ...c, bankAccount: e.target.value })} maxLength={40} /></label>
        </div>
        <div className="form-row mt-3">
          <label className="grow">Stopka faktury<input value={c.invoiceFooter ?? ''} onChange={e => setC({ ...c, invoiceFooter: e.target.value })} maxLength={500} /></label>
          <Button variant="primary">Zapisz dane firmy</Button>
        </div>
      </form>

      <h2>Tryb ewidencji</h2>
      <div className="card form-row">
        <p className="grow flush">Obecnie: <strong data-testid="accounting-mode">{accountingModeLabel[mode]}</strong>. Pełna księgowość księguje faktury na konta; KPiR i ryczałt prowadzą ewidencję nad fakturami (bez planu kont, dziennika i bilansu).</p>
        <label>Tryb ewidencji
          <select value={newMode} onChange={e => setNewMode(e.target.value as AccountingMode)}>
            {(['FullBooks', 'Kpir', 'Ryczalt'] as AccountingMode[]).map(m => <option key={m} value={m}>{accountingModeLabel[m]}</option>)}
          </select>
        </label>
        <Button disabled={newMode === mode} onClick={changeMode}>Zmień tryb</Button>
      </div>

      <MailSettingsSection onError={setError} onNotice={setNotice} />
      <InterestRatesSection onError={setError} onNotice={setNotice} />

      <h2>Kopie zapasowe</h2>
      <div className="card">
        <p>Aplikacja tworzy kopię bazy automatycznie raz dziennie (przechowywanych jest ostatnich 14). Zalecamy też regularne kopiowanie
          folderu <code>backups</code> na dysk zewnętrzny lub do chmury.</p>
        <Button variant="primary"  onClick={createBackup}>Utwórz kopię teraz</Button>
        {backups.data && backups.data.length === 0 && <p className="muted">Brak kopii – pierwsza powstanie automatycznie po uruchomieniu aplikacji.</p>}
        {backups.data && backups.data.length > 0 && (
          <table aria-label="Kopie zapasowe">
            <thead><tr><th>Plik</th><th>Rodzaj</th><th className="num">Rozmiar</th><th></th></tr></thead>
            <tbody>
              {backups.data.map(b => (
                <tr key={b.name}>
                  <td>{b.name}</td><td>{b.manual ? 'ręczna' : 'automatyczna'}</td>
                  <td className="num">{(b.size / 1024).toFixed(0)} KB</td>
                  <td className="actions"><Button variant="link" small  onClick={() => api.downloadBackup(b.name).catch(e => setError(e.message))}>Pobierz</Button></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <h2>Zamknięcie okresu</h2>
      <div className="card">
        <p>Po zamknięciu okresu nie można dodawać, stornować ani księgować faktur i płatności z datą nie późniejszą niż wskazana (np. po złożeniu deklaracji VAT).</p>
        <p data-testid="lock-status">
          {lock.data?.lockedThrough ? <>Okres zamknięty do: <strong>{fmtDate(lock.data.lockedThrough)}</strong></> : 'Żaden okres nie jest zamknięty.'}
        </p>
        <div className="form-row">
          <label>Zamknij do dnia<input type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
          <Button variant="primary"  disabled={!date} onClick={() => save(date)}>Zamknij okres</Button>
          <Button  disabled={!lock.data?.lockedThrough} onClick={() => save(null)}>Odblokuj wszystkie okresy</Button>
        </div>
      </div>

      {fullBooks && <><h2>Zamknięcie roku obrotowego</h2>
      <div className="card form-row">
        <p className="grow flush">Zeruje konta przychodów i kosztów, przenosi wynik na konto 860 „Wynik finansowy”, blokuje księgowanie do 31 grudnia i przenosi salda bilansowe na nowy rok.</p>
        <label>Rok<input type="number" min={2000} max={2100} value={year} onChange={e => setYear(e.target.value)} className="w-6" /></label>
        <Button variant="danger"  onClick={closeYear}>Zamknij rok</Button>
      </div></>}

      <h2>Dziennik zdarzeń (audyt)</h2>
      <div className="card form-row">
        <label className="grow">Szukaj<input value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} placeholder="użytkownik, akcja lub opis" /></label>
      </div>
      {audit.loading && !audit.data && <Loading />}
      {audit.data && (
        <DataTable label="Dziennik zdarzeń" columns={auditColumns} rows={audit.data.items} rowKey={a => a.id} loading={audit.loading}
          empty="Brak wpisów." sort={sort} onSort={s => { setSort(s); setPage(1) }} viewKey="audit" csvName="audyt-widok"
          pager={{ page, pageSize: audit.data.pageSize, total: audit.data.totalCount, onPage: setPage, unit: 'wpisów' }} />
      )}
    </>
  )
}
