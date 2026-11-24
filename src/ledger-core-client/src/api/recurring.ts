import { json } from './http'
import type { NumberingDto, NumberReset, RecurringGenerateResult, RecurringInput, RecurringItem } from './types'

/** Faktury cykliczne (szablony i generowanie zaległych) oraz wzór numeracji faktur sprzedaży. */
export const recurringApi = {
  recurringInvoices: () => json<RecurringItem[]>('GET', '/recurring-invoices'),
  saveRecurring: (id: string | null, b: RecurringInput) =>
    id ? json<{ id: string }>('PUT', `/recurring-invoices/${id}`, b) : json<{ id: string }>('POST', '/recurring-invoices', b),
  setRecurringActive: (id: string, active: boolean) => json<void>('PUT', `/recurring-invoices/${id}/active`, { active }),
  deleteRecurring: (id: string) => json<void>('DELETE', `/recurring-invoices/${id}`),
  generateRecurring: (asOf?: string, templateId?: string) => json<RecurringGenerateResult>('POST', '/recurring-invoices/generate', { asOf, templateId }),

  numbering: () => json<NumberingDto>('GET', '/admin/numbering'),
  previewNumbering: (pattern: string, reset: NumberReset, date?: string) => json<{ example: string }>('POST', '/admin/numbering/preview', { pattern, reset, date }),
  saveNumbering: (pattern: string, reset: NumberReset) => json<void>('PUT', '/admin/numbering', { pattern, reset }),
}
