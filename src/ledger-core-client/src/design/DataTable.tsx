import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Button } from './controls'
import { ErrorBox } from './display'

export type SortDir = 'asc' | 'desc'
export interface SortState { key: string; dir: SortDir }

export interface Column<T> {
  key: string
  header: string
  render: (row: T) => ReactNode
  /** Kolumna sortowalna. Sortowanie po stronie serwera, gdy podano `onSort`; w przeciwnym razie – lokalnie wg `sortValue`. */
  sortable?: boolean
  sortValue?: (row: T) => string | number
  /** Tekst do filtra kolumnowego (tylko tryb lokalny). Gdy podany, pod nagłówkiem pojawia się pole filtra. */
  filterText?: (row: T) => string
  /** Wartość do eksportu CSV (domyślnie `sortValue`/`filterText`). Kolumny bez wartości (np. „Akcje”) nie są eksportowane. */
  csv?: (row: T) => string | number
  align?: 'right'
  hiddenByDefault?: boolean
  /** Nie można ukryć (np. kolumna z akcjami). */
  fixed?: boolean
}

export interface Footer {
  label: ReactNode
  /** Komórki sum wg klucza kolumny; etykieta rozciąga się do pierwszej z nich. */
  cells: Record<string, ReactNode>
}

export interface Pager {
  page: number
  pageSize: number
  total: number
  onPage: (page: number) => void
  unit: string // np. „księgowań”
}

interface Props<T> {
  /** Nazwa tabeli (aria-label) – też kontrakt z testami. */
  label: string
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  rowClass?: (row: T) => string | undefined
  empty?: ReactNode
  error?: string
  loading?: boolean
  sort?: SortState | null
  onSort?: (s: SortState | null) => void
  pager?: Pager
  footer?: Footer
  /** Zapamiętuje widok (kolumny, gęstość, sortowanie lokalne) w localStorage pod tym kluczem. */
  viewKey?: string
  /** Pokazuje przycisk „Eksport widoku” (nazwa pliku bez rozszerzenia). */
  csvName?: string
  /** Pokazuje checkboxy wierszy; etykieta wiersza dla czytników ekranu. */
  selectable?: { label: (row: T) => string; bulk?: (selected: T[]) => ReactNode }
}

interface StoredView { hidden: string[]; compact: boolean; sort?: SortState | null }

function loadView(key?: string): StoredView | null {
  if (!key) return null
  try { return JSON.parse(localStorage.getItem(`lc.table.${key}`) ?? 'null') } catch { return null }
}

function csvCell(v: string | number) {
  let s = String(v)
  if (s.length > 0 && '=+-@'.includes(s[0]) && typeof v === 'string') s = `'${s}` // ochrona przed formułami w Excelu
  return `"${s.replace(/"/g, '""')}"`
}

