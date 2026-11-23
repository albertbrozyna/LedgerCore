// Cienka warstwa nad fetch: token JWT, jednorazowe odświeżenie sesji przy 401, błędy po polsku.

export type Role = 'Admin' | 'Accountant' | 'Viewer'
export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense'
export type Side = 'Debit' | 'Credit'

export interface UserInfo {
  id: string; firstName: string; lastName: string; email: string; phoneNumber?: string
  role: Role; isActive: boolean; createdAt: string; lastLogin?: string | null
}
export interface AccountDto {
  id: string; code: string; name: string; type: AccountType; isActive: boolean
  debitTotal: number; creditTotal: number; balance: number
}
export interface TxItem {
  id: string; number: string; date: string; description: string; amount: number
  entryCount: number; isReversal: boolean; isReversed: boolean
}
export interface TxList { items: TxItem[]; totalCount: number; page: number; pageSize: number }
export interface TxDetail {
  id: string; number: string; date: string; createdAt: string; description: string
  reversesTransactionId?: string | null; reversesNumber?: string | null
  reversedByTransactionId?: string | null; reversedByNumber?: string | null
  entries: { accountId: string; accountCode: string; accountName: string; amount: number; side: Side }[]
}
export interface DashboardDto {
  assets: number; liabilities: number; equity: number; monthIncome: number; monthExpenses: number
  monthResult: number; accountCount: number; transactionCount: number
  receivables: number; receivablesOverdue: number; payables: number; payablesOverdue: number
  months: { month: string; income: number; expenses: number }[]
  recent: { id: string; number: string; date: string; description: string; amount: number }[]
}
export interface TrialRow {
  accountId: string; code: string; name: string; type: AccountType
  opening: number; debitTurnover: number; creditTurnover: number; closing: number
}
export interface TrialBalanceDto { rows: TrialRow[]; totalDebitTurnover: number; totalCreditTurnover: number }
export interface LedgerDto {
  accountId: string; code: string; name: string; type: AccountType; openingBalance: number
  lines: { transactionId: string; number: string; date: string; description: string; debit: number; credit: number; balance: number }[]
  totalDebit: number; totalCredit: number; closingBalance: number
}
export interface PnlDto {
  income: { accountId: string; code: string; name: string; amount: number }[]
  expenses: { accountId: string; code: string; name: string; amount: number }[]
  totalIncome: number; totalExpenses: number; result: number
}

export class ApiError extends Error {
  status: number
  constructor(message: string, status: number) { super(message); this.status = status }
}

const KEY_ACCESS = 'lc.access'
const KEY_REFRESH = 'lc.refresh'
const KEY_COMPANY = 'lc.company'
export const companyStore = {
  get id() { try { return localStorage.getItem(KEY_COMPANY) } catch { return null } },
  set(id: string) { localStorage.setItem(KEY_COMPANY, id) },
  clear() { localStorage.removeItem(KEY_COMPANY) },
}

export const tokens = {
  get access() { try { return localStorage.getItem(KEY_ACCESS) } catch { return null } },
  get refresh() { try { return localStorage.getItem(KEY_REFRESH) } catch { return null } },
  set(access: string, refresh: string) { localStorage.setItem(KEY_ACCESS, access); localStorage.setItem(KEY_REFRESH, refresh) },
  clear() { localStorage.removeItem(KEY_ACCESS); localStorage.removeItem(KEY_REFRESH) },
}

let onUnauthorized: () => void = () => {}
export function setUnauthorizedHandler(fn: () => void) { onUnauthorized = fn }

const BASE = '/api/v1'

async function errorFrom(res: Response): Promise<ApiError> {
  let message = `Błąd serwera (${res.status})`
  try {
    const body = await res.json()
    if (body?.errors && typeof body.errors === 'object') {
      message = Object.values(body.errors as Record<string, string[]>).flat().join('. ')
    } else if (body?.detail) message = body.detail
    else if (typeof body === 'string') message = body
  } catch { /* brak treści JSON */ }
  return new ApiError(message, res.status)
}

let refreshing: Promise<boolean> | null = null
async function tryRefresh(): Promise<boolean> {
  if (!tokens.refresh) return false
  refreshing ??= (async () => {
    try {
      const res = await fetch(`${BASE}/auth/refresh`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokens.refresh }),
      })
      if (!res.ok) return false
      const data = await res.json()
      tokens.set(data.accessToken, data.refreshToken)
      return true
    } catch { return false } finally { setTimeout(() => { refreshing = null }, 0) }
  })()
  return refreshing
}

