import { expect, test } from '@playwright/test'
import { ADMIN, accessToken, loginOk } from './helpers'

// Plik przed 05: tylko odczyty, bez nowych księgowań.

test.describe.serial('Wyszukiwarka (Ctrl+K) i skróty klawiaturowe', () => {
  test('Ctrl+K → numer faktury → Enter otwiera fakturę', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const list = await (await request.get('/api/v1/invoices?type=Sales&from=2000-01-01&pageSize=5', { headers })).json()
    const inv = list.items[0] as { id: string; number: string }
    expect(inv).toBeTruthy()

    await page.keyboard.press('Control+k')
    const dialog = page.getByRole('dialog', { name: 'Wyszukiwarka i polecenia' })
    await expect(dialog).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Szukaj w programie' })).toBeFocused()
    await page.getByRole('combobox', { name: 'Szukaj w programie' }).fill(inv.number)
    const option = dialog.getByRole('option', { name: new RegExp(inv.number.replace('/', '\\/')) }).first()
    await expect(option).toBeVisible()
    await expect(dialog.getByRole('group', { name: 'Faktury' })).toBeVisible()
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(new RegExp(`/invoices/${inv.id}$`))
    await expect(dialog).toBeHidden()
  })

  test('wyniki: kontrahent po fragmencie nazwy, konto po numerze, księgowanie po opisie', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible() // aplikacja załadowana (skrót jest globalnym nasłuchem)
    await page.keyboard.press('Control+k')
    const box = page.getByRole('combobox', { name: 'Szukaj w programie' })
    const dialog = page.getByRole('dialog')

    await box.fill('Tabela Test')
    await expect(dialog.getByRole('group', { name: 'Kontrahenci' }).getByRole('option')).toHaveCount(2)
    await expect(dialog.getByRole('group', { name: 'Kontrahenci' }).getByRole('option').first()).toContainText('Aaa Tabela Test')

    await box.fill('Czynsz')
    await expect(dialog.getByRole('group', { name: 'Księgowania' }).getByRole('option').first()).toContainText('Czynsz')

    await box.fill('130')
    await dialog.getByRole('group', { name: 'Konta' }).getByRole('option', { name: /Rachunek bankowy/ }).click()
    await expect(page).toHaveURL(/\/accounts\/[0-9a-f-]{36}\/ledger$/)
  })

  test('polecenia: filtrowanie, strzałki i Enter; Esc oraz ponowne Ctrl+K zamykają', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.keyboard.press('Control+k')
    const box = page.getByRole('combobox', { name: 'Szukaj w programie' })
    await expect(page.getByRole('option', { name: /Nowe księgowanie/ })).toBeVisible()

    await box.fill('rapor')                       // bez względu na wielkość liter
    await expect(page.getByRole('option')).toHaveCount(1)
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/\/reports$/)

    await page.keyboard.press('Control+k')
    await box.fill('księgow')                    // „Dziennik księgowań”, „Nowe księgowanie”
    await expect(page.getByRole('option').first()).toHaveAttribute('aria-selected', 'true')
    await page.keyboard.press('ArrowDown')
    await expect(page.getByRole('option').nth(1)).toHaveAttribute('aria-selected', 'true')
    await page.keyboard.press('ArrowUp')
    await expect(page.getByRole('option').first()).toHaveAttribute('aria-selected', 'true')

    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toBeHidden()
    await page.keyboard.press('Control+k')
    await expect(page.getByRole('dialog')).toBeVisible()
    await page.keyboard.press('Control+k')        // przełącznik
    await expect(page.getByRole('dialog')).toBeHidden()
    await page.getByRole('button', { name: /Wyszukiwarka/ }).click()
    await expect(page.getByRole('dialog')).toBeVisible()
  })

  test('zapytanie z wildcardami i krótkie nie zwracają wyników z danych', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.keyboard.press('Control+k')
    const box = page.getByRole('combobox', { name: 'Szukaj w programie' })
    await box.fill('%%')
    await expect(page.getByText('Brak wyników.')).toBeVisible()
    await box.fill('_')
    await expect(page.getByRole('option', { name: /Nowe księgowanie/ })).toHaveCount(0) // „_” nie pasuje do żadnego polecenia
  })

  test('skróty N i F nawigują, ? pokazuje pomoc, Esc ją zamyka', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.keyboard.press('n')
    await expect(page).toHaveURL(/\/transactions\/new$/)
    await page.keyboard.press('f')
    await expect(page).toHaveURL(/\/invoices\/new\?type=Sales$/)

    await page.keyboard.press('Shift+?')
    const help = page.getByRole('dialog', { name: 'Skróty klawiaturowe' })
    await expect(help).toBeVisible()
    await expect(help.getByRole('table', { name: 'Lista skrótów' })).toContainText('Ctrl + K')
    await page.keyboard.press('n')                       // przy otwartym oknie skróty nie działają
    await expect(page).toHaveURL(/\/invoices\/new/)
    await page.keyboard.press('Escape')
    await expect(help).toBeHidden()
  })

  test('skróty nie reagują podczas pisania w polach, ale Ctrl+K działa także tam', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions')
    const search = page.getByLabel('Szukaj')
    await search.click()
    await page.keyboard.type('nf?')
    await expect(search).toHaveValue('nf?')
    await expect(page).toHaveURL(/\/transactions$/)
    await expect(page.getByRole('dialog')).toHaveCount(0)

    await page.getByLabel('Konto').focus()                 // pole wyboru też jest „polem”
    await page.keyboard.press('n')
    await expect(page).toHaveURL(/\/transactions$/)

    await search.click()
    await page.keyboard.press('Control+k')
    await expect(page.getByRole('dialog', { name: 'Wyszukiwarka i polecenia' })).toBeVisible()
  })

  test('rola Podgląd: bez skrótów i poleceń zapisu, ale z wyszukiwarką', async ({ page, request }) => {
    // VIEWER z testu 03 został awansowany na księgowego – tworzymy osobnego użytkownika z rolą Podgląd
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const viewer = { first: 'Olga', last: 'Skroty', email: 'olga.skroty@biuro.test', password: 'Haslo1234' }
    const created = await request.post('/api/v1/users/create', { headers, data: { firstName: viewer.first, lastName: viewer.last, email: viewer.email, password: viewer.password, role: 'Viewer', phoneNumber: '' } })
    expect(created.status()).toBeLessThan(300)
    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await loginOk(page, viewer)
    await page.keyboard.press('n')
    await page.keyboard.press('f')
    await expect(page).toHaveURL(/\/dashboard$/)

    await page.keyboard.press('Control+k')
    await expect(page.getByRole('option', { name: 'Raporty' })).toBeVisible()
    await expect(page.getByRole('option', { name: /Nowe księgowanie/ })).toHaveCount(0)
    await expect(page.getByRole('option', { name: /Nowa faktura sprzedaży/ })).toHaveCount(0)
    await expect(page.getByRole('option', { name: 'Administracja' })).toHaveCount(0)
    await page.getByRole('combobox', { name: 'Szukaj w programie' }).fill('Czynsz')
    await expect(page.getByRole('group', { name: 'Księgowania' }).getByRole('option').first()).toBeVisible()
  })

  test('API: /search wymaga logowania i ucina zbyt krótkie zapytania', async ({ page, request }) => {
    expect((await request.get('/api/v1/search?q=Czynsz')).status()).toBe(401)
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const short = await (await request.get('/api/v1/search?q=a', { headers })).json()
    expect(short.accounts.length + short.contractors.length + short.invoices.length + short.transactions.length).toBe(0)
    const ok = await (await request.get('/api/v1/search?q=Czynsz', { headers })).json()
    expect(ok.transactions.length).toBeGreaterThan(0)
    expect(ok.transactions.length).toBeLessThanOrEqual(5)
  })
})
