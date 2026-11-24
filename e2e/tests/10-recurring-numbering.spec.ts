import AxeBuilder from '@axe-core/playwright'
import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN, accessToken, confirmDialog, login, loginOk } from './helpers'

// Po 09: pracuje wyłącznie w nowej firmie, więc nie zmienia danych wcześniejszych plików.

const COMPANY = 'Firma cykliczne test'
const FIRST = 'Biuro Rachunkowe Test sp. z o.o.'
const CLIENT = 'Abonent Cykliczny sp. z o.o.'
const OLGA = { email: 'olga.skroty@biuro.test', password: 'Haslo1234' }

let ids: Record<string, string> = {}
let contractor = ''

const withCompany = (h: Record<string, string>, id: string) => ({ ...h, 'X-Company-Id': id })
async function auth(page: Page) { return { Authorization: `Bearer ${await accessToken(page)}` } }
async function companies(page: Page, request: APIRequestContext) {
  const list = await (await request.get('/api/v1/companies', { headers: await auth(page) })).json() as { id: string; name: string }[]
  return Object.fromEntries(list.map(c => [c.name, c.id]))
}
async function switchCompany(page: Page, name: string) {
  const picker = page.getByLabel('Firma', { exact: true })
  await picker.selectOption({ label: name })
  await expect(picker.locator('option:checked')).toHaveText(name)
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
}
const body = (over: Record<string, unknown> = {}) => ({
  name: 'Abonament', contractorId: contractor, frequency: 'Monthly', dayOfMonth: 15, dueDays: 14, nextDate: '2030-01-15',
  lines: [{ description: 'Abonament', net: 100, vatRate: 'R23' }], ...over,
})

