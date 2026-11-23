// Cienka warstwa nad fetch: token JWT, jednorazowe odświeżenie sesji przy 401, nagłówek firmy, błędy po polsku.

/** Błąd odpowiedzi API: `detail` to komunikat po polsku (pokazywany w ErrorBox), `status` – kod HTTP. */
export class ApiError extends Error {
  status: number
  detail: string
  constructor(detail: string, status: number) {
    super(detail)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
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

export const BASE = '/api/v1'

export async function errorFrom(res: Response): Promise<ApiError> {
  let message = ''
  try {
    const body = await res.json()
    if (body?.errors && typeof body.errors === 'object') {
      message = Object.values(body.errors as Record<string, string[]>).flat().join('. ')
    } else if (body?.detail) message = body.detail
    else if (typeof body === 'string') message = body
  } catch { /* brak treści JSON */ }
  if (!message) {
    message = res.status === 403 ? 'Brak uprawnień do tej operacji lub do wybranej firmy'
      : res.status === 404 ? 'Nie znaleziono żądanego zasobu'
      : `Błąd serwera (${res.status})`
  }
  return new ApiError(message, res.status)
}

let refreshing: Promise<boolean> | null = null
export async function tryRefresh(): Promise<boolean> {
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

/** Nagłówki autoryzacji i firmy dołączane do każdego żądania. */
export function authHeaders(): Record<string, string> {
  const h: Record<string, string> = {}
  if (tokens.access) h.Authorization = `Bearer ${tokens.access}`
  if (companyStore.id) h['X-Company-Id'] = companyStore.id
  return h
}

export async function raw(method: string, path: string, body?: unknown, retry = true): Promise<Response> {
  const headers: Record<string, string> = authHeaders()
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const res = await fetch(`${BASE}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  if (res.status === 401 && retry && !path.startsWith('/auth/login') && tokens.refresh) {
    if (await tryRefresh()) return raw(method, path, body, false)
    tokens.clear(); onUnauthorized()
  }
  if (!res.ok) throw await errorFrom(res)
  return res
}

export async function json<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await raw(method, path, body)
  return res.status === 204 ? (undefined as T) : res.json()
}

export const qs = (p: Record<string, string | number | undefined | null>) => {
  const e = Object.entries(p).filter(([, v]) => v !== undefined && v !== null && v !== '')
  return e.length ? '?' + new URLSearchParams(e.map(([k, v]) => [k, String(v)])).toString() : ''
}

/** Wysyła plik jako multipart (z jednorazowym odświeżeniem sesji przy 401). */
export async function upload(path: string, form: FormData): Promise<void> {
  const send = () => fetch(`${BASE}${path}`, { method: 'POST', headers: authHeaders(), body: form })
  let res = await send()
  if (res.status === 401 && (await tryRefresh())) res = await send()
  if (!res.ok) throw await errorFrom(res)
}

/** Pobiera plik z API i zapisuje go przez link tymczasowy (nazwa z Content-Disposition). */
export async function download(path: string, fallbackName: string) {
  const res = await raw('GET', path)
  const blob = await res.blob()
  const cd = res.headers.get('content-disposition') ?? ''
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd)?.[1] ?? fallbackName
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = decodeURIComponent(name); a.click()
  URL.revokeObjectURL(url)
}
