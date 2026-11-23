import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { ADMIN, accessToken, loginOk } from './helpers'

const iso = (offsetDays = 0) => {
  const d = new Date(); d.setDate(d.getDate() + offsetDays)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
const YEAR_START = `${new Date().getFullYear()}-01-01`

async function headersOf(page: Page) {
  return { Authorization: `Bearer ${await accessToken(page)}` }
}

async function contractorId(request: APIRequestContext, headers: Record<string, string>, prefix: string) {
  const list = await (await request.get('/api/v1/contractors', { headers })).json()
  return list.find((c: { name: string }) => c.name.startsWith(prefix)).id as string
}

async function newInvoice(request: APIRequestContext, headers: Record<string, string>, body: object) {
  const res = await request.post('/api/v1/invoices', { headers, data: body })
  expect(res.status(), await res.text()).toBe(200)
  return (await res.json()) as { invoiceId: string; number: string; transactionId: string }
}

async function vatSummary(request: APIRequestContext, headers: Record<string, string>) {
  return (await (await request.get(`/api/v1/reports/vat-summary?from=${YEAR_START}&to=${iso()}`, { headers })).json()) as { outputVat: number; inputVat: number }
}

async function entriesOf(request: APIRequestContext, headers: Record<string, string>, txId: string) {
  const tx = await (await request.get(`/api/v1/transactions/${txId}`, { headers })).json()
  const byCode: Record<string, { debit: number; credit: number }> = {}
  for (const e of tx.entries) {
    byCode[e.accountCode] ??= { debit: 0, credit: 0 }
    byCode[e.accountCode][e.side === 'Debit' ? 'debit' : 'credit'] += e.amount
  }
  return byCode
}

test.describe.serial('Faktury korygujące', () => {
  test.beforeEach(async ({ page }) => { await loginOk(page, ADMIN) })

  test('korekta in minus: różnice, księgowanie ze znakiem, VAT i kwota do zapłaty na fakturze korygowanej', async ({ page, request }) => {
    const headers = await headersOf(page)
    const before = await vatSummary(request, headers)
    const inv = await newInvoice(request, headers, {
      type: 'Sales', contractorId: await contractorId(request, headers, 'Acme Klient'), issueDate: iso(), dueDate: iso(14),
      lines: [{ description: 'Usługa A', net: 1000, vatRate: 'R23' }],
    })

    await page.goto(`/invoices/${inv.invoiceId}`)
    await page.getByRole('link', { name: 'Wystaw korektę' }).click()
    await expect(page.getByRole('heading', { name: `Korekta faktury ${inv.number}` })).toBeVisible()
    await page.getByLabel('Netto po korekcie pozycji 1').fill('800')
    await page.getByLabel('Przyczyna korekty').fill('Rabat 20%')
    await expect(page.getByTestId('corr-net')).toHaveText(/^[-−]200,00$/)
    await expect(page.getByTestId('corr-vat')).toHaveText(/^[-−]46,00$/)
    await expect(page.getByTestId('corr-gross')).toContainText(/[-−]246,00/)
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()

    await expect(page.getByRole('heading', { name: /Faktura KFV\/\d{4}\/0001/ })).toBeVisible()
    await expect(page.getByTestId('invoice-status')).toHaveText('Korekta')
    await expect(page.getByTestId('correction-info')).toContainText(inv.number)
    await expect(page.getByRole('form', { name: 'Nowa płatność' })).toHaveCount(0) // korekta nie podlega płatności

    // księgowanie: Wn 700 200, Wn 220 46, Ma 201 246 (storno przychodu, VAT i należności)
    const corrId = page.url().split('/').pop()!
    const corr = await (await request.get(`/api/v1/invoices/${corrId}`, { headers })).json()
    const e = await entriesOf(request, headers, corr.transactionId)
    expect(e['700']).toEqual({ debit: 200, credit: 0 })
    expect(e['220']).toEqual({ debit: 46, credit: 0 })
    expect(e['201']).toEqual({ debit: 0, credit: 246 })

    // faktura korygowana: nowa kwota do zapłaty
    await page.goto(`/invoices/${inv.invoiceId}`)
    await expect(page.getByTestId('invoice-after-corrections')).toHaveText(/984,00/)
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/984,00/)
    await expect(page.getByTestId('corrections')).toContainText('KFV/')

    // VAT należny zmalał o 46 względem stanu sprzed korekty (230 z faktury - 46 z korekty = +184)
    const after = await vatSummary(request, headers)
    expect(Math.round((after.outputVat - before.outputVat) * 100) / 100).toBe(184)

    // rejestr VAT sprzedaży zawiera korektę z ujemnym VAT
    await page.goto('/reports')
    await page.getByRole('tab', { name: 'Rejestry VAT' }).click()
    await page.getByLabel('Od', { exact: true }).fill(YEAR_START)
    await page.getByLabel('Do', { exact: true }).fill(iso())
    await expect(page.getByRole('row', { name: /KFV\// })).toContainText(/[-−]46,00/)

    // lista faktur: status i powiązanie
    await page.goto('/invoices')
    await expect(page.getByRole('row', { name: /KFV\// }).first()).toContainText('Korekta')
    await expect(page.getByRole('row', { name: /KFV\// }).first()).toContainText(`korekta do ${inv.number}`)
  })

  test('korekta nie może zmniejszyć faktury poniżej kwoty już zapłaconej', async ({ page, request }) => {
    const headers = await headersOf(page)
    const inv = await newInvoice(request, headers, {
      type: 'Sales', contractorId: await contractorId(request, headers, 'Acme Klient'), issueDate: iso(), dueDate: iso(14),
      lines: [{ description: 'Usługa B', net: 1000, vatRate: 'R23' }],
    })
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json()
    const bank = accounts.find((a: { code: string }) => a.code === '130').id
    expect((await request.post(`/api/v1/invoices/${inv.invoiceId}/payments`, { headers, data: { date: iso(), amount: 500, accountId: bank } })).status()).toBe(204)

    await page.goto(`/invoices/${inv.invoiceId}/correction`)
    await page.getByLabel('Netto po korekcie pozycji 1').fill('300') // brutto 369 < zapłacone 500
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()
    await expect(page.getByRole('alert')).toContainText('poniżej kwoty już zapłaconej')

    await page.getByLabel('Netto po korekcie pozycji 1').fill('600') // brutto 738 >= 500
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()
    await expect(page.getByTestId('invoice-status')).toHaveText('Korekta')
    await page.goto(`/invoices/${inv.invoiceId}`)
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/238,00/) // 738 - 500
  })

  test('korekta in plus, blokada anulowania faktury z korektą i cofnięcie korekty', async ({ page, request }) => {
    const headers = await headersOf(page)
    const inv = await newInvoice(request, headers, {
      type: 'Sales', contractorId: await contractorId(request, headers, 'Acme Klient'), issueDate: iso(), dueDate: iso(14),
      lines: [{ description: 'Usługa C', net: 100, vatRate: 'R23' }],
    })
    await page.goto(`/invoices/${inv.invoiceId}/correction`)
    await page.getByLabel('Netto po korekcie pozycji 1').fill('150') // +50 netto, +11,50 VAT
    await expect(page.getByTestId('corr-gross')).toContainText('61,50')
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()
    await expect(page.getByTestId('invoice-status')).toHaveText('Korekta')
    const corrUrl = page.url()
    const corr = await (await request.get(`/api/v1/invoices/${corrUrl.split('/').pop()}`, { headers })).json()
    const e = await entriesOf(request, headers, corr.transactionId)
    expect(e['201']).toEqual({ debit: 61.5, credit: 0 }) // zwiększenie należności po stronie Wn
    expect(e['700']).toEqual({ debit: 0, credit: 50 })
    expect(e['220']).toEqual({ debit: 0, credit: 11.5 })

    // oryginału z aktywną korektą nie można anulować
    await page.goto(`/invoices/${inv.invoiceId}`)
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/184,50/)
    page.once('dialog', d => d.accept())
    await page.getByRole('button', { name: 'Anuluj fakturę' }).click()
    await expect(page.getByRole('alert')).toContainText('anuluj najpierw korekty')

    // anulowanie korekty przywraca kwotę do zapłaty
    await page.goto(corrUrl)
    page.once('dialog', d => d.accept())
    await page.getByRole('button', { name: 'Anuluj fakturę' }).click()
    await expect(page.getByTestId('invoice-status')).toHaveText('Anulowana')
    await page.goto(`/invoices/${inv.invoiceId}`)
    await expect(page.getByTestId('invoice-remaining')).toHaveText(/123,00/)

    // teraz oryginał można anulować
    page.once('dialog', d => d.accept())
    await page.getByRole('button', { name: 'Anuluj fakturę' }).click()
    await expect(page.getByTestId('invoice-status')).toHaveText('Anulowana')
  })

  test('zmiana stawki VAT daje dwie pozycje korekty; wydruk korekty', async ({ page, request }) => {
    const headers = await headersOf(page)
    const inv = await newInvoice(request, headers, {
      type: 'Sales', contractorId: await contractorId(request, headers, 'Acme Klient'), issueDate: iso(), dueDate: iso(14),
      lines: [{ description: 'Usługa D', net: 100, vatRate: 'R23' }],
    })
    await page.goto(`/invoices/${inv.invoiceId}/correction`)
    await page.getByLabel('Stawka VAT po korekcie pozycji 1').selectOption('R8')
    await expect(page.getByTestId('corr-net')).toHaveText(/^0,00$/)
    await expect(page.getByTestId('corr-vat')).toHaveText(/^[-−]15,00$/)
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()
    await expect(page.getByTestId('invoice-status')).toHaveText('Korekta')
    const corrId = page.url().split('/').pop()!
    const corr = await (await request.get(`/api/v1/invoices/${corrId}`, { headers })).json()
    expect(corr.lines).toHaveLength(2)
    expect(corr.net).toBe(0)
    expect(corr.vat).toBe(-15)

    await page.goto(`/invoices/${corrId}/print`)
    await expect(page.getByTestId('invoice-sheet')).toContainText('Faktura korygująca')
    await expect(page.getByTestId('print-correction-ref')).toContainText(inv.number)
    await expect(page.getByTestId('print-total')).toHaveText(/[-−]15,00/)
  })

  test('korekta faktury zakupu wymaga numeru dostawcy; rola Podgląd nie wystawi korekty', async ({ page, request }) => {
    const headers = await headersOf(page)
    const inv = await newInvoice(request, headers, {
      type: 'Purchase', number: 'FZ/90/2026', contractorId: await contractorId(request, headers, 'Dostawca SA'),
      issueDate: iso(), dueDate: iso(14), lines: [{ description: 'Towar', net: 200, vatRate: 'R23' }],
    })
    await page.goto(`/invoices/${inv.invoiceId}/correction`)
    await page.getByLabel('Netto po korekcie pozycji 1').fill('150')
    await page.getByLabel('Numer korekty dostawcy').fill('KOR/1/2026')
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()
    await expect(page.getByRole('heading', { name: 'Faktura KOR/1/2026' })).toBeVisible()
    const corrId = page.url().split('/').pop()!
    const corr = await (await request.get(`/api/v1/invoices/${corrId}`, { headers })).json()
    const e = await entriesOf(request, headers, corr.transactionId)
    expect(e['202']).toEqual({ debit: 61.5, credit: 0 })   // zobowiązanie maleje
    expect(e['402']).toEqual({ debit: 0, credit: 50 })     // koszt maleje
    expect(e['220']).toEqual({ debit: 0, credit: 11.5 })   // VAT naliczony maleje

    // ten sam numer drugi raz
    await page.goto(`/invoices/${inv.invoiceId}/correction`)
    await page.getByLabel('Netto po korekcie pozycji 1').fill('100')
    await page.getByLabel('Numer korekty dostawcy').fill('KOR/1/2026')
    await page.getByRole('button', { name: 'Wystaw korektę i zaksięguj' }).click()
    await expect(page.getByRole('alert')).toContainText('jest już zarejestrowany')

    // rola Podgląd
    const viewer = { email: 'podglad@biuro.test', password: 'Haslo1234' }
    await page.evaluate(() => { localStorage.clear() })
    await loginOk(page, viewer)
    await page.goto(`/invoices/${inv.invoiceId}/correction`)
    await expect(page.getByRole('alert')).toContainText('nie pozwala')
    const vh = await headersOf(page)
    expect((await request.post(`/api/v1/invoices/${inv.invoiceId}/corrections`, { headers: vh, data: { issueDate: iso(), lines: [] } })).status()).toBe(403)
  })
})
