import { json, qs } from './http'
import type { NoticeChannel, NoticeDoc, NoticeDraft, NoticeListItem, NoticeRequest, NoticeStatus, OverdueDto } from './types'

/** Windykacja: faktury po terminie, wezwania do zapłaty i noty odsetkowe (wydruk lub e-mail). */
export const collectionsApi = {
  overdue: (asOf?: string, contractorId?: string) => json<OverdueDto>('GET', `/collections/overdue${qs({ asOf, contractorId })}`),
  previewNotice: (request: NoticeRequest) => json<NoticeDraft>('POST', '/collections/preview', request),
  issueNotice: (b: { request: NoticeRequest; channel: NoticeChannel; to?: string; subject?: string; body?: string }) =>
    json<{ id: string; status: NoticeStatus; error?: string | null }>('POST', '/collections', b),
  notices: (p: { contractorId?: string; invoiceId?: string }) => json<NoticeListItem[]>('GET', `/collections${qs(p)}`),
  notice: (id: string) => json<NoticeDoc>('GET', `/collections/${id}`),
}
