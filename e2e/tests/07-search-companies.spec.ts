import { expect, test } from '@playwright/test'
import { ADMIN, accessToken, loginOk } from './helpers'

// Po 06: istnieją dwie firmy z danymi (FIRST i SECOND). Wyniki wyszukiwania muszą dotyczyć tylko bieżącej firmy.

const FIRST = 'Biuro Rachunkowe Test sp. z o.o.'
const SECOND = 'Klient Drugi sp. z o.o.'

test.describe.serial('Wyszukiwarka i skróty a wielofirmowość', () => {
  test('API: wyniki tylko z firmy wskazanej w X-Company-Id', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const auth = { Authorization: `Bearer ${await accessToken(page)}` }
    const all = await (await request.get('/api/v1/companies', { headers: auth })).json() as { id: string; name: string }[]
    const first = { ...auth, 'X-Company-Id': all.find(c => c.name === FIRST)!.id }
    const second = { ...auth, 'X-Company-Id': all.find(c => c.name === SECOND)!.id }

    const inSecond = await (await request.get('/api/v1/search?q=Wp%C5%82ata%20kapita%C5%82u', { headers: second })).json()
    expect(inSecond.transactions).toHaveLength(1)
    const inFirst = await (await request.get('/api/v1/search?q=Wp%C5%82ata%20kapita%C5%82u', { headers: first })).json()
    expect(inFirst.transactions).toHaveLength(0)

    const numberInFirst = await (await request.get('/api/v1/search?q=FV%2F', { headers: first })).json()
    expect(numberInFirst.invoices.length).toBeGreaterThan(0)
    const numberInSecond = await (await request.get('/api/v1/search?q=FV%2F', { headers: second })).json()
    const ids = new Set(numberInFirst.invoices.map((i: { id: string }) => i.id))
    expect(numberInSecond.invoices.every((i: { id: string }) => !ids.has(i.id))).toBe(true)

    // obca firma → 403, także dla wyszukiwarki
    const fake = await request.get('/api/v1/search?q=Czynsz', { headers: { ...auth, 'X-Company-Id': '99999999-9999-9999-9999-999999999999' } })
    expect(fake.status()).toBe(403)
  })

  test('paleta po przełączeniu firmy pokazuje dane nowej firmy; polecenie „Przełącz firmę”', async ({ page }) => {
    await loginOk(page, ADMIN)
    const picker = page.getByLabel('Firma', { exact: true })
    await picker.selectOption({ label: SECOND })
    await expect(picker.locator('option:checked')).toHaveText(SECOND)

    await page.keyboard.press('Control+k')
    const box = page.getByRole('combobox', { name: 'Szukaj w programie' })
    await box.fill('Wpłata kapitału')
    await expect(page.getByRole('group', { name: 'Księgowania' }).getByRole('option').first()).toBeVisible()

    await box.fill(`Przełącz firmę: ${FIRST}`)
    await page.keyboard.press('Enter')
    await expect(picker.locator('option:checked')).toHaveText(FIRST)

    await page.keyboard.press('Control+k')
    await page.getByRole('combobox', { name: 'Szukaj w programie' }).fill('Wpłata kapitału')
    await expect(page.getByText('Brak wyników.')).toBeVisible()
  })

  test('Alt+↓ / Alt+↑ przełączają firmę cyklicznie', async ({ page }) => {
    await loginOk(page, ADMIN)
    const picker = page.getByLabel('Firma', { exact: true })
    const names = await picker.locator('option').allTextContents()
    expect(names.length).toBeGreaterThanOrEqual(2)
    const start = await picker.locator('option:checked').textContent()
    const i = names.indexOf(start!)

    await page.keyboard.press('Alt+ArrowDown')
    await expect(picker.locator('option:checked')).toHaveText(names[(i + 1) % names.length])
    await page.keyboard.press('Alt+ArrowUp')
    await expect(picker.locator('option:checked')).toHaveText(start!)
  })
})
