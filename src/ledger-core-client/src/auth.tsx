import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, companyStore, setUnauthorizedHandler, tokens, type AccountingMode, type CompanyListItem, type Role, type UserInfo } from './api'

interface AuthState {
  user: UserInfo | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  canWrite: boolean
  isAdmin: boolean
  /** Tryb ewidencji bieżącej firmy; `fullBooks` = pełna księgowość (konta, dziennik, bilans). */
  mode: AccountingMode
  fullBooks: boolean
  /** Firmy dostępne dla użytkownika i aktualnie wybrana (wszystkie dane w aplikacji dotyczą tej firmy). */
  companies: CompanyListItem[]
  company: CompanyListItem | null
  switchCompany: (id: string) => void
}

const Ctx = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [companies, setCompanies] = useState<CompanyListItem[]>([])
  const [companyId, setCompanyId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  /** Po zalogowaniu: pobiera firmy użytkownika i wybiera zapamiętaną (lub pierwszą dostępną). */
  const loadCompanies = useCallback(async (saved: string | null) => {
    const list = await api.myCompanies()
    const chosen = list.find(c => c.id === saved) ?? list[0]
    if (chosen) companyStore.set(chosen.id)
    setCompanies(list)
    setCompanyId(chosen?.id ?? null)
  }, [])

  const logout = useCallback(() => { tokens.clear(); setUser(null); setCompanies([]); setCompanyId(null) }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null))
    if (!tokens.access) { setLoading(false); return }
    const saved = companyStore.id
    companyStore.clear() // zapamiętana firma mogła przestać być dostępna – zapytania startowe idą bez nagłówka
    api.me().then(async u => { await loadCompanies(saved); setUser(u) }).catch(() => tokens.clear()).finally(() => setLoading(false))
  }, [loadCompanies])

  const login = useCallback(async (email: string, password: string) => {
    const saved = companyStore.id
    companyStore.clear()
    await api.login(email, password)
    const u = await api.me()
    await loadCompanies(saved)
    setUser(u)
  }, [loadCompanies])

  const switchCompany = useCallback((id: string) => {
    companyStore.set(id)
    // pełne przeładowanie czyści wszystkie dane widoków poprzedniej firmy
    window.location.assign('/dashboard')
  }, [])

  const role: Role | undefined = user?.role
  const current = companies.find(c => c.id === companyId) ?? null
  const mode: AccountingMode = current?.accountingMode ?? 'FullBooks'
  return (
    <Ctx.Provider value={{
      user, loading, login, logout, canWrite: role === 'Admin' || role === 'Accountant', isAdmin: role === 'Admin', mode, fullBooks: mode === 'FullBooks',
      companies, company: companies.find(c => c.id === companyId) ?? null, switchCompany,
    }}>
      {children}
    </Ctx.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const c = useContext(Ctx)
  if (!c) throw new Error('AuthProvider missing')
  return c
}
