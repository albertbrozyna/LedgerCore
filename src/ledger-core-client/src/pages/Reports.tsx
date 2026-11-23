import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, apiX, apiY, type InvoiceType } from '../api'
import { downloadCsv } from '../print'
import { accountTypeLabel, fmtDate, fmtMoney, fmtMoneyOrDash, invoiceTypeLabel, monthStartIso, todayIso, vatRateLabel, yearStartIso } from '../format'
import { ErrorBox, Loading, PageHeader, PrintHeader, useLoad } from '../ui'

export function LedgerPage() {
  const { id } = useParams()
  const [from, setFrom] = useState(yearStartIso())
  const [to, setTo] = useState('')
  const { data, error, loading } = useLoad(() => api.ledger(id!, from, to), [id, from, to])

  return (
    <>
      <PageHeader title={data ? `Karta konta ${data.code} ${data.name}` : 'Karta konta'}>
        <Link className="btn" to="/accounts">← Plan kont</Link>
      </PageHeader>
      <ErrorBox message={error} />
      <div className="card form-row">
        <label>Od<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label>Do<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
      </div>
      {loading && !data && <Loading />}
      {data && (
        <table>
          <thead><tr><th>Data</th><th>Dokument</th><th>Opis</th><th className="num">Winien</th><th className="num">Ma</th><th className="num">Saldo</th></tr></thead>
          <tbody>
            <tr className="muted-row"><td colSpan={5}>Saldo początkowe</td><td className="num">{fmtMoney(data.openingBalance)}</td></tr>
            {data.lines.map((l, i) => (
              <tr key={i}>
                <td>{fmtDate(l.date)}</td>
                <td><Link to={`/transactions/${l.transactionId}`}>{l.number}</Link></td>
                <td>{l.description}</td>
                <td className="num">{fmtMoneyOrDash(l.debit)}</td>
                <td className="num">{fmtMoneyOrDash(l.credit)}</td>
                <td className="num">{fmtMoney(l.balance)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr><td colSpan={3}>Razem / saldo końcowe</td>
              <td className="num">{fmtMoney(data.totalDebit)}</td><td className="num">{fmtMoney(data.totalCredit)}</td>
              <td className="num" data-testid="ledger-closing">{fmtMoney(data.closingBalance)}</td></tr>
          </tfoot>
        </table>
      )}
    </>
  )
}

const tabTitle = { tb: 'Zestawienie obrotów i sald', pnl: 'Rachunek wyników', bs: 'Bilans', vat: 'Rejestry VAT', settle: 'Rozrachunki' } as const

export function ReportsPage() {
  const [tab, setTab] = useState<'tb' | 'pnl' | 'bs' | 'vat' | 'settle'>('tb')
  const [from, setFrom] = useState(yearStartIso())
  const [to, setTo] = useState(todayIso())

  return (
    <>
      <PageHeader title="Raporty"><button className="btn" onClick={() => window.print()}>Drukuj / PDF</button></PageHeader>
      <PrintHeader title={tabTitle[tab]} period={tab === 'bs' ? `na dzień ${to}` : tab === 'settle' ? undefined : `${from} – ${to}`} />
      <div className="tabs no-print" role="tablist">
        <button role="tab" aria-selected={tab === 'tb'} className={tab === 'tb' ? 'active' : ''} onClick={() => setTab('tb')}>Zestawienie obrotów i sald</button>
        <button role="tab" aria-selected={tab === 'pnl'} className={tab === 'pnl' ? 'active' : ''} onClick={() => setTab('pnl')}>Rachunek wyników</button>
        <button role="tab" aria-selected={tab === 'bs'} className={tab === 'bs' ? 'active' : ''} onClick={() => setTab('bs')}>Bilans</button>
        <button role="tab" aria-selected={tab === 'vat'} className={tab === 'vat' ? 'active' : ''} onClick={() => setTab('vat')}>Rejestry VAT</button>
        <button role="tab" aria-selected={tab === 'settle'} className={tab === 'settle' ? 'active' : ''} onClick={() => setTab('settle')}>Rozrachunki</button>
      </div>
      <div className="card form-row">
        <label>Od<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label>Do<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
        <button className="btn btn-small" onClick={() => { setFrom(monthStartIso()); setTo(todayIso()) }}>Ten miesiąc</button>
        <button className="btn btn-small" onClick={() => { setFrom(yearStartIso()); setTo(todayIso()) }}>Ten rok</button>
      </div>
      {tab === 'tb' && <TrialBalance from={from} to={to} />}
      {tab === 'pnl' && <Pnl from={from} to={to} />}
      {tab === 'bs' && <Balance asOf={to} />}
      {tab === 'vat' && <Vat from={from} to={to} />}
      {tab === 'settle' && <SettlementsReport />}
    </>
  )
}

function TrialBalance({ from, to }: { from: string; to: string }) {
  const { data, error, loading } = useLoad(() => api.trialBalance(from, to), [from, to])
  return (
    <>
      <ErrorBox message={error} />
      {data && data.rows.length > 0 && <div className="no-print"><button className="btn btn-small" onClick={() => downloadCsv('obroty-i-salda.csv',
        ['Konto', 'Nazwa', 'BO Wn', 'BO Ma', 'Obroty Wn', 'Obroty Ma', 'BZ Wn', 'BZ Ma'],
        data.rows.map(r => [r.code, r.name, Math.max(r.opening, 0), Math.max(-r.opening, 0), r.debitTurnover, r.creditTurnover, Math.max(r.closing, 0), Math.max(-r.closing, 0)]))}>Eksport CSV</button></div>}
      {loading && !data && <Loading />}
      {data && (data.rows.length === 0 ? <p className="muted">Brak obrotów w wybranym okresie.</p> : (
        <table>
          <thead>
            <tr><th rowSpan={2}>Konto</th><th colSpan={2} className="num">Saldo początkowe</th><th colSpan={2} className="num">Obroty</th><th colSpan={2} className="num">Saldo końcowe</th></tr>
            <tr><th className="num">Wn</th><th className="num">Ma</th><th className="num">Wn</th><th className="num">Ma</th><th className="num">Wn</th><th className="num">Ma</th></tr>
          </thead>
          <tbody>
            {data.rows.map(r => (
              <tr key={r.accountId} data-testid={`tb-${r.code}`}>
                <td><Link to={`/accounts/${r.accountId}/ledger`}>{r.code}</Link> {r.name} <span className="muted small">({accountTypeLabel[r.type]})</span></td>
                <td className="num">{fmtMoneyOrDash(Math.max(r.opening, 0))}</td><td className="num">{fmtMoneyOrDash(Math.max(-r.opening, 0))}</td>
                <td className="num">{fmtMoneyOrDash(r.debitTurnover)}</td><td className="num">{fmtMoneyOrDash(r.creditTurnover)}</td>
                <td className="num">{fmtMoneyOrDash(Math.max(r.closing, 0))}</td><td className="num">{fmtMoneyOrDash(Math.max(-r.closing, 0))}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr><td>Razem obroty</td><td></td><td></td>
              <td className="num" data-testid="tb-total-debit">{fmtMoney(data.totalDebitTurnover)}</td>
              <td className="num" data-testid="tb-total-credit">{fmtMoney(data.totalCreditTurnover)}</td><td></td><td></td></tr>
          </tfoot>
        </table>
      ))}
    </>
  )
}

function Pnl({ from, to }: { from: string; to: string }) {
  const { data, error, loading } = useLoad(() => api.profitAndLoss(from, to), [from, to])
  return (
    <>
      <ErrorBox message={error} />
      {data && <div className="no-print"><button className="btn btn-small" onClick={() => downloadCsv('rachunek-wynikow.csv', ['Pozycja', 'Konto', 'Nazwa', 'Kwota'],
        [...data.income.map(r => ['Przychody', r.code, r.name, r.amount] as (string | number)[]), ['Przychody', '', 'RAZEM', data.totalIncome],
         ...data.expenses.map(r => ['Koszty', r.code, r.name, r.amount] as (string | number)[]), ['Koszty', '', 'RAZEM', data.totalExpenses], ['Wynik', '', 'WYNIK FINANSOWY', data.result]])}>Eksport CSV</button></div>}
      {loading && !data && <Loading />}
      {data && (
        <table>
          <tbody>
            <tr className="section"><td colSpan={2}>Przychody</td></tr>
            {data.income.map(r => <tr key={r.accountId}><td>{r.code} {r.name}</td><td className="num">{fmtMoney(r.amount)}</td></tr>)}
            <tr className="total"><td>Razem przychody</td><td className="num" data-testid="pnl-income">{fmtMoney(data.totalIncome)}</td></tr>
            <tr className="section"><td colSpan={2}>Koszty</td></tr>
            {data.expenses.map(r => <tr key={r.accountId}><td>{r.code} {r.name}</td><td className="num">{fmtMoney(r.amount)}</td></tr>)}
            <tr className="total"><td>Razem koszty</td><td className="num" data-testid="pnl-expenses">{fmtMoney(data.totalExpenses)}</td></tr>
            <tr className="total"><td>Wynik finansowy</td><td className="num" data-testid="pnl-result">{fmtMoney(data.result)}</td></tr>
          </tbody>
        </table>
      )}
    </>
  )
}

function Vat({ from, to }: { from: string; to: string }) {
  const [type, setType] = useState<InvoiceType>('Sales')
  const summary = useLoad(() => apiX.vatSummary(from, to), [from, to])
  const reg = useLoad(() => apiX.vatRegister(type, from, to), [type, from, to])
  const s = summary.data
  return (
    <>
      <ErrorBox message={summary.error || reg.error} />
      {reg.data && reg.data.rows.length > 0 && <div className="no-print"><button className="btn btn-small" onClick={() => downloadCsv(type === 'Sales' ? 'rejestr-vat-sprzedazy.csv' : 'rejestr-vat-zakupow.csv',
        ['Numer', 'Data wystawienia', 'Data sprzedazy', 'Kontrahent', 'NIP', 'Netto', 'VAT', 'Brutto'],
        reg.data!.rows.map(r => [r.number, r.issueDate.slice(0, 10), r.saleDate.slice(0, 10), r.contractorName, r.nip ?? '', r.net, r.vat, r.gross]))}>Eksport rejestru CSV</button></div>}
      {s && (
        <div className="tiles">
          <div className="tile"><div className="tile-label">VAT należny (sprzedaż)</div><div className="tile-value" data-testid="vat-output">{fmtMoney(s.outputVat)}</div></div>
          <div className="tile"><div className="tile-label">VAT naliczony (zakupy)</div><div className="tile-value" data-testid="vat-input">{fmtMoney(s.inputVat)}</div></div>
          <div className={`tile ${s.vatDue > 0 ? 'neg' : 'pos'}`}>
            <div className="tile-label">{s.vatDue >= 0 ? 'VAT do zapłaty' : 'VAT do przeniesienia / zwrotu'}</div>
            <div className="tile-value" data-testid="vat-due">{fmtMoney(Math.abs(s.vatDue))}</div>
          </div>
        </div>
      )}
      <h2>Rejestr VAT</h2>
      <div className="tabs" role="tablist" aria-label="Rodzaj rejestru">
        {(['Sales', 'Purchase'] as InvoiceType[]).map(t => (
          <button key={t} role="tab" aria-selected={type === t} className={type === t ? 'active' : ''} onClick={() => setType(t)}>
            Rejestr {t === 'Sales' ? 'sprzedaży' : 'zakupów'}
          </button>
        ))}
      </div>
      {reg.loading && !reg.data && <Loading />}
      {reg.data && (reg.data.rows.length === 0 ? <p className="muted">Brak faktur ({invoiceTypeLabel[type].toLowerCase()}) w wybranym okresie.</p> : (
        <>
          <table>
            <thead><tr><th>Numer</th><th>Data</th><th>Kontrahent</th><th>NIP</th><th className="num">Netto</th><th className="num">VAT</th><th className="num">Brutto</th></tr></thead>
            <tbody>
              {reg.data.rows.map(r => (
                <tr key={r.invoiceId}><td><Link to={`/invoices/${r.invoiceId}`}>{r.number}</Link></td><td>{fmtDate(r.issueDate)}</td>
                  <td>{r.contractorName}</td><td>{r.nip ?? ''}</td><td className="num">{fmtMoney(r.net)}</td>
                  <td className="num">{fmtMoney(r.vat)}</td><td className="num">{fmtMoney(r.gross)}</td></tr>
              ))}
            </tbody>
            <tfoot><tr><td colSpan={4}>Razem</td><td className="num">{fmtMoney(reg.data.totalNet)}</td>
              <td className="num" data-testid="vatreg-total-vat">{fmtMoney(reg.data.totalVat)}</td><td className="num">{fmtMoney(reg.data.totalGross)}</td></tr></tfoot>
          </table>
          <table>
            <thead><tr><th>Stawka</th><th className="num">Netto</th><th className="num">VAT</th></tr></thead>
            <tbody>
              {reg.data.byRate.map(b => <tr key={b.rate}><td>{vatRateLabel[b.rate]}</td><td className="num">{fmtMoney(b.net)}</td><td className="num">{fmtMoney(b.vat)}</td></tr>)}
            </tbody>
          </table>
        </>
      ))}
    </>
  )
}

function SettlementsReport() {
  const [type, setType] = useState<InvoiceType>('Sales')
  const { data, error, loading } = useLoad(() => apiX.settlements(type), [type])
  return (
    <>
      <ErrorBox message={error} />
      {data && data.rows.length > 0 && <div className="no-print"><button className="btn btn-small" onClick={() => downloadCsv(type === 'Sales' ? 'naleznosci.csv' : 'zobowiazania.csv',
        ['Kontrahent', 'NIP', 'Razem', 'W terminie', '1-30 dni', '31-60 dni', '>60 dni'],
        data.rows.map(r => [r.contractorName, r.nip ?? '', r.total, r.notDue, r.days1To30, r.days31To60, r.over60]))}>Eksport CSV</button></div>}
      <div className="tabs" role="tablist" aria-label="Rodzaj rozrachunków">
        <button role="tab" aria-selected={type === 'Sales'} className={type === 'Sales' ? 'active' : ''} onClick={() => setType('Sales')}>Należności</button>
        <button role="tab" aria-selected={type === 'Purchase'} className={type === 'Purchase' ? 'active' : ''} onClick={() => setType('Purchase')}>Zobowiązania</button>
      </div>
      {loading && !data && <Loading />}
      {data && (data.rows.length === 0 ? <p className="muted">Brak nieuregulowanych dokumentów.</p> : (
        <table>
          <thead><tr><th>Kontrahent</th><th className="num">Razem</th><th className="num">W terminie</th><th className="num">1–30 dni</th><th className="num">31–60 dni</th><th className="num">&gt; 60 dni</th></tr></thead>
          <tbody>
            {data.rows.map(r => (
              <tr key={r.contractorId}>
                <td><Link to={`/invoices?contractorId=${r.contractorId}`}>{r.contractorName}</Link> <span className="muted small">({r.invoiceCount})</span></td>
                <td className="num">{fmtMoney(r.total)}</td><td className="num">{fmtMoneyOrDash(r.notDue)}</td>
                <td className="num">{fmtMoneyOrDash(r.days1To30)}</td><td className="num">{fmtMoneyOrDash(r.days31To60)}</td><td className="num">{fmtMoneyOrDash(r.over60)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot><tr><td>Razem</td><td className="num" data-testid="settle-total">{fmtMoney(data.total)}</td>
            <td></td><td colSpan={3} className="num">w tym po terminie: <span data-testid="settle-overdue">{fmtMoney(data.overdue)}</span></td></tr></tfoot>
        </table>
      ))}
    </>
  )
}

function Balance({ asOf }: { asOf: string }) {
  const { data, error, loading } = useLoad(() => apiY.balanceSheet(asOf), [asOf])
  const section = (title: string, rows: { accountId: string; code: string; name: string; amount: number }[]) => (
    <>
      <tr className="section"><td colSpan={2}>{title}</td></tr>
      {rows.map(r => <tr key={r.accountId}><td>{r.code} {r.name}</td><td className="num">{fmtMoney(r.amount)}</td></tr>)}
    </>
  )
  return (
    <>
      <ErrorBox message={error} />
      {loading && !data && <Loading />}
      {data && (
        <>
          <div className="no-print"><button className="btn btn-small" onClick={() => downloadCsv('bilans.csv', ['Strona', 'Konto', 'Nazwa', 'Kwota'], [
            ...data.assets.map(r => ['Aktywa', r.code, r.name, r.amount] as (string | number)[]),
            ...data.liabilities.map(r => ['Zobowiazania', r.code, r.name, r.amount] as (string | number)[]),
            ...data.equity.map(r => ['Kapital', r.code, r.name, r.amount] as (string | number)[]),
            ['Kapital', '', 'Wynik biezacy', data.currentResult]])}>Eksport CSV</button></div>
          <div className="balance-grid">
            <table aria-label="Aktywa"><tbody>
              {section('AKTYWA', data.assets)}
              <tr className="total"><td>Razem aktywa</td><td className="num" data-testid="bs-assets">{fmtMoney(data.totalAssets)}</td></tr>
            </tbody></table>
            <table aria-label="Pasywa"><tbody>
              {section('ZOBOWIĄZANIA', data.liabilities)}
              {section('KAPITAŁ WŁASNY', data.equity)}
              <tr><td>Wynik bieżący (niezamknięty)</td><td className="num" data-testid="bs-result">{fmtMoney(data.currentResult)}</td></tr>
              <tr className="total"><td>Razem pasywa</td><td className="num" data-testid="bs-liabilities">{fmtMoney(data.totalLiabilitiesAndEquity)}</td></tr>
            </tbody></table>
          </div>
          <p className={data.totalAssets === data.totalLiabilitiesAndEquity ? 'balance ok' : 'balance bad'} data-testid="bs-check">
            {data.totalAssets === data.totalLiabilitiesAndEquity ? 'Bilans zgodny: aktywa = pasywa ✓' : 'Uwaga: aktywa ≠ pasywa'}
          </p>
        </>
      )}
    </>
  )
}
