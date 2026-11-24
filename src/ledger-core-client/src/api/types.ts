// Typy DTO zwracane przez API (zgodne z kontraktem backendu).

export type Role = 'Admin' | 'Accountant' | 'Viewer'
export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense'
export type Side = 'Debit' | 'Credit'
export type AccountingMode = 'FullBooks' | 'Kpir' | 'Ryczalt'
export type KpirKind = 'Revenue' | 'OtherRevenue' | 'GoodsPurchase' | 'PurchaseCosts' | 'OtherExpense' | 'Research'
export type InvoiceType = 'Sales' | 'Purchase'
export type VatRate = 'R23' | 'R8' | 'R5' | 'R0' | 'Exempt'
export type InvoiceStatus = 'unpaid' | 'overdue' | 'paid' | 'canceled' | 'correction'

// ---- Użytkownicy ----
export interface UserInfo {
  id: string; firstName: string; lastName: string; email: string; phoneNumber?: string
  role: Role; isActive: boolean; createdAt: string; lastLogin?: string | null
}

// ---- Księga ----
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
export interface TemplateLine { accountId: string; side: Side; amount?: number | null }
export interface TemplateDto { id: string; name: string; description: string; lines: TemplateLine[] }

// ---- Raporty ----
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
  totalCount: number; page: number; pageSize: number
}
export interface ReportRow { accountId: string; code: string; name: string; amount: number }
export interface PnlDto {
  income: ReportRow[]; expenses: ReportRow[]
  totalIncome: number; totalExpenses: number; result: number
}
export interface BalanceSheetDto {
  asOf: string
  assets: ReportRow[]; liabilities: ReportRow[]; equity: ReportRow[]
  currentResult: number; totalAssets: number; totalLiabilitiesAndEquity: number
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

// ---- Faktury i kontrahenci ----
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
  payments: { id: string; date: string; amount: number; accountCode: string; accountName: string; transactionId: string | null }[]
  isCorrection: boolean; correctedInvoiceId?: string | null; correctedNumber?: string | null
  correctedAmount: number; grossAfterCorrections: number
  corrections: { id: string; number: string; issueDate: string; gross: number; isCanceled: boolean }[]
  kpirKind?: KpirKind | null; ryczaltRate?: number | null
}
export interface InvoiceLineInput { description: string; net: number; vatRate: VatRate }
export interface AttachmentDto { id: string; fileName: string; contentType: string; size: number; uploadedAt: string }

// ---- Firmy i administracja ----
export interface CompanyDto { accountingMode?: AccountingMode; name: string; nip?: string | null; address?: string | null; email?: string | null; phone?: string | null; bankAccount?: string | null; invoiceFooter?: string | null }
export interface CompanyListItem { id: string; name: string; nip?: string | null; accountingMode: AccountingMode }
export interface CompanyAdminItem { id: string; name: string; nip?: string | null; address?: string | null; isActive: boolean; createdAt: string; accountingMode: AccountingMode }
export interface BackupItem { name: string; size: number; created: string; manual: boolean }
export interface AuditItem { id: string; at: string; userName: string; action: string; details: string }
export interface AuditList { items: AuditItem[]; totalCount: number; page: number; pageSize: number }

// ---- Wyszukiwarka globalna ----
export interface SearchResult {
  accounts: { id: string; code: string; name: string }[]
  contractors: { id: string; name: string; nip?: string | null }[]
  invoices: { id: string; number: string; contractorName: string; gross: number; isCanceled: boolean }[]
  transactions: { id: string; number: string; description: string; date: string }[]
}

