import AxeBuilder from '@axe-core/playwright'
import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import * as fs from 'node:fs'
import * as path from 'node:path'
import { ADMIN, accessToken, confirmDialog, login, loginOk, squash } from './helpers'

// Po 10: pracuje wyłącznie w nowej firmie, więc nie zmienia danych wcześniejszych plików.
// Pliki MT940 i Elixir-0 są wspólne z testami jednostkowymi parserów.

const COMPANY = 'Firma bank test'
const FIRST = 'Biuro Rachunkowe Test sp. z o.o.'
const CLIENT = 'Zażółć Gęślą Jaźń sp. z o.o.'
const OLGA = { email: 'olga.skroty@biuro.test', password: 'Haslo1234' }
const DATA = path.join(__dirname, '..', '..', 'tests', 'LedgerCore.Application.Tests', 'TestData')
const MT940 = path.join(DATA, 'wyciag.mt940')
const ELIXIR = fs.readFileSync(path.join(DATA, 'wyciag.elixir'), 'utf8')
const OWN = '11114020040000300201355387'

let ids: Record<string, string> = {}
let acc: Record<string, string> = {}
let contractor = ''
let saleId = ''

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
async function openBank(page: Page, tab?: string) {
  await loginOk(page, ADMIN)
  await switchCompany(page, COMPANY)
  await page.goto('/bank-import')
  await page.getByLabel('Konto bankowe').selectOption({ label: '130 Rachunek bankowy' })
  if (tab) await page.getByRole('tab', { name: tab }).click()
}
/** Plik w Windows-1250 (jak w eksportach polskich banków): tu wystarczą litery „ł” (0xB3) i „ą” (0xB9). */
const CP1250: Record<string, number> = { 'ł': 0xb3, 'ą': 0xb9 }
const cp1250 = (s: string) => Buffer.from(s.split('').map(c => CP1250[c] ?? c.charCodeAt(0)))

