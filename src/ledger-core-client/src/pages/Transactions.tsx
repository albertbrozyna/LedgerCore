import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type TxItem } from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, yearStartIso } from '../format'
import { Badge, Button, DataTable, ErrorBox, Loading, Notice, PageHeader, PrintHeader, SavedViews, type Column, type SortState, useDialogs, useLoad } from '../design'

export function JournalPage() {
  const [from, setFrom] = useState(yearStartIso())
  const [to, setTo] = useState('')
  const [search, setSearch] = useState('')
  const [accountId, setAccountId] = useState('')
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortState | null>(null)
  const [exportError, setExportError] = useState('')
  const { user, canWrite } = useAuth()
  const accounts = useLoad(() => api.accounts(true), [])
  const { data, error, loading } = useLoad(
    () => api.transactions({ from, to, search, accountId, page, pageSize: 20, sort: sort?.key, dir: sort?.dir }), [from, to, search, accountId, page, sort])

  const columns: Column<TxItem>[] = [
    { key: 'number', header: 'Numer', sortable: true, render: t => <Link to={`/transactions/${t.id}`}>{t.number}</Link>, csv: t => t.number },
    { key: 'date', header: 'Data', sortable: true, render: t => fmtDate(t.date), csv: t => t.date.slice(0, 10) },
    { key: 'description', header: 'Opis', sortable: true, csv: t => t.description,
      render: t => <>{t.description}{t.isReversal && <Badge> storno</Badge>}{t.isReversed && <Badge tone="warn"> stornowane</Badge>}</> },
    { key: 'amount', header: 'Kwota', sortable: true, align: 'right', render: t => fmtMoney(t.amount), csv: t => t.amount },
    ...(canWrite ? [{ key: 'actions', header: 'Akcje', fixed: true, render: (t: TxItem) => <Link to={`/transactions/new?copy=${t.id}`}>Kopiuj jako nowe</Link> }] : []),
  ]

  return (
    <>
      <PageHeader title="Dziennik księgowań">
        <Button  onClick={() => window.print()}>Drukuj / PDF</Button>
        <Button  onClick={() => api.exportJournal(from, to).catch(e => setExportError(e.message))}>Eksport CSV</Button>
        <Link className="btn btn-primary" to="/transactions/new">Nowe księgowanie</Link>
      </PageHeader>
      <PrintHeader title="Dziennik księgowań" period={`${from} – ${to || 'dziś'}`} />
      <ErrorBox message={error || exportError} />
      <SavedViews storageKey={`journal.${user?.id}`} state={{ from, to, search, accountId }}
        onApply={v => { setFrom(v.from); setTo(v.to); setSearch(v.search); setAccountId(v.accountId); setPage(1) }} />
      <div className="card form-row no-print">
        <label>Od<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1) }} /></label>
        <label>Do<input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1) }} /></label>
        <label>Konto
          <select value={accountId} onChange={e => { setAccountId(e.target.value); setPage(1) }}>
            <option value="">Wszystkie</option>
            {accounts.data?.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}
          </select>
        </label>
        <label className="grow">Szukaj<input placeholder="opis lub numer" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
      </div>
      {loading && !data && <Loading />}
      {data && (
        <DataTable label="Dziennik księgowań" columns={columns} rows={data.items} rowKey={t => t.id} loading={loading}
          empty="Brak księgowań dla wybranych kryteriów." sort={sort} onSort={s => { setSort(s); setPage(1) }} viewKey="journal" csvName="dziennik-widok"
          selectable={{ label: t => `Zaznacz księgowanie ${t.number}` }}
          pager={{ page, pageSize: data.pageSize, total: data.totalCount, onPage: setPage, unit: 'księgowań' }} />
      )}
    </>
  )
}

export function TransactionPage() {
  const { confirm } = useDialogs()
  const { id } = useParams()
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const { data, error, loading } = useLoad(() => api.transaction(id!), [id])
  const [actionError, setActionError] = useState('')
  const [notice, setNotice] = useState('')

  async function reverse() {
    if (!await confirm({ title: 'Utworzyć storno tego księgowania?', message: 'Zapis oryginalny pozostanie w dzienniku.', confirmLabel: 'Utwórz storno' })) return
    setActionError('')
    try {
      const r = await api.reverseTransaction(id!)
      setNotice(`Utworzono storno ${r.number}`)
      nav(`/transactions/${r.transactionId}`)
    } catch (e) { setActionError((e as Error).message) }
  }

  return (
    <>
      <PageHeader title={data ? `Księgowanie ${data.number}` : 'Księgowanie'}>
        <Link className="btn" to="/transactions">← Dziennik</Link>
        {canWrite && data && !data.reversesTransactionId && !data.reversedByTransactionId &&
          <Button variant="danger"  onClick={reverse}>Storno</Button>}
        {canWrite && data && <Button onClick={() => nav(`/transactions/new?copy=${data.id}`)}>Kopiuj jako nowe</Button>}
      </PageHeader>
      <ErrorBox message={error || actionError} />
      <Notice message={notice} />
      {loading && !data && <Loading />}
      {data && (
        <div className="card">
          <dl className="meta">
            <dt>Data</dt><dd>{fmtDate(data.date)}</dd>
            <dt>Opis</dt><dd>{data.description}</dd>
            {data.reversesTransactionId && <><dt>Storno dokumentu</dt><dd><Link to={`/transactions/${data.reversesTransactionId}`}>{data.reversesNumber}</Link></dd></>}
            {data.reversedByTransactionId && <><dt>Stornowane przez</dt><dd><Link to={`/transactions/${data.reversedByTransactionId}`}>{data.reversedByNumber}</Link></dd></>}
          </dl>
          <table>
            <thead><tr><th>Konto</th><th className="num">Winien</th><th className="num">Ma</th></tr></thead>
            <tbody>
              {data.entries.map((e, i) => (
                <tr key={i}>
                  <td><Link to={`/accounts/${e.accountId}/ledger`}>{e.accountCode}</Link> {e.accountName}</td>
                  <td className="num">{e.side === 'Debit' ? fmtMoney(e.amount) : ''}</td>
                  <td className="num">{e.side === 'Credit' ? fmtMoney(e.amount) : ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
