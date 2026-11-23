import { expect, test } from '@playwright/test'
import { ADMIN, VIEWER, accessToken, loginOk } from './helpers'

// Karta konta ze stronicowaniem (T-017). Plik przed 05 – tylko odczyty istniejących danych.

test.describe.serial('Karta konta: stronicowanie po stronie serwera', () => {
  test('strony składają się na pełną kartę, a saldo końcowe i sumy dotyczą całego okresu', async ({ page, request }) => {
    await loginOk(page, ADMIN)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const accounts: { id: string; code: string; debitTotal: number }[] = await (await request.get('/api/v1/accounts', { headers })).json()
    const busiest = [...accounts].sort((a, b) => b.debitTotal - a.debitTotal)[0]

    const full = await (await request.get(`/api/v1/accounts/${busiest.id}/ledger`, { headers })).json()
    expect(full.lines.length).toBeGreaterThan(1)

    const seen: string[] = []
    for (let p = 1; p <= full.lines.length; p++) {
      const res = await request.get(`/api/v1/accounts/${busiest.id}/ledger?page=${p}&pageSize=1`, { headers })
      expect(res.status()).toBe(200)
      const body = await res.json()
      expect(body.lines).toHaveLength(1)
      expect(body.totalCount).toBe(full.lines.length)
      expect(body.closingBalance).toBe(full.closingBalance)
      expect(body.totalDebit).toBe(full.totalDebit)
      expect(body.lines[0].balance).toBe(full.lines[p - 1].balance)
      seen.push(body.lines[0].transactionId)
    }
    expect(seen).toEqual(full.lines.map((l: { transactionId: string }) => l.transactionId))

    // strona poza zakresem jest pusta, ale nadal podaje salda
    const beyond = await (await request.get(`/api/v1/accounts/${busiest.id}/ledger?page=999&pageSize=50`, { headers })).json()
    expect(beyond.lines).toHaveLength(0)
    expect(beyond.closingBalance).toBe(full.closingBalance)
  })

  test('rola Podgląd czyta kartę konta; nieznane konto daje błąd, a UI pokazuje kartę', async ({ page, request }) => {
    await loginOk(page, VIEWER)
    const headers = { Authorization: `Bearer ${await accessToken(page)}` }
    const accounts: { id: string }[] = await (await request.get('/api/v1/accounts', { headers })).json()
    expect((await request.get(`/api/v1/accounts/${accounts[0].id}/ledger?page=1&pageSize=10`, { headers })).status()).toBe(200)
    expect((await request.get('/api/v1/accounts/00000000-0000-0000-0000-000000000001/ledger?page=1', { headers })).status()).toBe(400)

    await page.goto(`/accounts/${accounts[0].id}/ledger`)
    await expect(page.getByRole('heading', { name: /Karta konta/ })).toBeVisible()
    await expect(page.getByTestId('ledger-closing')).toBeVisible()
  })
})
