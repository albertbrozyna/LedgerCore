import { download, json, qs, tokens } from './http'
import type { AccountDto, AccountType, LedgerDto, Side, TemplateDto, TemplateLine, TxDetail, TxList, UserInfo } from './types'

/** Logowanie, konfiguracja początkowa, plan kont, księgowania i szablony. */
export const accountingApi = {
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
  ledger: (id: string, from?: string, to?: string, page?: number, pageSize?: number) =>
    json<LedgerDto>('GET', `/accounts/${id}/ledger${qs({ from, to, page, pageSize })}`),

  transactions: (p: { from?: string; to?: string; accountId?: string; search?: string; page?: number; pageSize?: number }) =>
    json<TxList>('GET', `/transactions${qs(p)}`),
  transaction: (id: string) => json<TxDetail>('GET', `/transactions/${id}`),
  createTransaction: (b: { description: string; date: string; entries: { accountId: string; amount: number; side: Side }[] }) =>
    json<{ transactionId: string; number: string }>('POST', '/transactions', b),
  reverseTransaction: (id: string, date?: string) =>
    json<{ transactionId: string; number: string }>('POST', `/transactions/${id}/reverse`, { date }),
  exportJournal: (from?: string, to?: string) => download(`/transactions/export${qs({ from, to })}`, 'dziennik.csv'),

  templates: () => json<TemplateDto[]>('GET', '/templates'),
  saveTemplate: (b: { name: string; description?: string; lines: TemplateLine[] }) => json<{ id: string }>('POST', '/templates', b),
  deleteTemplate: (id: string) => json<void>('DELETE', `/templates/${id}`),
}
