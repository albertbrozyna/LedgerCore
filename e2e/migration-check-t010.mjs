// Ręczna weryfikacja migracji T-010: stara paczka tworzy faktury sprzedaży, nowy kod otwiera tę samą bazę.
// Faktury muszą zostać z numerami, wzór numeracji ma być domyślny, a kolejna faktura kontynuuje numerację.
// Użycie: node migration-check-t010.mjs <old.exe> <new.exe> <katalog-danych>
import { spawn } from 'node:child_process'
import { rmSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'

const [oldExe, newExe, dataDir] = process.argv.slice(2)
rmSync(dataDir, { recursive: true, force: true }); mkdirSync(dataDir, { recursive: true })
const base = 'http://localhost:5000/api/v1'
const sleep = ms => new Promise(r => setTimeout(r, ms))
const start = exe => spawn(exe, [], { cwd: dirname(exe), env: { ...process.env, LEDGERCORE_DATA_DIR: dataDir, ASPNETCORE_ENVIRONMENT: 'Production' }, stdio: 'ignore' })
async function waitUp() {
  for (let i = 0; i < 60; i++) { try { if ((await fetch(`${base}/setup/status`)).ok) return } catch { /* jeszcze nie działa */ } await sleep(500) }
  throw new Error('serwer nie wystartował')
}
async function call(method, path, body, s) {
  const h = { 'Content-Type': 'application/json' }
  if (s?.token) h.Authorization = `Bearer ${s.token}`
  if (s?.company) h['X-Company-Id'] = s.company
  const r = await fetch(base + path, { method, headers: h, body: body ? JSON.stringify(body) : undefined })
  const t = await r.text()
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status} ${t}`)
  return t ? JSON.parse(t) : null
}
async function login() {
  const r = await call('POST', '/auth/login', { email: 'a@b.pl', password: 'Haslo1234' })
  const companies = await call('GET', '/companies/mine', null, { token: r.accessToken })
  return { token: r.accessToken, company: companies[0].id }
}
const check = (name, cond, info = '') => { console.log(`${cond ? 'OK   ' : 'BŁĄD '} ${name} ${cond ? '' : info}`); if (!cond) process.exitCode = 1 }

let proc = start(oldExe); await waitUp()
await call('POST', '/setup', { firstName: 'A', lastName: 'B', email: 'a@b.pl', password: 'Haslo1234', companyName: 'Stara' })
let s = await login()
const c = await call('POST', '/contractors', { name: 'Klient' }, s)
const inv = d => call('POST', '/invoices', { type: 'Sales', contractorId: c.id, issueDate: d, dueDate: d, lines: [{ description: 'Usługa', net: 100, vatRate: 'R23' }] }, s)
const n1 = (await inv('2024-04-01')).number
const n2 = (await inv('2024-04-02')).number
await call('POST', '/invoices', { type: 'Purchase', number: 'ZAK/1', contractorId: c.id, issueDate: '2024-04-03', dueDate: '2024-04-03', lines: [{ description: 'Zakup', net: 50, vatRate: 'R23' }] }, s)
console.log('stara wersja:', n1, n2)
proc.kill(); await sleep(1500)

proc = start(newExe); await waitUp()
s = await login()
const list = await call('GET', '/invoices?pageSize=50&from=2024-01-01', null, s)
check('faktury zachowane', list.items.length === 3 && list.items.some(i => i.number === n1) && list.items.some(i => i.number === n2), JSON.stringify(list.items.map(i => i.number)))
const num = await call('GET', '/admin/numbering', null, s)
check('domyślny wzór numeracji', num.pattern === 'FV/{RRRR}/{NNNN}' && num.reset === 'Yearly', JSON.stringify(num))
const next = (await inv('2024-05-01')).number
check('numeracja kontynuowana', next === 'FV/2024/0003', next)
const tpl = await call('POST', '/recurring-invoices', { name: 'Abo', contractorId: c.id, frequency: 'Monthly', dayOfMonth: 1, dueDays: 7, nextDate: '2024-06-01', lines: [{ description: 'Abonament', net: 10, vatRate: 'R23' }] }, s)
const gen = await call('POST', '/recurring-invoices/generate', { asOf: '2024-07-15' }, s)
check('generowanie po migracji', gen.generated.length === 2 && gen.generated[0].number === 'FV/2024/0004', JSON.stringify(gen.generated))
proc.kill()
console.log(tpl ? 'koniec' : '')
