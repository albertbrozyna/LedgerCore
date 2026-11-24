import { json, qs } from './http'
import type { KpirDto, RyczaltDto } from './types'

/** Ewidencje podatkowe: KPiR i ewidencja przychodów ryczałtu (widoki nad fakturami). */
export const registersApi = {
  kpir: (from: string, to: string) => json<KpirDto>('GET', `/registers/kpir${qs({ from, to })}`),
  ryczalt: (from: string, to: string) => json<RyczaltDto>('GET', `/registers/ryczalt${qs({ from, to })}`),
}
