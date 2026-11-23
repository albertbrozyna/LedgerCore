import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { fmtDate, roleLabel } from '../format'
import { Button, ErrorBox, Notice, PageHeader } from '../design'

export function MyAccountPage() {
  const { user } = useAuth()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [repeat, setRepeat] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(''); setNotice('')
    if (next !== repeat) { setError('Nowe hasła nie są takie same'); return }
    try {
      await api.changePassword(current, next)
      setNotice('Hasło zostało zmienione')
      setCurrent(''); setNext(''); setRepeat('')
    } catch (err) { setError((err as Error).message) }
  }

  return (
    <>
      <PageHeader title="Moje konto" />
      <div className="card">
        <dl className="meta">
          <dt>Użytkownik</dt><dd>{user?.firstName} {user?.lastName}</dd>
          <dt>E-mail</dt><dd>{user?.email}</dd>
          <dt>Rola</dt><dd>{user && roleLabel[user.role]}</dd>
          <dt>Ostatnie logowanie</dt><dd>{user?.lastLogin ? fmtDate(user.lastLogin) : '—'}</dd>
        </dl>
      </div>
      <h2>Zmiana hasła</h2>
      <ErrorBox message={error} />
      <Notice message={notice} />
      <form className="card form-row" onSubmit={submit} aria-label="Zmiana hasła">
        <label>Aktualne hasło<input type="password" value={current} onChange={e => setCurrent(e.target.value)} required autoComplete="current-password" /></label>
        <label>Nowe hasło (min. 8 znaków, w tym cyfra)<input type="password" value={next} onChange={e => setNext(e.target.value)} required autoComplete="new-password" /></label>
        <label>Powtórz nowe hasło<input type="password" value={repeat} onChange={e => setRepeat(e.target.value)} required autoComplete="new-password" /></label>
        <Button variant="primary">Zmień hasło</Button>
      </form>
    </>
  )
}
