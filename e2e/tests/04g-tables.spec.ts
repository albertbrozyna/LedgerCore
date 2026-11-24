import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { ADMIN, VIEWER, accessToken, confirmDialog, loginOk, squash } from './helpers'

// Plik przed 05 (zamknięcie roku): tylko odczyty i kontrahenci bez księgowań.

const num = (s: string | null) => Number(squash(s).replace(',', '.'))

async function columnValues(page: Page, table: string, col: number) {
  return page.getByRole('table', { name: table }).locator(`tbody tr:not(.empty-row) td:nth-child(${col})`).allTextContents()
}

test.describe.serial('Tabele: sortowanie, widoki, eksport, dostępność', () => {
  test('dziennik: sortowanie po kolumnie (serwer), aria-sort i powrót do domyślnego', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    const table = page.getByRole('table', { name: 'Dziennik księgowań' })
    await expect(table.locator('tbody tr').first()).toBeVisible()

    const amountHeader = table.getByRole('columnheader', { name: /Kwota/ })
    await amountHeader.getByRole('button').click()
    await expect(amountHeader).toHaveAttribute('aria-sort', 'ascending')
    await expect.poll(async () => {
      const v = (await columnValues(page, 'Dziennik księgowań', 5)).map(num)
      return v.every((x, i) => i === 0 || v[i - 1] <= x)
    }).toBe(true)

    await amountHeader.getByRole('button').click()
    await expect(amountHeader).toHaveAttribute('aria-sort', 'descending')
    await expect.poll(async () => {
      const v = (await columnValues(page, 'Dziennik księgowań', 5)).map(num)
      return v.every((x, i) => i === 0 || v[i - 1] >= x)
    }).toBe(true)

    await amountHeader.getByRole('button').click()
    await expect(amountHeader).toHaveAttribute('aria-sort', 'none')
  })

  test('sortowanie z klawiatury nie gubi fokusu po odświeżeniu danych', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    const header = page.getByRole('table', { name: 'Dziennik księgowań' }).getByRole('columnheader', { name: /Opis/ })
    await header.getByRole('button').focus()
    await page.keyboard.press('Enter')
    await expect(header).toHaveAttribute('aria-sort', 'ascending')
    await expect(header.getByRole('button')).toBeFocused()
    await page.keyboard.press('Space')
    await expect(header).toHaveAttribute('aria-sort', 'descending')
    await expect(header.getByRole('button')).toBeFocused()
    const names = await columnValues(page, 'Dziennik księgowań', 4)
    const sorted = [...names].sort((a, b) => b.localeCompare(a, 'pl'))
    expect(names.length).toBeGreaterThan(1)
    expect(names.map(n => n.toLowerCase())).toEqual(sorted.map(n => n.toLowerCase()))
  })

  test('wybór kolumn i gęstość są zapamiętywane po odświeżeniu', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions')
    const table = page.getByRole('table', { name: 'Dziennik księgowań' })
    await expect(table.getByRole('columnheader', { name: 'Opis' })).toBeVisible()

    await page.getByText('Kolumny', { exact: true }).click()
    await page.getByRole('group', { name: 'Widoczne kolumny' }).getByLabel('Pokaż kolumnę Opis').uncheck()
    await page.getByRole('button', { name: /Gęstość/ }).click()
    await expect(page.getByRole('button', { name: 'Gęstość: kompaktowa' })).toBeVisible()
    await expect(table).toHaveClass(/table-compact/)
    await expect(table.getByRole('columnheader', { name: 'Opis' })).toHaveCount(0)

    await page.reload()
    await expect(page.getByRole('table', { name: 'Dziennik księgowań' }).getByRole('columnheader', { name: 'Numer' })).toBeVisible()
    await expect(page.getByRole('table', { name: 'Dziennik księgowań' }).getByRole('columnheader', { name: 'Opis' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Gęstość: kompaktowa' })).toBeVisible()

    // przywrócenie
    await page.getByText('Kolumny', { exact: true }).click()
    await page.getByRole('group', { name: 'Widoczne kolumny' }).getByLabel('Pokaż kolumnę Opis').check()
    await page.getByRole('button', { name: /Gęstość/ }).click()
    await expect(page.getByRole('button', { name: 'Gęstość: standardowa' })).toBeVisible()
  })

  test('zapisane widoki filtrów: zapis, przywrócenie, usunięcie; są per użytkownik', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions')
    await page.getByLabel('Szukaj').fill('Czynsz')
    await page.getByRole('button', { name: 'Zapisz widok' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Nazwa widoku').fill('Tylko czynsze')
    await dialog.getByRole('button', { name: 'Zapisz' }).click()
    await expect(dialog).toBeHidden()

    await page.getByLabel('Szukaj').fill('')
    await page.getByLabel('Zapisane widoki').selectOption('Tylko czynsze')
    await expect(page.getByLabel('Szukaj')).toHaveValue('Czynsz')

    await page.reload() // widok przetrwał odświeżenie
    await expect(page.getByLabel('Zapisane widoki').locator('option', { hasText: 'Tylko czynsze' })).toHaveCount(1)

    await page.getByLabel('Zapisane widoki').selectOption('Tylko czynsze')
    await page.getByRole('button', { name: 'Usuń widok' }).click()
    await confirmDialog(page)
    await expect(page.getByLabel('Zapisane widoki').locator('option', { hasText: 'Tylko czynsze' })).toHaveCount(0)
  })

  test('eksport widoku do CSV zawiera widoczne kolumny; zaznaczanie wierszy pokazuje akcje zbiorcze', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/invoices')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    const table = page.getByRole('table', { name: 'Lista faktur' })
    await expect(table.locator('tbody tr:not(.empty-row)').first()).toBeVisible()

    const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'Eksport widoku' }).click()])
    expect(download.suggestedFilename()).toBe('faktury-widok.csv')
    const chunks: Buffer[] = []
    for await (const c of await download.createReadStream()) chunks.push(c as Buffer)
    const text = Buffer.concat(chunks).toString('utf8')
    expect(text.split('\r\n')[0].replace(/"/g, '')).toContain('Numer;Rodzaj;Kontrahent;Wystawiona;Termin;Brutto;Do zapłaty;Status')
    expect(text.split('\r\n').length).toBeGreaterThan(2)

    await table.locator('tbody tr:not(.empty-row)').first().getByRole('checkbox').check()
    await expect(page.getByRole('group', { name: 'Akcje dla zaznaczonych' })).toContainText('Zaznaczono: 1')
    await page.getByRole('button', { name: 'Wyczyść zaznaczenie' }).click()
    await expect(page.getByRole('group', { name: 'Akcje dla zaznaczonych' })).toHaveCount(0)
  })

  test('faktury: sortowanie po brutto i stopka z sumami niezależna od sortowania', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/invoices')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    const table = page.getByRole('table', { name: 'Lista faktur' })
    await expect(table.locator('tbody tr:not(.empty-row)').first()).toBeVisible()
    const remainingBefore = await page.getByTestId('invoices-remaining').textContent()

    await table.getByRole('columnheader', { name: /Brutto/ }).getByRole('button').click()
    await expect(table.getByRole('columnheader', { name: /Brutto/ })).toHaveAttribute('aria-sort', 'ascending')
    await expect.poll(async () => {
      const v = (await columnValues(page, 'Lista faktur', 7)).map(num)
      return v.every((x, i) => i === 0 || v[i - 1] <= x)
    }).toBe(true)
    await expect(page.getByTestId('invoices-remaining')).toHaveText(remainingBefore!)
  })

  test('kontrahenci i plan kont: filtr kolumnowy i sortowanie lokalne', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    for (const name of ['Zzz Tabela Test', 'Aaa Tabela Test'])
      expect((await request.post('/api/v1/contractors', { headers, data: { name } })).status()).toBeLessThan(300)

    await page.goto('/contractors')
    const table = page.getByRole('table', { name: 'Lista kontrahentów' })
    await expect(table.getByText('Zzz Tabela Test')).toBeVisible()
    await table.getByLabel('Filtr: Nazwa').fill('tabela test')
    await expect(table.locator('tbody tr:not(.empty-row)')).toHaveCount(2)
    await table.getByLabel('Filtr: Nazwa').fill('zzz')
    await expect(table.locator('tbody tr:not(.empty-row)')).toHaveCount(1)
    await table.getByLabel('Filtr: Nazwa').fill('nie ma takiego')
    await expect(table.getByText('Brak kontrahentów.')).toBeVisible()
    await table.getByLabel('Filtr: Nazwa').fill('tabela test')
    await table.getByRole('columnheader', { name: /Nazwa/ }).getByRole('button').click()
    await expect(table.locator('tbody tr:not(.empty-row) td:first-child').first()).toContainText('Aaa Tabela Test')
    await table.getByRole('columnheader', { name: /Nazwa/ }).getByRole('button').click()
    await expect(table.locator('tbody tr:not(.empty-row) td:first-child').first()).toContainText('Zzz Tabela Test')

    await page.goto('/accounts')
    const accounts = page.getByRole('table', { name: 'Plan kont' })
    await accounts.getByLabel('Filtr: Numer').fill('100')
    await expect(accounts.locator('tbody tr:not(.empty-row)')).toHaveCount(1)
  })

  test('API: sortowanie tylko po dozwolonych kolumnach; audyt tylko dla administratora', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const asc = await (await request.get('/api/v1/transactions?sort=amount&dir=asc&pageSize=50', { headers })).json()
    const desc = await (await request.get('/api/v1/transactions?sort=amount&dir=desc&pageSize=50', { headers })).json()
    expect(asc.items.map((i: { amount: number }) => i.amount)).toEqual([...desc.items.map((i: { amount: number }) => i.amount)].reverse())

    for (const bad of ['amount;DROP TABLE transactions', 'companyId', 'Description desc']) {
      const r = await request.get(`/api/v1/transactions?sort=${encodeURIComponent(bad)}`, { headers })
      expect(r.status(), bad).toBe(400)
      expect((await r.json()).detail).toContain('Nieprawidłowe sortowanie')
    }
    expect((await request.get('/api/v1/invoices?sort=password', { headers })).status()).toBe(400)
    expect((await request.get('/api/v1/admin/audit?sort=action&dir=asc', { headers })).status()).toBe(200)

    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await loginOk(page, VIEWER)
    const vh = { Authorization: `Bearer ${await accessToken(page)}` }
    expect((await request.get('/api/v1/transactions?sort=number&dir=desc', { headers: vh })).status()).toBe(200)
    expect((await request.get('/api/v1/admin/audit?sort=action', { headers: vh })).status()).toBe(403)
  })

  for (const theme of ['light', 'dark'] as const) {
    test(`axe: brak naruszeń serious/critical na głównych stronach (${theme})`, async ({ page }) => {
      await page.addInitScript(t => localStorage.setItem('lc.theme', t), theme)
      await loginOk(page, ADMIN)
      const pages = ['/dashboard', '/transactions', '/transactions/new', '/invoices', '/accounts', '/reports', '/contractors', '/admin']
      for (const path of pages) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await expect(page.locator('html')).toHaveAttribute('data-theme', theme)
        const res = await new AxeBuilder({ page }).analyze()
        const bad = res.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
        expect(bad.map(v => `${path}: ${v.id} – ${v.nodes.slice(0, 2).map(n => n.html.slice(0, 90)).join(' | ')}`), `axe na ${path}`).toEqual([])
      }
    })
  }
})
