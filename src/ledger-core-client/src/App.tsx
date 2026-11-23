import { useEffect, useState, type ReactNode } from 'react'
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { api } from './api'
import { AuthProvider, useAuth } from './auth'
import { roleLabel } from './format'
import { Badge, Button, DialogProvider, Loading, ToastProvider, themeLabel, useTheme } from './design'
import { SetupPage, LoginPage } from './pages/AuthPages'
import { DashboardPage } from './pages/Dashboard'
import { AccountsPage } from './pages/Accounts'
import { JournalPage, NewTransactionPage, TransactionPage } from './pages/Transactions'
import { LedgerPage, ReportsPage } from './pages/Reports'
import { UsersPage } from './pages/Users'
import { ContractorsPage } from './pages/Contractors'
import { InvoicePage, InvoicesPage, NewInvoicePage } from './pages/Invoices'
import { AdminPage } from './pages/Admin'
import { CompaniesPage } from './pages/Companies'
import { CorrectionPage } from './pages/Correction'
import { MyAccountPage } from './pages/MyAccount'
import { BankImportPage } from './pages/BankImport'
import { InvoicePrintPage } from './pages/InvoicePrint'
import { DesignCatalogPage } from './pages/DesignCatalog'

const navGroups: { title: string; adminOnly?: boolean; items: { to: string; label: string; end?: boolean }[] }[] = [
  { title: 'Księga', items: [
    { to: '/transactions', label: 'Dziennik', end: true }, { to: '/transactions/new', label: 'Nowe księgowanie' },
    { to: '/bank-import', label: 'Import wyciągu' }, { to: '/accounts', label: 'Plan kont' } ] },
  { title: 'Sprzedaż i zakupy', items: [{ to: '/invoices', label: 'Faktury' }, { to: '/contractors', label: 'Kontrahenci' }] },
  { title: 'Zestawienia', items: [{ to: '/reports', label: 'Raporty' }] },
  { title: 'Zarządzanie', adminOnly: true, items: [
    { to: '/companies', label: 'Firmy' }, { to: '/users', label: 'Użytkownicy' }, { to: '/admin', label: 'Administracja' } ] },
]

const KEY_COLLAPSED = 'lc.nav.collapsed'
function readCollapsed() { try { return localStorage.getItem(KEY_COLLAPSED) === '1' } catch { return false } }

function PeriodBadge() {
  const { company } = useAuth()
  const location = useLocation()
  const [lockedThrough, setLockedThrough] = useState<string | null | undefined>(undefined)
  useEffect(() => {
    let cancelled = false
    api.periodLock().then(r => { if (!cancelled) setLockedThrough(r.lockedThrough ?? null) }).catch(() => { if (!cancelled) setLockedThrough(undefined) })
    return () => { cancelled = true }
  }, [company?.id, location.pathname])
  if (lockedThrough === undefined) return null
  return lockedThrough
    ? <Badge tone="warn"><span data-testid="period-status">Okres zamknięty do {lockedThrough.slice(0, 10)}</span></Badge>
    : <Badge tone="ok"><span data-testid="period-status">Okres otwarty</span></Badge>
}

