import { expect, test, type Page } from '@playwright/test'
import { ADMIN, accessToken, login, loginOk, logout, postTransaction } from './helpers'

const SECOND = 'Klient Drugi sp. z o.o.'
const FIRST = 'Biuro Rachunkowe Test sp. z o.o.'
const NIP_SECOND = '774-000-14-54'
const NIP_SHARED_CONTRACTOR = '526-104-08-28' // ten sam NIP kontrahenta co w pierwszej firmie
const USER2 = { first: 'Piotr', last: 'Drugi', email: 'piotr.drugi@biuro.test', password: 'Haslo1234' }

const today = () => {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

async function switchTo(page: Page, name: string) {
  await page.getByLabel('Firma', { exact: true }).selectOption({ label: name })
  await expect(page).toHaveURL(/\/dashboard/)
  await expect(page.getByLabel('Firma', { exact: true }).locator('option:checked')).toHaveText(name)
  await expect(page.getByTestId('tile-assets')).toBeVisible()
}

async function companyIds(page: Page, request: import('@playwright/test').APIRequestContext) {
  const headers = { Authorization: `Bearer ${await accessToken(page)}` }
  const list = await (await request.get('/api/v1/companies', { headers })).json() as { id: string; name: string }[]
  return { headers, byName: (n: string) => list.find(c => c.name === n)!.id }
}

test.describe.serial('Wielofirmowość: izolacja danych, przełączanie, dostęp użytkowników', () => {
  test('administrator dodaje firmę (walidacja NIP, unikalność) z domyślnym planem kont', async ({ page }) => {
    await loginOk(page, ADMIN)
    await expect(page.getByTestId('company-name')).toHaveText(FIRST) // jedna firma: sama nazwa, bez przełącznika
    await page.getByRole('link', { name: 'Firmy', exact: true }).click()
    const form = page.getByRole('form', { name: 'Nowa firma' })

    await form.getByLabel('Nazwa firmy').fill(SECOND)
    await form.getByLabel('NIP').fill('1234567890')
    await form.getByRole('button', { name: 'Dodaj firmę' }).click()
    await expect(page.getByRole('alert')).toContainText('Nieprawidłowy NIP')

    await form.getByLabel('NIP').fill(NIP_SECOND)
    await form.getByLabel('Adres').fill('ul. Klienta 2, Kraków')
    await form.getByRole('button', { name: 'Dodaj firmę' }).click()
    await expect(page.getByRole('status')).toContainText(`Dodano firmę ${SECOND}`)
    await expect(page.getByRole('table', { name: 'Lista firm' }).getByRole('row', { name: new RegExp(SECOND) })).toBeVisible()

    await form.getByLabel('Nazwa firmy').fill('Duplikat NIP')
    await form.getByLabel('NIP').fill(NIP_SECOND)
    await form.getByRole('button', { name: 'Dodaj firmę' }).click()
    await expect(page.getByRole('alert')).toContainText('już istnieje')
  })

  test('druga firma jest pusta i niezależna: własny plan kont, numeracja, kontrahenci z tym samym NIP', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchTo(page, SECOND)

    // dane pierwszej firmy są niewidoczne
    await expect(page.getByTestId('tile-assets')).toHaveText(/^0,00$/)
    await page.goto('/contractors')
    await expect(page.getByText('Brak kontrahentów')).toBeVisible()
    await page.goto('/invoices')
    await expect(page.getByText('Brak faktur')).toBeVisible()
    await page.goto('/transactions')
    await expect(page.getByText('Brak księgowań')).toBeVisible()
    await page.goto('/accounts')
    await expect(page.getByRole('row', { name: /130 Rachunek bankowy/ })).toBeVisible() // własny, domyślny plan kont

    // ten sam NIP kontrahenta co w pierwszej firmie – dozwolony, bo unikalność jest per firma
    await page.goto('/contractors')
    const form = page.getByRole('form', { name: 'Kontrahent' })
    await form.getByLabel('Nazwa').fill('Acme (u klienta 2)')
    await form.getByLabel('NIP').fill(NIP_SHARED_CONTRACTOR)
    await form.getByRole('button', { name: 'Dodaj kontrahenta' }).click()
    await expect(page.getByRole('status')).toContainText('Dodano kontrahenta')

    // numeracja zaczyna się od 0001 niezależnie od pierwszej firmy
    await postTransaction(page, 'Wpłata kapitału', today(), [
      { account: '130 Rachunek bankowy', debit: '5000' }, { account: '800 Kapitał własny', credit: '5000' },
    ])
    await expect(page.getByRole('heading', { name: /Księgowanie DK\/\d{4}\/0001/ })).toBeVisible()
    await page.goto('/invoices/new?type=Sales')
    await page.getByLabel('Nabywca').selectOption({ label: 'Acme (u klienta 2) (NIP 5261040828)' })
    await page.getByLabel('Opis pozycji 1').fill('Usługa dla klienta 2')
    await page.getByLabel('Netto pozycji 1').fill('200')
    await page.getByRole('button', { name: 'Wystaw i zaksięguj' }).click()
    await expect(page.getByRole('heading', { name: /Faktura FV\/\d{4}\/0001/ })).toBeVisible()

    await page.goto('/dashboard')
    await expect(page.getByTestId('tile-assets')).toHaveText(/5\s?246,00/) // 5000 w banku + 246 należności
  })

  test('dane firmy (wystawca) są per firma, a zamknięcie roku i audyt nie przenikają między firmami', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchTo(page, SECOND)
    await page.goto('/admin')
    const form = page.getByRole('form', { name: 'Dane firmy' })
    await expect(form.getByLabel('Nazwa firmy')).toHaveValue(SECOND)
    await expect(form.getByLabel('NIP')).toHaveValue('7740001454')
    await form.getByLabel('Numer rachunku bankowego').fill('22 2222 2222 2222 2222 2222 2222')
    await form.getByRole('button', { name: 'Zapisz dane firmy' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano dane firmy')

    // druga firma nie ma zamkniętych okresów, audyt nie zawiera operacji pierwszej firmy
    await expect(page.getByTestId('lock-status')).toContainText('Żaden okres nie jest zamknięty')
    const audit = page.getByRole('table', { name: 'Dziennik zdarzeń' })
    await expect(audit).toContainText('Zarejestrowano fakturę')
    await expect(audit).not.toContainText('Zamknięto rok')
    await expect(audit).not.toContainText('Dostawca SA')

    // wydruk faktury z danymi drugiej firmy
    await page.goto('/invoices')
    await page.getByRole('row', { name: /Acme \(u klienta 2\)/ }).getByRole('link').first().click()
    await page.getByRole('link', { name: 'Drukuj / PDF' }).click()
    await expect(page.getByTestId('invoice-sheet')).toContainText(SECOND)
    await expect(page.getByTestId('invoice-sheet')).toContainText('22 2222 2222 2222 2222 2222 2222')
    await expect(page.getByTestId('invoice-sheet')).not.toContainText('11 1111 1111 1111 1111 1111 1111')

    // pierwsza firma: rok zamknięty (test 05) – nadal, i jej dane są nietknięte
    await page.goto('/dashboard')
    await switchTo(page, FIRST)
    await page.goto('/admin')
    await expect(page.getByTestId('lock-status')).toContainText('-12-31')
    await expect(page.getByRole('table', { name: 'Dziennik zdarzeń' })).toContainText('Zamknięto rok')
    await page.goto('/invoices')
    await expect(page.getByRole('row', { name: /Acme Klient/ }).first()).toBeVisible()
    await expect(page.getByRole('row', { name: /Acme \(u klienta 2\)/ })).toHaveCount(0)
  })

  test('API: rekord drugiej firmy jest „nieistniejący” w kontekście pierwszej, a niedozwolona firma daje 403', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const { headers, byName } = await companyIds(page, request)
    const first = { ...headers, 'X-Company-Id': byName(FIRST) }
    const second = { ...headers, 'X-Company-Id': byName(SECOND) }

    const secondTx = await (await request.get('/api/v1/transactions', { headers: second })).json()
    expect(secondTx.totalCount).toBe(2) // wpłata kapitału + księgowanie faktury
    const txId = secondTx.items.find((t: { description: string }) => t.description === 'Wpłata kapitału').id as string
    expect((await request.get(`/api/v1/transactions/${txId}`, { headers: second })).status()).toBe(200)
    const crossRead = await request.get(`/api/v1/transactions/${txId}`, { headers: first })
    expect(crossRead.status()).toBe(400)
    expect((await crossRead.json()).detail).toContain('Nie znaleziono księgowania')

    // storno cudzego księgowania też nie przejdzie
    expect((await request.post(`/api/v1/transactions/${txId}/reverse`, { headers: first, data: {} })).status()).toBe(400)

    // wymyślona firma
    const fake = await request.get('/api/v1/accounts', { headers: { ...headers, 'X-Company-Id': '99999999-9999-9999-9999-999999999999' } })
    expect(fake.status()).toBe(403)

    // księgowy z pierwszej firmy (jan) nie ma dostępu do drugiej
    await logout(page)
    await loginOk(page, { email: 'jan@biuro.test', password: 'Trzecie123' })
    const jan = { Authorization: `Bearer ${await accessToken(page)}` }
    expect((await request.get('/api/v1/accounts', { headers: { ...jan, 'X-Company-Id': byName(SECOND) } })).status()).toBe(403)
    expect((await request.get('/api/v1/accounts', { headers: { ...jan, 'X-Company-Id': byName(FIRST) } })).status()).toBe(200)
    const mine = await (await request.get('/api/v1/companies/mine', { headers: jan })).json()
    expect(mine.map((c: { name: string }) => c.name)).toEqual([FIRST])
    expect((await request.get('/api/v1/companies', { headers: jan })).status()).toBe(403) // lista wszystkich firm tylko dla administratora
  })

  test('użytkownik dodany w drugiej firmie widzi tylko ją, dopóki administrator nie przyzna dostępu do kolejnych', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchTo(page, SECOND)
    await page.goto('/users')
    const form = page.getByRole('form', { name: 'Nowy użytkownik' })
    await form.getByLabel('Imię').fill(USER2.first)
    await form.getByLabel('Nazwisko').fill(USER2.last)
    await form.getByLabel('E-mail').fill(USER2.email)
    await form.getByLabel('Hasło').fill(USER2.password)
    await form.getByLabel('Rola').selectOption('Accountant')
    await form.getByRole('button', { name: 'Dodaj użytkownika' }).click()
    await expect(page.getByRole('status')).toContainText(`Dodano użytkownika ${USER2.email}`)
    await logout(page)

    // użytkownik widzi wyłącznie drugą firmę
    await loginOk(page, USER2)
    await expect(page.getByTestId('company-name')).toHaveText(SECOND)
    await expect(page.getByLabel('Firma', { exact: true })).toHaveCount(0)
    await expect(page.getByTestId('tile-assets')).toHaveText(/5\s?246,00/)
    await page.goto('/invoices')
    await expect(page.getByRole('row', { name: /Acme \(u klienta 2\)/ })).toBeVisible()
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const { byName } = await (async () => {
      await logout(page); await loginOk(page, ADMIN)
      return companyIds(page, request)
    })()
    expect((await request.get('/api/v1/accounts', { headers: { ...headers, 'X-Company-Id': byName(FIRST) } })).status()).toBe(403)

    // administrator przyznaje dostęp do pierwszej firmy
    await page.goto('/users')
    await page.getByRole('row', { name: new RegExp(USER2.email) }).getByRole('button', { name: 'Firmy' }).click()
    const group = page.getByRole('group', { name: `Firmy użytkownika ${USER2.email}` })
    await expect(group.getByLabel(SECOND)).toBeChecked()
    await group.getByLabel(FIRST).check()
    await page.getByRole('button', { name: 'Zapisz dostęp' }).click()
    await expect(page.getByRole('status')).toContainText(`Zapisano dostęp do firm: ${USER2.email}`)
    await logout(page)

    await loginOk(page, USER2)
    const select = page.getByLabel('Firma', { exact: true })
    await expect(select).toBeVisible()
    await expect(select.locator('option')).toHaveText([SECOND, FIRST].sort((a, b) => a.localeCompare(b, 'pl')))
    await switchTo(page, FIRST)
    await page.goto('/invoices')
    await expect(page.getByRole('row', { name: /Acme Klient/ }).first()).toBeVisible()
  })

  test('dezaktywacja firmy: bieżącej nie można, nieaktywna znika z listy i jest niedostępna', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchTo(page, FIRST)
    await page.goto('/companies')
    const form = page.getByRole('form', { name: 'Nowa firma' })
    await form.getByLabel('Nazwa firmy').fill('Firma do zamknięcia')
    await form.getByRole('button', { name: 'Dodaj firmę' }).click()
    await expect(page.getByRole('status')).toContainText('Dodano firmę Firma do zamknięcia')

    const table = page.getByRole('table', { name: 'Lista firm' })
    await table.getByRole('row', { name: new RegExp(FIRST) }).getByRole('button', { name: 'Dezaktywuj' }).click()
    await expect(page.getByRole('alert')).toContainText('aktualnie pracujesz')

    await table.getByRole('row', { name: /Firma do zamknięcia/ }).getByRole('button', { name: 'Dezaktywuj' }).click()
    await expect(table.getByRole('row', { name: /Firma do zamknięcia/ })).toContainText('Nieaktywna')

    const { headers, byName } = await companyIds(page, request)
    expect((await request.get('/api/v1/accounts', { headers: { ...headers, 'X-Company-Id': byName('Firma do zamknięcia') } })).status()).toBe(403)
    await page.reload()
    await expect(page.getByLabel('Firma', { exact: true }).locator('option', { hasText: 'Firma do zamknięcia' })).toHaveCount(0)

    // zalogowanie na świeżo wybiera pierwszą dostępną firmę nawet przy nieaktualnym zapamiętaniu
    await logout(page)
    await page.evaluate(() => localStorage.setItem('lc.company', '99999999-9999-9999-9999-999999999999'))
    await login(page, ADMIN.email, ADMIN.password)
    await expect(page).toHaveURL(/\/dashboard/)
    await expect(page.getByTestId('tile-assets')).toBeVisible()
  })
})
