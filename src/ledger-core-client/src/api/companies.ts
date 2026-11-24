import { json } from './http'
import type { AccountingMode, CompanyAdminItem, CompanyListItem } from './types'

/** Firmy (klienci biura) i ich przypisanie do użytkowników. */
export const companiesApi = {
  myCompanies: () => json<CompanyListItem[]>('GET', '/companies/mine'),
  allCompanies: () => json<CompanyAdminItem[]>('GET', '/companies'),
  createCompany: (b: { name: string; nip?: string; address?: string; seedChartOfAccounts: boolean; mode?: AccountingMode }) => json<{ id: string }>('POST', '/companies', b),
  updateCompany: (id: string, b: { name: string; isActive: boolean }) => json<void>('PUT', `/companies/${id}`, b),
  userCompanies: (userId: string) => json<string[]>('GET', `/users/${userId}/companies`),
  setUserCompanies: (userId: string, companyIds: string[]) => json<void>('PUT', `/users/${userId}/companies`, { companyIds }),
}
