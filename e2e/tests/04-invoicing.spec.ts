import { expect, test, type Page } from '@playwright/test'
import { ADMIN, accessToken, login, loginOk, logout, confirmDialog } from './helpers'

const iso = (offsetDays = 0) => {
  const d = new Date(); d.setDate(d.getDate() + offsetDays)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
const THIS_YEAR_START = `${new Date().getFullYear()}-01-01`
const TODAY = iso()

// NIP-y z poprawną sumą kontrolną
const NIP_CLIENT = '526-104-08-28'
const NIP_SUPPLIER = '774-000-14-54'

const PODGLAD = { email: 'podglad@biuro.test', password: 'Haslo1234' }

async function addContractor(page: Page, name: string, nip: string) {
  await page.goto('/contractors')
  const form = page.getByRole('form', { name: 'Kontrahent' })
  await form.getByLabel('Nazwa').fill(name)
  await form.getByLabel('NIP').fill(nip)
  await form.getByRole('button', { name: 'Dodaj kontrahenta' }).click()
}

test.describe.serial('Faktury, VAT, rozrachunki, zamknięcie okresu i audyt', () => {
  test.beforeEach(async ({ page }) => { await loginOk(page, ADMIN) })

  test('kontrahenci: walidacja NIP (suma kontrolna i unikalność)', async ({ page }) => {
    await addContractor(page, 'Zły NIP sp. z o.o.', '1234567890')
    await expect(page.getByRole('alert')).toContainText('Nieprawidłowy NIP')

    await addContractor(page, 'Acme Klient sp. z o.o.', NIP_CLIENT)
    await expect(page.getByRole('status')).toContainText('Dodano kontrahenta Acme Klient')
    await expect(page.getByRole('row', { name: /Acme Klient/ })).toContainText('5261040828')

    await addContractor(page, 'Dostawca SA', NIP_SUPPLIER)
    await expect(page.getByRole('status')).toContainText('Dodano kontrahenta Dostawca SA')

    await addContractor(page, 'Duplikat', NIP_CLIENT)
    await expect(page.getByRole('alert')).toContainText('już istnieje')

    // wyszukiwanie po NIP
    await page.getByLabel('Szukaj').fill('7740001454')
    await expect(page.getByRole('row', { name: /Dostawca SA/ })).toBeVisible()
    await expect(page.getByRole('row', { name: /Acme Klient/ })).toHaveCount(0)
  })

  test('faktura sprzedaży: wyliczenie VAT, numeracja i automatyczne księgowanie', async ({ page }) => {
    await page.goto('/invoices/new?type=Sales')
    await page.getByLabel('Nabywca').selectOption({ label: 'Acme Klient sp. z o.o. (NIP 5261040828)' })
    await page.getByLabel('Opis pozycji 1').fill('Prowadzenie ksiąg – listopad')
    await page.getByLabel('Netto pozycji 1').fill('1000')
    await page.getByRole('button', { name: '+ Dodaj pozycję' }).click()
    await page.getByLabel('Opis pozycji 2').fill('Materiały drukowane')
    await page.getByLabel('Netto pozycji 2').fill('100')
    await page.getByLabel('Stawka VAT pozycji 2').selectOption('R5')

    await expect(page.getByTestId('inv-net')).toHaveText(/1\s?100,00/)
    await expect(page.getByTestId('inv-vat')).toHaveText(/235,00/) // 230 (23%) + 5 (5%)
    await expect(page.getByTestId('inv-gross')).toHaveText(/1\s?335,00/)
    await page.getByRole('button', { name: 'Wystaw i zaksięguj' }).click()

    await expect(page.getByRole('heading', { name: /Faktura FV\/\d{4}\/0001/ })).toBeVisible()
    await expect(page.getByTestId('invoice-gross')).toHaveText(/1\s?335,00/)
    await expect(page.getByTestId('invoice-status')).toHaveText('Do zapłaty')

    // księgowanie: Wn 201 brutto / Ma 700 netto + Ma 220 VAT
    await page.getByRole('link', { name: /^DK\// }).click()
    await expect(page.getByRole('row', { name: /201 Rozrachunki z odbiorcami/ })).toContainText(/1\s?335,00/)
    await expect(page.getByRole('row', { name: /700 Przychody ze sprzedaży usług/ })).toContainText(/1\s?100,00/)
    await expect(page.getByRole('row', { name: /220 Rozrachunki z urzędami/ })).toContainText('235,00')
  })

  test('płatności częściowe i pełne, kontrola nadpłaty, zmiana statusu', async ({ page }) => {
    await page.goto('/invoices')
    await page.getByRole('row', { name: /Acme Klient/ }).getByRole('link').first().click()

    const pay = page.getByRole('form', { name: 'Nowa płatność' })
    await pay.getByLabel('Kwota').fill('500')
    await pay.getByLabel('Wpłynęło na konto').selectOption({ label: '130 Rachunek bankowy' })
    await pay.getByRole('button', { name: 'Zarejestruj płatność' }).click()
    await expect(page.getByRole('status')).toContainText('Zarejestrowano płatność')
    await expect(page.getByTestId('invoice-paid')).toHaveText(/500,00/)
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/835,00/)

    await pay.getByLabel('Kwota').fill('9999')
    await pay.getByRole('button', { name: 'Zarejestruj płatność' }).click()
    await expect(page.getByRole('alert')).toContainText('przekracza pozostałe do zapłaty')

    // z płatnościami nie można anulować faktury
    await expect(page.getByRole('button', { name: 'Anuluj fakturę' })).toHaveCount(0)

    await pay.getByRole('button', { name: 'Cała kwota' }).click()
    await pay.getByRole('button', { name: 'Zarejestruj płatność' }).click()
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/^0,00$/)
    await expect(page.getByTestId('invoice-status')).toHaveText('Opłacona')
    await expect(page.getByRole('form', { name: 'Nowa płatność' })).toHaveCount(0)
  })

  test('faktura zakupu po terminie: duplikat numeru, rozrachunki i kafelki pulpitu', async ({ page }) => {
    const register = async () => {
      await page.goto('/invoices/new?type=Purchase')
      await page.getByLabel('Dostawca').selectOption({ label: 'Dostawca SA (NIP 7740001454)' })
      await page.getByLabel('Numer faktury dostawcy').fill('FZ/77/2026')
      await page.getByLabel('Data wystawienia').fill(iso(-30))
      await page.getByLabel('Termin płatności').fill(iso(-10))
      await page.getByLabel('Opis pozycji 1').fill('Licencja oprogramowania')
      await page.getByLabel('Netto pozycji 1').fill('200')
      await page.getByRole('button', { name: 'Zarejestruj i zaksięguj' }).click()
    }
    await register()
    await expect(page.getByRole('heading', { name: 'Faktura FZ/77/2026' })).toBeVisible()
    await expect(page.getByTestId('invoice-gross')).toHaveText(/246,00/)
    await expect(page.getByTestId('invoice-status')).toHaveText('Po terminie')

    await register() // ten sam numer tego samego dostawcy
    await expect(page.getByRole('alert')).toContainText('jest już zarejestrowana')

    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Rozrachunki' }).click()
    await page.getByRole('tab', { name: 'Zobowiązania' }).click()
    await expect(page.getByRole('row', { name: /Dostawca SA/ })).toBeVisible()
    await expect(page.getByTestId('settle-total')).toHaveText(/246,00/)
    await expect(page.getByTestId('settle-overdue')).toHaveText(/246,00/)

    await page.goto('/dashboard')
    await expect(page.getByTestId('tile-payables')).toHaveText(/246,00/)
    await expect(page.getByTestId('tile-payables-overdue')).toHaveText(/246,00/)
    await expect(page.getByTestId('tile-receivables')).toHaveText(/^0,00$/) // sprzedaż opłacona

    await page.goto('/invoices?contractorId=')
    await page.getByLabel('Status').selectOption('overdue')
    await expect(page.getByRole('row', { name: /FZ\/77\/2026/ })).toBeVisible()
  })

  test('rozliczenie VAT: należny − naliczony oraz rejestry', async ({ page }) => {
    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Rejestry VAT' }).click()
    await page.getByLabel('Od', { exact: true }).fill(THIS_YEAR_START)
    await page.getByLabel('Do', { exact: true }).fill(TODAY)
    await expect(page.getByTestId('vat-output')).toHaveText(/235,00/)
    await expect(page.getByTestId('vat-input')).toHaveText(/46,00/)
    await expect(page.getByTestId('vat-due')).toHaveText(/189,00/)

    await expect(page.getByRole('row', { name: /Acme Klient/ })).toBeVisible()
    await page.getByRole('tab', { name: 'Rejestr zakupów' }).click()
    await expect(page.getByRole('row', { name: /FZ\/77\/2026/ })).toBeVisible()
    await expect(page.getByTestId('vatreg-total-vat')).toHaveText(/46,00/)
  })

  test('anulowanie faktury robi storno i wyłącza ją z VAT; księgowania faktury nie da się stornować ręcznie', async ({ page, request }) => {
    await page.goto('/invoices/new?type=Sales')
    await page.getByLabel('Nabywca').selectOption({ label: 'Acme Klient sp. z o.o. (NIP 5261040828)' })
    await page.getByLabel('Opis pozycji 1').fill('Usługa do anulowania')
    await page.getByLabel('Netto pozycji 1').fill('100')
    await page.getByRole('button', { name: 'Wystaw i zaksięguj' }).click()
    await expect(page.getByRole('heading', { name: /Faktura FV\/\d{4}\/0002/ })).toBeVisible()
    const invoiceUrl = page.url()

    // ręczne storno księgowania faktury jest zablokowane (serwer)
    const txId = (await page.getByRole('link', { name: /^DK\// }).getAttribute('href'))!.split('/').pop()
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const res = await request.post(`/api/v1/transactions/${txId}/reverse`, { headers, data: {} })
    expect(res.status()).toBe(400)
    expect((await res.json()).detail).toContain('anuluj fakturę')

    await page.getByRole('button', { name: 'Anuluj fakturę' }).click()
    await confirmDialog(page)
    await expect(page.getByTestId('invoice-status')).toHaveText('Anulowana')

    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Rejestry VAT' }).click()
    await page.getByLabel('Od', { exact: true }).fill(THIS_YEAR_START)
    await page.getByLabel('Do', { exact: true }).fill(TODAY)
    await expect(page.getByTestId('vat-output')).toHaveText(/235,00/) // bez zmian
    await page.goto(invoiceUrl)
    await expect(page.getByRole('button', { name: 'Anuluj fakturę' })).toHaveCount(0)
  })

  test('zamknięcie okresu blokuje księgowanie i faktury w zamkniętym okresie', async ({ page, request }) => {
    await page.goto('/admin')
    await page.getByLabel('Zamknij do dnia').fill(iso(-20))
    await page.getByRole('button', { name: 'Zamknij okres' }).click()
    await expect(page.getByTestId('lock-status')).toContainText(iso(-20))

    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json()
    const id = (code: string) => accounts.find((a: { code: string }) => a.code === code).id
    const entries = [{ accountId: id('100'), amount: 5, side: 'Debit' }, { accountId: id('130'), amount: 5, side: 'Credit' }]

    const closed = await request.post('/api/v1/transactions', { headers, data: { description: 'Wsteczne', date: iso(-25), entries } })
    expect(closed.status()).toBe(400)
    expect((await closed.json()).detail).toContain('Okres jest zamknięty')

    const open = await request.post('/api/v1/transactions', { headers, data: { description: 'Bieżące', date: TODAY, entries } })
    expect(open.status()).toBe(200)

    // faktura wsteczna też zablokowana (przez UI)
    await page.goto('/invoices/new?type=Purchase')
    await page.getByLabel('Dostawca').selectOption({ label: 'Dostawca SA (NIP 7740001454)' })
    await page.getByLabel('Numer faktury dostawcy').fill('FZ/78/2026')
    await page.getByLabel('Data wystawienia').fill(iso(-30))
    await page.getByLabel('Termin płatności').fill(iso(-10))
    await page.getByLabel('Opis pozycji 1').fill('Wsteczna')
    await page.getByLabel('Netto pozycji 1').fill('10')
    await page.getByRole('button', { name: 'Zarejestruj i zaksięguj' }).click()
    await expect(page.getByRole('alert')).toContainText('Okres jest zamknięty')

    // odblokowanie
    await page.goto('/admin')
    await page.getByRole('button', { name: 'Odblokuj wszystkie okresy' }).click()
    await expect(page.getByTestId('lock-status')).toContainText('Żaden okres nie jest zamknięty')
    const nowOk = await request.post('/api/v1/transactions', { headers, data: { description: 'Wsteczne po odblokowaniu', date: iso(-25), entries } })
    expect(nowOk.status()).toBe(200)
  })

  test('dziennik audytu rejestruje operacje i jest dostępny tylko dla administratora', async ({ page, request }) => {
    await page.goto('/admin')
    const audit = page.getByRole('table', { name: 'Dziennik zdarzeń' })
    await expect(audit).toContainText('Zarejestrowano fakturę')
    await expect(audit).toContainText('Zarejestrowano płatność')
    await expect(audit).toContainText('Anulowano fakturę')
    await expect(audit).toContainText('Zamknięto okres')
    await expect(audit).toContainText(ADMIN.email)
    await page.getByLabel('Szukaj').fill('Dodano kontrahenta')
    await expect(audit.getByRole('row', { name: /Zarejestrowano fakturę/ })).toHaveCount(0) // filtr zadziałał
    await expect(audit.getByRole('row', { name: /Dostawca SA/ })).toHaveCount(1)

    // użytkownik z rolą Podgląd
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const created = await request.post('/api/v1/users/create', {
      headers, data: { firstName: 'Piotr', lastName: 'Podgląd', email: PODGLAD.email, password: PODGLAD.password, role: 'Viewer', phoneNumber: '' },
    })
    expect(created.status()).toBe(200)
    await logout(page)

    await loginOk(page, PODGLAD)
    await expect(page.getByRole('link', { name: 'Administracja' })).toHaveCount(0)
    await page.goto('/admin')
    await expect(page).toHaveURL(/\/dashboard/)
    const vh = { Authorization: `Bearer ${await accessToken(page)}` }
    expect((await request.get('/api/v1/admin/audit', { headers: vh })).status()).toBe(403)
    expect((await request.put('/api/v1/admin/period-lock', { headers: vh, data: { lockedThrough: TODAY } })).status()).toBe(403)

    // widzi faktury i kontrahentów, ale nie może wystawiać
    await page.goto('/invoices')
    await expect(page.getByRole('link', { name: 'Nowa faktura sprzedaży' })).toHaveCount(0)
    await expect(page.getByRole('row', { name: /FZ\/77\/2026/ })).toBeVisible()
    await page.goto('/contractors')
    await expect(page.getByRole('form', { name: 'Kontrahent' })).toHaveCount(0)
    expect((await request.post('/api/v1/invoices', { headers: vh, data: {} })).status()).toBe(403)
    expect((await request.post('/api/v1/contractors', { headers: vh, data: { name: 'X' } })).status()).toBe(403)
  })

  test('księgowy może wystawiać faktury', async ({ page }) => {
    await logout(page).catch(() => {})
    await login(page, 'jan@biuro.test', 'NoweHaslo99')
    await expect(page).toHaveURL(/\/dashboard/)
    await page.goto('/invoices')
    await expect(page.getByRole('link', { name: 'Nowa faktura sprzedaży' })).toBeVisible()
  })
})