test.describe.serial('Wyciągi bankowe: formaty, deduplikacja, reguły, uzgodnienie', () => {
  test('dane: firma, kontrahent, faktury, kapitał początkowy', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = await auth(page)
    expect((await request.post('/api/v1/companies', { headers: h, data: { name: COMPANY } })).status()).toBe(200)
    ids = await companies(page, request)
    const hc = withCompany(h, ids[COMPANY])
    const accounts = await (await request.get('/api/v1/accounts', { headers: hc })).json() as { id: string; code: string }[]
    acc = Object.fromEntries(accounts.map(a => [a.code, a.id]))
    contractor = (await (await request.post('/api/v1/contractors', { headers: hc, data: { name: CLIENT } })).json()).id as string
    const inv = async (type: string, net: number, number?: string) => {
      const r = await request.post('/api/v1/invoices', { headers: hc, data: { type, number, contractorId: contractor, issueDate: '2026-11-02', dueDate: '2026-11-16', lines: [{ description: 'Usługa', net, vatRate: 'R23' }] } })
      expect(r.status()).toBe(200)
      return (await r.json()).invoiceId as string
    }
    saleId = await inv('Sales', 1000)                    // FV/2026/0001, brutto 1230,00
    await inv('Purchase', 162.6, 'FZ/77/2026')           // brutto 200,00
    expect((await request.post('/api/v1/transactions', { headers: hc, data: { description: 'Kapitał początkowy', date: '2026-11-01', entries: [
      { accountId: acc['130'], amount: 10000, side: 'Debit' }, { accountId: acc['800'], amount: 10000, side: 'Credit' }] } })).status()).toBe(200)
  })

  test('reguła „opłata bankowa” dodana w UI; walidacja reguł', async ({ page, request }) => {
    await openBank(page, 'Reguły księgowania')
    const form = page.getByRole('form', { name: 'Nowa reguła' })
    await form.getByLabel('Nazwa reguły').fill('Opłata bankowa')
    await form.getByLabel('Tekst w opisie lub u kontrahenta').fill('Opłata za prowadzenie')
    await form.getByLabel('Kierunek').selectOption('Out')
    await form.getByLabel('Konto księgowania').selectOption({ label: '402 Usługi obce' })
    await form.getByRole('button', { name: 'Zapisz regułę' }).click()
    await expect(page.getByRole('status')).toContainText('Dodano regułę „Opłata bankowa”')
    const row = page.getByRole('table', { name: 'Reguły księgowania' }).getByRole('row', { name: /Opłata bankowa/ })
    await expect(row).toContainText('Wypłata')
    await expect(row).toContainText('Aktywna')

    const hc = withCompany(await auth(page), ids[COMPANY])
    const bad = await request.post('/api/v1/bank/rules', { headers: hc, data: { name: 'X', matchText: 'ab', direction: 'Any', targetAccountId: acc['402'] } })
    expect(bad.status()).toBe(400)
    expect(JSON.stringify(await bad.json())).toContain('co najmniej 3 znaki')
    expect((await request.post('/api/v1/bank/rules', { headers: hc, data: { name: 'X', matchText: 'abc', direction: 'Any', targetAccountId: '00000000-0000-0000-0000-000000000001' } })).status()).toBe(400)
  })

  test('import MT940: polskie znaki, reguła księguje opłatę na konto kosztowe, płatności do faktur', async ({ page, request }) => {
    await openBank(page)
    await page.getByLabel('Plik wyciągu').setInputFiles(MT940)
    await expect(page.getByRole('status')).toContainText('Zaimportowano operacje: 3')
    await expect(page.getByRole('status')).toContainText('zaksięgowano wg reguł: 1')
    const table = page.getByRole('table', { name: 'Operacje z wyciągu' })
    await expect(table.getByRole('row')).toHaveCount(4)
    await expect(table.getByRole('row', { name: /Zapłata za FV 2026\/0001 usługa księgowa Łódź/ })).toContainText('Zażółć Gęślą Jaźń sp. z o.o.')
    await expect(table.getByRole('row', { name: /Zapłata za FV/ })).toContainText('pewne')
    await expect(table.getByRole('row', { name: /FZ\/77\/2026/ })).toContainText('pewne')
    await expect(table.getByRole('row', { name: /Opłata za prowadzenie/ })).toContainText('zaksięgowana')

    await expect(page.getByRole('button', { name: 'Zaksięguj zaznaczone (2)' })).toBeEnabled()
    await page.getByRole('button', { name: 'Zaksięguj zaznaczone (2)' }).click()
    await expect(page.getByRole('status')).toContainText('Zaksięgowano płatności: 2')

    const hc = withCompany(await auth(page), ids[COMPANY])
    const sale = await (await request.get(`/api/v1/invoices/${saleId}`, { headers: hc })).json()
    expect(sale.status).toBe('paid')
    expect(sale.payments).toHaveLength(1)
    // opłata bankowa: Wn 402 / Ma 130 bez faktury
    const accounts = await (await request.get('/api/v1/accounts', { headers: hc })).json() as { code: string; balance: number }[]
    expect(accounts.find(a => a.code === '402')!.balance).toBe(178.1)   // koszt faktury zakupu 162,60 + opłata bankowa 15,50
    expect(accounts.find(a => a.code === '130')!.balance).toBe(11014.5)
  })

  test('ponowny import tego samego pliku nie tworzy drugiego zestawu operacji ani płatności', async ({ page, request }) => {
    await openBank(page)
    await page.getByLabel('Plik wyciągu').setInputFiles(MT940)
    await expect(page.getByRole('status')).toContainText('Zaimportowano operacje: 0')
    await expect(page.getByRole('status')).toContainText('pominięto duplikaty: 3')
    const table = page.getByRole('table', { name: 'Operacje z wyciągu' })
    await expect(table.getByRole('row', { name: /Zapłata za FV/ })).toContainText('duplikat (zaksięgowana)')
    await expect(page.getByRole('button', { name: 'Zaksięguj zaznaczone (0)' })).toBeDisabled()

    const hc = withCompany(await auth(page), ids[COMPANY])
    const ops = await (await request.get(`/api/v1/bank/transactions?accountId=${acc['130']}`, { headers: hc })).json() as { id: string; status: string }[]
    expect(ops).toHaveLength(3)
    expect(ops.every(o => o.status === 'Posted')).toBe(true)
    const sale = await (await request.get(`/api/v1/invoices/${saleId}`, { headers: hc })).json()
    expect(sale.payments).toHaveLength(1)
    // próba ręcznego ponownego zaksięgowania tej samej operacji jest odrzucona
    const again = await request.post(`/api/v1/bank/transactions/${ops[0].id}/post-invoice`, { headers: hc, data: { invoiceId: saleId } })
    expect(again.status()).toBe(400)
    expect(JSON.stringify(await again.json())).toContain('już zaksięgowana')
  })

  test('uzgodnienie: saldo z pliku MT940 zgadza się z księgą', async ({ page }) => {
    await openBank(page, 'Uzgodnienie z bankiem')
    await expect(page.getByTestId('rec-ledger')).toBeVisible()
    expect(squash(await page.getByTestId('rec-ledger').textContent())).toBe('11014,50')
    expect(squash(await page.getByTestId('rec-statement').textContent())).toBe('11014,50')
    await expect(page.getByText('Zgodne', { exact: true })).toBeVisible()
    const history = page.getByRole('table', { name: 'Zaimportowane wyciągi' }).getByRole('row', { name: /wyciag\.mt940/ })
    await expect(history).toHaveCount(2)                           // pierwszy import (3 nowe) i powtórzony (3 duplikaty)
    await expect(history.first().getByRole('cell').nth(3)).toHaveText('0')       // najnowszy: nowe 0, duplikaty 3
    await expect(history.first().getByRole('cell').nth(4)).toHaveText('3')
    await expect(history.last().getByRole('cell').nth(3)).toHaveText('3')
  })

  test('import CSV w Windows-1250: niezaksięgowana operacja i różnica z listą w uzgodnieniu', async ({ page }) => {
    await openBank(page)
    await page.getByLabel('Plik wyciągu').setInputFiles({
      name: 'bank.csv', mimeType: 'text/csv', buffer: cp1250('Data;Kwota;Tytuł\n2026-11-09;-7,00;Opłata łączna\nzła;linia;x\n'),
    })
    const table = page.getByRole('table', { name: 'Operacje z wyciągu' })
    await expect(table.getByRole('row', { name: /Opłata łączna/ })).toBeVisible()          // „ł” odczytane poprawnie mimo kodowania 1250
    await expect(page.getByText('Pominięto wierszy bez poprawnej daty lub kwoty: 1')).toBeVisible()
    await page.getByRole('tab', { name: 'Uzgodnienie z bankiem' }).click()
    await page.getByLabel('Saldo końcowe z wyciągu (opcjonalnie)').fill('11007,50')
    await page.getByRole('button', { name: 'Sprawdź uzgodnienie' }).click()
    expect(squash(await page.getByTestId('rec-diff').textContent())).toBe('7,00')
    expect(squash(await page.getByTestId('rec-diff-after').textContent())).toBe('0,00')    // różnica to w całości niezaksięgowana operacja
    await expect(page.getByRole('table', { name: 'Niezaksięgowane operacje' })).toContainText('Opłata łączna')
    await expect(page.getByText('wpisane ręcznie')).toBeVisible()
  })

  test('pominięcie i przywrócenie operacji; reguły stosowane do zaimportowanych', async ({ page, request }) => {
    await openBank(page)
    await page.getByLabel('Treść wyciągu').fill('Data;Kwota;Tytuł\n2026-11-10;-3,00;Opłata za prowadzenie rachunku grudzień\n2026-11-10;-4,00;Nieznana operacja')
    await page.getByLabel('Zastosuj reguły księgowania').uncheck()
    await page.getByRole('button', { name: 'Analizuj' }).click()
    await expect(page.getByRole('status')).toContainText('Zaimportowano operacje: 2')
    const table = page.getByRole('table', { name: 'Operacje z wyciągu' })
    await page.getByRole('button', { name: 'Pomiń operację 2' }).click()
    await expect(table.getByRole('row', { name: /Nieznana operacja/ })).toContainText('pominięta')
    await page.getByRole('button', { name: 'Przywróć operację 2' }).click()
    await expect(table.getByRole('row', { name: /Nieznana operacja/ })).not.toContainText('pominięta')

    await page.getByRole('tab', { name: 'Reguły księgowania' }).click()
    await page.getByRole('button', { name: 'Zastosuj reguły do niezaksięgowanych' }).click()
    await expect(page.getByRole('status')).toContainText('Zaksięgowano wg reguł: 1')
    const hc = withCompany(await auth(page), ids[COMPANY])
    const ops = await (await request.get(`/api/v1/bank/transactions?accountId=${acc['130']}&status=New`, { headers: hc })).json() as { title: string }[]
    expect(ops.map(o => o.title).sort()).toEqual(['Nieznana operacja', 'Opłata łączna'])
  })

  test('Elixir-0 przez API: kierunek z numeru rachunku, duplikaty, wymagany numer rachunku', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const hc = withCompany(await auth(page), ids[COMPANY])
    const imp = (data: Record<string, unknown>) => request.post('/api/v1/bank/import', { headers: hc, data: { accountId: acc['100'], format: 'Elixir', content: ELIXIR, applyRules: false, ...data } })
    const missing = await imp({})
    expect(missing.status()).toBe(400)
    expect(JSON.stringify(await missing.json())).toContain('numer rachunku firmy')
    const ok = await (await imp({ ownAccount: OWN, fileName: 'wyciag.elixir' })).json()
    expect([ok.imported, ok.duplicates, ok.skipped]).toEqual([3, 0, 1])
    expect(ok.rows.map((r: { amount: number }) => r.amount)).toEqual([1230, -15.5, -200])
    expect(ok.rows[0].counterparty).toContain('Zażółć Gęślą Jaźń')
    const again = await (await imp({ ownAccount: `PL ${OWN}` })).json()
    expect([again.imported, again.duplicates]).toEqual([0, 3])
    // konto, które nie jest kasą/bankiem
    const wrong = await imp({ accountId: acc['700'], ownAccount: OWN })
    expect(wrong.status()).toBe(400)
    expect(JSON.stringify(await wrong.json())).toContain('kasa/bank')
    const garbage = await request.post('/api/v1/bank/import', { headers: hc, data: { accountId: acc['130'], format: 'Mt940', content: 'to nie jest wyciąg' } })
    expect(garbage.status()).toBe(400)
    expect(JSON.stringify(await garbage.json())).toContain('Nie znaleziono żadnych operacji')
  })

  test('błędy w UI: brak konta i nieprawidłowy plik', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/bank-import')
    await page.getByLabel('Treść wyciągu').fill('to;nie;jest;wyciąg')
    await page.getByRole('button', { name: 'Analizuj' }).click()
    await expect(page.getByRole('alert')).toContainText('Wybierz konto bankowe')
    await page.getByLabel('Konto bankowe').selectOption({ label: '130 Rachunek bankowy' })
    await page.getByRole('button', { name: 'Analizuj' }).click()
    await expect(page.getByRole('alert')).toContainText('Nie znaleziono żadnych operacji')
  })

  test('usunięcie reguły', async ({ page }) => {
    await openBank(page, 'Reguły księgowania')
    await page.getByRole('button', { name: 'Wyłącz regułę Opłata bankowa' }).click()
    await expect(page.getByRole('table', { name: 'Reguły księgowania' }).getByRole('row', { name: /Opłata bankowa/ })).toContainText('Wyłączona')
    await page.getByRole('button', { name: 'Usuń regułę Opłata bankowa' }).click()
    await confirmDialog(page, 'Usuń regułę')
    await expect(page.getByRole('status')).toContainText('Usunięto regułę')
    await expect(page.getByRole('table', { name: 'Reguły księgowania' })).toHaveCount(0)
  })

  test('rola Podgląd: odczyt tak, import/reguły/księgowanie 403; izolacja firm', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const a = await auth(page)
    const users = await (await request.get('/api/v1/users', { headers: a })).json() as { id: string; email: string }[]
    const olga = users.find(u => u.email === OLGA.email)!
    const own = await (await request.get(`/api/v1/users/${olga.id}/companies`, { headers: a })).json() as string[]
    expect((await request.put(`/api/v1/users/${olga.id}/companies`, { headers: a, data: { companyIds: [...new Set([...own, ids[COMPANY]])] } })).status()).toBeLessThan(300)
    const ha = withCompany(a, ids[COMPANY])
    const opId = (await (await request.get(`/api/v1/bank/transactions?accountId=${acc['130']}&status=New`, { headers: ha })).json() as { id: string }[])[0].id

    // izolacja: pierwsza firma nie widzi operacji, wyciągów ani reguł tej firmy
    const first = withCompany(a, (await companies(page, request))[FIRST])
    // pierwsza firma ma własne operacje z testu 04b, ale żadnej z tej firmy
    const firstOps = await (await request.get('/api/v1/bank/transactions', { headers: first })).json() as { id: string; title: string }[]
    expect(firstOps.some(o => o.id === opId || o.title.includes('Opłata łączna') || o.title.includes('prowadzenie'))).toBe(false)
    expect(await (await request.get('/api/v1/bank/rules', { headers: first })).json()).toHaveLength(0)
    expect((await request.get(`/api/v1/bank/reconciliation?accountId=${acc['130']}`, { headers: first })).status()).toBe(400)
    expect((await request.post(`/api/v1/bank/transactions/${opId}/post-invoice`, { headers: first, data: { invoiceId: saleId } })).status()).toBe(400)

    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await login(page, OLGA.email, OLGA.password)
    await expect(page).toHaveURL(/\/dashboard/)
    const v = withCompany(await auth(page), ids[COMPANY])
    expect((await request.get('/api/v1/bank/transactions', { headers: v })).status()).toBe(200)
    expect((await request.get('/api/v1/bank/rules', { headers: v })).status()).toBe(200)
    expect((await request.get(`/api/v1/bank/statements?accountId=${acc['130']}`, { headers: v })).status()).toBe(200)
    expect((await request.get(`/api/v1/bank/reconciliation?accountId=${acc['130']}`, { headers: v })).status()).toBe(200)
    expect((await request.post('/api/v1/bank/import', { headers: v, data: { accountId: acc['130'], format: 'Mt940', content: ':61:x' } })).status()).toBe(403)
    expect((await request.post('/api/v1/bank/rules', { headers: v, data: { name: 'X', matchText: 'abc', direction: 'Any', targetAccountId: acc['402'] } })).status()).toBe(403)
    expect((await request.post('/api/v1/bank/apply-rules', { headers: v, data: { accountId: acc['130'] } })).status()).toBe(403)
    expect((await request.put(`/api/v1/bank/transactions/${opId}/skip`, { headers: v, data: { skip: true } })).status()).toBe(403)
    expect((await request.post(`/api/v1/bank/transactions/${opId}/post-invoice`, { headers: v, data: { invoiceId: saleId } })).status()).toBe(403)
    expect((await request.get('/api/v1/bank/rules')).status()).toBe(401)

    await switchCompany(page, COMPANY)
    await page.goto('/bank-import')
    await expect(page.getByRole('alert')).toContainText('nie pozwala')
  })

  for (const theme of ['light', 'dark'] as const) {
    test(`axe: strony wyciągów bankowych bez naruszeń serious/critical (${theme})`, async ({ page }) => {
      await page.addInitScript(t => localStorage.setItem('lc.theme', t), theme)
      await openBank(page)
      for (const tab of ['Import wyciągu', 'Reguły księgowania', 'Uzgodnienie z bankiem']) {
        await page.getByRole('tab', { name: tab }).click()
        await expect(page.getByRole('tab', { name: tab })).toHaveAttribute('aria-selected', 'true')
        const res = await new AxeBuilder({ page }).analyze()
        const bad = res.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
        expect(bad.map(v => `${tab}: ${v.id} – ${v.nodes.slice(0, 2).map(n => n.html.slice(0, 90)).join(' | ')}`)).toEqual([])
      }
    })
  }
})
