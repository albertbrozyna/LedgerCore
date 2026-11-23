import { useEffect, useState } from 'react'
import { apiX, apiY, apiZ, type CompanyDto } from '../api'
import { fmtDate } from '../format'
import { ErrorBox, Loading, Notice, PageHeader, useLoad } from '../ui'

export function AdminPage() {
  const lock = useLoad(() => apiX.periodLock(), [])
  const [date, setDate] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const audit = useLoad(() => apiX.audit({ search, page, pageSize: 25 }), [search, page])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const company = useLoad(() => apiY.company(), [])
  const [c, setC] = useState<CompanyDto>({ name: '' })
  useEffect(() => { if (company.data) setC(company.data) }, [company.data])
  const [year, setYear] = useState(String(new Date().getFullYear() - 1))
  const backups = useLoad(() => apiZ.backups(), [])
  const pages = audit.data ? Math.max(1, Math.ceil(audit.data.totalCount / audit.data.pageSize)) : 1

  async function saveCompany(e: React.FormEvent) {
    e.preventDefault()
    setError(''); setNotice('')
    try { await apiY.saveCompany(c); setNotice('Zapisano dane firmy'); audit.reload() } catch (err) { setError((err as Error).message) }
  }

  async function createBackup() {
    setError(''); setNotice('')
    try { const r = await apiZ.createBackup(); setNotice(`Utworzono kopię zapasową ${r.name}`); backups.reload(); audit.reload() } catch (err) { setError((err as Error).message) }
  }

  async function closeYear() {
    if (!confirm(`Zamknąć rok obrotowy ${year}? Wynik zostanie przeniesiony na konto 860, a księgowanie do 31.12.${year} zablokowane.`)) return
    setError(''); setNotice('')
    try {
      const r = await apiY.closeYear(Number(year))
      setNotice(`Zamknięto rok ${year} (${r.number}), wynik finansowy: ${r.result.toFixed(2).replace('.', ',')}`)
      lock.reload(); audit.reload()
    } catch (err) { setError((err as Error).message) }
  }

  async function save(value: string | null) {
    setError(''); setNotice('')
    try {
      await apiX.setPeriodLock(value)
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
        <div className="form-row" style={{ marginTop: '.8rem' }}>
          <label>E-mail<input type="email" value={c.email ?? ''} onChange={e => setC({ ...c, email: e.target.value })} /></label>
          <label>Telefon<input value={c.phone ?? ''} onChange={e => setC({ ...c, phone: e.target.value })} /></label>
          <label className="grow">Numer rachunku bankowego<input value={c.bankAccount ?? ''} onChange={e => setC({ ...c, bankAccount: e.target.value })} maxLength={40} /></label>
        </div>
        <div className="form-row" style={{ marginTop: '.8rem' }}>
          <label className="grow">Stopka faktury<input value={c.invoiceFooter ?? ''} onChange={e => setC({ ...c, invoiceFooter: e.target.value })} maxLength={500} /></label>
          <button className="btn btn-primary">Zapisz dane firmy</button>
        </div>
      </form>

      <h2>Kopie zapasowe</h2>
      <div className="card">
        <p>Aplikacja tworzy kopię bazy automatycznie raz dziennie (przechowywanych jest ostatnich 14). Zalecamy też regularne kopiowanie
          folderu <code>backups</code> na dysk zewnętrzny lub do chmury.</p>
        <button className="btn btn-primary" onClick={createBackup}>Utwórz kopię teraz</button>
        {backups.data && backups.data.length === 0 && <p className="muted">Brak kopii – pierwsza powstanie automatycznie po uruchomieniu aplikacji.</p>}
        {backups.data && backups.data.length > 0 && (
          <table aria-label="Kopie zapasowe">
            <thead><tr><th>Plik</th><th>Rodzaj</th><th className="num">Rozmiar</th><th></th></tr></thead>
            <tbody>
              {backups.data.map(b => (
                <tr key={b.name}>
                  <td>{b.name}</td><td>{b.manual ? 'ręczna' : 'automatyczna'}</td>
                  <td className="num">{(b.size / 1024).toFixed(0)} KB</td>
                  <td className="actions"><button className="btn btn-small btn-link" onClick={() => apiZ.downloadBackup(b.name).catch(e => setError(e.message))}>Pobierz</button></td>
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
          <button className="btn btn-primary" disabled={!date} onClick={() => save(date)}>Zamknij okres</button>
          <button className="btn" disabled={!lock.data?.lockedThrough} onClick={() => save(null)}>Odblokuj wszystkie okresy</button>
        </div>
      </div>

      <h2>Zamknięcie roku obrotowego</h2>
      <div className="card form-row">
        <p className="grow" style={{ margin: 0 }}>Zeruje konta przychodów i kosztów, przenosi wynik na konto 860 „Wynik finansowy”, blokuje księgowanie do 31 grudnia i przenosi salda bilansowe na nowy rok.</p>
        <label>Rok<input type="number" min={2000} max={2100} value={year} onChange={e => setYear(e.target.value)} style={{ width: '6rem' }} /></label>
        <button className="btn btn-danger" onClick={closeYear}>Zamknij rok</button>
      </div>

      <h2>Dziennik zdarzeń (audyt)</h2>
      <div className="card form-row">
        <label className="grow">Szukaj<input value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} placeholder="użytkownik, akcja lub opis" /></label>
      </div>
      {audit.loading && !audit.data && <Loading />}
      {audit.data && (
        <>
          <table aria-label="Dziennik zdarzeń">
            <thead><tr><th>Kiedy</th><th>Użytkownik</th><th>Akcja</th><th>Szczegóły</th></tr></thead>
            <tbody>
              {audit.data.items.map(a => (
                <tr key={a.id}>
                  <td>{new Date(a.at + 'Z').toLocaleString('pl-PL')}</td><td>{a.userName}</td><td>{a.action}</td><td>{a.details}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pager">
            <button className="btn btn-small" disabled={page <= 1} onClick={() => setPage(page - 1)}>‹ Poprzednia</button>
            <span>Strona {page} z {pages} ({audit.data.totalCount} wpisów)</span>
            <button className="btn btn-small" disabled={page >= pages} onClick={() => setPage(page + 1)}>Następna ›</button>
          </div>
        </>
      )}
    </>
  )
}
