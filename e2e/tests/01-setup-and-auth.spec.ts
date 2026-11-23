import { expect, test } from '@playwright/test'
import { ADMIN, login, loginOk, logout } from './helpers'

test.describe.serial('Pierwsze uruchomienie i logowanie', () => {
  test('świeża instalacja kieruje na kreator konfiguracji', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL(/\/setup/)
    await expect(page.getByRole('heading', { name: 'Witaj w LedgerCore' })).toBeVisible()
  })

  test('kreator odrzuca różne hasła i zbyt słabe hasło', async ({ page }) => {
    await page.goto('/setup')
    await page.getByLabel('Imię').fill(ADMIN.first)
    await page.getByLabel('Nazwisko').fill(ADMIN.last)
    await page.getByLabel('E-mail').fill(ADMIN.email)
    await page.getByLabel('Hasło (min. 8 znaków, w tym cyfra)').fill(ADMIN.password)
    await page.getByLabel('Powtórz hasło').fill('Inne1234')
    await page.getByRole('button', { name: 'Utwórz konto administratora' }).click()
    await expect(page.getByRole('alert')).toContainText('Hasła nie są takie same')

    await page.getByLabel('Hasło (min. 8 znaków, w tym cyfra)').fill('bezcyfry')
    await page.getByLabel('Powtórz hasło').fill('bezcyfry')
    await page.getByRole('button', { name: 'Utwórz konto administratora' }).click()
    await expect(page.getByRole('alert')).toBeVisible() // serwer odrzuca hasło bez cyfry
    await expect(page).toHaveURL(/\/setup/)
  })

  test('administrator tworzy konto i trafia na pulpit z domyślnym planem kont', async ({ page }) => {
    await page.goto('/setup')
    await page.getByLabel('Imię').fill(ADMIN.first)
    await page.getByLabel('Nazwisko').fill(ADMIN.last)
    await page.getByLabel('E-mail').fill(ADMIN.email)
    await page.getByLabel('Hasło (min. 8 znaków, w tym cyfra)').fill(ADMIN.password)
    await page.getByLabel('Powtórz hasło').fill(ADMIN.password)
    await page.getByRole('button', { name: 'Utwórz konto administratora' }).click()

    await expect(page).toHaveURL(/\/dashboard/)
    await expect(page.getByTestId('current-user')).toContainText('Anna Księgowa')
    await page.getByRole('link', { name: 'Plan kont' }).click()
    await expect(page.getByRole('row', { name: /130 Rachunek bankowy/ })).toBeVisible()
    await expect(page.getByRole('row', { name: /700 Przychody ze sprzedaży usług/ })).toBeVisible()
  })

  test('kreator jest zablokowany po utworzeniu administratora', async ({ page, request }) => {
    await page.goto('/setup')
    await expect(page).toHaveURL(/\/login/)
    const res = await request.post('/api/v1/setup', {
      data: { firstName: 'Intruz', lastName: 'X', email: 'intruz@x.pl', password: 'Haslo1234' },
    })
    expect(res.status()).toBe(400)
  })

  test('błędne hasło jest odrzucane, poprawne loguje, sesja przetrwa odświeżenie strony', async ({ page }) => {
    await login(page, ADMIN.email, 'ZleHaslo123')
    await expect(page.getByRole('alert')).toContainText('Nieprawidłowy e-mail lub hasło')
    await expect(page).toHaveURL(/\/login/)

    await loginOk(page, ADMIN)
    await page.reload()
    await expect(page.getByTestId('current-user')).toContainText('Anna Księgowa')
    await logout(page)
  })

  test('chronione strony i API wymagają logowania', async ({ page, request }) => {
    await page.goto('/transactions')
    await expect(page).toHaveURL(/\/login/)
    for (const url of ['/api/v1/accounts', '/api/v1/transactions', '/api/v1/reports/dashboard', '/api/v1/users']) {
      expect((await request.get(url)).status(), url).toBe(401)
    }
  })
})
