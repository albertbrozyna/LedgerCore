import { expect, test } from '@playwright/test'
import { ADMIN, loginOk, squash } from './helpers'

const THIS_YEAR = new Date().getFullYear()

test.describe.serial('Dane firmy, wydruki, bilans, eksporty i zamknięcie roku', () => {
  test.beforeEach(async ({ page }) => { await loginOk(page, ADMIN) })

  test('dane firmy: walidacja NIP i zapis', async ({ page }) => {
    await page.goto('/admin')
    const form = page.getByRole('form', { name: 'Dane firmy' })
    await form.getByLabel('Nazwa firmy').fill('Biuro Rachunkowe Test sp. z o.o.')
    await form.getByLabel('NIP').fill('1234567890')
    await form.getByRole('button', { name: 'Zapisz dane firmy' }).click()
    await expect(page.getByRole('alert')).toContainText('Nieprawidłowy NIP firmy')

    await form.getByLabel('NIP').fill('525-224-84-81')
    await form.getByLabel('Adres').fill('ul. Testowa 1, 00-001 Warszawa')
    await form.getByLabel('Numer rachunku bankowego').fill('11 1111 1111 1111 1111 1111 1111')
    await form.getByLabel('Stopka faktury').fill('Dziękujemy za terminową płatność.')
    await form.getByRole('button', { name: 'Zapisz dane firmy' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano dane firmy')

    await page.reload()
    await expect(page.getByRole('form', { name: 'Dane firmy' }).getByLabel('NIP')).toHaveValue('5252248481')
  })

  test('wydruk faktury: dane stron, kwota słownie, ukrycie menu w druku', async ({ page }) => {
    await page.goto('/invoices/new?type=Sales')
    await page.getByLabel('Nabywca').selectOption({ label: 'Acme Klient sp. z o.o. (NIP 5261040828)' })
    await page.getByLabel('Opis pozycji 1').fill('Doradztwo podatkowe')
    await page.getByLabel('Netto pozycji 1').fill('1000')
    await page.getByRole('button', { name: 'Wystaw i zaksięguj' }).click()
    await expect(page.getByRole('heading', { name: /Faktura FV\// })).toBeVisible()

    await page.getByRole('link', { name: 'Drukuj / PDF' }).click()
    const sheet = page.getByTestId('invoice-sheet')
    await expect(sheet).toContainText('Biuro Rachunkowe Test sp. z o.o.')
    await expect(sheet).toContainText('NIP 5252248481')
    await expect(sheet).toContainText('Acme Klient sp. z o.o.')
    await expect(sheet).toContainText('11 1111 1111 1111 1111 1111 1111')
    await expect(sheet).toContainText('Dziękujemy za terminową płatność.')
    await expect(page.getByTestId('print-total')).toHaveText(/1\s?230,00/)
    await expect(page.getByTestId('print-words')).toContainText('tysiąc dwieście trzydzieści złotych 00/100')

    await page.emulateMedia({ media: 'print' })
    await expect(page.locator('.sidebar')).toBeHidden()
    await expect(page.getByRole('button', { name: 'Drukuj / zapisz jako PDF' })).toBeHidden()
    await expect(sheet).toBeVisible()
  })

  test('wydruk raportu ma nagłówek z firmą i okresem, a formularze są ukryte', async ({ page }) => {
    await page.goto('/reports')
    await page.emulateMedia({ media: 'print' })
    const header = page.getByTestId('print-header')
    await expect(header).toBeVisible()
    await expect(header).toContainText('Biuro Rachunkowe Test sp. z o.o.')
    await expect(header).toContainText('Zestawienie obrotów i sald')
    await expect(page.locator('.sidebar')).toBeHidden()
    await expect(page.getByRole('tab', { name: 'Bilans' })).toBeHidden()
    await page.emulateMedia({ media: 'screen' })
    await expect(header).toBeHidden()
  })

  test('bilans zgadza się: aktywa = pasywa; eksport CSV rozrachunków', async ({ page }) => {
    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Bilans' }).click()
    await expect(page.getByTestId('bs-check')).toContainText('Bilans zgodny')
    const assets = squash(await page.getByTestId('bs-assets').textContent())
    expect(assets).toBe(squash(await page.getByTestId('bs-liabilities').textContent()))
    expect(assets).not.toBe('0,00')
    await expect(page.getByRole('table', { name: 'Aktywa' })).toContainText('130 Rachunek bankowy')

    await page.getByRole('tab', { name: 'Rozrachunki' }).click()
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Eksport CSV' }).click(),
    ])
    expect(download.suggestedFilename()).toBe('naleznosci.csv')
    const fs = await import('node:fs')
    const text = fs.readFileSync((await download.path())!, 'utf8')
    expect(text.charCodeAt(0)).toBe(0xfeff) // BOM dla Excela
    expect(text).toContain('"Kontrahent";"NIP";"Razem"')
    expect(text).toContain('Acme Klient')
  })

  test('zamknięcie roku zeruje wynik na koncie 860, nie zmienia raportu wyników i blokuje księgowanie', async ({ page, request }) => {
    // wynik przed zamknięciem
    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Rachunek wyników' }).click()
    await page.getByLabel('Od', { exact: true }).fill(`${THIS_YEAR}-01-01`)
    await page.getByLabel('Do', { exact: true }).fill(`${THIS_YEAR}-12-31`)
    const before = squash(await page.getByTestId('pnl-result').textContent())
    expect(before).not.toBe('0,00')

    await page.goto('/admin')
    page.once('dialog', d => d.accept())
    await page.getByLabel('Rok', { exact: true }).fill(String(THIS_YEAR))
    await page.getByRole('button', { name: 'Zamknij rok' }).click()
    await expect(page.getByRole('status')).toContainText(`Zamknięto rok ${THIS_YEAR}`)
    await expect(page.getByTestId('lock-status')).toContainText(`${THIS_YEAR}-12-31`)

    // rachunek wyników nadal pokazuje wynik roku (księgowanie zamykające jest pomijane)
    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Rachunek wyników' }).click()
    await page.getByLabel('Od', { exact: true }).fill(`${THIS_YEAR}-01-01`)
    await page.getByLabel('Do', { exact: true }).fill(`${THIS_YEAR}-12-31`)
    await expect(page.getByTestId('pnl-result')).toHaveText(new RegExp(before.replace(',', ',')))

    // bilans: wynik przeniesiony do kapitału (konto 860), wynik bieżący = 0, bilans zgodny
    await page.getByRole('tab', { name: 'Bilans' }).click()
    await page.getByLabel('Do', { exact: true }).fill(`${THIS_YEAR}-12-31`)
    await expect(page.getByRole('table', { name: 'Pasywa' })).toContainText('860 Wynik finansowy')
    await expect(page.getByTestId('bs-result')).toHaveText(/^0,00$/)
    await expect(page.getByTestId('bs-check')).toContainText('Bilans zgodny')

    // drugi raz nie można, a księgowanie w zamkniętym roku jest zablokowane
    await page.goto('/admin')
    await page.getByLabel('Rok', { exact: true }).fill(String(THIS_YEAR))
    page.once('dialog', d => d.accept())
    await page.getByRole('button', { name: 'Zamknij rok' }).click()
    await expect(page.getByRole('alert')).toContainText('został już zamknięty')

    const token = await page.evaluate(() => localStorage.getItem('lc.access'))
    const headers = { Authorization: `Bearer ${token}` }
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json()
    const id = (code: string) => accounts.find((a: { code: string }) => a.code === code).id
    const res = await request.post('/api/v1/transactions', {
      headers, data: { description: 'Po zamknięciu', date: new Date().toISOString().slice(0, 10),
        entries: [{ accountId: id('100'), amount: 1, side: 'Debit' }, { accountId: id('130'), amount: 1, side: 'Credit' }] },
    })
    expect(res.status()).toBe(400)
    expect((await res.json()).detail).toContain('Okres jest zamknięty')
  })
})
