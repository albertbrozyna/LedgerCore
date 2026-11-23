import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { ADMIN, VIEWER, loginOk } from './helpers'

// Plik jest przed 05 (zamknięcie roku): nie tworzy żadnych księgowań, tylko ogląda interfejs.

const PAGES = ['/dashboard', '/transactions', '/transactions/new', '/invoices', '/invoices/new?type=Sales', '/contractors', '/accounts', '/reports', '/admin', '/users', '/companies', '/bank-import', '/my-account']

async function noHorizontalScroll(page: Page) {
  return page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)
}

test.describe.serial('Design system', () => {
  test('menu jest pogrupowane, a Podgląd nie widzi grupy „Zarządzanie”', async ({ page }) => {
    await loginOk(page, ADMIN)
    const nav = page.getByRole('navigation', { name: 'Menu główne' })
    for (const g of ['Księga', 'Sprzedaż i zakupy', 'Zestawienia', 'Zarządzanie'])
      await expect(nav.getByRole('group', { name: g })).toBeVisible()
    await expect(nav.getByRole('group', { name: 'Zarządzanie' }).getByRole('link', { name: 'Administracja' })).toBeVisible()

    await page.getByRole('button', { name: 'Wyloguj' }).click()
    await loginOk(page, VIEWER)
    await expect(nav.getByRole('group', { name: 'Zarządzanie' })).toHaveCount(0)
    await expect(nav.getByRole('group', { name: 'Sprzedaż i zakupy' })).toBeVisible()
  })

  test('menu zwija się i stan jest pamiętany; w nagłówku widać firmę i stan okresu', async ({ page }) => {
    await loginOk(page, ADMIN)
    await expect(page.getByTestId('company-name')).toBeVisible()
    await expect(page.getByTestId('period-status')).toHaveText('Okres otwarty')

    await page.getByRole('button', { name: 'Zwiń menu' }).click()
    await expect(page.locator('aside.sidebar')).toHaveClass(/collapsed/)
    await page.reload()
    await expect(page.getByRole('button', { name: 'Rozwiń menu' })).toBeVisible()
    // zwinięte menu nadal ma dostępne nazwy linków
    await expect(page.getByRole('link', { name: 'Faktury' })).toBeVisible()
    await page.getByRole('button', { name: 'Rozwiń menu' }).click()
    await expect(page.locator('aside.sidebar')).not.toHaveClass(/collapsed/)
  })

  test('przełącznik motywu: auto → jasny → ciemny, zapamiętany po odświeżeniu', async ({ page }) => {
    await loginOk(page, ADMIN)
    const html = page.locator('html')
    await page.getByRole('button', { name: /Motyw/ }).click()
    await expect(html).toHaveAttribute('data-theme', 'light')
    await page.getByRole('button', { name: /Motyw/ }).click()
    await expect(html).toHaveAttribute('data-theme', 'dark')
    await page.reload()
    await expect(html).toHaveAttribute('data-theme', 'dark')
    await expect(page.getByRole('button', { name: 'Motyw: ciemny' })).toBeVisible()
    await page.getByRole('button', { name: /Motyw/ }).click()
    await expect(html).not.toHaveAttribute('data-theme', /.+/)
  })

  test('okno potwierdzenia: Esc i „Wróć” anulują, nic się nie dzieje', async ({ page }) => {
    await loginOk(page, ADMIN)
    await page.goto('/transactions')
    await page.getByRole('row').nth(1).getByRole('link').first().click()
    await expect(page.getByRole('heading', { name: /Księgowanie DK/ })).toBeVisible()

    await page.getByRole('button', { name: 'Storno' }).click()
    const dialog = page.getByRole('dialog', { name: /storno/i })
    await expect(dialog).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(dialog).toBeHidden()

    await page.getByRole('button', { name: 'Storno' }).click()
    await dialog.getByRole('button', { name: 'Wróć' }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByRole('alert')).toHaveCount(0)
    await expect(page).toHaveURL(/\/transactions\/[0-9a-f-]{36}$/)
  })

  for (const theme of ['light', 'dark'] as const) {
    test(`kontrast WCAG AA (axe) na stronach – motyw ${theme}`, async ({ page }) => {
      await page.addInitScript(t => localStorage.setItem('lc.theme', t), theme)
      await loginOk(page, ADMIN)
      for (const path of PAGES) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await expect(page.locator('html')).toHaveAttribute('data-theme', theme)
        const res = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze()
        expect(res.violations.map(v => `${path}: ${v.nodes.map(n => n.html.slice(0, 80)).join(' | ')}`), `kontrast na ${path}`).toEqual([])
      }
    })
  }

  for (const width of [360, 1920]) {
    test(`brak poziomego przewijania strony przy szerokości ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 })
      await loginOk(page, ADMIN)
      for (const path of PAGES) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        expect(await noHorizontalScroll(page), `przewijanie poziome na ${path}`).toBe(true)
      }
    })
  }
})
