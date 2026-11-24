// Jeden punkt wejścia do API: `import { api } from '../api'`.
import { accountingApi } from './accounting'
import { adminApi } from './admin'
import { collectionsApi } from './collections'
import { companiesApi } from './companies'
import { invoicingApi } from './invoicing'
import { registersApi } from './registers'
import { reportsApi } from './reports'
import { searchApi } from './search'

export const api = {
  ...accountingApi,
  ...invoicingApi,
  ...reportsApi,
  ...companiesApi,
  ...adminApi,
  ...searchApi,
  ...registersApi,
  ...collectionsApi,
}

export { ApiError, companyStore, setUnauthorizedHandler, tokens } from './http'
export * from './types'
