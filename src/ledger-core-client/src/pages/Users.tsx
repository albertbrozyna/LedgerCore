import { Fragment, useState, type FormEvent } from 'react'
import { api, type Role } from '../api'
import { useAuth } from '../auth'
import { fmtDate, roleLabel } from '../format'
import { Button, ErrorBox, Loading, Notice, PageHeader, useDialogs, useLoad } from '../design'

const roles = Object.keys(roleLabel) as Role[]

export function UsersPage() {
  const { prompt } = useDialogs()
  const { user: me } = useAuth()
  const { data, error, loading, reload } = useLoad(() => api.users(), [])
  const [f, setF] = useState({ firstName: '', lastName: '', email: '', password: '', role: 'Accountant' as Role })
  const [formError, setFormError] = useState('')
  const [notice, setNotice] = useState('')
  const [backupBusy, setBackupBusy] = useState(false)
  const companies = useLoad(() => api.allCompanies(), [])
  const [editingCompanies, setEditingCompanies] = useState<string | null>(null)
  const [selectedCompanies, setSelectedCompanies] = useState<string[]>([])

  async function openCompanies(userId: string) {
    setFormError('')
    try { setSelectedCompanies(await api.userCompanies(userId)); setEditingCompanies(userId) } catch (e) { setFormError((e as Error).message) }
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

  async function resetPassword(id: string, email: string) {
    const pw = await prompt({ title: `Zmiana hasła: ${email}`, label: 'Nowe hasło', type: 'password', hint: 'Min. 8 znaków, w tym cyfra' })
    if (pw) run(() => api.resetPassword(id, pw), `Zmieniono hasło użytkownika ${email}`)
  }

  return (
    <>
      <PageHeader title="Użytkownicy">
        <Button  disabled={backupBusy} onClick={async () => {
          setBackupBusy(true)
          await run(() => api.backup(), 'Pobrano kopię zapasową bazy danych')
          setBackupBusy(false)
        }}>Pobierz kopię zapasową</Button>
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
        <Button variant="primary">Dodaj użytkownika</Button>
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
                    <Button variant="link" small  onClick={() => resetPassword(u.id, u.email)}>Zmień hasło</Button>
                    {u.role !== 'Admin' && <Button variant="link" small  onClick={() => openCompanies(u.id)}>Firmy</Button>}
                    {!self && <Button variant="link" small  onClick={() => run(() => api.setUserActive(u.id, !u.isActive))}>{u.isActive ? 'Zablokuj' : 'Odblokuj'}</Button>}
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
                    <Button variant="primary" small  className="mt-2"
                      onClick={async () => { await run(() => api.setUserCompanies(u.id, selectedCompanies), `Zapisano dostęp do firm: ${u.email}`); setEditingCompanies(null) }}>Zapisz dostęp</Button>
                    <Button variant="link" small  onClick={() => setEditingCompanies(null)}>Anuluj</Button>
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
