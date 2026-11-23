import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ACCOUNTANT, ADMIN, accessToken, login, loginOk, logout, confirmDialog } from './helpers'

const today = () => {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

async function api(page: Page) {
  return { Authorization: `Bearer ${await accessToken(page)}` }
}

async function contractorId(request: APIRequestContext, headers: Record<string, string>, name: string) {
  const list = await (await request.get('/api/v1/contractors', { headers })).json()
  return list.find((c: { name: string }) => c.name.startsWith(name)).id as string
}

async function createInvoice(request: APIRequestContext, headers: Record<string, string>, body: object) {
  const res = await request.post('/api/v1/invoices', { headers, data: body })
  expect(res.status(), await res.text()).toBe(200)
  return (await res.json()) as { invoiceId: string; number: string }
}

test.describe.serial('Kopie zapasowe, konto, załączniki, szablony, import wyciągu, pulpit', () => {
  test('automatyczna kopia zapasowa powstaje po starcie, ręczną można pobrać; ścieżki spoza folderu są blokowane', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await page.goto('/admin')
    const table = page.getByRole('table', { name: 'Kopie zapasowe' })
    await expect(async () => {
      await page.reload()
      await expect(table.getByRole('row', { name: /automatyczna/ })).toHaveCount(1)
    }).toPass({ timeout: 30_000 })

    await page.getByRole('button', { name: 'Utwórz kopię teraz' }).click()
    await expect(page.getByRole('status')).toContainText('Utworzono kopię zapasową ledgercore-')
    await expect(table.getByRole('row', { name: /ręczna/ })).toHaveCount(1)

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      table.getByRole('row', { name: /ręczna/ }).getByRole('button', { name: 'Pobierz' }).click(),
    ])
    expect(download.suggestedFilename()).toMatch(/^ledgercore-\d{4}-\d{2}-\d{2}-\d{6}-manual\.db$/)
    const fs = await import('node:fs')
    expect(fs.readFileSync((await download.path())!).subarray(0, 15).toString('latin1')).toBe('SQLite format 3')

    const headers = await api(page)
    for (const evil of ['..%5Cjwt.key', '..%2Fjwt.key', 'jwt.key', 'ledgercore.db']) {
      expect((await request.get(`/api/v1/admin/backups/${evil}`, { headers })).status(), evil).toBe(404)
    }
  })

  test('zmiana własnego hasła: błędne aktualne hasło, zgodność, logowanie nowym hasłem', async ({ page }) => {
    await loginOk(page, { email: ACCOUNTANT.email, password: 'NoweHaslo99' })
    await page.getByRole('link', { name: 'Moje konto' }).click()
    const form = page.getByRole('form', { name: 'Zmiana hasła' })

    await form.getByLabel('Aktualne hasło').fill('ZleHaslo000')
    await form.getByLabel('Nowe hasło (min. 8 znaków, w tym cyfra)').fill('Trzecie123')
    await form.getByLabel('Powtórz nowe hasło').fill('Trzecie123')
    await form.getByRole('button', { name: 'Zmień hasło' }).click()
    await expect(page.getByRole('alert')).toContainText('Aktualne hasło jest nieprawidłowe')

    await form.getByLabel('Aktualne hasło').fill('NoweHaslo99')
    await form.getByLabel('Powtórz nowe hasło').fill('Inne1234')
    await form.getByRole('button', { name: 'Zmień hasło' }).click()
    await expect(page.getByRole('alert')).toContainText('nie są takie same')

    await form.getByLabel('Powtórz nowe hasło').fill('Trzecie123')
    await form.getByRole('button', { name: 'Zmień hasło' }).click()
    await expect(page.getByRole('status')).toContainText('Hasło zostało zmienione')
    await logout(page)

    await login(page, ACCOUNTANT.email, 'NoweHaslo99')
    await expect(page.getByRole('alert')).toContainText('Nieprawidłowy e-mail lub hasło')
    await loginOk(page, { email: ACCOUNTANT.email, password: 'Trzecie123' })
  })

  test('załączniki do faktury: PDF dodany i pobrany, błędne pliki odrzucone, usuwanie', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = await api(page)
    const inv = await createInvoice(request, headers, {
      type: 'Purchase', number: 'FZ/80/2026', contractorId: await contractorId(request, headers, 'Dostawca SA'),
      issueDate: today(), dueDate: today(), lines: [{ description: 'Papier', net: 100, vatRate: 'R23' }],
    })
    await page.goto(`/invoices/${inv.invoiceId}`)
    const input = page.getByLabel('Plik załącznika')
    const pdf = Buffer.from('%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF')

    await input.setInputFiles({ name: 'skan-faktury.pdf', mimeType: 'application/pdf', buffer: pdf })
    await expect(page.getByRole('status')).toContainText('Dodano załącznik skan-faktury.pdf')
    const list = page.getByRole('list', { name: 'Załączniki' })
    await expect(list).toContainText('skan-faktury.pdf')

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      list.getByRole('button', { name: 'skan-faktury.pdf' }).click(),
    ])
    expect(download.suggestedFilename()).toBe('skan-faktury.pdf')
    const fs = await import('node:fs')
    expect(fs.readFileSync((await download.path())!).toString('latin1')).toContain('%PDF-1.4')

    // podszywanie się: plik .pdf z treścią tekstową oraz niedozwolone rozszerzenie
    await input.setInputFiles({ name: 'falszywy.pdf', mimeType: 'application/pdf', buffer: Buffer.from('to nie jest pdf') })
    await expect(page.getByRole('alert')).toContainText('Zawartość pliku nie odpowiada')
    await input.setInputFiles({ name: 'program.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('MZ....') })
    await expect(page.getByRole('alert')).toContainText('Dozwolone formaty')
    await expect(list.getByRole('listitem')).toHaveCount(1)

    await list.getByRole('button', { name: 'Usuń' }).click()
    await confirmDialog(page)
    await expect(page.getByText('Brak załączników')).toBeVisible()

    // użytkownik bez uprawnień zapisu nie ma uploadu (serwer też odmawia)
    await logout(page)
    await loginOk(page, { email: 'podglad@biuro.test', password: 'Haslo1234' })
    await page.goto(`/invoices/${inv.invoiceId}`)
    await expect(page.getByLabel('Plik załącznika')).toHaveCount(0)
    const res = await request.post(`/api/v1/invoices/${inv.invoiceId}/attachments`, {
      headers: await api(page), multipart: { file: { name: 'a.pdf', mimeType: 'application/pdf', buffer: pdf } },
    })
    expect(res.status()).toBe(403)
  })

  test('szablony księgowań: zapis, użycie, duplikat nazwy', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions/new')
    await page.getByLabel('Opis').fill('Czynsz za biuro – miesiąc')
    await page.getByLabel('Konto w wierszu 1').selectOption({ label: '402 Usługi obce' })
    await page.getByLabel('Winien w wierszu 1').fill('1500')
    await page.getByLabel('Konto w wierszu 2').selectOption({ label: '130 Rachunek bankowy' })
    await page.getByLabel('Ma w wierszu 2').fill('1500')
    await page.getByLabel('Nazwa szablonu').fill('Czynsz')
    await page.getByRole('button', { name: 'Zapisz szablon' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano szablon „Czynsz”')

    await page.getByLabel('Nazwa szablonu').fill('Czynsz')
    await page.getByRole('button', { name: 'Zapisz szablon' }).click()
    await expect(page.getByRole('alert')).toContainText('już istnieje')

    // świeża strona: wybór szablonu wypełnia formularz
    await page.goto('/transactions/new')
    await page.getByLabel('Szablon', { exact: true }).selectOption({ label: 'Czynsz' })
    await expect(page.getByLabel('Opis')).toHaveValue('Czynsz za biuro – miesiąc')
    await expect(page.getByLabel('Konto w wierszu 1')).toHaveValue(/.+/)
    await expect(page.getByLabel('Winien w wierszu 1')).toHaveValue('1500')
    await expect(page.getByLabel('Ma w wierszu 2')).toHaveValue('1500')
    await expect(page.getByTestId('balance-status')).toContainText('zbilansowane')
    await page.getByRole('button', { name: 'Zaksięguj' }).click()
    await expect(page).toHaveURL(/\/transactions\/[0-9a-f-]{36}/)
  })

  test('import wyciągu bankowego: dopasowanie do faktur i księgowanie płatności', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = await api(page)
    const sale = await createInvoice(request, headers, {
      type: 'Sales', contractorId: await contractorId(request, headers, 'Acme Klient'),
      issueDate: today(), dueDate: today(), lines: [{ description: 'Usługa', net: 1000, vatRate: 'R23' }],
    })
    const d = today()
    const csv = [
      'Data;Kwota;Tytuł;Kontrahent',
      `${d};1 230,00;Zapłata za ${sale.number.replace('FV/', 'FV ')};Acme Klient sp. z o.o.`,
      `${d};-246,00;przelew FZ/77/2026 licencja;Dostawca SA`,
      `${d};500,00;Wpłata bez numeru;Nieznany Jan`,
      'to;nie;jest;wiersz',
    ].join('\n')

    await page.goto('/bank-import')
    await page.getByLabel('Konto bankowe').selectOption({ label: '130 Rachunek bankowy' })
    await page.getByLabel('Treść wyciągu').fill(csv)
    await page.getByRole('button', { name: 'Analizuj' }).click()

    const table = page.getByRole('table', { name: 'Operacje z wyciągu' })
    await expect(table.getByRole('row')).toHaveCount(4) // nagłówek + 3 poprawne operacje
    await expect(table.getByRole('row', { name: /Zapłata za FV/ })).toContainText('pewne')
    await expect(table.getByRole('row', { name: /FZ\/77\/2026/ })).toContainText('pewne')
    await expect(table.getByRole('row', { name: /Wpłata bez numeru/ })).toContainText('brak')
    await expect(page.getByText('Pominięto wierszy bez poprawnej daty lub kwoty: 1')).toBeVisible()

    await expect(page.getByRole('button', { name: 'Zaksięguj zaznaczone (2)' })).toBeEnabled()
    await page.getByRole('button', { name: 'Zaksięguj zaznaczone (2)' }).click()
    await expect(page.getByRole('status')).toContainText('Zaksięgowano płatności: 2')
    await expect(table.getByRole('row', { name: /Zapłata za FV/ })).toContainText('Zaksięgowano 1')

    const saleNow = await (await request.get(`/api/v1/invoices/${sale.invoiceId}`, { headers })).json()
    expect(saleNow.status).toBe('paid')
    const list = await (await request.get('/api/v1/invoices?search=FZ/77/2026', { headers })).json()
    expect(list.items[0].status).toBe('paid')
  })

  test('pulpit: wykres 12 miesięcy i kalendarz terminów', async ({ page }) => {
    await loginOk(page, ADMIN)
    await expect(page.getByTestId('month-chart')).toBeVisible()
    await expect(page.getByTestId('month-chart').locator('rect')).toHaveCount(24)
    const cal = page.getByTestId('tax-calendar')
    await expect(cal).toContainText('JPK_V7 / VAT')
    await expect(cal).toContainText('Składki ZUS')
    expect(await cal.getByRole('listitem').count()).toBeGreaterThanOrEqual(3)
  })

  test('import wyciągu niedostępny dla roli Podgląd', async ({ page }) => {
    await loginOk(page, { email: 'podglad@biuro.test', password: 'Haslo1234' })
    await page.goto('/bank-import')
    await expect(page.getByRole('alert')).toContainText('nie pozwala')
  })
})
