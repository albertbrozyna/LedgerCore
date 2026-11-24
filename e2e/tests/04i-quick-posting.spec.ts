import { expect, test } from '@playwright/test'
import { ADMIN, accessToken, loginOk, postTransaction, squash } from './helpers'

// Plik przed 05. Księgowania są drobne i bez wpływu na wynik (Kasa ↔ Kapitał), żeby nie zmieniać danych, na których polegają późniejsze testy.

const OLGA = { email: 'olga.skroty@biuro.test', password: 'Haslo1234' } // użytkownik z rolą Podgląd utworzony w 04h
const iso = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` }

test.describe.serial('Szybkie księgowanie z klawiatury', () => {
  let postedUrl = ''
  let postedDescription = ''

  test('księgowanie 2 wierszy samą klawiaturą: konto → kwota → Enter → konto (kwota sama) → Ctrl+Enter', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions/new')
    postedDescription = `Szybkie księgowanie ${Date.now()}`
    await page.getByLabel('Opis').focus()                      // start; dalej wyłącznie klawiatura
    await page.keyboard.type(postedDescription)
    await page.keyboard.press('Tab')
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 1' })).toBeFocused()
    await page.keyboard.type('100')
    await expect(page.getByRole('option', { name: '100 Kasa' })).toBeVisible()
    await page.keyboard.press('Enter')                          // wybór konta
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 1' })).toHaveValue('100 Kasa')
    await page.keyboard.press('Tab')
    await expect(page.getByLabel('Winien w wierszu 1')).toBeFocused()
    await page.keyboard.type('12,34')
    await page.keyboard.press('Enter')                          // → pole Ma
    await expect(page.getByLabel('Ma w wierszu 1')).toBeFocused()
    await page.keyboard.press('Enter')                          // → konto w następnym wierszu
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 2' })).toBeFocused()
    await page.keyboard.type('800')
    await page.keyboard.press('Tab')                            // Tab też zatwierdza podświetloną podpowiedź
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 2' })).toHaveValue('800 Kapitał własny')
    await expect(page.getByLabel('Ma w wierszu 2')).toHaveValue('12,34')   // brakująca kwota uzupełniona różnicą
    await expect(page.getByTestId('balance-status')).toContainText('zbilansowane')

    await page.keyboard.press('Control+Enter')
    await expect(page).toHaveURL(/\/transactions\/[0-9a-f-]{36}/)
    postedUrl = page.url()
    await expect(page.getByRole('heading', { name: /Księgowanie DK/ })).toBeVisible()
    await expect(page.getByText(postedDescription)).toBeVisible()
    await expect(page.getByRole('row', { name: /100/ })).toContainText('12,34')
  })

  test('Enter w ostatnim polu dodaje wiersz; Ctrl+Enter przy niezbilansowanym księgowaniu nie księguje', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions/new')
    await page.getByLabel('Opis').fill('Test niezbilansowany')
    await page.getByRole('combobox', { name: 'Konto w wierszu 1' }).fill('100')
    await page.keyboard.press('Enter')
    await page.getByLabel('Winien w wierszu 1').fill('5')
    await page.getByRole('combobox', { name: 'Konto w wierszu 2' }).fill('800')
    await page.keyboard.press('Enter')
    await page.getByLabel('Ma w wierszu 2').fill('4')       // różnica 1,00

    await page.keyboard.press('Control+Enter')
    await expect(page.getByRole('alert')).toContainText('nie jest zbilansowane')
    await expect(page).toHaveURL(/\/transactions\/new$/)

    await page.getByLabel('Ma w wierszu 2').focus()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 3' })).toBeFocused()
    await expect(page.locator('table.lines tbody tr')).toHaveCount(3)
    await expect(page.getByTestId('balance-status')).toContainText('1,00')
  })

  test('autouzupełnianie kwoty po wyborze konta (w obie strony) i filtrowanie po nazwie bez polskich znaków', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions/new')
    await page.getByLabel('Winien w wierszu 1').fill('50')
    const box2 = page.getByRole('combobox', { name: 'Konto w wierszu 2' })
    await box2.fill('kapital')                                  // bez „ł”
    await expect(page.getByRole('option', { name: '800 Kapitał własny' })).toBeVisible()
    await page.keyboard.press('Enter')
    await expect(page.getByLabel('Ma w wierszu 2')).toHaveValue('50,00')

    await page.reload()
    await page.getByLabel('Ma w wierszu 1').fill('75,5')
    await page.getByRole('combobox', { name: 'Konto w wierszu 2' }).fill('bank')
    await page.keyboard.press('Enter')
    await expect(page.getByLabel('Winien w wierszu 2')).toHaveValue('75,50')

    // Esc zamyka listę i przywraca poprzednią wartość; nieistniejące konto – brak podpowiedzi
    const box1 = page.getByRole('combobox', { name: 'Konto w wierszu 1' })
    await box1.fill('zzzz')
    await expect(page.getByText('Brak pasujących pozycji')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(box1).toHaveValue('')
  })

  test('ostatnio używane konta są na górze listy', async ({ page }) => {
    await loginOk(page, ADMIN)
    // lista „ostatnio używane” jest pamiętana w przeglądarce (nowy kontekst testu zaczyna pusty) – zaksięguj, potem otwórz formularz ponownie
    await postTransaction(page, 'Konta do listy ostatnich', iso(), [{ account: '800 Kapitał własny', credit: '1' }, { account: '100 Kasa', debit: '1' }])
    await page.goto('/transactions/new')
    await page.getByRole('combobox', { name: 'Konto w wierszu 1' }).click()
    const list = page.getByRole('listbox', { name: /Konto w wierszu 1/ })
    await expect(list).toContainText('Ostatnio używane')
    const first = await list.getByRole('option').allTextContents()
    expect(first.slice(0, 2)).toEqual(['800 Kapitał własny', '100 Kasa'])
  })

  test('podgląd wpływu na salda zgadza się z kartą konta po zaksięgowaniu', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json() as { id: string; code: string; balance: number }[]
    const kasa = accounts.find(a => a.code === '100')!

    await page.goto('/transactions/new')
    await page.getByLabel('Opis').fill('Podgląd sald')
    await page.getByRole('combobox', { name: 'Konto w wierszu 1' }).fill('100')
    await page.keyboard.press('Enter')
    await page.getByLabel('Winien w wierszu 1').fill('20,10')
    await page.getByRole('combobox', { name: 'Konto w wierszu 2' }).fill('800')
    await page.keyboard.press('Enter')

    const preview = page.getByRole('table', { name: 'Wpływ na salda' })
    await expect(preview.getByTestId('preview-before-100')).not.toHaveText('…')
    const before = squash(await preview.getByTestId('preview-before-100').textContent())
    expect(before).toBe(squash(kasa.balance.toFixed(2).replace('.', ',')))
    await expect(preview.getByTestId('preview-change-100')).toHaveText(/20,10/)
    const after = squash(await preview.getByTestId('preview-after-100').textContent())
    // kapitał (pasywa) rośnie po stronie Ma – zmiana dodatnia w znaku naturalnym konta
    await expect(preview.getByTestId('preview-change-800')).toHaveText(/20,10/)

    await page.getByRole('button', { name: 'Zaksięguj' }).click()
    await expect(page).toHaveURL(/\/transactions\/[0-9a-f-]{36}/)

    await page.goto(`/accounts/${kasa.id}/ledger`)
    await expect(page.getByTestId('ledger-closing')).toBeVisible()
    expect(squash(await page.getByTestId('ledger-closing').textContent())).toBe(after)
  })

  test('„Kopiuj jako nowe” ze szczegółów i z listy: opis i wiersze zachowane, data na dziś', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto(postedUrl)
    await page.getByRole('button', { name: 'Kopiuj jako nowe' }).click()
    await expect(page).toHaveURL(/\/transactions\/new\?copy=/)
    await expect(page.getByLabel('Opis')).toHaveValue(postedDescription)
    await expect(page.getByLabel('Data', { exact: true })).toHaveValue(iso())
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 1' })).toHaveValue('100 Kasa')
    await expect(page.getByLabel('Winien w wierszu 1')).toHaveValue('12,34')
    await expect(page.getByRole('combobox', { name: 'Konto w wierszu 2' })).toHaveValue('800 Kapitał własny')
    await expect(page.getByLabel('Ma w wierszu 2')).toHaveValue('12,34')
    await expect(page.getByRole('status')).toContainText('Skopiowano księgowanie DK/')
    await expect(page.getByTestId('balance-status')).toContainText('zbilansowane')

    // z listy
    await page.goto('/transactions')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    await page.getByLabel('Szukaj').fill(postedDescription)
    const row = page.getByRole('row', { name: new RegExp(postedDescription) })
    await row.getByRole('link', { name: 'Kopiuj jako nowe' }).click()
    await expect(page.getByLabel('Opis')).toHaveValue(postedDescription)

    // kopia zaksięgowana osobno – jest nowym dokumentem
    await page.getByRole('button', { name: 'Zaksięguj' }).click()
    await expect(page).toHaveURL(/\/transactions\/[0-9a-f-]{36}/)
    expect(page.url()).not.toBe(postedUrl)
  })

  test('rola Podgląd: brak kopiowania i księgowania; salda przez API dostępne tylko do odczytu', async ({ page, request }) => {
    await loginOk(page, OLGA)
    await page.goto('/transactions')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    await expect(page.getByRole('table', { name: 'Dziennik księgowań' }).locator('tbody tr').first()).toBeVisible()
    await expect(page.getByRole('link', { name: 'Kopiuj jako nowe' })).toHaveCount(0)

    await page.goto(postedUrl)
    await expect(page.getByRole('heading', { name: /Księgowanie DK/ })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Kopiuj jako nowe' })).toHaveCount(0)

    await page.goto('/transactions/new')
    await expect(page.getByRole('alert')).toContainText('nie pozwala na księgowanie')

    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json() as { id: string }[]
    const ok = await request.post('/api/v1/accounts/balances', { headers, data: { accountIds: [accounts[0].id] } })
    expect(ok.status()).toBe(200)
    expect((await ok.json())).toHaveLength(1)
    expect((await request.post('/api/v1/accounts/balances', { headers, data: { accountIds: Array.from({ length: 51 }, () => accounts[0].id).map((_, i) => `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`) } })).status()).toBe(400)
    expect((await request.post('/api/v1/accounts/balances', { data: { accountIds: [] } })).status()).toBe(401)
  })
})
