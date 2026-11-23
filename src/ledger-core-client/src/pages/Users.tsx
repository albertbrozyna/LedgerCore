import { Fragment, useState, type FormEvent } from 'react'
import { api, apiC, type Role } from '../api'
import { useAuth } from '../auth'
import { fmtDate, roleLabel } from '../format'
import { ErrorBox, Loading, Notice, PageHeader, useLoad } from '../ui'

const roles = Object.keys(roleLabel) as Role[]

export function UsersPage() {
  const { user: me } = useAuth()
  const { data, error, loading, reload } = useLoad(() => api.users(), [])
  const [f, setF] = useState({ firstName: '', lastName: '', email: '', password: '', role: 'Accountant' as Role })
  const [formError, setFormError] = useState('')
  const [notice, setNotice] = useState('')
  const [backupBusy, setBackupBusy] = useState(false)
  const companies = useLoad(() => apiC.allCompanies(), [])
  const [editingCompanies, setEditingCompanies] = useState<string | null>(null)
  const [selectedCompanies, setSelectedCompanies] = useState<string[]>([])

  async function openCompanies(userId: string) {
    setFormError('')
    try { setSelectedCompanies(await apiC.userCompanies(userId)); setEditingCompanies(userId) } catch (e) { setFormError((e as Error).message) }
  }

  async function run(fn: () => Promise<unknown>, ok?: string) {
    setFormError(''); setNotice('')
    try { await fn(); if (ok) setNotice(ok); reload() } catch (e) { setFormError((e as Error).message) }
  }

  async function add(e: FormEvent) {
    e.preventDefault()
    await run(async () => {
      await api.createUser(f)
      setF({ firstName: '', lastName: '', email: '', password: '', role: f.role })
    }, `Dodano użytkownika ${f.email}`)
  }

  function resetPassword(id: string, email: string) {
    const pw = prompt(`Nowe hasło dla ${email} (min. 8 znaków, w tym cyfra):`)
    if (pw) run(() => api.resetPassword(id, pw), `Zmieniono hasło użytkownika ${email}`)
  }

  return (
    <>
      <PageHeader title="Użytkownicy">
        <button className="btn" disabled={backupBusy} onClick={async () => {
          setBackupBusy(true)
          await run(() => api.backup(), 'Pobrano kopię zapasową bazy danych')
          setBackupBusy(false)
        }}>Pobierz kopię zapasową</button>
      </PageHeader>
      <ErrorBox message={error || formError} />
      <Notice message={notice} />

      <form className="card form-row" onSubmit={add} aria-label="Nowy użytkownik">
        <label>Imię<input value={f.firstName} onChange={e => setF({ ...f, firstName: e.target.value })} required /></label>
        <label>Nazwisko<input value={f.lastName} onChange={e => setF({ ...f, lastName: e.target.value })} required /></label>
        <label className="grow">E-mail<input type="email" value={f.email} onChange={e => setF({ ...f, email: e.target.value })} required /></label>
        <label>Hasło<input type="password" value={f.password} onChange={e => setF({ ...f, password: e.target.value })} required /></label>
        <label>Rola
          <select value={f.role} onChange={e => setF({ ...f, role: e.target.value as Role })}>
            {roles.map(r => <option key={r} value={r}>{roleLabel[r]}</option>)}
          </select>
        </label>
        <button className="btn btn-primary">Dodaj użytkownika</button>
      </form>

      {loading && !data && <Loading />}
      {data && (
        <table>
          <thead><tr><th>Użytkownik</th><th>E-mail</th><th>Rola</th><th>Ostatnie logowanie</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {data.map(u => {
              const self = u.id === me?.id
              return (
                <Fragment key={u.id}>
                <tr className={u.isActive ? '' : 'inactive'}>
                  <td>{u.firstName} {u.lastName}</td>
                  <td>{u.email}</td>
                  <td>
                    <select aria-label={`Rola: ${u.email}`} value={u.role} disabled={self}
                      onChange={e => run(() => api.setUserRole(u.id, e.target.value as Role))}>
                      {roles.map(r => <option key={r} value={r}>{roleLabel[r]}</option>)}
                    </select>
                  </td>
                  <td>{u.lastLogin ? fmtDate(u.lastLogin) : '—'}</td>
                  <td>{u.isActive ? 'Aktywny' : 'Zablokowany'}</td>
                  <td className="actions">
                    <button className="btn btn-small btn-link" onClick={() => resetPassword(u.id, u.email)}>Zmień hasło</button>
                    {u.role !== 'Admin' && <button className="btn btn-small btn-link" onClick={() => openCompanies(u.id)}>Firmy</button>}
                    {!self && <button className="btn btn-small btn-link" onClick={() => run(() => api.setUserActive(u.id, !u.isActive))}>{u.isActive ? 'Zablokuj' : 'Odblokuj'}</button>}
                  </td>
                </tr>
                {editingCompanies === u.id && (
                  <tr className="sub-row"><td colSpan={6}>
                    <div className="checks" role="group" aria-label={`Firmy użytkownika ${u.email}`}>
                      {companies.data?.filter(c => c.isActive).map(c => (
                        <label key={c.id}>
                          <input type="checkbox" checked={selectedCompanies.includes(c.id)}
                            onChange={e => setSelectedCompanies(e.target.checked ? [...selectedCompanies, c.id] : selectedCompanies.filter(x => x !== c.id))} />
                          {c.name}
                        </label>
                      ))}
                    </div>
                    <button className="btn btn-small btn-primary" style={{ marginTop: '.5rem' }}
                      onClick={async () => { await run(() => apiC.setUserCompanies(u.id, selectedCompanies), `Zapisano dostęp do firm: ${u.email}`); setEditingCompanies(null) }}>Zapisz dostęp</button>
                    <button className="btn btn-small btn-link" onClick={() => setEditingCompanies(null)}>Anuluj</button>
                  </td></tr>
                )}
                </Fragment>
              )
            })}
          </tbody>
        </table>
      )}
    </>
  )
}