function Shell({ children }: { children: ReactNode }) {
  const { user, logout, isAdmin, companies, company, switchCompany } = useAuth()
  const { theme, cycle } = useTheme()
  const nav = useNavigate()
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const toggle = () => setCollapsed(c => { try { localStorage.setItem(KEY_COLLAPSED, c ? '0' : '1') } catch { /* tryb prywatny */ } return !c })
  return (
    <div className="shell">
      <aside className={`sidebar${collapsed ? ' collapsed' : ''}`}>
        <div className="brand">
          <span className="brand-text">LedgerCore</span>
          <button type="button" className="sidebar-toggle" onClick={toggle} aria-expanded={!collapsed}
            aria-label={collapsed ? 'Rozwiń menu' : 'Zwiń menu'}>{collapsed ? '»' : '«'}</button>
        </div>
        <nav aria-label="Menu główne">
          <NavLink to="/dashboard" title="Pulpit"><span className="nav-label">Pulpit</span><span className="sr-only">{collapsed ? 'Pulpit' : ''}</span></NavLink>
          {navGroups.filter(g => !g.adminOnly || isAdmin).map(g => (
            <div key={g.title} role="group" aria-label={g.title}>
              <div className="nav-group-title" aria-hidden="true">{g.title}</div>
              {g.items.map(i => <NavLink key={i.to} to={i.to} end={i.end} title={i.label}><span className="nav-label">{i.label}</span><span className="sr-only">{collapsed ? i.label : ''}</span></NavLink>)}
            </div>
          ))}
        </nav>
        <div className="sidebar-user">
          <div className="user-info">
            <div data-testid="current-user">{user?.firstName} {user?.lastName}</div>
            <div className="muted small">{user && roleLabel[user.role]}</div>
            <NavLink to="/my-account" className="btn btn-link">Moje konto</NavLink><br />
          </div>
          <Button variant="link" onClick={() => { logout(); nav('/login') }}>Wyloguj</Button>
        </div>
      </aside>
      <div className="main-col">
        <header className="topbar">
          {companies.length > 1 ? (
            <label>Firma
              <select aria-label="Firma" value={company?.id ?? ''} onChange={e => switchCompany(e.target.value)}>
                {companies.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </label>
          ) : (
            company && <div className="company-name" data-testid="company-name">{company.name}</div>
          )}
          <PeriodBadge />
          <span className="grow-space" />
          <Button small onClick={cycle} aria-label={themeLabel[theme]}>{theme === 'dark' ? '☾ Ciemny' : theme === 'light' ? '☀ Jasny' : '◐ Auto'}</Button>
        </header>
        <main className="content">{children}</main>
      </div>
    </div>
  )
}

function Protected({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth()
  if (loading) return <Loading />
  if (!user) return <Navigate to="/login" replace />
  return <Shell>{children}</Shell>
}

function AdminOnly({ children }: { children: ReactNode }) {
  const { isAdmin } = useAuth()
  return isAdmin ? <>{children}</> : <Navigate to="/dashboard" replace />
}

function Home() {
  const { user, loading } = useAuth()
  const [needsSetup, setNeedsSetup] = useState<boolean | null>(null)
  useEffect(() => { api.setupStatus().then(s => setNeedsSetup(s.needsSetup)).catch(() => setNeedsSetup(false)) }, [])
  if (loading || needsSetup === null) return <Loading />
  if (needsSetup) return <Navigate to="/setup" replace />
  return <Navigate to={user ? '/dashboard' : '/login'} replace />
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
       <ToastProvider><DialogProvider>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/setup" element={<SetupPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/dashboard" element={<Protected><DashboardPage /></Protected>} />
          <Route path="/accounts" element={<Protected><AccountsPage /></Protected>} />
          <Route path="/accounts/:id/ledger" element={<Protected><LedgerPage /></Protected>} />
          <Route path="/transactions" element={<Protected><JournalPage /></Protected>} />
          <Route path="/transactions/new" element={<Protected><NewTransactionPage /></Protected>} />
          <Route path="/transactions/:id" element={<Protected><TransactionPage /></Protected>} />
          <Route path="/invoices" element={<Protected><InvoicesPage /></Protected>} />
          <Route path="/invoices/new" element={<Protected><NewInvoicePage /></Protected>} />
          <Route path="/invoices/:id" element={<Protected><InvoicePage /></Protected>} />
          <Route path="/invoices/:id/print" element={<Protected><InvoicePrintPage /></Protected>} />
          <Route path="/contractors" element={<Protected><ContractorsPage /></Protected>} />
          <Route path="/my-account" element={<Protected><MyAccountPage /></Protected>} />
          <Route path="/bank-import" element={<Protected><BankImportPage /></Protected>} />
          <Route path="/companies" element={<Protected><AdminOnly><CompaniesPage /></AdminOnly></Protected>} />
          <Route path="/invoices/:id/correction" element={<Protected><CorrectionPage /></Protected>} />
          <Route path="/admin" element={<Protected><AdminOnly><AdminPage /></AdminOnly></Protected>} />
          <Route path="/reports" element={<Protected><ReportsPage /></Protected>} />
          <Route path="/users" element={<Protected><AdminOnly><UsersPage /></AdminOnly></Protected>} />
          <Route path="*" element={<Navigate to="/" replace />} />
          {import.meta.env.DEV && <Route path="/design" element={<Protected><DesignCatalogPage /></Protected>} />}
        </Routes>
       </DialogProvider></ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
