import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'
import { Button, ErrorBox } from '../design'

export function SetupPage() {
  const { login } = useAuth()
  const nav = useNavigate()
  const [allowed, setAllowed] = useState<boolean | null>(null)
  const [f, setF] = useState({ companyName: 'Moja firma', firstName: '', lastName: '', email: '', password: '', password2: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => { api.setupStatus().then(s => setAllowed(s.needsSetup)).catch(() => setAllowed(false)) }, [])
  if (allowed === false) return <Navigate to="/login" replace />
  if (allowed === null) return null

  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (f.password !== f.password2) { setError('Hasła nie są takie same'); return }
    setBusy(true)
    try {
      await api.setup({ companyName: f.companyName, firstName: f.firstName, lastName: f.lastName, email: f.email, password: f.password })
      await login(f.email, f.password)
      nav('/dashboard')
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <div className="auth-wrap">
      <form className="card auth-card" onSubmit={submit}>
        <h1>Witaj w LedgerCore</h1>
        <p className="muted">Pierwsze uruchomienie – utwórz konto administratora. Zostanie też dodany domyślny plan kont, który możesz później zmienić.</p>
        <ErrorBox message={error} />
        <label>Nazwa firmy (możesz dodać kolejne później)<input value={f.companyName} onChange={set('companyName')} required maxLength={200} /></label>
        <label>Imię<input value={f.firstName} onChange={set('firstName')} required /></label>
        <label>Nazwisko<input value={f.lastName} onChange={set('lastName')} required /></label>
        <label>E-mail<input type="email" value={f.email} onChange={set('email')} required /></label>
        <label>Hasło (min. 8 znaków, w tym cyfra)<input type="password" value={f.password} onChange={set('password')} required /></label>
        <label>Powtórz hasło<input type="password" value={f.password2} onChange={set('password2')} required /></label>
        <Button variant="primary"  disabled={busy}>{busy ? 'Tworzenie…' : 'Utwórz konto administratora'}</Button>
      </form>
    </div>
  )
}

export function LoginPage() {
  const { user, login } = useAuth()
  const nav = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [needsSetup, setNeedsSetup] = useState(false)

  useEffect(() => { api.setupStatus().then(s => setNeedsSetup(s.needsSetup)).catch(() => {}) }, [])
  if (user) return <Navigate to="/dashboard" replace />
  if (needsSetup) return <Navigate to="/setup" replace />

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(''); setBusy(true)
    try { await login(email, password); nav('/dashboard') }
    catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <div className="auth-wrap">
      <form className="card auth-card" onSubmit={submit}>
        <h1>LedgerCore</h1>
        <p className="muted">Zaloguj się, aby kontynuować.</p>
        <ErrorBox message={error} />
        <label>E-mail<input type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus /></label>
        <label>Hasło<input type="password" value={password} onChange={e => setPassword(e.target.value)} required /></label>
        <Button variant="primary"  disabled={busy}>{busy ? 'Logowanie…' : 'Zaloguj'}</Button>
      </form>
    </div>
  )
}
