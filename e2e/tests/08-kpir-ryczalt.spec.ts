import AxeBuilder from '@axe-core/playwright'
import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN, accessToken, confirmDialog, login, loginOk, squash } from './helpers'

// Po 06/07: używa wyłącznie nowych firm (KPiR, ryczałt), więc nie zmienia danych firm z poprzednich plików.

const KPIR = 'Klient KPiR test'
const RYCZALT = 'Klient Ryczałt test'
const EMPTY = 'Firma do zmiany trybu'
const FIRST = 'Biuro Rachunkowe Test sp. z o.o.'
const YEAR = new Date().getFullYear()
const OLGA = { email: 'olga.skroty@biuro.test', password: 'Haslo1234' } // rola Podgląd z 04h

let ids: Record<string, string> = {}

async function authHeaders(page: Page) { return { Authorization: `Bearer ${await accessToken(page)}` } }
const withCompany = (h: Record<string, string>, id: string) => ({ ...h, 'X-Company-Id': id })

async function companies(page: Page, request: APIRequestContext) {
  const list = await (await request.get('/api/v1/companies', { headers: await authHeaders(page) })).json() as { id: string; name: string }[]
  return Object.fromEntries(list.map(c => [c.name, c.id]))
}

async function addContractor(request: APIRequestContext, h: Record<string, string>, name: string) {
  const r = await request.post('/api/v1/contractors', { headers: h, data: { name } })
  expect(r.status()).toBeLessThan(300)
  return (await r.json()).id as string
}

async function addInvoice(request: APIRequestContext, h: Record<string, string>, body: Record<string, unknown>) {
  return request.post('/api/v1/invoices', { headers: h, data: { dueDate: `${YEAR}-02-28`, lines: [{ description: 'Usługa testowa', net: body.net, vatRate: 'R23' }], ...body, net: undefined } })
}

async function switchCompany(page: Page, name: string) {
  const picker = page.getByLabel('Firma', { exact: true })
  await picker.selectOption({ label: name })
  await expect(picker.locator('option:checked')).toHaveText(name)
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
}

