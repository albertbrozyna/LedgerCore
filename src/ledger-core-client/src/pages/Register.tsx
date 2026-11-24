import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type KpirDto, type KpirRow, type KpirTotals } from '../api'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, fmtMoneyOrDash } from '../format'
import { Button, DataTable, ErrorBox, Loading, PageHeader, PrintHeader, type Column, useLoad } from '../design'

const months = ['styczeń', 'luty', 'marzec', 'kwiecień', 'maj', 'czerwiec', 'lipiec', 'sierpień', 'wrzesień', 'październik', 'listopad', 'grudzień']
const pad = (n: number) => String(n).padStart(2, '0')
const monthLabel = (ym: string) => `${months[Number(ym.slice(5)) - 1]} ${ym.slice(0, 4)}`

/** Wspólny wybór zakresu: rok oraz miesiące od–do (ewidencje dotyczą jednego roku podatkowego). */
function usePeriod() {
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [fromMonth, setFromMonth] = useState(1)
  const [toMonth, setToMonth] = useState(now.getMonth() + 1)
  const last = new Date(year, toMonth, 0).getDate()
  const from = `${year}-${pad(fromMonth)}-01`
  const to = `${year}-${pad(toMonth)}-${pad(last)}`
  const invalid = toMonth < fromMonth
  return { year, setYear, fromMonth, setFromMonth, toMonth, setToMonth, from, to, invalid, label: `${monthLabel(from.slice(0, 7))} – ${monthLabel(to.slice(0, 7))}` }
}