test.describe.serial('Faktury cykliczne i numeracja faktur sprzedaży', () => {
  test('dane: nowa firma z kontrahentem', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = await auth(page)
    expect((await request.post('/api/v1/companies', { headers: h, data: { name: COMPANY } })).status()).toBe(200)
    ids = await companies(page, request)
    contractor = (await (await request.post('/api/v1/contractors', { headers: withCompany(h, ids[COMPANY]), data: { name: CLIENT } })).json()).id as string
  })

  test('szablon: dodanie, generowanie zaległych faktur i brak duplikatów przy ponownym uruchomieniu', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/recurring')
    await expect(page.getByRole('heading', { name: 'Faktury cykliczne', level: 1 })).toBeVisible()
    await page.getByRole('button', { name: 'Nowy szablon' }).click()
    const form = page.getByRole('form', { name: 'Nowy szablon' })
    await form.getByLabel('Nazwa szablonu').fill('Abonament IT')
    await form.getByLabel('Nabywca').selectOption({ label: CLIENT })
    await form.getByLabel('Następna faktura').fill('2025-01-15')
    await form.getByLabel('Opis pozycji 1').fill('Abonament miesięczny')
    await form.getByLabel('Netto pozycji 1').fill('200,00')
    await expect(form.getByTestId('rec-gross')).toContainText('246,00')
    await form.getByRole('button', { name: 'Zapisz szablon' }).click()
    await expect(page.getByRole('status')).toContainText('Dodano szablon „Abonament IT”')
    const row = page.getByRole('table', { name: 'Faktury cykliczne' }).getByRole('row', { name: /Abonament IT/ })
    await expect(row).toContainText('2025-01-15')
    await expect(row).toContainText('246,00')
    await expect(row).toContainText('Aktywny')

    await page.getByLabel('Generuj do dnia').fill('2025-03-20')
    await page.getByRole('button', { name: 'Wygeneruj zaległe' }).click()
    await expect(page.getByRole('status')).toContainText('Wygenerowano faktur: 3')
    const generated = page.getByRole('list', { name: 'Wygenerowane faktury' })
    await expect(generated.getByRole('listitem')).toHaveCount(3)
    await expect(generated).toContainText('FV/2025/0001')
    await expect(generated).toContainText('FV/2025/0003')
    await expect(row).toContainText('2025-04-15')

    // drugi raz za ten sam okres – nic nie powstaje
    await page.getByRole('button', { name: 'Wygeneruj zaległe' }).click()
    await expect(page.getByRole('status')).toContainText('Brak zaległych faktur do wygenerowania')
    const h = withCompany(await auth(page), ids[COMPANY])
    const list = await (await request.get('/api/v1/invoices?pageSize=50', { headers: h })).json() as { items: { number: string; issueDate: string; gross: number }[] }
    expect(list.items.map(i => i.number).sort()).toEqual(['FV/2025/0001', 'FV/2025/0002', 'FV/2025/0003'])
    expect(list.items.every(i => i.gross === 246)).toBe(true)
  })

  test('wygenerowane faktury są zaksięgowane i opisane w audycie jako cykliczne', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = withCompany(await auth(page), ids[COMPANY])
    const list = await (await request.get('/api/v1/invoices?pageSize=50&search=FV/2025/0002', { headers: h })).json() as { items: { id: string }[] }
    const inv = await (await request.get(`/api/v1/invoices/${list.items[0].id}`, { headers: h })).json()
    expect(inv.transactionId).toBeTruthy()
    expect(inv.dueDate.slice(0, 10)).toBe('2025-03-01')
    const audit = await (await request.get('/api/v1/admin/audit?pageSize=100', { headers: h })).json() as { items: { action: string; details: string }[] }
    expect(audit.items.filter(a => a.action === 'Faktura cykliczna' && a.details.includes('Wygenerowano fakturę FV/2025/')).length).toBe(3)

    await switchCompany(page, COMPANY)
    await page.goto('/invoices')
    await page.getByLabel('Od', { exact: true }).fill('2025-01-01')
    await expect(page.getByRole('row', { name: /FV\/2025\/0003/ })).toContainText('Abonent Cykliczny')
  })

  test('wstrzymanie, wznowienie i usunięcie szablonu (faktury zostają)', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/recurring')
    await page.getByLabel('Generuj do dnia').fill('2025-05-20')
    const row = page.getByRole('table', { name: 'Faktury cykliczne' }).getByRole('row', { name: /Abonament IT/ })
    await page.getByRole('button', { name: 'Wstrzymaj szablon Abonament IT' }).click()
    await expect(row).toContainText('Wstrzymany')
    await page.getByRole('button', { name: 'Wygeneruj zaległe' }).click()
    await expect(page.getByRole('status')).toContainText('Brak zaległych faktur')
    await page.getByRole('button', { name: 'Wznów szablon Abonament IT' }).click()
    await expect(row).toContainText('Aktywny')
    await page.getByRole('button', { name: 'Wygeneruj z szablonu Abonament IT' }).click()
    await expect(page.getByRole('status')).toContainText('Wygenerowano faktur: 2')      // kwiecień i maj

    await page.getByRole('button', { name: 'Usuń szablon Abonament IT' }).click()
    await confirmDialog(page, 'Usuń szablon')
    await expect(page.getByRole('status')).toContainText('Usunięto szablon')
    await expect(page.getByRole('table', { name: 'Faktury cykliczne' })).toHaveCount(0)
    const h = withCompany(await auth(page), ids[COMPANY])
    expect(((await (await request.get('/api/v1/invoices?pageSize=50', { headers: h })).json()) as { items: unknown[] }).items).toHaveLength(5)
  })

  test('walidacja szablonu: dzień, termin, pozycje, data zakończenia, kontrahent', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = withCompany(await auth(page), ids[COMPANY])
    const post = (data: Record<string, unknown>) => request.post('/api/v1/recurring-invoices', { headers: h, data })
    const detail = async (r: Awaited<ReturnType<typeof post>>) => JSON.stringify(await r.json())
    let r = await post(body({ dayOfMonth: 40 })); expect(r.status()).toBe(400); expect(await detail(r)).toContain('Dzień wystawienia')
    r = await post(body({ dueDays: -3 })); expect(r.status()).toBe(400); expect(await detail(r)).toContain('Termin płatności')
    r = await post(body({ lines: [] })); expect(r.status()).toBe(400); expect(await detail(r)).toContain('co najmniej jednej pozycji')
    r = await post(body({ endDate: '2029-01-01' })); expect(r.status()).toBe(400); expect(await detail(r)).toContain('Data zakończenia')
    r = await post(body({ contractorId: '00000000-0000-0000-0000-000000000001' })); expect(r.status()).toBe(400); expect(await detail(r)).toContain('Nie znaleziono kontrahenta')
    r = await post(body({ name: '' })); expect(r.status()).toBe(400)
    expect((await post(body({ name: 'Poprawny' }))).status()).toBe(200)
  })

  test('zamknięty okres: szablon jest pomijany z komunikatem i czeka na odblokowanie', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = withCompany(await auth(page), ids[COMPANY])
    const list = await (await request.get('/api/v1/recurring-invoices', { headers: h })).json() as { id: string; name: string }[]
    for (const t of list) await request.delete(`/api/v1/recurring-invoices/${t.id}`, { headers: h })
    expect((await request.post('/api/v1/recurring-invoices', { headers: h, data: body({ name: 'Zamknięty okres', nextDate: '2025-06-10' }) })).status()).toBe(200)
    expect((await request.put('/api/v1/admin/period-lock', { headers: h, data: { lockedThrough: '2025-06-30' } })).status()).toBeLessThan(300)

    await switchCompany(page, COMPANY)
    await page.goto('/recurring')
    await page.getByLabel('Generuj do dnia').fill('2025-07-20')
    await page.getByRole('button', { name: 'Wygeneruj zaległe' }).click()
    const skipped = page.getByRole('alert', { name: 'Pominięte szablony' })
    await expect(skipped).toContainText('Zamknięty okres')
    await expect(skipped).toContainText('zamknięty do 2025-06-30')
    await expect(page.getByRole('table', { name: 'Faktury cykliczne' }).getByRole('row', { name: /Zamknięty okres/ })).toContainText('2025-06-10')

    expect((await request.put('/api/v1/admin/period-lock', { headers: h, data: { lockedThrough: null } })).status()).toBeLessThan(300)
    await page.getByRole('button', { name: 'Wygeneruj zaległe' }).click()
    await expect(page.getByRole('status')).toContainText('Wygenerowano faktur: 2')       // czerwiec i lipiec
  })

  test('numeracja: wzór z podglądem, odrzucenie błędnych wzorów, kolejne numery wg wzoru', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/admin')
    const form = page.getByRole('form', { name: 'Numeracja faktur' })
    await expect(form.getByLabel('Wzór numeru')).toHaveValue('FV/{RRRR}/{NNNN}')

    await form.getByLabel('Wzór numeru').fill('FV/{NNNN}')
    await expect(page.getByRole('alert')).toContainText('musi zawierać rok')
    await form.getByRole('button', { name: 'Zapisz wzór' }).click()
    await expect(page.getByRole('alert')).toContainText('musi zawierać rok')
    await form.getByLabel('Wzór numeru').fill('FV/{RRRR}/{XX}')
    await expect(page.getByRole('alert')).toContainText('Nieznany znacznik {XX}')
    await form.getByLabel('Wzór numeru').fill('FV/{RRRR}/')
    await expect(page.getByRole('alert')).toContainText('dokładnie jeden znacznik numeru')

    await form.getByLabel('Wzór numeru').fill('{NNN}/{MM}/{RR}')
    await form.getByLabel('Zerowanie numeracji').selectOption('Monthly')
    await expect(page.getByTestId('numbering-example')).toHaveText(/^001\/\d{2}\/\d{2}$/)
    await expect(page.getByRole('alert')).toHaveCount(0)
    await form.getByRole('button', { name: 'Zapisz wzór' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano wzór numeracji')

    const h = withCompany(await auth(page), ids[COMPANY])
    const issue = async (date: string) => {
      const r = await request.post('/api/v1/invoices', { headers: h, data: { type: 'Sales', contractorId: contractor, issueDate: date, dueDate: date, lines: [{ description: 'Usługa', net: 10, vatRate: 'R23' }] } })
      expect(r.status()).toBe(200)
      return (await r.json()).number as string
    }
    expect(await issue('2025-09-03')).toBe('001/09/25')
    expect(await issue('2025-09-04')).toBe('002/09/25')
    expect(await issue('2025-10-01')).toBe('001/10/25')       // nowy miesiąc – numeracja od początku
    expect(await issue('2025-09-30')).toBe('003/09/25')       // wcześniejszy okres kontynuuje swoją numerację
  })

  test('numeracja jest per firma; zmiana wzoru nie rusza wystawionych faktur', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const hFirst = withCompany(await auth(page), (await companies(page, request))[FIRST])
    const cur = await (await request.get('/api/v1/admin/numbering', { headers: hFirst })).json()
    expect(cur.pattern).toBe('FV/{RRRR}/{NNNN}')
    const h = withCompany(await auth(page), ids[COMPANY])
    expect((await (await request.get('/api/v1/admin/numbering', { headers: h })).json()).pattern).toBe('{NNN}/{MM}/{RR}')
    const list = await (await request.get('/api/v1/invoices?pageSize=100', { headers: h })).json() as { items: { number: string }[] }
    expect(list.items.some(i => i.number.startsWith('FV/2025/'))).toBe(true)
    // izolacja szablonów
    expect(await (await request.get('/api/v1/recurring-invoices', { headers: hFirst })).json()).toHaveLength(0)
  })

  test('rola Podgląd: odczyt tak, zapis szablonów, generowanie i numeracji – 403', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const a = await auth(page)
    const users = await (await request.get('/api/v1/users', { headers: a })).json() as { id: string; email: string }[]
    const olga = users.find(u => u.email === OLGA.email)!
    const own = await (await request.get(`/api/v1/users/${olga.id}/companies`, { headers: a })).json() as string[]
    expect((await request.put(`/api/v1/users/${olga.id}/companies`, { headers: a, data: { companyIds: [...new Set([...own, ids[COMPANY]])] } })).status()).toBeLessThan(300)
    const tpl = (await (await request.post('/api/v1/recurring-invoices', { headers: withCompany(a, ids[COMPANY]), data: body({ name: 'Do podglądu' }) })).json()).id as string

    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await login(page, OLGA.email, OLGA.password)
    await expect(page).toHaveURL(/\/dashboard/)
    const v = withCompany(await auth(page), ids[COMPANY])
    expect((await request.get('/api/v1/recurring-invoices', { headers: v })).status()).toBe(200)
    expect((await request.get('/api/v1/admin/numbering', { headers: v })).status()).toBe(200)
    expect((await request.post('/api/v1/recurring-invoices', { headers: v, data: body() })).status()).toBe(403)
    expect((await request.put(`/api/v1/recurring-invoices/${tpl}`, { headers: v, data: body() })).status()).toBe(403)
    expect((await request.put(`/api/v1/recurring-invoices/${tpl}/active`, { headers: v, data: { active: false } })).status()).toBe(403)
    expect((await request.delete(`/api/v1/recurring-invoices/${tpl}`, { headers: v })).status()).toBe(403)
    expect((await request.post('/api/v1/recurring-invoices/generate', { headers: v, data: { asOf: '2031-01-01' } })).status()).toBe(403)
    expect((await request.put('/api/v1/admin/numbering', { headers: v, data: { pattern: 'X/{RRRR}/{NNN}', reset: 'Yearly' } })).status()).toBe(403)
    expect((await request.get('/api/v1/recurring-invoices')).status()).toBe(401)

    await switchCompany(page, COMPANY)
    await page.goto('/recurring')
    await expect(page.getByRole('table', { name: 'Faktury cykliczne' })).toContainText('Do podglądu')
    await expect(page.getByRole('button', { name: 'Nowy szablon' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Wygeneruj zaległe' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /Edytuj szablon/ })).toHaveCount(0)
  })

  test('edycja szablonu przez UI zachowuje pozycje', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/recurring')
    await page.getByRole('button', { name: 'Edytuj szablon Do podglądu' }).click()
    const form = page.getByRole('form', { name: 'Edycja szablonu' })
    await expect(form.getByLabel('Netto pozycji 1')).toHaveValue('100')
    await form.getByLabel('Nazwa szablonu').fill('Po edycji')
    await form.getByLabel('Częstotliwość').selectOption('Quarterly')
    await form.getByRole('button', { name: '+ Dodaj pozycję' }).click()
    await form.getByLabel('Opis pozycji 2').fill('Dodatek')
    await form.getByLabel('Netto pozycji 2').fill('50,50')
    await form.getByRole('button', { name: 'Zapisz szablon' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano szablon „Po edycji”')
    const row = page.getByRole('table', { name: 'Faktury cykliczne' }).getByRole('row', { name: /Po edycji/ })
    await expect(row).toContainText('Co kwartał')
    await expect(row).toContainText('185,12')     // netto 150,50 + VAT 34,62 (23,00 + 11,62)
  })

  for (const theme of ['light', 'dark'] as const) {
    test(`axe: strona faktur cyklicznych i numeracja bez naruszeń serious/critical (${theme})`, async ({ page }) => {
      await page.addInitScript(t => localStorage.setItem('lc.theme', t), theme)
      await loginOk(page, ADMIN)
      await switchCompany(page, COMPANY)
      for (const path of ['/recurring', '/admin']) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        if (path === '/recurring') await page.getByRole('button', { name: 'Nowy szablon' }).click()
        const res = await new AxeBuilder({ page }).analyze()
        const bad = res.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
        expect(bad.map(v => `${path}: ${v.id} – ${v.nodes.slice(0, 2).map(n => n.html.slice(0, 90)).join(' | ')}`)).toEqual([])
      }
    })
  }
})
