import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type AccountType } from '../api'
import { useAuth } from '../auth'
import { accountTypeLabel, fmtMoney } from '../format'
import { Badge, Button, ErrorBox, Loading, Notice, PageHeader, useLoad } from '../design'

const types = Object.keys(accountTypeLabel) as AccountType[]

export function AccountsPage() {
  const { canWrite } = useAuth()
  const [showInactive, setShowInactive] = useState(false)
  const { data, error, loading, reload } = useLoad(() => api.accounts(showInactive), [showInactive])
  const [form, setForm] = useState({ code: '', name: '', type: 'Asset' as AccountType })
  const [formError, setFormError] = useState('')
  const [notice, setNotice] = useState('')
  const [editing, setEditing] = useState<string | null>(null)
  const [editName, setEditName] = useState('')

  async function add(e: FormEvent) {
    e.preventDefault()
    setFormError(''); setNotice('')
    try {
      await api.createAccount(form)
      setNotice(`Dodano konto ${form.code} ${form.name}`)
      setForm({ code: '', name: '', type: form.type })
      reload()
    } catch (err) { setFormError((err as Error).message) }
  }

  async function save(id: string, isActive: boolean, name: string) {
    setFormError(''); setNotice('')
    try { await api.updateAccount(id, { name, isActive }); setEditing(null); reload() }
    catch (err) { setFormError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title="Plan kont">
        <label className="inline"><input type="checkbox" checked={showInactive} onChange={e => setShowInactive(e.target.checked)} /> Pokaż nieaktywne</label>
      </PageHeader>
      <ErrorBox message={error || formError} />
      <Notice message={notice} />

      {canWrite && (
        <form className="card form-row" onSubmit={add} aria-label="Nowe konto">
          <label>Numer<input value={form.code} onChange={e => setForm({ ...form, code: e.target.value })} required maxLength={30} /></label>
          <label className="grow">Nazwa<input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} required maxLength={200} /></label>
          <label>Typ
            <select value={form.type} onChange={e => setForm({ ...form, type: e.target.value as AccountType })}>
              {types.map(t => <option key={t} value={t}>{accountTypeLabel[t]}</option>)}
            </select>
          </label>
          <Button variant="primary">Dodaj konto</Button>
        </form>
      )}

      {loading && !data && <Loading />}
      {data && (
        <table>
          <thead><tr><th>Numer</th><th>Nazwa</th><th>Typ</th><th className="num">Obroty Wn</th><th className="num">Obroty Ma</th><th className="num">Saldo</th><th></th></tr></thead>
          <tbody>
            {data.map(a => (
              <tr key={a.id} className={a.isActive ? '' : 'inactive'}>
                <td>{a.code}</td>
                <td>
                  {editing === a.id ? (
                    <input aria-label="Nazwa konta" value={editName} onChange={e => setEditName(e.target.value)} />
                  ) : (<>{a.name}{!a.isActive && <Badge> nieaktywne</Badge>}</>)}
                </td>
                <td>{accountTypeLabel[a.type]}</td>
                <td className="num">{fmtMoney(a.debitTotal)}</td>
                <td className="num">{fmtMoney(a.creditTotal)}</td>
                <td className="num">{fmtMoney(a.balance)}</td>
                <td className="actions">
                  <Link to={`/accounts/${a.id}/ledger`}>Karta konta</Link>
                  {canWrite && (editing === a.id ? (
                    <>
                      <Button small  onClick={() => save(a.id, a.isActive, editName)}>Zapisz</Button>
                      <Button variant="link" small  onClick={() => setEditing(null)}>Anuluj</Button>
                    </>
                  ) : (
                    <>
                      <Button variant="link" small  onClick={() => { setEditing(a.id); setEditName(a.name) }}>Zmień nazwę</Button>
                      <Button variant="link" small  onClick={() => save(a.id, !a.isActive, a.name)}>{a.isActive ? 'Dezaktywuj' : 'Aktywuj'}</Button>
                    </>
                  ))}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  )
}
