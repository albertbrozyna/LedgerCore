import { expect, test } from '@playwright/test'
import { ACCOUNTANT, ADMIN, VIEWER, accessToken, login, loginOk, logout, postTransaction, answerPrompt } from './helpers'

const today = new Date().toISOString().slice(0, 10)

test.describe.serial('Użytkownicy i uprawnienia', () => {
  test('administrator dodaje księgowego i użytkownika z podglądem', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.getByRole('link', { name: 'Użytkownicy' }).click()
    const form = page.getByRole('form', { name: 'Nowy użytkownik' })

    for (const [u, role] of [[ACCOUNTANT, 'Accountant'], [VIEWER, 'Viewer']] as const) {
      await form.getByLabel('Imię').fill(u.first)
      await form.getByLabel('Nazwisko').fill(u.last)
      await form.getByLabel('E-mail').fill(u.email)
      await form.getByLabel('Hasło').fill(u.password)
      await form.getByLabel('Rola').selectOption(role)
      await form.getByRole('button', { name: 'Dodaj użytkownika' }).click()
      await expect(page.getByRole('status')).toContainText(`Dodano użytkownika ${u.email}`)
    }
    await expect(page.getByRole('row', { name: new RegExp(ACCOUNTANT.email) })).toBeVisible()
    await expect(page.getByRole('row', { name: new RegExp(VIEWER.email) })).toBeVisible()

    // duplikat e-maila
    await form.getByLabel('Imię').fill('X')
    await form.getByLabel('Nazwisko').fill('Y')
    await form.getByLabel('E-mail').fill(ACCOUNTANT.email)
    await form.getByLabel('Hasło').fill('Haslo1234')
    await form.getByRole('button', { name: 'Dodaj użytkownika' }).click()
    await expect(page.getByRole('alert')).toContainText('already exist')
  })

  test('administrator nie może zablokować ani zdegradować samego siebie', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/users')
    const me = page.getByRole('row', { name: new RegExp(ADMIN.email) })
    await expect(me.getByRole('button', { name: 'Zablokuj' })).toHaveCount(0)
    await expect(me.getByLabel(`Rola: ${ADMIN.email}`)).toBeDisabled()
  })

  test('księgowy może księgować, ale nie widzi zarządzania użytkownikami', async ({ page, request }) => {
    await loginOk(page, ACCOUNTANT)
    await expect(page.getByRole('link', { name: 'Użytkownicy' })).toHaveCount(0)
    await page.goto('/users')
    await expect(page).toHaveURL(/\/dashboard/)
    await postTransaction(page, 'Wypłata z kasy', today, [
      { account: '100 Kasa', debit: '50' }, { account: '130 Rachunek bankowy', credit: '50' },
    ])
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    expect((await request.get('/api/v1/users', { headers })).status()).toBe(403)
  })

  test('użytkownik z podglądem widzi dane, ale nie może nic zmieniać', async ({ page, request }) => {
    await loginOk(page, VIEWER)
    await expect(page.getByRole('link', { name: 'Użytkownicy' })).toHaveCount(0)

    await page.goto('/transactions/new')
    await expect(page.getByRole('alert')).toContainText('nie pozwala na księgowanie')

    await page.goto('/accounts')
    await expect(page.getByRole('form', { name: 'Nowe konto' })).toHaveCount(0)
    await expect(page.getByRole('row', { name: /130 Rachunek bankowy/ })).toBeVisible()

    await page.goto('/transactions')
    await page.getByRole('row', { name: /Wypłata z kasy/ }).getByRole('link').first().click()
    await expect(page.getByRole('button', { name: 'Storno' })).toHaveCount(0)

    // to samo wymuszone po stronie serwera
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    expect((await request.post('/api/v1/accounts', { headers, data: { name: 'X', code: '888', type: 'Asset' } })).status()).toBe(403)
    expect((await request.post('/api/v1/transactions', { headers, data: { description: 'x', date: today, entries: [] } })).status()).toBe(403)
    expect((await request.get('/api/v1/admin/backup', { headers })).status()).toBe(403)
    expect((await request.get('/api/v1/reports/dashboard', { headers })).status()).toBe(200)
  })

  test('administrator zmienia rolę, blokuje użytkownika – zablokowany nie zaloguje się', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/users')
    await page.getByLabel(`Rola: ${VIEWER.email}`).selectOption('Accountant')
    await expect(page.getByLabel(`Rola: ${VIEWER.email}`)).toHaveValue('Accountant')

    await page.getByRole('row', { name: new RegExp(VIEWER.email) }).getByRole('button', { name: 'Zablokuj' }).click()
    await expect(page.getByRole('row', { name: new RegExp(VIEWER.email) })).toContainText('Zablokowany')
    await logout(page)

    await login(page, VIEWER.email, VIEWER.password)
    await expect(page.getByRole('alert')).toContainText('zablokowane przez administratora')

    // odblokowanie przywraca dostęp
    await loginOk(page, ADMIN)
    await page.goto('/users')
    await page.getByRole('row', { name: new RegExp(VIEWER.email) }).getByRole('button', { name: 'Odblokuj' }).click()
    await expect(page.getByRole('row', { name: new RegExp(VIEWER.email) })).toContainText('Aktywny')
    await logout(page)
    await loginOk(page, VIEWER)
  })

  test('reset hasła przez administratora i kopia zapasowa bazy', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/users')
    await page.getByRole('row', { name: new RegExp(ACCOUNTANT.email) }).getByRole('button', { name: 'Zmień hasło' }).click()
    await answerPrompt(page, 'NoweHaslo99')
    await expect(page.getByRole('status')).toContainText('Zmieniono hasło')

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Pobierz kopię zapasową' }).click(),
    ])
    expect(download.suggestedFilename()).toMatch(/^ledgercore-backup-.*\.db$/)
    const fs = await import('node:fs')
    const head = fs.readFileSync((await download.path())!).subarray(0, 15).toString('latin1')
    expect(head).toBe('SQLite format 3')
    await logout(page)

    await login(page, ACCOUNTANT.email, ACCOUNTANT.password)
    await expect(page.getByRole('alert')).toContainText('Nieprawidłowy e-mail lub hasło')
    await loginOk(page, { email: ACCOUNTANT.email, password: 'NoweHaslo99' })
  })
})
