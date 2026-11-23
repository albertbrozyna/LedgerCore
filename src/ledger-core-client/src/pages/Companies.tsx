import { useState, type FormEvent } from 'react'
import { apiC } from '../api'
import { useAuth } from '../auth'
import { fmtDate } from '../format'
import { ErrorBox, Loading, Notice, PageHeader, useLoad } from '../ui'

export function CompaniesPage() {
  const { company, switchCompany } = useAuth()
  const { data, error, loading, reload } = useLoad(() => apiC.allCompanies(), [])
  const [f, setF] = useState({ name: '', nip: '', address: '', seed: true })
  const [formError, setFormError] = useState('')
  const [notice, setNotice] = useState('')

  async function add(e: FormEvent) {
    e.preventDefault()
    setFormError(''); setNotice('')
    try {
      await apiC.createCompany({ name: f.name, nip: f.nip, address: f.address, seedChartOfAccounts: f.seed })
      setNotice(`Dodano firmę ${f.name}`)
      setF({ name: '', nip: '', address: '', seed: true })
      reload()
    } catch (err) { setFormError((err as Error).message) }
  }

  async function update(id: string, name: string, isActive: boolean) {
    setFormError(''); setNotice('')
    try { await apiC.updateCompany(id, { name, isActive }); reload() } catch (err) { setFormError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title="Firmy (klienci biura)" />
      <p className="muted">Każda firma ma własny plan kont, księgowania, faktury, kontrahentów, raporty i zamknięcia okresów. Dane firm są od siebie odizolowane.
        Dostęp użytkowników do firm ustawisz w menu „Użytkownicy”.</p>
      <ErrorBox message={error || formError} />
      <Notice message={notice} />

      <form className="card form-row" onSubmit={add} aria-label="Nowa firma">
        <label className="grow">Nazwa firmy<input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} required maxLength={200} /></label>
        <label>NIP<input value={f.nip} onChange={e => setF({ ...f, nip: e.target.value })} placeholder="opcjonalnie" /></label>
        <label className="grow">Adres<input value={f.address} onChange={e => setF({ ...f, address: e.target.value })} /></label>
        <label className="inline"><input type="checkbox" checked={f.seed} onChange={e => setF({ ...f, seed: e.target.checked })} /> Domyślny plan kont</label>
        <button className="btn btn-primary">Dodaj firmę</button>
      </form>

      {loading && !data && <Loading />}
      {data && (
        <table aria-label="Lista firm">
          <thead><tr><th>Nazwa</th><th>NIP</th><th>Adres</th><th>Dodana</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {data.map(c => (
              <tr key={c.id} className={c.isActive ? '' : 'inactive'}>
                <td>{c.name}{c.id === company?.id && <span className="badge"> bieżąca</span>}</td>
                <td>{c.nip ?? ''}</td><td>{c.address ?? ''}</td><td>{fmtDate(c.createdAt)}</td>
                <td>{c.isActive ? 'Aktywna' : 'Nieaktywna'}</td>
                <td className="actions">
                  {c.isActive && c.id !== company?.id && <button className="btn btn-small btn-link" onClick={() => switchCompany(c.id)}>Przełącz</button>}
                  <button className="btn btn-small btn-link" onClick={() => {
                    const name = prompt('Nowa nazwa firmy:', c.name)
                    if (name && name.trim()) update(c.id, name.trim(), c.isActive)
                  }}>Zmień nazwę</button>
                  <button className="btn btn-small btn-link" onClick={() => update(c.id, c.name, !c.isActive)}>{c.isActive ? 'Dezaktywuj' : 'Aktywuj'}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  )
}