async function raw(method: string, path: string, body?: unknown, retry = true): Promise<Response> {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (tokens.access) headers.Authorization = `Bearer ${tokens.access}`
  if (companyStore.id) headers['X-Company-Id'] = companyStore.id
  const res = await fetch(`${BASE}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  if (res.status === 401 && retry && !path.startsWith('/auth/login') && tokens.refresh) {
    if (await tryRefresh()) return raw(method, path, body, false)
    tokens.clear(); onUnauthorized()
  }
  if (!res.ok) throw await errorFrom(res)
  return res
}

async function json<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await raw(method, path, body)
  return res.status === 204 ? (undefined as T) : res.json()
}

const qs = (p: Record<string, string | number | undefined | null>) => {
  const e = Object.entries(p).filter(([, v]) => v !== undefined && v !== null && v !== '')
  return e.length ? '?' + new URLSearchParams(e.map(([k, v]) => [k, String(v)])).toString() : ''
}

async function download(path: string, fallbackName: string) {
  const res = await raw('GET', path)
  const blob = await res.blob()
  const cd = res.headers.get('content-disposition') ?? ''
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd)?.[1] ?? fallbackName
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = decodeURIComponent(name); a.click()
  URL.revokeObjectURL(url)
}

export const api = {
  setupStatus: () => json<{ needsSetup: boolean }>('GET', '/setup/status'),
  setup: (b: { firstName: string; lastName: string; email: string; password: string; companyName?: string }) => json('POST', '/setup', b),
  login: async (email: string, password: string) => {
    const r = await json<{ userId: string; accessToken: string; refreshToken: string }>('POST', '/auth/login', { email, password })
    tokens.set(r.accessToken, r.refreshToken)
  },
  me: () => json<UserInfo>('GET', '/auth/me'),

  accounts: (includeInactive = false) => json<AccountDto[]>('GET', `/accounts${qs({ includeInactive: includeInactive ? 'true' : undefined })}`),
  createAccount: (b: { name: string; code: string; type: AccountType }) => json('POST', '/accounts', b),
  updateAccount: (id: string, b: { name: string; isActive: boolean }) => json<void>('PUT', `/accounts/${id}`, b),
  ledger: (id: string, from?: string, to?: string) => json<LedgerDto>('GET', `/accounts/${id}/ledger${qs({ from, to })}`),

  transactions: (p: { from?: string; to?: string; accountId?: string; search?: string; page?: number; pageSize?: number }) =>
    json<TxList>('GET', `/transactions${qs(p)}`),
  transaction: (id: string) => json<TxDetail>('GET', `/transactions/${id}`),
  createTransaction: (b: { description: string; date: string; entries: { accountId: string; amount: number; side: Side }[] }) =>
    json<{ transactionId: string; number: string }>('POST', '/transactions', b),
  reverseTransaction: (id: string, date?: string) =>
    json<{ transactionId: string; number: string }>('POST', `/transactions/${id}/reverse`, { date }),
  exportJournal: (from?: string, to?: string) => download(`/transactions/export${qs({ from, to })}`, 'dziennik.csv'),

  dashboard: () => json<DashboardDto>('GET', '/reports/dashboard'),
  trialBalance: (from?: string, to?: string) => json<TrialBalanceDto>('GET', `/reports/trial-balance${qs({ from, to })}`),
  profitAndLoss: (from: string, to: string) => json<PnlDto>('GET', `/reports/profit-and-loss${qs({ from, to })}`),

  users: () => json<UserInfo[]>('GET', '/users'),
  createUser: (b: { firstName: string; lastName: string; email: string; password: string; role: Role }) =>
    json('POST', '/users/create', { ...b, phoneNumber: '' }),
  setUserActive: (id: string, active: boolean) => json<void>('POST', `/users/${id}/${active ? 'unblock' : 'block'}`),
  setUserRole: (id: string, role: Role) => json<void>('PUT', `/users/${id}/role`, { role }),
  resetPassword: (id: string, newPassword: string) => json<void>('POST', `/users/${id}/password`, { newPassword }),
  backup: () => download('/admin/backup', 'kopia.db'),
}

// ---- Faktury, kontrahenci, VAT, administracja ----
export type InvoiceType = 'Sales' | 'Purchase'
export type VatRate = 'R23' | 'R8' | 'R5' | 'R0' | 'Exempt'
export type InvoiceStatus = 'unpaid' | 'overdue' | 'paid' | 'canceled' | 'correction'

export interface ContractorDto {
  id: string; name: string; nip?: string | null; address?: string | null; email?: string | null
  isActive: boolean; receivables: number; payables: number
}
export interface InvoiceItem {
  id: string; type: InvoiceType; number: string; contractorId: string; contractorName: string
  issueDate: string; dueDate: string; net: number; vat: number; gross: number
  paidAmount: number; remaining: number; status: InvoiceStatus
  isCorrection: boolean; correctedInvoiceId?: string | null; correctedNumber?: string | null
}
export interface InvoiceList { items: InvoiceItem[]; totalCount: number; page: number; pageSize: number; totalGross: number; totalRemaining: number }
export interface InvoiceDetail {
  id: string; type: InvoiceType; number: string; contractorId: string; contractorName: string; contractorNip?: string | null
  issueDate: string; saleDate: string; dueDate: string; net: number; vat: number; gross: number
  paidAmount: number; remaining: number; status: InvoiceStatus; notes?: string | null
  transactionId?: string | null; transactionNumber?: string | null
  lines: { description: string; net: number; vatRate: VatRate; vat: number; gross: number }[]
  payments: { id: string; date: string; amount: number; accountCode: string; accountName: string; transactionId: string }[]
  isCorrection: boolean; correctedInvoiceId?: string | null; correctedNumber?: string | null
  correctedAmount: number; grossAfterCorrections: number
  corrections: { id: string; number: string; issueDate: string; gross: number; isCanceled: boolean }[]
}
export interface VatRegisterDto {
  rows: { invoiceId: string; number: string; issueDate: string; saleDate: string; contractorName: string; nip?: string | null; net: number; vat: number; gross: number }[]
  byRate: { rate: VatRate; net: number; vat: number }[]
  totalNet: number; totalVat: number; totalGross: number
}
export interface VatSummaryDto { outputNet: number; outputVat: number; inputNet: number; inputVat: number; vatDue: number }
export interface SettlementsDto {
  rows: { contractorId: string; contractorName: string; nip?: string | null; total: number; notDue: number; days1To30: number; days31To60: number; over60: number; invoiceCount: number }[]
  total: number; overdue: number
}
export interface AuditList { items: { id: string; at: string; userName: string; action: string; details: string }[]; totalCount: number; page: number; pageSize: number }

export const apiX = {
  contractors: (search?: string, includeInactive = false) =>
    json<ContractorDto[]>('GET', `/contractors${qs({ search, includeInactive: includeInactive ? 'true' : undefined })}`),
  saveContractor: (id: string | null, b: { name: string; nip?: string; address?: string; email?: string; isActive?: boolean }) =>
    id ? json<void>('PUT', `/contractors/${id}`, b) : json<{ id: string }>('POST', '/contractors', b),

  invoices: (p: { type?: InvoiceType; contractorId?: string; from?: string; to?: string; status?: string; search?: string; page?: number; pageSize?: number }) =>
    json<InvoiceList>('GET', `/invoices${qs(p)}`),
  invoice: (id: string) => json<InvoiceDetail>('GET', `/invoices/${id}`),
  createInvoice: (b: {
    type: InvoiceType; number?: string; contractorId: string; issueDate: string; saleDate?: string; dueDate: string
    resultAccountId?: string; notes?: string; lines: { description: string; net: number; vatRate: VatRate }[]
  }) => json<{ invoiceId: string; number: string; transactionId: string }>('POST', '/invoices', b),
  addPayment: (id: string, b: { date: string; amount: number; accountId: string }) => json<void>('POST', `/invoices/${id}/payments`, b),
  cancelInvoice: (id: string) => json<void>('POST', `/invoices/${id}/cancel`),

  vatRegister: (type: InvoiceType, from: string, to: string) => json<VatRegisterDto>('GET', `/reports/vat-register${qs({ type, from, to })}`),
  vatSummary: (from: string, to: string) => json<VatSummaryDto>('GET', `/reports/vat-summary${qs({ from, to })}`),
  settlements: (type: InvoiceType) => json<SettlementsDto>('GET', `/reports/settlements${qs({ type })}`),

  periodLock: () => json<{ lockedThrough?: string | null }>('GET', '/admin/period-lock'),
  setPeriodLock: (lockedThrough: string | null) => json<void>('PUT', '/admin/period-lock', { lockedThrough }),
  audit: (p: { search?: string; page?: number; pageSize?: number }) => json<AuditList>('GET', `/admin/audit${qs(p)}`),
}

// ---- Dane firmy, rok obrotowy, bilans ----
export interface CompanyDto { name: string; nip?: string | null; address?: string | null; email?: string | null; phone?: string | null; bankAccount?: string | null; invoiceFooter?: string | null }
export interface BalanceSheetDto {
  asOf: string
  assets: { accountId: string; code: string; name: string; amount: number }[]
  liabilities: { accountId: string; code: string; name: string; amount: number }[]
  equity: { accountId: string; code: string; name: string; amount: number }[]
  currentResult: number; totalAssets: number; totalLiabilitiesAndEquity: number
}
export const apiY = {
  company: () => json<CompanyDto>('GET', '/admin/company'),
  saveCompany: (b: CompanyDto) => json<void>('PUT', '/admin/company', b),
  closeYear: (year: number) => json<{ number: string; result: number }>('POST', '/admin/close-year', { year }),
  balanceSheet: (asOf: string) => json<BalanceSheetDto>('GET', `/reports/balance-sheet${qs({ asOf })}`),
}

// ---- Kopie zapasowe, szablony, załączniki, hasło ----
export interface BackupItem { name: string; size: number; created: string; manual: boolean }
export interface TemplateDto { id: string; name: string; description: string; lines: { accountId: string; side: Side; amount?: number | null }[] }
export interface AttachmentDto { id: string; fileName: string; contentType: string; size: number; uploadedAt: string }

export const apiZ = {
  backups: () => json<BackupItem[]>('GET', '/admin/backups'),
  createBackup: () => json<{ name: string }>('POST', '/admin/backups'),
  downloadBackup: (name: string) => download(`/admin/backups/${encodeURIComponent(name)}`, name),
  changePassword: (currentPassword: string, newPassword: string) => json<void>('POST', '/auth/change-password', { currentPassword, newPassword }),

  templates: () => json<TemplateDto[]>('GET', '/templates'),
  saveTemplate: (b: { name: string; description?: string; lines: { accountId: string; side: Side; amount?: number | null }[] }) =>
    json<{ id: string }>('POST', '/templates', b),
  deleteTemplate: (id: string) => json<void>('DELETE', `/templates/${id}`),

  attachments: (invoiceId: string) => json<AttachmentDto[]>('GET', `/invoices/${invoiceId}/attachments`),
  uploadAttachment: async (invoiceId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    const send = () => fetch(`${BASE}/invoices/${invoiceId}/attachments`, {
      method: 'POST', headers: { ...(tokens.access ? { Authorization: `Bearer ${tokens.access}` } : {}), ...(companyStore.id ? { 'X-Company-Id': companyStore.id } : {}) }, body: form,
    })
    let res = await send()
    if (res.status === 401 && (await tryRefresh())) res = await send()
    if (!res.ok) throw await errorFrom(res)
  },
  downloadAttachment: (a: AttachmentDto) => download(`/attachments/${a.id}`, a.fileName),
  deleteAttachment: (id: string) => json<void>('DELETE', `/attachments/${id}`),
}

// ---- Firmy i korekty ----
export interface CompanyListItem { id: string; name: string; nip?: string | null }
export interface CompanyAdminItem { id: string; name: string; nip?: string | null; address?: string | null; isActive: boolean; createdAt: string }

export const apiC = {
  myCompanies: () => json<CompanyListItem[]>('GET', '/companies/mine'),
  allCompanies: () => json<CompanyAdminItem[]>('GET', '/companies'),
  createCompany: (b: { name: string; nip?: string; address?: string; seedChartOfAccounts: boolean }) => json<{ id: string }>('POST', '/companies', b),
  updateCompany: (id: string, b: { name: string; isActive: boolean }) => json<void>('PUT', `/companies/${id}`, b),
  userCompanies: (userId: string) => json<string[]>('GET', `/users/${userId}/companies`),
  setUserCompanies: (userId: string, companyIds: string[]) => json<void>('PUT', `/users/${userId}/companies`, { companyIds }),

  createCorrection: (invoiceId: string, b: { number?: string; issueDate: string; reason?: string; lines: { description: string; net: number; vatRate: VatRate }[] }) =>
    json<{ invoiceId: string; number: string; transactionId: string }>('POST', `/invoices/${invoiceId}/corrections`, b),
}
