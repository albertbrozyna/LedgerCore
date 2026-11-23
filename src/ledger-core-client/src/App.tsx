import { useEffect, useState, type ReactNode } from 'react'
import { BrowserRouter, Navigate, NavLink, Route, Routes, useNavigate } from 'react-router-dom'
import { api } from './api'
import { AuthProvider, useAuth } from './auth'
import { roleLabel } from './format'
import { Loading } from './ui'
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

function Shell({ children }: { children: ReactNode }) {
  const { user, logout, isAdmin, companies, company, switchCompany } = useAuth()
  const nav = useNavigate()
  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">LedgerCore</div>
        {companies.length > 1 ? (
          <label className="company-switch">Firma
            <select aria-label="Firma" value={company?.id ?? ''} onChange={e => switchCompany(e.target.value)}>
              {companies.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </label>
        ) : (
          company && <div className="company-name" data-testid="company-name">{company.name}</div>
        )}
        <nav aria-label="Menu główne">
          <NavLink to="/dashboard">Pulpit</NavLink>
          <NavLink to="/transactions" end>Dziennik</NavLink>
          <NavLink to="/transactions/new">Nowe księgowanie</NavLink>
          <NavLink to="/invoices">Faktury</NavLink>
          <NavLink to="/contractors">Kontrahenci</NavLink>
          <NavLink to="/bank-import">Import wyciągu</NavLink>
          <NavLink to="/accounts">Plan kont</NavLink>
          <NavLink to="/reports">Raporty</NavLink>
          {isAdmin && <NavLink to="/companies">Firmy</NavLink>}
          {isAdmin && <NavLink to="/users">Użytkownicy</NavLink>}
          {isAdmin && <NavLink to="/admin">Administracja</NavLink>}
        </nav>
        <div className="sidebar-user">
          <div data-testid="current-user">{user?.firstName} {user?.lastName}</div>
          <div className="muted small">{user && roleLabel[user.role]}</div>
          <NavLink to="/my-account" className="btn btn-link">Moje konto</NavLink><br />
          <button className="btn btn-link" onClick={() => { logout(); nav('/login') }}>Wyloguj</button>
        </div>
      </aside>
      <main className="content">{children}</main>
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
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
