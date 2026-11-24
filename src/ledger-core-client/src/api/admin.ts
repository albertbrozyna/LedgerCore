import { download, json, qs } from './http'
import type { AuditList, BackupItem, CompanyDto, Role, UserInfo } from './types'

/** Użytkownicy, dane firmy, blokada okresu, zamknięcie roku, audyt i kopie zapasowe. */
export const adminApi = {
  users: () => json<UserInfo[]>('GET', '/users'),
  createUser: (b: { firstName: string; lastName: string; email: string; password: string; role: Role }) =>
    json('POST', '/users/create', { ...b, phoneNumber: '' }),
  setUserActive: (id: string, active: boolean) => json<void>('POST', `/users/${id}/${active ? 'unblock' : 'block'}`),
  setUserRole: (id: string, role: Role) => json<void>('PUT', `/users/${id}/role`, { role }),
  resetPassword: (id: string, newPassword: string) => json<void>('POST', `/users/${id}/password`, { newPassword }),
  changePassword: (currentPassword: string, newPassword: string) => json<void>('POST', '/auth/change-password', { currentPassword, newPassword }),

  company: () => json<CompanyDto>('GET', '/admin/company'),
  saveCompany: (b: CompanyDto) => json<void>('PUT', '/admin/company', b),
  closeYear: (year: number) => json<{ number: string; result: number }>('POST', '/admin/close-year', { year }),
  periodLock: () => json<{ lockedThrough?: string | null }>('GET', '/admin/period-lock'),
  setPeriodLock: (lockedThrough: string | null) => json<void>('PUT', '/admin/period-lock', { lockedThrough }),
  audit: (p: { search?: string; page?: number; pageSize?: number; sort?: string; dir?: string }) => json<AuditList>('GET', `/admin/audit${qs(p)}`),

  backup: () => download('/admin/backup', 'kopia.db'),
  backups: () => json<BackupItem[]>('GET', '/admin/backups'),
  createBackup: () => json<{ name: string }>('POST', '/admin/backups'),
  downloadBackup: (name: string) => download(`/admin/backups/${encodeURIComponent(name)}`, name),
}
