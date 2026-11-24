import { expect, test } from '@playwright/test'
import { ADMIN, loginOk, postTransaction, squash, confirmDialog, pickAccount } from './helpers'

const today = new Date().toISOString().slice(0, 10)

test.describe.serial('Księgowość: plan kont, księgowania, raporty', () => {
  test.beforeEach(async ({ page }) => { await loginOk(page, ADMIN) })

  test('dodawanie konta, blokada duplikatu numeru i dezaktywacja', async ({ page }) => {
    await page.goto('/accounts')
    const form = page.getByRole('form', { name: 'Nowe konto' })
    await form.getByLabel('Numer').fill('999')
    await form.getByLabel('Nazwa').fill('Konto testowe')
    await form.getByLabel('Typ').selectOption('Expense')
    await form.getByRole('button', { name: 'Dodaj konto' }).click()
    await expect(page.getByRole('status')).toContainText('Dodano konto 999')
    await expect(page.getByRole('row', { name: /999 Konto testowe/ })).toBeVisible()

    await form.getByLabel('Numer').fill('999')
    await form.getByLabel('Nazwa').fill('Duplikat')
    await form.getByRole('button', { name: 'Dodaj konto' }).click()
    await expect(page.getByRole('alert')).toContainText('już istnieje')

    await page.getByRole('row', { name: /999 Konto testowe/ }).getByRole('button', { name: 'Dezaktywuj' }).click()
    await expect(page.getByRole('row', { name: /999 Konto testowe/ })).toHaveCount(0)
    await page.getByLabel('Pokaż nieaktywne').check()
    await expect(page.getByRole('row', { name: /999 Konto testowe/ })).toContainText('nieaktywne')
  })

  test('niezbilansowane księgowanie nie może zostać zapisane', async ({ page }) => {
    await page.goto('/transactions/new')
    await page.getByLabel('Opis').fill('Niezbilansowane')
    await pickAccount(page, 1, '130 Rachunek bankowy')
    await page.getByLabel('Winien w wierszu 1').fill('100')
    await pickAccount(page, 2, '700 Przychody ze sprzedaży usług')
    await page.getByLabel('Ma w wierszu 2').fill('90,50')
    await expect(page.getByTestId('balance-status')).toContainText('Różnica')
    await expect(page.getByRole('button', { name: 'Zaksięguj' })).toBeDisabled()

    await page.getByLabel('Ma w wierszu 2').fill('100')
    await expect(page.getByRole('button', { name: 'Zaksięguj' })).toBeEnabled()
    await page.getByLabel('Ma w wierszu 2').fill('abc')
    await expect(page.getByTestId('balance-status')).toContainText('Niepoprawna kwota')
    await expect(page.getByRole('button', { name: 'Zaksięguj' })).toBeDisabled()
  })

  test('serwer odrzuca niezbilansowane księgowanie także wysłane bezpośrednio do API', async ({ page, request }) => {
    const token = await page.evaluate(() => localStorage.getItem('lc.access'))
    const headers = { Authorization: `Bearer ${token}` }
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json()
    const bank = accounts.find((a: { code: string }) => a.code === '130').id
    const inc = accounts.find((a: { code: string }) => a.code === '700').id
    const res = await request.post('/api/v1/transactions', {
      headers, data: { description: 'Zły', date: today, entries: [
        { accountId: bank, amount: 10, side: 'Debit' }, { accountId: inc, amount: 11, side: 'Credit' }] },
    })
    expect(res.status()).toBe(400)
    expect((await res.json()).detail).toContain('nie jest zbilansowane')
    const one = await request.post('/api/v1/transactions', {
      headers, data: { description: 'Jeden zapis', date: today, entries: [{ accountId: bank, amount: 10, side: 'Debit' }] },
    })
    expect(one.status()).toBe(400)
  })

  test('księgowanie przychodu i kosztu aktualizuje salda, kartę konta i raporty', async ({ page }) => {
    await postTransaction(page, 'Faktura FV 1/2026 – usługi księgowe', today, [
      { account: '130 Rachunek bankowy', debit: '1 230,50' },
      { account: '700 Przychody ze sprzedaży usług', credit: '1230,50' },
    ])
    await expect(page.getByRole('heading', { name: /Księgowanie DK\/\d{4}\/0001/ })).toBeVisible()

    await postTransaction(page, 'Czynsz za biuro', today, [
      { account: '402 Usługi obce', debit: '300' },
      { account: '130 Rachunek bankowy', credit: '300' },
    ])

    // Pulpit
    await page.goto('/dashboard')
    await expect(page.getByTestId('tile-assets')).toHaveText(/930,50/)
    await expect(page.getByTestId('tile-income')).toHaveText(/1\s?230,50/)
    await expect(page.getByTestId('tile-expenses')).toHaveText(/300,00/)
    await expect(page.getByTestId('tile-result')).toHaveText(/930,50/)

    // Dziennik
    await page.goto('/transactions')
    await expect(page.getByRole('row', { name: /Faktura FV 1\/2026/ })).toBeVisible()
    await page.getByLabel('Szukaj').fill('czynsz')
    await expect(page.getByRole('row', { name: /Czynsz za biuro/ })).toBeVisible()
    await expect(page.getByRole('row', { name: /Faktura FV/ })).toHaveCount(0)

    // Karta konta (saldo narastające)
    await page.goto('/accounts')
    await page.getByRole('row', { name: /130 Rachunek bankowy/ }).getByRole('link', { name: 'Karta konta' }).click()
    await expect(page.getByTestId('ledger-closing')).toHaveText(/930,50/)
    await expect(page.getByRole('row', { name: /Czynsz za biuro/ })).toContainText('300,00')

    // Zestawienie obrotów i sald
    await page.goto('/reports')
    await expect(page.getByTestId('tb-total-debit')).toHaveText(/1\s?530,50/)
    await expect(page.getByTestId('tb-total-credit')).toHaveText(/1\s?530,50/)
    await expect(page.getByTestId('tb-130')).toBeVisible()

    // Rachunek wyników
    await page.getByRole('tab', { name: 'Rachunek wyników' }).click()
    await expect(page.getByTestId('pnl-income')).toHaveText(/1\s?230,50/)
    await expect(page.getByTestId('pnl-expenses')).toHaveText(/300,00/)
    await expect(page.getByTestId('pnl-result')).toHaveText(/930,50/)
  })

  test('storno odwraca księgowanie, nie kasuje go, i nie można stornować dwa razy', async ({ page }) => {
    await page.goto('/transactions')
    await page.getByRole('row', { name: /Czynsz za biuro/ }).getByRole('link').first().click()
    await page.getByRole('button', { name: 'Storno' }).click()
    await confirmDialog(page)
    await expect(page.getByRole('heading', { name: /Księgowanie DK/ })).toBeVisible()
    await expect(page.getByText('Storno dokumentu')).toBeVisible()

    await page.goto('/transactions')
    await expect(page.getByRole('row', { name: /Czynsz za biuro/ })).toHaveCount(2) // oryginał + storno
    await expect(page.getByText('stornowane', { exact: true })).toBeVisible()

    // Koszt wrócił do zera, saldo banku do 1 230,50
    await page.goto('/dashboard')
    await expect(page.getByTestId('tile-expenses')).toHaveText(/^0,00$/)
    await expect(page.getByTestId('tile-assets')).toHaveText(/1\s?230,50/)

    // Oryginał nie ma już przycisku Storno
    await page.goto('/transactions')
    await page.getByRole('row', { name: /Czynsz za biuro.*stornowane/ }).getByRole('link').first().click()
    await expect(page.getByText('Stornowane przez')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Storno' })).toHaveCount(0)
  })

  test('eksport CSV zawiera księgowania', async ({ page }) => {
    await page.goto('/transactions')
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Eksport CSV' }).click(),
    ])
    expect(download.suggestedFilename()).toMatch(/^dziennik-\d{4}-\d{2}-\d{2}\.csv$/)
    const fs = await import('node:fs')
    const text = fs.readFileSync((await download.path())!, 'utf8')
    expect(text).toContain('Numer;Data;Opis;Konto')
    expect(squash(text)).toContain('1230,50')
    expect(text).toContain('Faktura FV 1/2026')
  })
})
