import { defineConfig } from '@playwright/test'
import * as path from 'node:path'

const PORT = 5199
const dataDir = path.join(__dirname, '.e2e-data') // czyszczony przy każdym starcie serwera

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1, // scenariusze współdzielą jedną bazę i są uruchamiane po kolei
  timeout: 30_000,
  expect: { timeout: 7_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${PORT}`,
    // Używa zainstalowanej Microsoft Edge – nie trzeba pobierać osobnej przeglądarki.
    // Zmień na 'chromium' po `npx playwright install chromium`, jeśli Edge nie jest dostępny.
    channel: process.env.E2E_BROWSER_CHANNEL ?? 'msedge',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'pl-PL',
  },
  webServer: {
    command: 'node prepare-server.mjs .e2e-data && dotnet run --project ../src/LedgerCore.Api -c Release --no-launch-profile',
    url: `http://localhost:${PORT}/api/v1/setup/status`,
    timeout: 180_000,
    reuseExistingServer: false,
    env: { ASPNETCORE_URLS: `http://localhost:${PORT}`, LEDGERCORE_DATA_DIR: dataDir, Backup__StartupDelaySeconds: '3' },
  },
})