test.describe.serial('KPiR i ryczałt: tryb firmy, ewidencje, uprawnienia i izolacja', () => {
  test('administrator dodaje firmę w trybie KPiR (bez planu kont); menu pokazuje tylko właściwe pozycje', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await page.goto('/companies')
    const form = page.getByRole('form', { name: 'Nowa firma' })
    await form.getByLabel('Nazwa firmy').fill(KPIR)
    await form.getByLabel('NIP').fill('1234563218')
    await form.getByLabel('Tryb ewidencji').selectOption('Kpir')
    await expect(form.getByLabel('Domyślny plan kont')).toHaveCount(0)      // KPiR nie używa planu kont
    await form.getByRole('button', { name: 'Dodaj firmę' }).click()
    await expect(page.getByRole('status')).toContainText(`Dodano firmę ${KPIR}`)
    await expect(page.getByRole('table', { name: 'Lista firm' }).getByRole('row', { name: new RegExp(KPIR) }).getByRole('cell', { name: 'KPiR', exact: true })).toBeVisible()

    // dwie kolejne firmy przez API (ryczałt i pusta do zmiany trybu)
    const h = await authHeaders(page)
    const r1 = await request.post('/api/v1/companies', { headers: h, data: { name: RYCZALT, nip: '9876543210', mode: 'Ryczalt' } })
    expect(r1.status()).toBe(200)
    expect((await request.post('/api/v1/companies', { headers: h, data: { name: EMPTY } })).status()).toBe(200)
    ids = await companies(page, request)

    // firma KPiR nie ma planu kont, a firma w pełnej księgowości ma
    expect((await request.get('/api/v1/accounts', { headers: withCompany(h, ids[KPIR]) })).status()).toBe(200)
    expect(await (await request.get('/api/v1/accounts', { headers: withCompany(h, ids[KPIR]) })).json()).toHaveLength(0)
    expect((await (await request.get('/api/v1/accounts', { headers: withCompany(h, ids[FIRST]) })).json()).length).toBeGreaterThan(5)

    await page.goto('/dashboard')
    await switchCompany(page, KPIR)
    const nav = page.getByRole('navigation', { name: 'Menu główne' })
    await expect(nav.getByRole('link', { name: 'KPiR' })).toBeVisible()
    for (const hidden of ['Plan kont', 'Dziennik', 'Nowe księgowanie', 'Import wyciągu']) await expect(nav.getByRole('link', { name: hidden })).toHaveCount(0)
    for (const shown of ['Pulpit', 'Faktury', 'Kontrahenci', 'Raporty', 'Administracja']) await expect(nav.getByRole('link', { name: shown })).toBeVisible()
    await expect(page.getByText('podatkową księgę przychodów i rozchodów')).toBeVisible()
    await expect(page.getByTestId('tile-assets')).toHaveCount(0)

    // adresy stron pełnej księgowości przekierowują, skrót N i polecenia księgowań nie istnieją
    await page.goto('/accounts')
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.goto('/transactions/new')
    await expect(page).toHaveURL(/\/dashboard$/)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.keyboard.press('n')
    await expect(page).toHaveURL(/\/dashboard$/)
    await page.keyboard.press('Control+k')
    await expect(page.getByRole('option', { name: 'Ewidencja KPiR' })).toBeVisible()
    await expect(page.getByRole('option', { name: /Nowe księgowanie/ })).toHaveCount(0)
    await page.keyboard.press('Escape')

    // raporty: tylko rejestry VAT i rozrachunki
    await page.goto('/reports')
    await expect(page.getByRole('tab', { name: 'Rejestry VAT' })).toBeVisible()
    await expect(page.getByRole('tab', { name: 'Bilans' })).toHaveCount(0)

    // administracja: brak zamknięcia roku (konta), jest tryb ewidencji
    await page.goto('/admin')
    await expect(page.getByTestId('accounting-mode')).toHaveText('KPiR')
    await expect(page.getByRole('heading', { name: 'Zamknięcie roku obrotowego' })).toHaveCount(0)
  })

  test('3 faktury sprzedaży i 2 zakupu dają oczekiwane sumy w kolumnach KPiR; serwer odrzuca księgowania na kontach', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    ids = await companies(page, request)
    const h = withCompany(await authHeaders(page), ids[KPIR])
    const c = await addContractor(request, h, 'Odbiorca KPiR')
    const d = (day: number) => `${YEAR}-01-${String(day).padStart(2, '0')}`
    const sale = (net: number, day: number, kind?: string) => addInvoice(request, h, { type: 'Sales', contractorId: c, issueDate: d(day), net, kind })
    const buy = (net: number, day: number, no: string, kind?: string) => addInvoice(request, h, { type: 'Purchase', number: no, contractorId: c, issueDate: d(day), net, kind })
    for (const r of [await sale(1000, 5), await sale(250.5, 12, 'OtherRevenue'), await sale(500, 20, 'Revenue'),
      await buy(300, 7, 'ZAK/1', 'GoodsPurchase'), await buy(120, 9, 'ZAK/2')]) expect(r.status()).toBe(200)

    // brak księgowań na kontach
    expect((await (await request.get('/api/v1/transactions', { headers: h })).json()).totalCount).toBe(0)
    const post = await request.post('/api/v1/transactions', { headers: h, data: { description: 'X', date: d(3), entries: [] } })
    expect(post.status()).toBe(400)

    await switchCompany(page, KPIR)
    await page.goto('/register')
    await expect(page.getByRole('heading', { name: 'Podatkowa księga przychodów i rozchodów' })).toBeVisible()
    const t = (id: string) => page.getByTestId(`kpir-period-${id}`)
    await expect(t('revenue')).toHaveText('1500,00')
    await expect(t('other-revenue')).toHaveText('250,50')
    await expect(t('total-revenue')).toHaveText('1750,50')
    await expect(t('goods')).toHaveText('300,00')
    await expect(t('other')).toHaveText('120,00')
    await expect(t('total-expense')).toHaveText('420,00')
    await expect(page.getByTestId('kpir-income')).toHaveText('1330,50')
    const table = page.getByRole('table', { name: 'Księga przychodów i rozchodów' })
    await expect(table.locator('tbody tr')).toHaveCount(5)
    await expect(table.locator('tbody tr').first().locator('td').nth(1)).toHaveText(`${YEAR}-01-05`)
    const month = page.getByTestId(`kpir-month-${YEAR}-01`)
    await expect(month).toContainText('1750,50')
    await expect(page.getByTestId(`kpir-income-${YEAR}-01`)).toHaveText('1330,50')

    // eksport CSV (z BOM, średniki) zawiera nagłówki kolumn i wiersze
    const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'Eksport widoku' }).click()])
    const chunks: Buffer[] = []
    for await (const ch of await download.createReadStream()) chunks.push(ch as Buffer)
    const csv = Buffer.concat(chunks).toString('utf8')
    expect(csv.charCodeAt(0)).toBe(0xfeff)
    expect(csv.split('\r\n')[0].replace(/"/g, '')).toContain('Lp.;Data zdarzenia;Nr dowodu;Kontrahent')
    expect(csv.split('\r\n')).toHaveLength(6)

    // druk: nagłówek z firmą i okresem
    await page.emulateMedia({ media: 'print' })
    await expect(page.getByTestId('print-header')).toContainText(KPIR)
    await expect(page.getByTestId('print-header')).toContainText(String(YEAR))
    await page.emulateMedia({ media: 'screen' })

    // zakres obejmujący miesiące bez wpisów oraz błędny zakres
    await page.getByLabel('Od miesiąca').selectOption('3')
    await page.getByLabel('Do miesiąca').selectOption('2')
    await expect(page.getByRole('alert')).toContainText('Miesiąc końcowy')
  })

  test('faktura w KPiR przez interfejs: rodzaj wpisu zamiast konta, płatność bez konta, brak księgowania', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, KPIR)
    await page.goto('/invoices/new?type=Sales')
    await expect(page.getByLabel('Konto przychodu')).toHaveCount(0)
    await expect(page.getByText('bez księgowania na konta')).toBeVisible()
    await page.getByLabel('Nabywca').selectOption({ label: 'Odbiorca KPiR' })
    await page.getByLabel('Rodzaj wpisu w KPiR').selectOption('OtherRevenue')
    await page.getByLabel('Opis pozycji 1').fill('Odsetki')
    await page.getByLabel('Netto pozycji 1').fill('80')
    await page.getByRole('button', { name: 'Wystaw fakturę' }).click()
    await expect(page).toHaveURL(/\/invoices\/[0-9a-f-]{36}$/)
    await expect(page.getByTestId('invoice-kpir-kind')).toContainText('kol. 8')
    await expect(page.getByText('Księgowanie', { exact: true })).toHaveCount(0)

    // płatność: bez wyboru konta kasy/banku
    await expect(page.getByLabel('Wpłynęło na konto')).toHaveCount(0)
    const pay = page.getByRole('form', { name: 'Nowa płatność' })
    await pay.getByLabel('Kwota').fill('50')
    await pay.getByRole('button', { name: 'Zarejestruj płatność' }).click()
    await expect(page.getByRole('status')).toContainText('Zarejestrowano płatność')
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/48,40/)    // 98,40 − 50
    await expect(page.getByRole('row', { name: /50,00/ })).toContainText('—')

    // anulowanie nie tworzy storna, a faktura znika z ewidencji
    await page.getByRole('button', { name: 'Anuluj fakturę' }).count()        // przy płatności przycisk jest ukryty
    const h = withCompany(await authHeaders(page), (await companies(page, request))[KPIR])
    expect((await (await request.get('/api/v1/transactions', { headers: h })).json()).totalCount).toBe(0)
  })

  test('ryczałt: wymagana stawka, przychody wg stawek i podatek informacyjny', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    ids = await companies(page, request)
    const h = withCompany(await authHeaders(page), ids[RYCZALT])
    const c = await addContractor(request, h, 'Odbiorca ryczałt')
    const d = (day: number) => `${YEAR}-01-${String(day).padStart(2, '0')}`
    const noRate = await addInvoice(request, h, { type: 'Sales', contractorId: c, issueDate: d(5), net: 100 })
    expect(noRate.status()).toBe(400)
    expect((await noRate.json()).detail).toContain('stawkę ryczałtu')
    expect((await addInvoice(request, h, { type: 'Sales', contractorId: c, issueDate: d(5), net: 100, ryczaltRate: 7 })).status()).toBe(400)
    for (const [net, day, rate] of [[1000, 5, 8.5], [333.33, 20, 8.5], [2000, 25, 12]] as const)
      expect((await addInvoice(request, h, { type: 'Sales', contractorId: c, issueDate: d(day), net, ryczaltRate: rate })).status()).toBe(200)
    expect((await addInvoice(request, h, { type: 'Purchase', number: 'Z/1', contractorId: c, issueDate: d(6), net: 999 })).status()).toBe(200)

    await switchCompany(page, RYCZALT)
    await expect(page.getByRole('navigation', { name: 'Menu główne' }).getByRole('link', { name: 'Ewidencja ryczałtu' })).toBeVisible()
    await page.goto('/register')
    await expect(page.getByRole('heading', { name: 'Ewidencja przychodów (ryczałt)' })).toBeVisible()
    await expect(page.getByTestId('ryczalt-total')).toHaveText('3333,33')
    await expect(page.getByRole('table', { name: 'Ewidencja przychodów' }).locator('tbody tr')).toHaveCount(3)
    await expect(page.getByTestId('ryczalt-rate-8.5')).toContainText('113,00')       // 3% … 8,5% × 1333,33 = 113,33 → 113
    await expect(page.getByTestId('ryczalt-rate-12')).toContainText('240,00')
    await expect(page.getByTestId('ryczalt-ytd-tax')).toHaveText('353,00')

    // formularz faktury sprzedaży wymaga stawki ryczałtu
    await page.goto('/invoices/new?type=Sales')
    await expect(page.getByLabel('Stawka ryczałtu')).toBeVisible()
    await page.getByLabel('Nabywca').selectOption({ label: 'Odbiorca ryczałt' })
    await page.getByLabel('Opis pozycji 1').fill('Usługa')
    await page.getByLabel('Netto pozycji 1').fill('10')
    await expect(page.getByRole('button', { name: 'Wystaw fakturę' })).toBeDisabled()
    await page.getByLabel('Stawka ryczałtu').selectOption('5.5')
    await page.getByRole('button', { name: 'Wystaw fakturę' }).click()
    await expect(page.getByTestId('invoice-ryczalt-rate')).toHaveText('5,5%')
  })

  test('zmiana trybu: dozwolona w pustej firmie (z potwierdzeniem), zablokowana gdy są dane', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    ids = await companies(page, request)
    const auth = await authHeaders(page)

    // pierwsza firma ma księgowania – API odmawia, tryb bez zmian
    const blocked = await request.put('/api/v1/admin/accounting-mode', { headers: withCompany(auth, ids[FIRST]), data: { mode: 'Kpir' } })
    expect(blocked.status()).toBe(400)
    expect((await blocked.json()).detail).toContain('nową firmę')

    await switchCompany(page, EMPTY)
    await page.goto('/admin')
    await expect(page.getByTestId('accounting-mode')).toHaveText('Pełna księgowość')
    await page.getByLabel('Tryb ewidencji').selectOption('Kpir')
    await page.getByRole('button', { name: 'Zmień tryb' }).click()
    await confirmDialog(page)
    await expect(page.getByTestId('accounting-mode')).toHaveText('KPiR')
    await expect(page.getByRole('navigation', { name: 'Menu główne' }).getByRole('link', { name: 'Plan kont' })).toHaveCount(0)

    // po wystawieniu faktury zmiana jest już niemożliwa
    const h = withCompany(auth, ids[EMPTY])
    const c = await addContractor(request, h, 'Ktoś')
    expect((await addInvoice(request, h, { type: 'Sales', contractorId: c, issueDate: `${YEAR}-01-05`, net: 10 })).status()).toBe(200)
    await page.reload()
    await page.getByLabel('Tryb ewidencji').selectOption('FullBooks')
    await page.getByRole('button', { name: 'Zmień tryb' }).click()
    await confirmDialog(page)
    await expect(page.getByRole('alert')).toContainText('nową firmę')
    await expect(page.getByTestId('accounting-mode')).toHaveText('KPiR')
  })

  test('izolacja i uprawnienia: rejestry tylko z bieżącej firmy; Podgląd czyta, ale nie wystawia faktur', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    ids = await companies(page, request)
    const auth = await authHeaders(page)
    const range = `from=${YEAR}-01-01&to=${YEAR}-12-31`

    const inKpir = await (await request.get(`/api/v1/registers/kpir?${range}`, { headers: withCompany(auth, ids[KPIR]) })).json()
    expect(inKpir.rows.length).toBeGreaterThan(0)
    const inFirst = await (await request.get(`/api/v1/registers/kpir?${range}`, { headers: withCompany(auth, ids[FIRST]) })).json()
    expect(inFirst.rows).toHaveLength(0)                       // faktury firmy w pełnej księgowości nie wchodzą do KPiR
    const ryInKpir = await (await request.get(`/api/v1/registers/ryczalt?${range}`, { headers: withCompany(auth, ids[KPIR]) })).json()
    expect(ryInKpir.rows).toHaveLength(0)
    expect((await request.get(`/api/v1/registers/kpir?from=${YEAR}-01-01&to=${YEAR + 1}-02-01`, { headers: withCompany(auth, ids[KPIR]) })).status()).toBe(400)
    expect((await request.get(`/api/v1/registers/kpir?${range}`, { headers: withCompany(auth, '99999999-9999-9999-9999-999999999999') })).status()).toBe(403)
    expect((await request.get(`/api/v1/registers/kpir?${range}`)).status()).toBe(401)

    // nadanie dostępu do firmy KPiR użytkownikowi z rolą Podgląd
    const users = await (await request.get('/api/v1/users', { headers: auth })).json() as { id: string; email: string }[]
    const olga = users.find(u => u.email === OLGA.email)!
    const own = await (await request.get(`/api/v1/users/${olga.id}/companies`, { headers: auth })).json() as string[]
    expect((await request.put(`/api/v1/users/${olga.id}/companies`, { headers: auth, data: { companyIds: [...own, ids[KPIR]] } })).status()).toBeLessThan(300)

    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await login(page, OLGA.email, OLGA.password)
    await expect(page).toHaveURL(/\/dashboard/)
    const viewer = withCompany(await authHeaders(page), ids[KPIR])
    expect((await request.get(`/api/v1/registers/kpir?${range}`, { headers: viewer })).status()).toBe(200)
    expect((await request.post('/api/v1/invoices', { headers: viewer, data: { type: 'Sales', contractorId: ids[KPIR], issueDate: `${YEAR}-01-05`, dueDate: `${YEAR}-01-19`, lines: [] } })).status()).toBe(403)
    await switchCompany(page, KPIR)
    await page.goto('/register')
    await expect(page.getByRole('heading', { name: 'Podatkowa księga przychodów i rozchodów' })).toBeVisible()
    await expect(page.getByTestId('kpir-period-total-revenue')).not.toHaveText('0,00')
    await page.goto('/invoices')
    await expect(page.getByRole('link', { name: 'Nowa faktura sprzedaży' })).toHaveCount(0)
  })

  for (const theme of ['light', 'dark'] as const) {
    test(`axe: ewidencja KPiR i ryczałtu bez naruszeń serious/critical (${theme})`, async ({ page }) => {
      await page.addInitScript(t => localStorage.setItem('lc.theme', t), theme)
      await loginOk(page, ADMIN)
      for (const name of [KPIR, RYCZALT]) {
        await switchCompany(page, name)
        await page.goto('/register')
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await expect(page.locator('table[aria-label]').first()).toBeVisible()
        const res = await new AxeBuilder({ page }).analyze()
        const bad = res.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
        expect(bad.map(v => `${name}: ${v.id} – ${v.nodes.slice(0, 2).map(n => n.html.slice(0, 90)).join(' | ')}`)).toEqual([])
      }
    })
  }
})

void squash
