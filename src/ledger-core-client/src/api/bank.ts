import { json, qs } from './http'
import type { BankFormat, BankImportResult, BankReconciliationDto, BankRowDto, BankRuleDto, BankStatementDto, BankTxStatus, CsvProfile, RuleDirection } from './types'

/** Import wyciągów (CSV, MT940, Elixir-0), reguły księgowania i uzgodnienie salda z bankiem. */
export const bankApi = {
  importBank: (b: { accountId: string; format: BankFormat; profile?: CsvProfile; content: string; fileName?: string; ownAccount?: string; applyRules?: boolean }) =>
    json<BankImportResult>('POST', '/bank/import', b),
  bankTransactions: (p: { accountId?: string; status?: BankTxStatus; statementId?: string }) => json<BankRowDto[]>('GET', `/bank/transactions${qs(p)}`),
  postBankToInvoice: (id: string, invoiceId: string) => json<{ amount: number }>('POST', `/bank/transactions/${id}/post-invoice`, { invoiceId }),
  skipBankTransaction: (id: string, skip: boolean) => json<void>('PUT', `/bank/transactions/${id}/skip`, { skip }),
  applyBankRules: (accountId: string) => json<{ posted: number; matched: number; errors: string[] }>('POST', '/bank/apply-rules', { accountId }),
  bankRules: () => json<BankRuleDto[]>('GET', '/bank/rules'),
  saveBankRule: (id: string | null, b: { name: string; matchText: string; direction: RuleDirection; targetAccountId: string; description?: string; isActive?: boolean }) =>
    id ? json<{ id: string }>('PUT', `/bank/rules/${id}`, b) : json<{ id: string }>('POST', '/bank/rules', b),
  deleteBankRule: (id: string) => json<void>('DELETE', `/bank/rules/${id}`),
  bankStatements: (accountId: string) => json<BankStatementDto[]>('GET', `/bank/statements${qs({ accountId })}`),
  bankReconciliation: (accountId: string, asOf?: string, closingBalance?: number) => json<BankReconciliationDto>('GET', `/bank/reconciliation${qs({ accountId, asOf, closingBalance })}`),
}