/** Pobiera plik CSV (separator „;”, BOM – poprawnie otwiera się w polskim Excelu). */
export function downloadCsvFile(name: string, header: string[], rows: (string | number)[][]) {
  const text = [header, ...rows].map(r => r.map(csvCell).join(';')).join('\r\n')
  const blob = new Blob(['﻿' + text], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = `${name}.csv`; a.click()
  URL.revokeObjectURL(url)
}

/**
 * Tabela danych: sortowanie (serwerowe lub lokalne), filtry kolumnowe, wybór kolumn, gęstość, stopka z sumami,
 * stan pusty i błędu, zaznaczanie wierszy z akcjami zbiorczymi, stronicowanie, eksport widoku do CSV.
 * Nagłówki sortowania to przyciski (Enter/Spacja) z `aria-sort`; tabela nie jest odmontowywana przy odświeżaniu,
 * więc fokus zostaje na klikniętym nagłówku.
 */
export function DataTable<T>({ label, columns, rows, rowKey, rowClass, empty, error, loading, sort: sortProp, onSort, pager, footer, viewKey, csvName, selectable }: Props<T>) {
  const stored = useMemo(() => loadView(viewKey), [viewKey])
  const [hidden, setHidden] = useState<Set<string>>(() => new Set(stored?.hidden ?? columns.filter(c => c.hiddenByDefault).map(c => c.key)))
  const [compact, setCompact] = useState(stored?.compact ?? false)
  const [localSort, setLocalSort] = useState<SortState | null>(stored?.sort ?? null)
  const [filters, setFilters] = useState<Record<string, string>>({})
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [columnsOpen, setColumnsOpen] = useState(false)

  const serverSide = !!onSort
  const sort = serverSide ? sortProp ?? null : localSort

  useEffect(() => {
    if (!viewKey) return
    try { localStorage.setItem(`lc.table.${viewKey}`, JSON.stringify({ hidden: [...hidden], compact, sort: serverSide ? undefined : localSort } satisfies StoredView)) } catch { /* tryb prywatny */ }
  }, [viewKey, hidden, compact, localSort, serverSide])

  const visible = columns.filter(c => !hidden.has(c.key))

  const shown = useMemo(() => {
    let r = rows
    if (!serverSide) {
      for (const c of columns) {
        const f = filters[c.key]?.trim().toLowerCase()
        if (f && c.filterText) r = r.filter(x => c.filterText!(x).toLowerCase().includes(f))
      }
      const col = sort && columns.find(c => c.key === sort.key)
      if (col?.sortValue) {
        const v = col.sortValue
        const dir = sort!.dir === 'asc' ? 1 : -1
        r = [...r].sort((a, b) => {
          const x = v(a), y = v(b)
          return (typeof x === 'string' && typeof y === 'string' ? x.localeCompare(y, 'pl') : x < y ? -1 : x > y ? 1 : 0) * dir
        })
      }
    }
    return r
  }, [rows, columns, filters, sort, serverSide])

  function toggleSort(key: string) {
    const next: SortState | null = !sort || sort.key !== key ? { key, dir: 'asc' } : sort.dir === 'asc' ? { key, dir: 'desc' } : null
    if (serverSide) onSort!(next); else setLocalSort(next)
  }

  function toggleColumn(key: string) {
    setHidden(h => { const n = new Set(h); if (n.has(key)) n.delete(key); else n.add(key); return n })
  }

  const exportable = visible.filter(c => c.csv || c.sortValue || c.filterText)
  const cellValue = (c: Column<T>, r: T) => (c.csv ?? c.sortValue ?? ((x: T) => c.filterText!(x)))(r)
  function exportView(only?: T[]) {
    downloadCsvFile(csvName ?? 'widok', exportable.map(c => c.header), (only ?? shown).map(r => exportable.map(c => cellValue(c, r))))
  }

  const selectedRows = shown.filter(r => selected.has(rowKey(r)))
  const allSelected = shown.length > 0 && selectedRows.length === shown.length
  const hasFilters = !serverSide && columns.some(c => c.filterText)
  const pages = pager ? Math.max(1, Math.ceil(pager.total / pager.pageSize)) : 1
  const colCount = visible.length + (selectable ? 1 : 0)

  let footerSpan = 0, footerCells: Column<T>[] = []
  if (footer) {
    const first = visible.findIndex(c => c.key in footer.cells)
    footerSpan = (first < 0 ? visible.length : first) + (selectable ? 1 : 0)
    footerCells = first < 0 ? [] : visible.slice(first)
  }

  return (
    <div className="datatable">
      <div className="datatable-tools no-print">
        {selectedRows.length > 0 && selectable && (
          <span className="datatable-bulk" role="group" aria-label="Akcje dla zaznaczonych">
            <span>Zaznaczono: {selectedRows.length}</span>
            {csvName && <Button small onClick={() => exportView(selectedRows)}>Eksport zaznaczonych</Button>}
            {selectable.bulk?.(selectedRows)}
            <Button small variant="link" onClick={() => setSelected(new Set())}>Wyczyść zaznaczenie</Button>
          </span>
        )}
        <span className="grow-space" />
        {csvName && <Button small onClick={() => exportView()}>Eksport widoku</Button>}
        <Button small aria-pressed={compact} onClick={() => setCompact(c => !c)}>Gęstość: {compact ? 'kompaktowa' : 'standardowa'}</Button>
        <details className="datatable-columns" onToggle={e => setColumnsOpen(e.currentTarget.open)}>
          <summary>Kolumny</summary>
          {columnsOpen && <div className="datatable-columns-menu" role="group" aria-label="Widoczne kolumny">
            {columns.filter(c => !c.fixed).map(c => (
              // menu jest renderowane dopiero po otwarciu, by „Pokaż kolumnę Status” nie kolidowało z etykietą pola „Status” (getByLabel)
              <div key={c.key} className="column-toggle">
                <input type="checkbox" aria-label={`Pokaż kolumnę ${c.header}`} checked={!hidden.has(c.key)} onChange={() => toggleColumn(c.key)} />
                <span aria-hidden="true" onClick={() => toggleColumn(c.key)}>{c.header}</span>
              </div>
            ))}
          </div>}
        </details>
      </div>
      <ErrorBox message={error} />
      <div className="table-scroll">
        <table aria-label={label} aria-busy={loading || undefined} className={compact ? 'table-compact' : undefined}>
          <thead>
            <tr>
              {selectable && (
                <th className="select-col">
                  <input type="checkbox" aria-label="Zaznacz wszystkie wiersze" checked={allSelected}
                    onChange={() => setSelected(allSelected ? new Set() : new Set(shown.map(rowKey)))} />
                </th>
              )}
              {visible.map(c => {
                const active = sort?.key === c.key ? sort : null
                return (
                  <th key={c.key} className={c.align === 'right' ? 'num' : undefined}
                    aria-sort={c.sortable ? (active ? (active.dir === 'asc' ? 'ascending' : 'descending') : 'none') : undefined}>
                    {c.sortable
                      ? <button type="button" className="th-sort" onClick={() => toggleSort(c.key)}>{c.header}<span aria-hidden="true">{active ? (active.dir === 'asc' ? ' ▲' : ' ▼') : ''}</span></button>
                      : c.header}
                  </th>
                )
              })}
            </tr>
            {hasFilters && (
              <tr className="filter-row no-print">
                {selectable && <th />}
                {visible.map(c => (
                  <th key={c.key}>
                    {c.filterText && <input aria-label={`Filtr: ${c.header}`} placeholder="filtr" value={filters[c.key] ?? ''}
                      onChange={e => setFilters(f => ({ ...f, [c.key]: e.target.value }))} />}
                  </th>
                ))}
              </tr>
            )}
          </thead>
          <tbody>
            {shown.length === 0 && (
              <tr className="empty-row"><td colSpan={colCount}>{empty ?? 'Brak danych.'}</td></tr>
            )}
            {shown.map(r => (
              <tr key={rowKey(r)} className={rowClass?.(r)} aria-selected={selectable ? selected.has(rowKey(r)) : undefined}>
                {selectable && (
                  <td className="select-col">
                    <input type="checkbox" aria-label={selectable.label(r)} checked={selected.has(rowKey(r))}
                      onChange={() => setSelected(s => { const n = new Set(s); const k = rowKey(r); if (n.has(k)) n.delete(k); else n.add(k); return n })} />
                  </td>
                )}
                {visible.map(c => <td key={c.key} className={c.align === 'right' ? 'num' : undefined}>{c.render(r)}</td>)}
              </tr>
            ))}
          </tbody>
          {footer && (
            <tfoot>
              <tr>
                <td colSpan={Math.max(footerSpan, 1)}>{footer.label}</td>
                {footerCells.map(c => <td key={c.key} className={c.align === 'right' ? 'num' : undefined}>{footer.cells[c.key]}</td>)}
              </tr>
            </tfoot>
          )}
        </table>
      </div>
      {pager && (
        <div className="pager no-print">
          <Button small disabled={pager.page <= 1} onClick={() => pager.onPage(pager.page - 1)}>‹ Poprzednia</Button>
          <span>Strona {pager.page} z {pages} ({pager.total} {pager.unit})</span>
          <Button small disabled={pager.page >= pages} onClick={() => pager.onPage(pager.page + 1)}>Następna ›</Button>
        </div>
      )}
    </div>
  )
}
