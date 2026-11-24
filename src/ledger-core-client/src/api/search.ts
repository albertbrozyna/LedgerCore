import { json, qs } from './http'
import type { SearchResult } from './types'

/** Globalne wyszukiwanie w bieżącej firmie (paleta poleceń). */
export const searchApi = {
  search: (q: string) => json<SearchResult>('GET', `/search${qs({ q })}`),
}
