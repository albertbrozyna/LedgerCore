import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type AccountDto, type AccountType } from '../api'
import { useAuth } from '../auth'
import { accountTypeLabel, fmtMoney } from '../format'
import { Badge, Button, DataTable, ErrorBox, Loading, Notice, PageHeader, type Column, useLoad } from '../design'

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

  const columns: Column<AccountDto>[] = [
    { key: 'code', header: 'Numer', sortable: true, sortValue: a => a.code, filterText: a => a.code, render: a => a.code },
    { key: 'name', header: 'Nazwa', sortable: true, sortValue: a => a.name, filterText: a => a.name, render: a =>
      editing === a.id ? <input aria-label="Nazwa konta" value={editName} onChange={e => setEditName(e.target.value)} />
        : <>{a.name}{!a.isActive && <Badge> nieaktywne</Badge>}</> },
    { key: 'type', header: 'Typ', sortable: true, sortValue: a => accountTypeLabel[a.type], filterText: a => accountTypeLabel[a.type], render: a => accountTypeLabel[a.type] },
    { key: 'debit', header: 'Obroty Wn', sortable: true, align: 'right', sortValue: a => a.debitTotal, render: a => fmtMoney(a.debitTotal) },
    { key: 'credit', header: 'Obroty Ma', sortable: true, align: 'right', sortValue: a => a.creditTotal, render: a => fmtMoney(a.creditTotal) },
    { key: 'balance', header: 'Saldo', sortable: true, align: 'right', sortValue: a => a.balance, render: a => fmtMoney(a.balance) },
    { key: 'actions', header: 'Akcje', fixed: true, render: a => (
      <span className="actions">
        <Link to={`/accounts/${a.id}/ledger`}>Karta konta</Link>
        {canWrite && (editing === a.id ? (
          <>
            <Button small onClick={() => save(a.id, a.isActive, editName)}>Zapisz</Button>
            <Button variant="link" small onClick={() => setEditing(null)}>Anuluj</Button>
          </>
        ) : (
          <>
            <Button variant="link" small onClick={() => { setEditing(a.id); setEditName(a.name) }}>Zmień nazwę</Button>
            <Button variant="link" small onClick={() => save(a.id, !a.isActive, a.name)}>{a.isActive ? 'Dezaktywuj' : 'Aktywuj'}</Button>
          </>
        ))}
      </span>) },
  ]


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
        <DataTable label="Plan kont" columns={columns} rows={data} rowKey={a => a.id} loading={loading}
          empty="Brak kont." rowClass={a => (a.isActive ? '' : 'inactive')} viewKey="accounts" csvName="plan-kont-widok" />
      )}
    </>
  )
}