// ---- Ewidencje KPiR i ryczałtu ----
export interface KpirTotals {
  revenue: number; otherRevenue: number; totalRevenue: number; goodsPurchase: number; purchaseCosts: number
  wages: number; otherExpense: number; totalExpense: number; research: number
}
export interface KpirRow extends KpirTotals {
  lp: number; invoiceId: string; date: string; document: string; contractor: string; address?: string | null
  description: string; remarks?: string | null
}
export interface KpirDto {
  rows: KpirRow[]; period: KpirTotals; yearToDate: KpirTotals; income: number; advanceInfo: number
  months: { month: string; period: KpirTotals; yearToDate: KpirTotals; income: number; advanceInfo: number }[]
}
export interface RyczaltRateTotal { rate: number; revenue: number; tax: number }
export interface RyczaltDto {
  rows: { lp: number; invoiceId: string; date: string; document: string; contractor: string; description: string; rate: number; revenue: number }[]
  totalRevenue: number; byRate: RyczaltRateTotal[]; yearToDateByRate: RyczaltRateTotal[]; yearToDateTax: number
  months: { month: string; revenue: number; yearToDateRevenue: number; yearToDateTax: number }[]
}

// ---- Windykacja: wezwania, noty odsetkowe, poczta, stopy odsetek ----
export type NoticeKind = 'Reminder' | 'InterestNote'
export type NoticeChannel = 'Print' | 'Email'
export type NoticeStatus = 'Issued' | 'Sent' | 'Failed'
export interface OverdueItem {
  invoiceId: string; number: string; contractorId: string; contractorName: string; contractorEmail?: string | null
  issueDate: string; dueDate: string; gross: number; remaining: number; daysOverdue: number; interest?: number | null
}
export interface OverdueDto { items: OverdueItem[]; interestError?: string | null }
export interface NoticeRequest { contractorId: string; invoiceIds?: string[]; kind: NoticeKind; asOf: string; paymentDays: number; includeInterest: boolean }
export interface NoticeLine {
  invoiceId: string; number: string; issueDate: string; dueDate: string; amountDue: number; principal: number; days: number; interest: number; rateInfo: string
}
export interface NoticeDraft {
  contractorId: string; contractorName: string; contractorEmail?: string | null; kind: NoticeKind; asOf: string; deadline: string
  lines: NoticeLine[]; principal: number; interest: number; total: number; subject: string; body: string
}
export interface NoticeListItem {
  id: string; contractorId: string; contractorName: string; kind: NoticeKind; channel: NoticeChannel; status: NoticeStatus; createdAt: string
  createdBy: string; calculationDate: string; principal: number; interest: number; sentTo?: string | null; error?: string | null; invoiceCount: number
}
export interface NoticeParty { name: string; nip?: string | null; address?: string | null; bankAccount?: string | null; email?: string | null }
export interface NoticeDoc {
  id: string; kind: NoticeKind; channel: NoticeChannel; status: NoticeStatus; createdAt: string; calculationDate: string; paymentDeadline: string
  issuer: NoticeParty; recipient: NoticeParty; lines: NoticeLine[]; principal: number; interest: number; total: number; subject: string; body: string
}
export interface MailSettingsDto {
  configured: boolean; host: string; port: number; useTls: boolean; userName?: string | null; hasPassword: boolean; fromAddress: string; fromName?: string | null
  reminderSubject: string; reminderBody: string; interestSubject: string; interestBody: string
}
export interface InterestRateDto { id: string; validFrom: string; annualPercent: number; note?: string | null }

export type RecurrenceFrequency = 'Monthly' | 'Quarterly' | 'Yearly'
export type NumberReset = 'Yearly' | 'Monthly' | 'Never'
export interface RecurringItem {
  id: string; name: string; contractorId: string; contractorName: string; frequency: RecurrenceFrequency
  dayOfMonth: number; dueDays: number; nextDate: string; endDate?: string | null; isActive: boolean
  resultAccountId?: string | null; kind?: KpirKind | null; ryczaltRate?: number | null; notes?: string | null
  net: number; gross: number; generatedCount: number; lines: InvoiceLineInput[]
}
export interface RecurringInput {
  name: string; contractorId: string; frequency: RecurrenceFrequency; dayOfMonth: number; dueDays: number; nextDate: string
  endDate?: string | null; resultAccountId?: string | null; kind?: KpirKind | null; ryczaltRate?: number | null; notes?: string | null; lines: InvoiceLineInput[]
}
export interface RecurringGenerateResult {
  generated: { template: string; number: string; issueDate: string; gross: number }[]
  skipped: { template: string; date: string; reason: string }[]
}
export interface NumberingDto { pattern: string; reset: NumberReset; nextExample: string }
