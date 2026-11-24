import AxeBuilder from '@axe-core/playwright'
import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import * as net from 'node:net'
import { ADMIN, accessToken, login, loginOk, squash } from './helpers'

// Po 08: pracuje wyłącznie w nowej firmie, więc nie zmienia danych wcześniejszych plików.
// Serwer SMTP jest prawdziwy (prosty odbiornik w tym procesie), a aplikacja wysyła przez MailKit.

const COMPANY = 'Firma windykacja test'
const FIRST = 'Biuro Rachunkowe Test sp. z o.o.'
const DEBTOR = 'Dłużnik Testowy sp. z o.o.'
const DEBTOR_EMAIL = 'dluznik@example.test'
const AS_OF = '2025-05-08'       // 45 dni po terminie 24.03.2025
const OLGA = { email: 'olga.skroty@biuro.test', password: 'Haslo1234' }

interface Mail { from: string; to: string[]; data: string }

function startSmtpSink() {
  const received: Mail[] = []
  const server = net.createServer(sock => {
    sock.write('220 sink ESMTP\r\n')
    let buf = ''
    let inData = false
    let cur: Mail = { from: '', to: [], data: '' }
    sock.on('data', chunk => {
      buf += chunk.toString('utf8')
      for (;;) {
        if (inData) {
          const idx = buf.indexOf('\r\n.\r\n')
          if (idx < 0) break
          cur.data = buf.slice(0, idx); buf = buf.slice(idx + 5); inData = false
          received.push(cur); sock.write('250 OK queued\r\n'); continue
        }
        const nl = buf.indexOf('\r\n')
        if (nl < 0) break
        const line = buf.slice(0, nl); buf = buf.slice(nl + 2)
        const up = line.toUpperCase()
        if (up.startsWith('EHLO') || up.startsWith('HELO')) sock.write('250-sink\r\n250 8BITMIME\r\n')
        else if (up.startsWith('MAIL FROM')) { cur = { from: line, to: [], data: '' }; sock.write('250 OK\r\n') }
        else if (up.startsWith('RCPT TO')) { cur.to.push(line); sock.write('250 OK\r\n') }
        else if (up === 'DATA') { inData = true; sock.write('354 go ahead\r\n') }
        else if (up === 'QUIT') { sock.write('221 bye\r\n'); sock.end() }
        else sock.write('250 OK\r\n')
      }
    })
    sock.on('error', () => { /* klient zamknął połączenie */ })
  })
  return new Promise<{ port: number; received: Mail[]; close: () => Promise<void> }>(resolve =>
    server.listen(0, '127.0.0.1', () => resolve({
      port: (server.address() as net.AddressInfo).port, received, close: () => new Promise(r => server.close(() => r())),
    })))
}

let sink: Awaited<ReturnType<typeof startSmtpSink>>
let ids: Record<string, string> = {}
let noticeId = ''

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
async function configureMail(page: Page, port: number) {
  await page.goto('/admin')
  const form = page.getByRole('form', { name: 'Ustawienia poczty' })
  await form.getByLabel('Serwer SMTP').fill('127.0.0.1')
  await form.getByLabel('Port SMTP').fill(String(port))
  await form.getByLabel('Połączenie szyfrowane (TLS)').uncheck()
  await form.getByLabel('Adres nadawcy').fill('biuro@firma.test')
  await form.getByLabel('Nazwa nadawcy').fill('Biuro Test')
  await form.getByRole('button', { name: 'Zapisz ustawienia poczty' }).click()
  await expect(page.getByRole('status')).toContainText('Zapisano ustawienia poczty')
}

