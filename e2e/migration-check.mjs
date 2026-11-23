// Ręczna weryfikacja migracji T-017: stara paczka (dist\LedgerCore) tworzy dane, nowy kod otwiera tę samą bazę,
// a raporty muszą być identyczne. Użycie: node migration-check.mjs <old.exe> <new.exe> <katalog-danych>
import { spawn } from 'node:child_process'
import { rmSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'

const [oldExe, newExe, dataDir] = process.argv.slice(2)
rmSync(dataDir, { recursive: true, force: true }); mkdirSync(dataDir, { recursive: true })
const PORT = 5000 // aplikacja ma port wpisany na stałe
const base = `http://localhost:${PORT}/api/v1`
const sleep = ms => new Promise(r => setTimeout(r, ms))

function start(exe) {
  const p = spawn(exe, [], { cwd: dirname(exe), env: { ...process.env, LEDGERCORE_DATA_DIR: dataDir, ASPNETCORE_ENVIRONMENT: 'Production' }, stdio: 'ignore' })
  return p
}
async function waitUp() {
  for (let i = 0; i < 60; i++) { try { if ((await fetch(`${base}/setup/status`)).ok) return } catch { /* jeszcze nie działa */ } await sleep(500) }
  throw new Error('serwer nie wystartował')
}
async function call(method, path, body, token, company) {
  const h = { 'Content-Type': 'application/json' }
  if (token) h.Authorization = `Bearer ${token}`
  if (company) h['X-Company-Id'] = company
  const r = await fetch(base + path, { method, headers: h, body: body ? JSON.stringify(body) : undefined })
  const t = await r.text()
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status} ${t}`)
  return t ? JSON.parse(t) : null
}
async function login() {
  const r = await call('POST', '/auth/login', { email: 'a@b.pl', password: 'Haslo1234' })
  const companies = await call('GET', '/companies/mine', null, r.accessToken)
  return { token: r.accessToken, company: companies[0].id }
}
async function reports(s) {
  const get = p => call('GET', p, null, s.token, s.company)
  const strip = o => JSON.stringify(o, (k, v) => (k === 'id' || k === 'transactionId' || k === 'accountId' ? undefined : v))
  return {
    accounts: strip((await get('/accounts')).map(a => ({ c: a.code, d: a.debitTotal, k: a.creditTotal, b: a.balance }))),
    trial: strip((await get('/reports/trial-balance')).rows.map(r => [r.code, r.opening, r.debitTurnover, r.creditTurnover, r.closing])),
    pnl: strip(await get('/reports/profit-and-loss?from=2024-01-01&to=2024-12-31')),
    bs: strip(await get('/reports/balance-sheet?asOf=2025-12-31')),
    dash: strip((({ assets, liabilities, equity, monthIncome, monthExpenses, transactionCount }) => ({ assets, liabilities, equity, monthIncome, monthExpenses, transactionCount }))(await get('/reports/dashboard'))),
  }
}

// ---- stara wersja: dane ----
let proc = start(oldExe); await waitUp()
await call('POST', '/setup', { firstName: 'A', lastName: 'B', email: 'a@b.pl', password: 'Haslo1234', companyName: 'Stara' })
let s = await login()
const accs = await call('GET', '/accounts', null, s.token, s.company)
const id = c => accs.find(a => a.code === c).id
const post = (d, desc, dr, cr, amt) => call('POST', '/transactions', { description: desc, date: d, entries: [{ accountId: id(dr), amount: amt, side: 'Debit' }, { accountId: id(cr), amount: amt, side: 'Credit' }] }, s.token, s.company)
await post('2024-01-10', 'Kapitał', '130', '800', 10000.55)
await post('2024-02-11', 'Koszt', '402', '100', 1234.56)
await post('2024-03-12', 'Przychód', '130', '700', 99.99)
await post('2025-01-05', 'Koszt 2025', '401', '130', 0.07)
const c = await call('POST', '/contractors', { name: 'Klient' }, s.token, s.company)
await call('POST', '/invoices', { type: 'Sales', contractorId: c.id, issueDate: '2024-04-01', dueDate: '2024-04-15', lines: [{ description: 'Usługa', net: 1000.33, vatRate: 'R23' }] }, s.token, s.company)
await call('POST', '/admin/close-year', { year: 2024 }, s.token, s.company)
const before = await reports(s)
proc.kill(); await sleep(1500)

// ---- nowa wersja: ta sama baza (migracja) ----
proc = start(newExe); await waitUp()
s = await login()
const after = await reports(s)
proc.kill()

let ok = true
for (const k of Object.keys(before)) {
  const same = before[k] === after[k]
  ok &&= same
  console.log(`${same ? 'OK   ' : 'RÓŻNICA'} ${k}`)
  if (!same) console.log('  przed:', before[k], '\n  po:   ', after[k])
}
process.exit(ok ? 0 : 1)
