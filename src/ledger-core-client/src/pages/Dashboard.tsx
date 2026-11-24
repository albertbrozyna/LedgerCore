import { Link } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney } from '../format'
import { upcomingDeadlines } from '../taxCalendar'
import { ErrorBox, Loading, PageHeader, useLoad } from '../design'

export function DashboardPage() {
  const { fullBooks, mode } = useAuth()
  const { data, error, loading } = useLoad(() => api.dashboard(), [])
  const month = new Date().toLocaleDateString('pl-PL', { month: 'long', year: 'numeric' })

  return (
    <>
      <PageHeader title="Pulpit" />
      <ErrorBox message={error} />
      {loading && !data && <Loading />}
      {data && (
        <>
          {!fullBooks && <p className="card">Ta firma prowadzi {mode === 'Kpir' ? 'podatkową księgę przychodów i rozchodów' : 'ewidencję przychodów ryczałtu'} – zapisy powstają z faktur. <Link to="/register">Otwórz ewidencję</Link>.</p>}
          {fullBooks && <div className="tiles">
            <Tile label="Aktywa" value={data.assets} id="tile-assets" />
            <Tile label="Zobowiązania" value={data.liabilities} id="tile-liabilities" />
            <Tile label="Kapitał własny" value={data.equity} id="tile-equity" />
          </div>}
          {fullBooks && <><h2>Bieżący miesiąc ({month})</h2>
          <div className="tiles">
            <Tile label="Przychody" value={data.monthIncome} id="tile-income" />
            <Tile label="Koszty" value={data.monthExpenses} id="tile-expenses" />
            <Tile label="Wynik" value={data.monthResult} id="tile-result" tone={data.monthResult < 0 ? 'neg' : 'pos'} />
          </div>
          <h2>Przychody i koszty – ostatnie 12 miesięcy</h2>
          <MonthChart months={data.months} />
          <p className="small"><span className="legend ok-dot" /> przychody <span className="legend bad-dot" /> koszty</p></>}
          <h2>Najbliższe terminy podatkowe</h2>
          <ul className="deadlines" data-testid="tax-calendar">
            {upcomingDeadlines(new Date()).map(d => (
              <li key={d.date + d.label}><strong>{fmtDate(d.date)}</strong> – {d.label} <span className="muted">({d.detail})</span></li>
            ))}
          </ul>
          <p className="muted small">Terminy orientacyjne – zależą od formy opodatkowania i rodzaju płatnika; zweryfikuj w swoim przypadku.</p>
          <h2>Rozrachunki</h2>
          <div className="tiles">
            <Tile label="Należności" value={data.receivables} id="tile-receivables" />
            <Tile label="w tym po terminie" value={data.receivablesOverdue} id="tile-receivables-overdue" tone={data.receivablesOverdue > 0 ? "neg" : undefined} />
            <Tile label="Zobowiązania" value={data.payables} id="tile-payables" />
            <Tile label="w tym po terminie" value={data.payablesOverdue} id="tile-payables-overdue" tone={data.payablesOverdue > 0 ? "neg" : undefined} />
          </div>
          {fullBooks && <><h2>Ostatnie księgowania</h2>
          {data.recent.length === 0 ? (
            <p className="muted">Brak księgowań. <Link to="/transactions/new">Dodaj pierwsze</Link>.</p>
          ) : (
            <table>
              <thead><tr><th>Numer</th><th>Data</th><th>Opis</th><th className="num">Kwota</th></tr></thead>
              <tbody>
                {data.recent.map(t => (
                  <tr key={t.id}>
                    <td><Link to={`/transactions/${t.id}`}>{t.number}</Link></td>
                    <td>{fmtDate(t.date)}</td><td>{t.description}</td><td className="num">{fmtMoney(t.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}</>}
        </>
      )}
    </>
  )
}

function Tile({ label, value, id, tone }: { label: string; value: number; id: string; tone?: 'pos' | 'neg' }) {
  return (
    <div className={`tile ${tone ?? ''}`}>
      <div className="tile-label">{label}</div>
      <div className="tile-value" data-testid={id}>{fmtMoney(value)}</div>
    </div>
  )
}

function MonthChart({ months }: { months: { month: string; income: number; expenses: number }[] }) {
  const max = Math.max(1, ...months.flatMap(m => [m.income, m.expenses]))
  const w = 720, h = 180, pad = 24, bw = (w - pad * 2) / months.length
  const y = (v: number) => h - pad - (Math.max(v, 0) / max) * (h - pad * 2)
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="chart" role="img" aria-label="Wykres przychodów i kosztów" data-testid="month-chart">
      <line x1={pad} y1={h - pad} x2={w - pad} y2={h - pad} stroke="currentColor" opacity=".3" />
      {months.map((m, i) => {
        const x = pad + i * bw
        return (
          <g key={m.month}>
            <title>{`${m.month}: przychody ${fmtMoney(m.income)}, koszty ${fmtMoney(m.expenses)}`}</title>
            <rect x={x + bw * 0.12} y={y(m.income)} width={bw * 0.34} height={h - pad - y(m.income)} fill="var(--ok)" />
            <rect x={x + bw * 0.5} y={y(m.expenses)} width={bw * 0.34} height={h - pad - y(m.expenses)} fill="var(--danger)" />
            <text x={x + bw / 2} y={h - 8} fontSize="9" textAnchor="middle" fill="currentColor">{m.month.slice(5)}</text>
          </g>
        )
      })}
    </svg>
  )
}
