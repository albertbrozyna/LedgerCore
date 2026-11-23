import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, type ContractorDto } from '../api'
import { useAuth } from '../auth'
import { fmtMoney, fmtMoneyOrDash } from '../format'
import { Badge, Button, ErrorBox, Loading, Notice, PageHeader, useLoad } from '../design'

const empty = { name: '', nip: '', address: '', email: '' }

export function ContractorsPage() {
  const { canWrite } = useAuth()
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const { data, error, loading, reload } = useLoad(() => api.contractors(search, showInactive), [search, showInactive])
  const [editing, setEditing] = useState<ContractorDto | null>(null)
  const [f, setF] = useState(empty)
  const [formError, setFormError] = useState('')
  const [notice, setNotice] = useState('')

  function startEdit(c: ContractorDto | null) {
    setEditing(c)
    setF(c ? { name: c.name, nip: c.nip ?? '', address: c.address ?? '', email: c.email ?? '' } : empty)
    setFormError(''); setNotice('')
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setFormError(''); setNotice('')
    try {
      await api.saveContractor(editing?.id ?? null, { ...f, isActive: editing?.isActive ?? true })
      setNotice(editing ? `Zapisano zmiany: ${f.name}` : `Dodano kontrahenta ${f.name}`)
      startEdit(null); setNotice(editing ? `Zapisano zmiany: ${f.name}` : `Dodano kontrahenta ${f.name}`)
      reload()
    } catch (err) { setFormError((err as Error).message) }
  }

  async function toggle(c: ContractorDto) {
    setFormError('')
    try { await api.saveContractor(c.id, { name: c.name, nip: c.nip ?? '', address: c.address ?? '', email: c.email ?? '', isActive: !c.isActive }); reload() }
    catch (err) { setFormError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title="Kontrahenci">
        <label className="inline"><input type="checkbox" checked={showInactive} onChange={e => setShowInactive(e.target.checked)} /> Pokaż nieaktywnych</label>
      </PageHeader>
      <ErrorBox message={error || formError} />
      <Notice message={notice} />

      {canWrite && (
        <form className="card form-row" onSubmit={submit} aria-label="Kontrahent">
          <label className="grow">Nazwa<input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} required maxLength={200} /></label>
          <label>NIP<input value={f.nip} onChange={e => setF({ ...f, nip: e.target.value })} placeholder="opcjonalnie" /></label>
          <label className="grow">Adres<input value={f.address} onChange={e => setF({ ...f, address: e.target.value })} /></label>
          <label>E-mail<input type="email" value={f.email} onChange={e => setF({ ...f, email: e.target.value })} /></label>
          <Button variant="primary">{editing ? 'Zapisz zmiany' : 'Dodaj kontrahenta'}</Button>
          {editing && <Button type="button"  onClick={() => startEdit(null)}>Anuluj</Button>}
        </form>
      )}

      <div className="card form-row">
        <label className="grow">Szukaj<input placeholder="nazwa lub NIP" value={search} onChange={e => setSearch(e.target.value)} /></label>
      </div>

      {loading && !data && <Loading />}
      {data && (data.length === 0 ? <p className="muted">Brak kontrahentów.</p> : (
        <table>
          <thead><tr><th>Nazwa</th><th>NIP</th><th>Adres</th><th className="num">Należności</th><th className="num">Zobowiązania</th><th></th></tr></thead>
          <tbody>
            {data.map(c => (
              <tr key={c.id} className={c.isActive ? '' : 'inactive'}>
                <td>{c.name}{!c.isActive && <Badge> nieaktywny</Badge>}</td>
                <td>{c.nip ?? ''}</td><td>{c.address ?? ''}</td>
                <td className="num">{fmtMoneyOrDash(c.receivables)}</td>
                <td className="num">{fmtMoneyOrDash(c.payables)}</td>
                <td className="actions">
                  <Link to={`/invoices?contractorId=${c.id}`}>Faktury</Link>
                  {canWrite && <>
                    <Button variant="link" small  onClick={() => startEdit(c)}>Edytuj</Button>
                    <Button variant="link" small  onClick={() => toggle(c)}>{c.isActive ? 'Dezaktywuj' : 'Aktywuj'}</Button>
                  </>}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot><tr><td colSpan={3}>Razem</td>
            <td className="num">{fmtMoney(data.reduce((s, c) => s + c.receivables, 0))}</td>
            <td className="num">{fmtMoney(data.reduce((s, c) => s + c.payables, 0))}</td><td></td></tr></tfoot>
        </table>
      ))}
    </>
  )
}
