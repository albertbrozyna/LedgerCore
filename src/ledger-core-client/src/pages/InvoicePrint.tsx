import { Link, useParams } from 'react-router-dom'
import { api } from '../api'
import { fmtDate, fmtMoney, invoiceTypeLabel, vatRateLabel } from '../format'
import { amountInWords } from '../print'
import { Button, ErrorBox, Loading, PageHeader, useLoad } from '../design'

/** Widok do wydruku / zapisu jako PDF (Ctrl+P → „Zapisz jako PDF”). */
export function InvoicePrintPage() {
  const { id } = useParams()
  const inv = useLoad(() => api.invoice(id!), [id])
  const company = useLoad(() => api.company(), [])
  const d = inv.data
  const c = company.data
  const sales = d?.type === 'Sales'

  return (
    <>
      <PageHeader title="Podgląd wydruku faktury">
        <Link className="btn" to={`/invoices/${id}`}>← Faktura</Link>
        <Button variant="primary"  onClick={() => window.print()}>Drukuj / zapisz jako PDF</Button>
      </PageHeader>
      <ErrorBox message={inv.error || company.error} />
      {(inv.loading || company.loading) && !d && <Loading />}
      {d && c && (
        <div className="invoice-sheet" data-testid="invoice-sheet">
          <div className="invoice-title">
            <h1>{d.isCorrection ? 'Faktura korygująca' : sales ? 'Faktura VAT' : 'Faktura zakupu'} {d.number}</h1>
            <div className="text-right">
              <div>Data wystawienia: <strong>{fmtDate(d.issueDate)}</strong></div>
              <div>Data sprzedaży: <strong>{fmtDate(d.saleDate)}</strong></div>
              <div>Termin płatności: <strong>{fmtDate(d.dueDate)}</strong></div>
            </div>
          </div>
          {d.isCorrection && <p data-testid="print-correction-ref">Dotyczy faktury: <strong>{d.correctedNumber}</strong>. Pozycje przedstawiają różnice względem faktury korygowanej.</p>}
          {!c.name && <p className="alert alert-error no-print">Uzupełnij dane firmy w menu Administracja – pojawią się na fakturze jako dane sprzedawcy.</p>}
          <div className="invoice-parties">
            <div>
              <h3>{sales ? 'Sprzedawca' : 'Nabywca (my)'}</h3>
              <p><strong>{c.name || '—'}</strong>{c.nip ? `\nNIP ${c.nip}` : ''}{c.address ? `\n${c.address}` : ''}</p>
            </div>
            <div>
              <h3>{sales ? 'Nabywca' : 'Sprzedawca'}</h3>
              <p><strong>{d.contractorName}</strong>{d.contractorNip ? `\nNIP ${d.contractorNip}` : ''}</p>
            </div>
          </div>
          <table>
            <thead><tr><th>Lp.</th><th>Nazwa towaru / usługi</th><th className="num">Netto</th><th>Stawka</th><th className="num">VAT</th><th className="num">Brutto</th></tr></thead>
            <tbody>
              {d.lines.map((l, i) => (
                <tr key={i}><td>{i + 1}</td><td>{l.description}</td><td className="num">{fmtMoney(l.net)}</td>
                  <td>{vatRateLabel[l.vatRate]}</td><td className="num">{fmtMoney(l.vat)}</td><td className="num">{fmtMoney(l.gross)}</td></tr>
              ))}
            </tbody>
            <tfoot><tr><td colSpan={2}>Razem</td><td className="num">{fmtMoney(d.net)}</td><td></td><td className="num">{fmtMoney(d.vat)}</td><td className="num">{fmtMoney(d.gross)}</td></tr></tfoot>
          </table>
          <div className="invoice-total">
            {d.isCorrection ? (
              <>Zmiana należności: <strong data-testid="print-total">{fmtMoney(d.gross)} zł</strong>
                <div className="small muted">{d.gross < 0 ? 'Kwota do zmniejszenia należności (rozliczenie na fakturze korygowanej)' : 'Kwota do zwiększenia należności'}</div></>
            ) : (
              <>Do zapłaty: <strong data-testid="print-total">{fmtMoney(d.remaining)} zł</strong>
                <div className="small muted" data-testid="print-words">Słownie: {amountInWords(d.remaining)}</div></>
            )}
          </div>
          <p>Sposób płatności: przelew{c.bankAccount ? <> na rachunek <strong>{c.bankAccount}</strong></> : ''}. {invoiceTypeLabel[d.type]}.</p>
          {d.notes && <p>Uwagi: {d.notes}</p>}
          {c.invoiceFooter && <div className="invoice-footer">{c.invoiceFooter}</div>}
        </div>
      )}
    </>
  )
}
