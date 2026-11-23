import { expect, type Page } from '@playwright/test'

export const ADMIN = { first: 'Anna', last: 'Księgowa', email: 'admin@biuro.test', password: 'Haslo1234' }
export const ACCOUNTANT = { first: 'Jan', last: 'Kowalski', email: 'jan@biuro.test', password: 'Haslo1234' }
export const VIEWER = { first: 'Ewa', last: 'Podglad', email: 'ewa@biuro.test', password: 'Haslo1234' }

/** Usuwa spacje (także twarde) z kwoty, np. "1 230,50" -> "1230,50". */
export const squash = (s: string | null) => (s ?? '').replace(/[\s  ]/g, '')

export async function login(page: Page, email: string, password: string) {
  await page.goto('/login')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Hasło').fill(password)
  await page.getByRole('button', { name: 'Zaloguj' }).click()
}

export async function loginOk(page: Page, u: { email: string; password: string }) {
  await login(page, u.email, u.password)
  await expect(page).toHaveURL(/\/dashboard/)
}

export async function logout(page: Page) {
  await page.getByRole('button', { name: 'Wyloguj' }).click()
  await expect(page).toHaveURL(/\/login/)
}

export async function postTransaction(
  page: Page, description: string, date: string,
  lines: { account: string; debit?: string; credit?: string }[],
) {
  await page.goto('/transactions/new')
  await page.getByLabel('Data').fill(date)
  await page.getByLabel('Opis').fill(description)
  while ((await page.locator('table.lines tbody tr').count()) < lines.length)
    await page.getByRole('button', { name: '+ Dodaj wiersz' }).click()
  for (const [i, l] of lines.entries()) {
    await page.getByLabel(`Konto w wierszu ${i + 1}`).selectOption({ label: l.account })
    if (l.debit) await page.getByLabel(`Winien w wierszu ${i + 1}`).fill(l.debit)
    if (l.credit) await page.getByLabel(`Ma w wierszu ${i + 1}`).fill(l.credit)
  }
  await expect(page.getByTestId('balance-status')).toContainText('zbilansowane')
  await page.getByRole('button', { name: 'Zaksięguj' }).click()
  await expect(page).toHaveURL(/\/transactions\/[0-9a-f-]{36}/)
}

/** Token JWT zalogowanego użytkownika (do testów bezpośrednio na API). */
export async function accessToken(page: Page) {
  return page.evaluate(() => localStorage.getItem('lc.access'))
}

/** Potwierdza okno dialogowe aplikacji (zamiennik natywnego `confirm()`); wywołaj po kliknięciu akcji. */
export async function confirmDialog(page: Page, label?: string) {
  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await (label ? dialog.getByRole('button', { name: label }) : dialog.getByRole('button').last()).click() // ostatni = akcja potwierdzająca
  await expect(dialog).toBeHidden()
}

/** Wpisuje wartość w oknie dialogowym z polem (zamiennik `prompt()`) i zatwierdza. */
export async function answerPrompt(page: Page, value: string) {
  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await dialog.locator('input').fill(value)
  await dialog.getByRole('button', { name: 'Zapisz' }).click()
  await expect(dialog).toBeHidden()
}