test.describe.serial('Windykacja: odsetki, wezwania, noty, poczta SMTP', () => {
  test.beforeAll(async () => { sink = await startSmtpSink() })
  test.afterAll(async () => { await sink.close() })

  test('dane: firma, kontrahent i 4 faktury (po terminie, opłacona, anulowana, jeszcze niewymagalna)', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = await auth(page)
    expect((await request.post('/api/v1/companies', { headers: h, data: { name: COMPANY } })).status()).toBe(200)
    ids = await companies(page, request)
    const hc = withCompany(h, ids[COMPANY])
    const accounts = await (await request.get('/api/v1/accounts', { headers: hc })).json() as { id: string; code: string }[]
    const bank = accounts.find(a => a.code === '130')!.id
    const contractor = (await (await request.post('/api/v1/contractors', { headers: hc, data: { name: DEBTOR, email: DEBTOR_EMAIL, address: 'ul. Dłużnicza 1, Kraków' } })).json()).id as string
    const invoice = async (net: number, issue: string, due: string) => {
      const r = await request.post('/api/v1/invoices', { headers: hc, data: { type: 'Sales', contractorId: contractor, issueDate: issue, dueDate: due, lines: [{ description: 'Usługa', net, vatRate: 'R0' }] } })
      expect(r.status()).toBe(200)
      return (await r.json()).invoiceId as string
    }
    await invoice(1000, '2025-03-10', '2025-03-24')                                                   // po terminie – 1000 zł
    const paid = await invoice(500, '2025-03-11', '2025-03-25')
    expect((await request.post(`/api/v1/invoices/${paid}/payments`, { headers: hc, data: { date: '2025-03-20', amount: 500, accountId: bank } })).status()).toBeLessThan(300)
    const canceled = await invoice(300, '2025-03-12', '2025-03-26')
    expect((await request.post(`/api/v1/invoices/${canceled}/cancel`, { headers: hc })).status()).toBeLessThan(300)
    await invoice(700, '2025-05-01', '2025-06-30')                                                   // termin w przyszłości (na 8.05.2025)
  })

  test('bez tabeli stóp odsetek lista pokazuje czytelny komunikat; po dodaniu stopy odsetki się liczą', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/collections')
    await page.getByLabel('Na dzień').fill(AS_OF)
    const table = page.getByRole('table', { name: 'Zadłużenie kontrahentów' })
    await expect(table.getByRole('row', { name: new RegExp(DEBTOR) })).toBeVisible()
    await expect(page.getByRole('alert')).toContainText('tabelę stóp odsetek')

    await page.goto('/admin')
    const form = page.getByRole('form', { name: 'Nowa stopa odsetek' })
    await form.getByLabel('Obowiązuje od').fill('2025-01-01')
    await form.getByLabel('Stopa roczna (%)').fill('101')
    await form.getByRole('button', { name: 'Dodaj stopę' }).click()
    await expect(page.getByRole('alert')).toContainText('0–100%')
    await form.getByLabel('Stopa roczna (%)').fill('11,5')
    await form.getByRole('button', { name: 'Dodaj stopę' }).click()
    await expect(page.getByRole('status')).toContainText('Dodano stopę')
    await expect(page.getByRole('table', { name: 'Stopy odsetek' }).getByRole('row', { name: /2025-01-01/ })).toContainText('11,50%')
    await form.getByLabel('Stopa roczna (%)').fill('12')
    await form.getByRole('button', { name: 'Dodaj stopę' }).click()
    await expect(page.getByRole('alert')).toContainText('już istnieje')                 // jedna stopa na datę

    await page.goto('/collections')
    await page.getByLabel('Na dzień').fill(AS_OF)
    const row = page.getByRole('table', { name: 'Zadłużenie kontrahentów' }).getByRole('row', { name: new RegExp(DEBTOR) })
    await expect(row).toContainText('1000,00')                  // tylko faktura po terminie
    await expect(row).toContainText('14,18')                    // 1000 × 11,5% × 45 / 365
    await expect(row.getByRole('cell').nth(1)).toHaveText('1')  // opłacona, anulowana i niewymagalna pominięte
    await expect(page.getByRole('alert')).toHaveCount(0)
  })

  test('wezwanie: podgląd z odsetkami, nota odsetkowa i wydruk z zapisem w historii', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/collections')
    await page.getByLabel('Na dzień').fill(AS_OF)
    await page.getByRole('link', { name: DEBTOR }).click()
    await expect(page.getByRole('heading', { name: `Windykacja: ${DEBTOR}` })).toBeVisible()
    await expect(page.getByLabel('Na dzień')).toHaveValue(AS_OF)

    await page.getByRole('button', { name: 'Przygotuj dokument' }).click()
    const draft = page.getByTestId('notice-draft')
    await expect(draft.getByRole('heading', { name: 'Wezwanie do zapłaty' })).toBeVisible()
    await expect(page.getByTestId('notice-total')).toHaveText(/1014,18/)
    await expect(draft.getByRole('table', { name: 'Pozycje dokumentu' }).locator('tbody tr')).toHaveCount(1)
    await expect(draft.getByLabel('Adres e-mail odbiorcy')).toHaveValue(DEBTOR_EMAIL)
    await expect(draft.getByLabel('Treść')).toHaveValue(/Prosimy o uregulowanie należności do dnia 2025-05-15/)

    // bez odsetków tylko kapitał; nota odsetkowa – tylko odsetki
    await page.getByLabel('Doliczyć odsetki').uncheck()
    await expect(draft).toBeHidden()
    await page.getByRole('button', { name: 'Przygotuj dokument' }).click()
    await expect(page.getByTestId('notice-total')).toHaveText(/1000,00/)
    await page.getByLabel('Rodzaj dokumentu').selectOption('InterestNote')
    await page.getByRole('button', { name: 'Przygotuj dokument' }).click()
    await expect(page.getByTestId('notice-draft').getByRole('heading', { name: 'Nota odsetkowa' })).toBeVisible()
    await expect(page.getByTestId('notice-total')).toHaveText(/14,18/)

    // wydruk: dokument zapisany w historii, układ z nagłówkiem firmy
    await page.getByRole('button', { name: 'Zapisz i drukuj' }).click()
    await expect(page).toHaveURL(/\/collections\/notices\/[0-9a-f-]{36}$/)
    noticeId = page.url().split('/').pop()!
    const sheet = page.getByTestId('notice-sheet')
    await expect(sheet).toContainText(DEBTOR)
    await expect(sheet).toContainText('ul. Dłużnicza 1, Kraków')
    await expect(sheet).toContainText('2025-05-15')
    await expect(page.getByTestId('notice-sheet-total')).toHaveText(/14,18/)
    await page.emulateMedia({ media: 'print' })
    await expect(page.getByTestId('print-header')).toContainText(COMPANY)
    await expect(page.getByRole('navigation', { name: 'Menu główne' })).toBeHidden()
    await page.emulateMedia({ media: 'screen' })

    await page.goBack()
    const history = page.getByRole('table', { name: 'Historia windykacji' })
    await expect(history.getByRole('row', { name: /Nota odsetkowa/ })).toContainText('wystawiono')
    await expect(history.getByRole('row', { name: /Nota odsetkowa/ })).toContainText('14,18')
  })

  test('ustawienia poczty: hasło nigdy nie wraca, test wysyłki dociera do serwera, błędny port daje czytelny komunikat', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await configureMail(page, sink.port)
    const form = page.getByRole('form', { name: 'Ustawienia poczty' })
    await form.getByLabel('Hasło SMTP').fill('super-tajne-haslo-123')
    await form.getByRole('button', { name: 'Zapisz ustawienia poczty' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano')

    await page.reload()
    const again = page.getByRole('form', { name: 'Ustawienia poczty' })
    await expect(again.getByLabel('Serwer SMTP')).toHaveValue('127.0.0.1')
    await expect(again.getByLabel('Hasło SMTP')).toHaveValue('')
    await expect(again.getByLabel('Hasło SMTP')).toHaveAttribute('placeholder', /zapisane/)
    const api = await (await request.get('/api/v1/admin/mail', { headers: withCompany(await auth(page), ids[COMPANY]) })).text()
    expect(api).not.toContain('super-tajne-haslo-123')
    expect(JSON.parse(api).hasPassword).toBe(true)

    // test wysyłki trafia do (prawdziwego) serwera SMTP
    const before = sink.received.length
    await again.getByLabel('Adres testowy').fill('ktos@example.test')
    await again.getByRole('button', { name: 'Wyślij wiadomość testową' }).click()
    await expect(page.getByRole('status')).toContainText('Wysłano wiadomość testową')
    expect(sink.received.length).toBe(before + 1)
    expect(sink.received.at(-1)!.to[0]).toContain('ktos@example.test')

    // zły port: komunikat po polsku bez haseł i śladów technicznych
    await again.getByLabel('Port SMTP').fill('1')
    await again.getByRole('button', { name: 'Zapisz ustawienia poczty' }).click()
    await expect(page.getByRole('status')).toContainText('Zapisano')
    await again.getByRole('button', { name: 'Wyślij wiadomość testową' }).click()
    const alert = page.getByRole('alert')
    await expect(alert).toContainText('Nie można połączyć się z serwerem poczty 127.0.0.1:1')
    await expect(alert).not.toContainText('super-tajne-haslo-123')
    await expect(alert).not.toContainText('Exception')
    await configureMail(page, sink.port)
  })

  test('wysyłka e-mail: wiadomość z tabelą trafia do odbiorcy, a błąd wysyłki zostaje w historii', async ({ page }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/collections')
    await page.getByLabel('Na dzień').fill(AS_OF)
    await page.getByRole('link', { name: DEBTOR }).click()
    await page.getByRole('button', { name: 'Przygotuj dokument' }).click()
    await page.getByTestId('notice-draft').getByLabel('Temat wiadomości').fill('Pilne wezwanie ASCII')

    const before = sink.received.length
    await page.getByRole('button', { name: 'Wyślij e-mail' }).click()
    await expect(page.getByRole('status')).toContainText(`Wysłano wezwanie do zapłaty na adres ${DEBTOR_EMAIL}`)
    expect(sink.received.length).toBe(before + 1)
    const mail = sink.received.at(-1)!
    expect(mail.to[0]).toContain(DEBTOR_EMAIL)
    expect(mail.from).toContain('biuro@firma.test')
    expect(mail.data).toContain('Subject: Pilne wezwanie ASCII')
    expect(mail.data).toContain('text/html')
    expect(mail.data).toMatch(/FV=2F2025=2F0001|FV\/2025\/0001/)           // numer faktury w treści (kodowanie quoted-printable)
    const history = page.getByRole('table', { name: 'Historia windykacji' })
    await expect(history.getByRole('row', { name: /e-mail/ }).first()).toContainText('wysłano')

    // błąd: serwer niedostępny → komunikat i wpis „błąd wysyłki”, wystawiony dokument zostaje w historii
    await sink.close()
    await page.getByRole('button', { name: 'Wyślij e-mail' }).click()
    await expect(page.getByRole('alert')).toContainText('Nie udało się wysłać wiadomości')
    await expect(page.getByRole('alert')).toContainText('127.0.0.1')
    await expect(history.getByRole('row', { name: /błąd wysyłki/ })).toBeVisible()
    sink = await startSmtpSink()
  })

  test('karta faktury pokazuje historię wezwań; opłacona faktura znika z listy zadłużenia', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    await switchCompany(page, COMPANY)
    await page.goto('/invoices')
    await page.getByLabel('Od', { exact: true }).fill('2000-01-01')
    await page.getByRole('row', { name: /FV\/2025\/0001/ }).getByRole('link', { name: 'FV/2025/0001' }).click()
    const hist = page.getByRole('table', { name: 'Historia windykacji' })
    await expect(hist).toBeVisible()
    await expect(hist.getByRole('row', { name: /Nota odsetkowa/ })).toContainText('wydruk')
    await expect(hist.getByRole('row', { name: /Wezwanie do zapłaty/ }).first()).toBeVisible()

    // dokument zapisany jest niezależny od późniejszej wpłaty (migawka)
    const headers = withCompany(await auth(page), ids[COMPANY])
    const id = page.url().split('/').pop()!
    const accounts = await (await request.get('/api/v1/accounts', { headers })).json() as { id: string; code: string }[]
    expect((await request.post(`/api/v1/invoices/${id}/payments`, { headers, data: { date: '2025-05-09', amount: 1000, accountId: accounts.find(a => a.code === '130')!.id } })).status()).toBeLessThan(300)
    const doc = await (await request.get(`/api/v1/collections/${noticeId}`, { headers })).json()
    expect(doc.principal).toBe(1000)
    expect(doc.interest).toBe(14.18)

    await page.goto('/collections')
    await page.getByLabel('Na dzień').fill(AS_OF)
    await expect(page.getByText('Brak faktur po terminie na wybrany dzień.')).toBeVisible()
  })

  test('rola Podgląd czyta windykację, ale nie wystawia dokumentów i nie zmienia ustawień; izolacja firm', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const h = await auth(page)
    const users = await (await request.get('/api/v1/users', { headers: h })).json() as { id: string; email: string }[]
    const olga = users.find(u => u.email === OLGA.email)!
    const own = await (await request.get(`/api/v1/users/${olga.id}/companies`, { headers: h })).json() as string[]
    expect((await request.put(`/api/v1/users/${olga.id}/companies`, { headers: h, data: { companyIds: [...new Set([...own, ids[COMPANY]])] } })).status()).toBeLessThan(300)

    // izolacja: pierwsza firma nie widzi ani dokumentu, ani ustawień poczty tej firmy
    const first = withCompany(h, (await companies(page, request))[FIRST])
    expect((await (await request.get('/api/v1/collections', { headers: first })).json())).toHaveLength(0)
    expect((await request.get(`/api/v1/collections/${noticeId}`, { headers: first })).status()).toBe(400)
    expect((await (await request.get('/api/v1/admin/mail', { headers: first })).json()).configured).toBe(false)

    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await login(page, OLGA.email, OLGA.password)
    await expect(page).toHaveURL(/\/dashboard/)
    const v = withCompany(await auth(page), ids[COMPANY])
    expect((await request.get(`/api/v1/collections/overdue?asOf=${AS_OF}`, { headers: v })).status()).toBe(200)
    expect((await request.get('/api/v1/collections', { headers: v })).status()).toBe(200)
    const req = { contractorId: ids[COMPANY], kind: 'Reminder', asOf: AS_OF, paymentDays: 7, includeInterest: false }
    expect((await request.post('/api/v1/collections/preview', { headers: v, data: req })).status()).toBe(403)
    expect((await request.post('/api/v1/collections', { headers: v, data: { request: req, channel: 'Print' } })).status()).toBe(403)
    expect((await request.get('/api/v1/admin/mail', { headers: v })).status()).toBe(403)
    expect((await request.put('/api/v1/admin/mail', { headers: v, data: {} })).status()).toBe(403)
    expect((await request.post('/api/v1/admin/interest-rates', { headers: v, data: { validFrom: '2030-01-01', annualPercent: 5 } })).status()).toBe(403)
    expect((await request.get('/api/v1/admin/interest-rates', { headers: v })).status()).toBe(200)
    expect((await request.post('/api/v1/collections/preview', { data: req })).status()).toBe(401)

    await switchCompany(page, COMPANY)
    await page.goto('/collections')
    await expect(page.getByRole('heading', { name: 'Windykacja' })).toBeVisible()
    await page.goto(`/collections/${(await (await request.get('/api/v1/contractors', { headers: v })).json())[0].id}`)
    await expect(page.getByRole('table', { name: 'Historia windykacji' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Przygotuj dokument' })).toHaveCount(0)
  })

  for (const theme of ['light', 'dark'] as const) {
    test(`axe: strony windykacji bez naruszeń serious/critical (${theme})`, async ({ page }) => {
      await page.addInitScript(t => localStorage.setItem('lc.theme', t), theme)
      await loginOk(page, ADMIN)
      await switchCompany(page, COMPANY)
      for (const path of ['/collections', `/collections/notices/${noticeId}`, '/admin']) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        const res = await new AxeBuilder({ page }).analyze()
        const bad = res.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
        expect(bad.map(v => `${path}: ${v.id} – ${v.nodes.slice(0, 2).map(n => n.html.slice(0, 90)).join(' | ')}`)).toEqual([])
      }
    })
  }
})

void squash