function PeriodPicker({ p }: { p: ReturnType<typeof usePeriod> }) {
  const thisYear = new Date().getFullYear()
  return (
    <div className="card form-row no-print">
      <label>Rok
        <select value={p.year} onChange={e => p.setYear(Number(e.target.value))}>
          {[thisYear - 3, thisYear - 2, thisYear - 1, thisYear, thisYear + 1].map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </label>
      <label>Od miesiąca
        <select value={p.fromMonth} onChange={e => p.setFromMonth(Number(e.target.value))}>
          {months.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
        </select>
      </label>
      <label>Do miesiąca
        <select value={p.toMonth} onChange={e => p.setToMonth(Number(e.target.value))}>
          {months.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
        </select>
      </label>
    </div>
  )
}

export function RegisterPage() {
  const { mode } = useAuth()
  return mode === 'Kpir' ? <KpirView /> : <RyczaltView />
}

const money = (n: number) => fmtMoneyOrDash(n)

function KpirView() {
  const p = usePeriod()
  const { data, error, loading } = useLoad(() => p.invalid ? Promise.resolve(null as KpirDto | null) : api.kpir(p.from, p.to), [p.from, p.to, p.invalid])

  const col = (key: string, header: string, pick: (r: KpirRow) => number, hidden = false): Column<KpirRow> =>
    ({ key, header, align: 'right', render: r => money(pick(r)), csv: pick, hiddenByDefault: hidden })
  const columns: Column<KpirRow>[] = [
    { key: 'lp', header: 'Lp.', render: r => r.lp, csv: r => r.lp },
    { key: 'date', header: 'Data zdarzenia', render: r => fmtDate(r.date), csv: r => r.date.slice(0, 10) },
    { key: 'doc', header: 'Nr dowodu', render: r => <Link to={`/invoices/${r.invoiceId}`}>{r.document}</Link>, csv: r => r.document },
    { key: 'contractor', header: 'Kontrahent', render: r => r.contractor, csv: r => r.contractor },
    { key: 'address', header: 'Adres', render: r => r.address ?? '', csv: r => r.address ?? '', hiddenByDefault: true },
    { key: 'desc', header: 'Opis zdarzenia', render: r => r.description, csv: r => r.description },
    col('revenue', 'Wartość sprzedanych towarów i usług (7)', r => r.revenue),
    col('otherRevenue', 'Pozostałe przychody (8)', r => r.otherRevenue),
    col('totalRevenue', 'Razem przychód (9)', r => r.totalRevenue),
    col('goods', 'Zakup towarów handlowych i materiałów (10)', r => r.goodsPurchase),
    col('side', 'Koszty uboczne zakupu (11)', r => r.purchaseCosts),
    col('wages', 'Wynagrodzenia (12)', r => r.wages, true),
    col('other', 'Pozostałe wydatki (13)', r => r.otherExpense),
    col('totalExpense', 'Razem wydatki (14)', r => r.totalExpense),
    col('research', 'Koszty B+R (16)', r => r.research),
    { key: 'remarks', header: 'Uwagi (17)', render: r => r.remarks ?? '', csv: r => r.remarks ?? '' },
  ]
  const totalCells = (t: KpirTotals, prefix: string) => ({
    revenue: <span data-testid={`${prefix}-revenue`}>{fmtMoney(t.revenue)}</span>,
    otherRevenue: <span data-testid={`${prefix}-other-revenue`}>{fmtMoney(t.otherRevenue)}</span>,
    totalRevenue: <span data-testid={`${prefix}-total-revenue`}>{fmtMoney(t.totalRevenue)}</span>,
    goods: <span data-testid={`${prefix}-goods`}>{fmtMoney(t.goodsPurchase)}</span>,
    side: <span data-testid={`${prefix}-side`}>{fmtMoney(t.purchaseCosts)}</span>,
    wages: fmtMoney(t.wages),
    other: <span data-testid={`${prefix}-other`}>{fmtMoney(t.otherExpense)}</span>,
    totalExpense: <span data-testid={`${prefix}-total-expense`}>{fmtMoney(t.totalExpense)}</span>,
    research: <span data-testid={`${prefix}-research`}>{fmtMoney(t.research)}</span>,
  })

  return (
    <>
      <PageHeader title="Podatkowa księga przychodów i rozchodów">
        <Button onClick={() => window.print()}>Drukuj / PDF</Button>
      </PageHeader>
      <PrintHeader title="Podatkowa księga przychodów i rozchodów" period={p.label} />
      <PeriodPicker p={p} />
      {p.invalid && <ErrorBox message="Miesiąc końcowy nie może być wcześniejszy niż początkowy" />}
      <ErrorBox message={error} />
      {loading && !data && <Loading />}
      {data && (
        <>
          <DataTable label="Księga przychodów i rozchodów" columns={columns} rows={data.rows} rowKey={r => r.invoiceId} loading={loading}
            empty="Brak wpisów w wybranym okresie (ewidencja powstaje z faktur sprzedaży i zakupu)." viewKey="kpir" csvName={`kpir-${p.from.slice(0, 7)}-${p.to.slice(0, 7)}`}
            footer={{ label: 'Razem w okresie', cells: totalCells(data.period, 'kpir-period') }} />
          <h2>Podsumowanie miesięczne i narastające od początku roku</h2>
          <table aria-label="Podsumowanie miesięczne">
            <thead><tr><th>Miesiąc</th><th className="num">Przychód w miesiącu</th><th className="num">Wydatki w miesiącu</th>
              <th className="num">Przychód narastająco</th><th className="num">Wydatki narastająco</th><th className="num">Dochód narastająco</th><th className="num">Zaliczka 19% (informacyjnie)</th></tr></thead>
            <tbody>
              {data.months.map(m => (
                <tr key={m.month} data-testid={`kpir-month-${m.month}`}>
                  <td>{monthLabel(m.month)}</td>
                  <td className="num">{fmtMoney(m.period.totalRevenue)}</td><td className="num">{fmtMoney(m.period.totalExpense)}</td>
                  <td className="num" data-testid={`kpir-ytd-revenue-${m.month}`}>{fmtMoney(m.yearToDate.totalRevenue)}</td>
                  <td className="num" data-testid={`kpir-ytd-expense-${m.month}`}>{fmtMoney(m.yearToDate.totalExpense)}</td>
                  <td className="num" data-testid={`kpir-income-${m.month}`}>{fmtMoney(m.income)}</td>
                  <td className="num">{fmtMoney(m.advanceInfo)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot><tr><td>Od początku roku</td><td></td><td></td>
              <td className="num">{fmtMoney(data.yearToDate.totalRevenue)}</td><td className="num">{fmtMoney(data.yearToDate.totalExpense)}</td>
              <td className="num" data-testid="kpir-income">{fmtMoney(data.income)}</td><td className="num">{fmtMoney(data.advanceInfo)}</td></tr></tfoot>
          </table>
          <p className="muted small">Kwoty netto z faktur. Zaliczka to orientacyjna wartość wg stawki liniowej 19% od dochodu narastająco (bez składek i wcześniejszych wpłat) –
            nie zastępuje rozliczenia podatku. Wynagrodzenia (kol. 12) i inne zapisy bez faktury nie są jeszcze ewidencjonowane.</p>
        </>
      )}
    </>
  )
}

function RyczaltView() {
  const p = usePeriod()
  const { data, error, loading } = useLoad(() => p.invalid ? Promise.resolve(null) : api.ryczalt(p.from, p.to), [p.from, p.to, p.invalid])
  type R = NonNullable<typeof data>['rows'][number]
  const columns: Column<R>[] = [
    { key: 'lp', header: 'Lp.', render: r => r.lp, csv: r => r.lp },
    { key: 'date', header: 'Data uzyskania przychodu', render: r => fmtDate(r.date), csv: r => r.date.slice(0, 10) },
    { key: 'doc', header: 'Nr dowodu', render: r => <Link to={`/invoices/${r.invoiceId}`}>{r.document}</Link>, csv: r => r.document },
    { key: 'contractor', header: 'Kontrahent', render: r => r.contractor, csv: r => r.contractor },
    { key: 'desc', header: 'Opis zdarzenia', render: r => r.description, csv: r => r.description },
    { key: 'rate', header: 'Stawka', align: 'right', render: r => `${String(r.rate).replace('.', ',')}%`, csv: r => r.rate },
    { key: 'revenue', header: 'Przychód', align: 'right', render: r => fmtMoney(r.revenue), csv: r => r.revenue },
  ]
  return (
    <>
      <PageHeader title="Ewidencja przychodów (ryczałt)">
        <Button onClick={() => window.print()}>Drukuj / PDF</Button>
      </PageHeader>
      <PrintHeader title="Ewidencja przychodów (ryczałt)" period={p.label} />
      <PeriodPicker p={p} />
      {p.invalid && <ErrorBox message="Miesiąc końcowy nie może być wcześniejszy niż początkowy" />}
      <ErrorBox message={error} />
      {loading && !data && <Loading />}
      {data && (
        <>
          <DataTable label="Ewidencja przychodów" columns={columns} rows={data.rows} rowKey={r => r.invoiceId} loading={loading}
            empty="Brak przychodów w wybranym okresie (ewidencja powstaje z faktur sprzedaży)." viewKey="ryczalt" csvName={`ryczalt-${p.from.slice(0, 7)}-${p.to.slice(0, 7)}`}
            footer={{ label: 'Razem w okresie', cells: { revenue: <span data-testid="ryczalt-total">{fmtMoney(data.totalRevenue)}</span> } }} />
          <h2>Przychody wg stawek – od początku roku</h2>
          <table aria-label="Przychody wg stawek">
            <thead><tr><th>Stawka</th><th className="num">Przychód narastająco</th><th className="num">Podatek (informacyjnie)</th></tr></thead>
            <tbody>
              {data.yearToDateByRate.map(t => (
                <tr key={t.rate} data-testid={`ryczalt-rate-${t.rate}`}>
                  <td>{String(t.rate).replace('.', ',')}%</td><td className="num">{fmtMoney(t.revenue)}</td><td className="num">{fmtMoney(t.tax)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot><tr><td>Razem</td><td className="num">{fmtMoney(data.yearToDateByRate.reduce((s, t) => s + t.revenue, 0))}</td>
              <td className="num" data-testid="ryczalt-ytd-tax">{fmtMoney(data.yearToDateTax)}</td></tr></tfoot>
          </table>
          <h2>Podsumowanie miesięczne</h2>
          <table aria-label="Podsumowanie miesięczne">
            <thead><tr><th>Miesiąc</th><th className="num">Przychód w miesiącu</th><th className="num">Przychód narastająco</th><th className="num">Podatek narastająco (informacyjnie)</th></tr></thead>
            <tbody>
              {data.months.map(m => (
                <tr key={m.month}><td>{monthLabel(m.month)}</td><td className="num">{fmtMoney(m.revenue)}</td>
                  <td className="num">{fmtMoney(m.yearToDateRevenue)}</td><td className="num">{fmtMoney(m.yearToDateTax)}</td></tr>
              ))}
            </tbody>
          </table>
          <p className="muted small">Kwoty netto z faktur sprzedaży. Podatek liczony od sum wg stawek, zaokrąglony do pełnych złotych – orientacyjnie, bez składek ZUS i ulg.</p>
        </>
      )}
    </>
  )
}
