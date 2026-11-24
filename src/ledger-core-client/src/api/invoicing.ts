import { download, json, qs, upload } from './http'
import type { AttachmentDto, ContractorDto, InvoiceDetail, InvoiceLineInput, InvoiceList, InvoiceType, KpirKind } from './types'

/** Kontrahenci, faktury (z płatnościami, korektami i załącznikami). */
export const invoicingApi = {
  contractors: (search?: string, includeInactive = false) =>
    json<ContractorDto[]>('GET', `/contractors${qs({ search, includeInactive: includeInactive ? 'true' : undefined })}`),
  saveContractor: (id: string | null, b: { name: string; nip?: string; address?: string; email?: string; isActive?: boolean }) =>
    id ? json<void>('PUT', `/contractors/${id}`, b) : json<{ id: string }>('POST', '/contractors', b),

  invoices: (p: { type?: InvoiceType; contractorId?: string; from?: string; to?: string; status?: string; search?: string; page?: number; pageSize?: number; sort?: string; dir?: string }) =>
    json<InvoiceList>('GET', `/invoices${qs(p)}`),
  invoice: (id: string) => json<InvoiceDetail>('GET', `/invoices/${id}`),
  createInvoice: (b: {
    type: InvoiceType; number?: string; contractorId: string; issueDate: string; saleDate?: string; dueDate: string
    resultAccountId?: string; notes?: string; lines: InvoiceLineInput[]
    kind?: KpirKind; ryczaltRate?: number
  }) => json<{ invoiceId: string; number: string; transactionId: string }>('POST', '/invoices', b),
  addPayment: (id: string, b: { date: string; amount: number; accountId: string }) => json<void>('POST', `/invoices/${id}/payments`, b),
  cancelInvoice: (id: string) => json<void>('POST', `/invoices/${id}/cancel`),
  createCorrection: (invoiceId: string, b: { number?: string; issueDate: string; reason?: string; lines: InvoiceLineInput[] }) =>
    json<{ invoiceId: string; number: string; transactionId: string }>('POST', `/invoices/${invoiceId}/corrections`, b),

  attachments: (invoiceId: string) => json<AttachmentDto[]>('GET', `/invoices/${invoiceId}/attachments`),
  uploadAttachment: (invoiceId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return upload(`/invoices/${invoiceId}/attachments`, form)
  },
  downloadAttachment: (a: AttachmentDto) => download(`/attachments/${a.id}`, a.fileName),
  deleteAttachment: (id: string) => json<void>('DELETE', `/attachments/${id}`),
}
