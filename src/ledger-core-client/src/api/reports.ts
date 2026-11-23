import { json, qs } from './http'
import type { BalanceSheetDto, DashboardDto, InvoiceType, PnlDto, SettlementsDto, TrialBalanceDto, VatRegisterDto, VatSummaryDto } from './types'

/** Pulpit i raporty (obroty i salda, wyniki, bilans, VAT, rozrachunki). */
export const reportsApi = {
  dashboard: () => json<DashboardDto>('GET', '/reports/dashboard'),
  trialBalance: (from?: string, to?: string) => json<TrialBalanceDto>('GET', `/reports/trial-balance${qs({ from, to })}`),
  profitAndLoss: (from: string, to: string) => json<PnlDto>('GET', `/reports/profit-and-loss${qs({ from, to })}`),
  balanceSheet: (asOf: string) => json<BalanceSheetDto>('GET', `/reports/balance-sheet${qs({ asOf })}`),
  vatRegister: (type: InvoiceType, from: string, to: string) => json<VatRegisterDto>('GET', `/reports/vat-register${qs({ type, from, to })}`),
  vatSummary: (from: string, to: string) => json<VatSummaryDto>('GET', `/reports/vat-summary${qs({ from, to })}`),
  settlements: (type: InvoiceType) => json<SettlementsDto>('GET', `/reports/settlements${qs({ type })}`),
}
