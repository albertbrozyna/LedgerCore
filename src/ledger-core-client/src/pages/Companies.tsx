import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { fmtDate } from '../format'
import { Badge, Button, ErrorBox, Loading, Notice, PageHeader, useDialogs, useLoad } from '../design'

export function CompaniesPage() {
  const { prompt } = useDialogs()
  const { company, switchCompany } = useAuth()
  const { data, error, loading, reload } = useLoad(() => api.allCompanies(), [])
  const [f, setF] = useState({ name: '', nip: '', address: '', seed: true })
  const [formError, setFormError] = useState('')
  const [notice, setNotice] = useState('')

  async function add(e: FormEvent) {
    e.preventDefault()
    setFormError(''); setNotice('')
    try {
      await api.createCompany({ name: f.name, nip: f.nip, address: f.address, seedChartOfAccounts: f.seed })
      setNotice(`Dodano firmę ${f.name}`)
      setF({ name: '', nip: '', address: '', seed: true })
      reload()
    } catch (err) { setFormError((err as Error).message) }
  }

  async function update(id: string, name: string, isActive: boolean) {
    setFormError(''); setNotice('')
    try { await api.updateCompany(id, { name, isActive }); reload() } catch (err) { setFormError((err as Error).message) }
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
        <Button variant="primary">Dodaj firmę</Button>
      </form>

      {loading && !data && <Loading />}
      {data && (
        <table aria-label="Lista firm">
          <thead><tr><th>Nazwa</th><th>NIP</th><th>Adres</th><th>Dodana</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {data.map(c => (
              <tr key={c.id} className={c.isActive ? '' : 'inactive'}>
                <td>{c.name}{c.id === company?.id && <Badge> bieżąca</Badge>}</td>
                <td>{c.nip ?? ''}</td><td>{c.address ?? ''}</td><td>{fmtDate(c.createdAt)}</td>
                <td>{c.isActive ? 'Aktywna' : 'Nieaktywna'}</td>
                <td className="actions">
                  {c.isActive && c.id !== company?.id && <Button variant="link" small  onClick={() => switchCompany(c.id)}>Przełącz</Button>}
                  <Button variant="link" small onClick={async () => {
                    const name = await prompt({ title: 'Zmiana nazwy firmy', label: 'Nowa nazwa firmy', initial: c.name })
                    if (name && name.trim()) update(c.id, name.trim(), c.isActive)
                  }}>Zmień nazwę</Button>
                  <Button variant="link" small  onClick={() => update(c.id, c.name, !c.isActive)}>{c.isActive ? 'Dezaktywuj' : 'Aktywuj'}</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  )
}
