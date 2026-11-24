import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, type SearchResult } from './api'
import { useAuth } from './auth'
import { Button, Modal } from './design'
import { fmtMoney } from './format'
import { shortcutList } from './shortcuts'

interface Item { id: string; group: string; label: string; hint?: string; run: () => void }

const norm = (s: string) => s.toLowerCase().normalize('NFD').replace(/\p{Diacritic}/gu, '').replace(/ł/g, 'l')

/** Okno z listą skrótów klawiaturowych (otwierane klawiszem „?”). */
export function ShortcutHelp({ onClose }: { onClose: () => void }) {
  return (
    <Modal title="Skróty klawiaturowe" onClose={onClose} footer={<Button variant="primary" onClick={onClose}>Zamknij</Button>}>
      <table aria-label="Lista skrótów">
        <tbody>
          {shortcutList.map(s => <tr key={s.keys}><td><kbd>{s.keys}</kbd></td><td>{s.description}</td></tr>)}
        </tbody>
      </table>
      <p className="muted small">Skróty jednoklawiszowe nie działają podczas pisania w polach formularzy.</p>
    </Modal>
  )
}

/**
 * Paleta poleceń (Ctrl+K): wyniki wyszukiwania w bieżącej firmie (konta, kontrahenci, faktury, księgowania)
 * oraz polecenia nawigacyjne. Strzałki wybierają, Enter otwiera, Esc zamyka.
 */
export function CommandPalette({ onClose, onHelp }: { onClose: () => void; onHelp: () => void }) {
  const nav = useNavigate()
  const { canWrite, isAdmin, companies, company, switchCompany, fullBooks, mode } = useAuth()
  const [q, setQ] = useState('')
  const [result, setResult] = useState<{ q: string; data: SearchResult } | null>(null)
  const [error, setError] = useState('')
  const [active, setActive] = useState(0)

  const go = (path: string) => () => { onClose(); nav(path) }

  const commands: Item[] = useMemo(() => {
    const c: Item[] = []
    const add = (label: string, run: () => void, hint?: string) => c.push({ id: `cmd-${c.length}`, group: 'Polecenia', label, run, hint })
    add('Pulpit', go('/dashboard'))
    if (canWrite && fullBooks) {
      add('Nowe księgowanie', go('/transactions/new'), 'N')
    }
    if (canWrite) {
      add('Nowa faktura sprzedaży', go('/invoices/new?type=Sales'), 'F')
      add('Rejestruj fakturę zakupu', go('/invoices/new?type=Purchase'))
    }
    if (fullBooks) add('Dziennik księgowań', go('/transactions'))
    else add(mode === 'Kpir' ? 'Ewidencja KPiR' : 'Ewidencja ryczałtu', go('/register'))
    add('Faktury', go('/invoices'))
    add('Kontrahenci', go('/contractors'))
    add('Windykacja – faktury po terminie', go('/collections'))
    if (fullBooks) add('Plan kont', go('/accounts'))
    add('Raporty', go('/reports'))
    if (fullBooks) add('Import wyciągu bankowego', go('/bank-import'))
    add('Moje konto', go('/my-account'))
    if (isAdmin) { add('Firmy', go('/companies')); add('Użytkownicy', go('/users')); add('Administracja', go('/admin')) }
    for (const x of companies) if (x.id !== company?.id) add(`Przełącz firmę: ${x.name}`, () => { onClose(); switchCompany(x.id) })
    add('Pokaż skróty klawiaturowe', () => { onClose(); onHelp() }, '?')
    return c
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canWrite, isAdmin, companies, company?.id, fullBooks, mode])

  useEffect(() => {
    const text = q.trim()
    if (text.length < 2) { setResult(null); setError(''); return }
    let cancelled = false
    const timer = setTimeout(() => {
      api.search(text).then(r => { if (!cancelled) { setResult({ q: text, data: r }); setError('') } })
        .catch(e => { if (!cancelled) setError(e instanceof Error ? e.message : String(e)) })
    }, 150)
    return () => { cancelled = true; clearTimeout(timer) }
  }, [q])

  const items: Item[] = useMemo(() => {
    const list: Item[] = []
    const push = (group: string, id: string, label: string, path: string, hint?: string) =>
      list.push({ id: `res-${group}-${id}`, group, label, hint, run: go(path) })
    // wynik z innego zapytania nie może zostać „dopasowany” do nowego tekstu – dane są odświeżane z opóźnieniem
    if (result && result.q === q.trim()) {
      const r = result.data
      r.accounts.forEach(a => push('Konta', a.id, `${a.code} ${a.name}`, `/accounts/${a.id}/ledger`, 'karta konta'))
      r.contractors.forEach(c => push('Kontrahenci', c.id, c.name, `/invoices?contractorId=${c.id}`, c.nip ? `NIP ${c.nip}` : undefined))
      r.invoices.forEach(i => push('Faktury', i.id, i.number, `/invoices/${i.id}`, `${i.contractorName} · ${fmtMoney(i.gross)}${i.isCanceled ? ' · anulowana' : ''}`))
      r.transactions.forEach(t => push('Księgowania', t.id, `${t.number} ${t.description}`, `/transactions/${t.id}`))
    }
    const nq = norm(q.trim())
    const matching = nq ? commands.filter(c => norm(c.label).includes(nq)) : commands
    return [...list, ...matching]
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [result, q, commands])

  useEffect(() => { setActive(0) }, [q, result])

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive(a => Math.min(a + 1, items.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(a => Math.max(a - 1, 0)) }
    else if (e.key === 'Enter') { e.preventDefault(); items[active]?.run() }
  }

  const groups = [...new Set(items.map(i => i.group))]
  return (
    <Modal title="Wyszukiwarka i polecenia" onClose={onClose}>
      <input role="combobox" aria-label="Szukaj w programie" aria-expanded="true" aria-controls="palette-list" aria-autocomplete="list"
        aria-activedescendant={items[active]?.id} autoFocus autoComplete="off" placeholder="Numer faktury, kontrahent, konto, polecenie…"
        value={q} onChange={e => setQ(e.target.value)} onKeyDown={onKeyDown} />
      {error && <div className="alert alert-error" role="alert">{error}</div>}
      <div id="palette-list" role="listbox" aria-label="Wyniki" className="palette-list">
        {items.length === 0 && <p className="muted" role="presentation">Brak wyników.</p>}
        {groups.map(g => (
          <div key={g} role="group" aria-label={g}>
            <div className="palette-group" aria-hidden="true">{g}</div>
            {items.filter(i => i.group === g).map(i => (
              <div key={i.id} id={i.id} role="option" aria-selected={items[active]?.id === i.id}
                className={`palette-item${items[active]?.id === i.id ? ' active' : ''}`}
                onMouseMove={() => setActive(items.indexOf(i))} onClick={i.run}>
                <span>{i.label}</span>{i.hint && <span className="muted small">{i.hint}</span>}
              </div>
            ))}
          </div>
        ))}
      </div>
    </Modal>
  )
}
