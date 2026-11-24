import { useEffect, useState, type FormEvent } from 'react'
import { api, type MailSettingsDto } from '../api'
import { fmtDate, todayIso } from '../format'
import { Button, DataTable, useLoad, type Column } from '../design'
import type { InterestRateDto } from '../api'

interface SectionProps { onError: (m: string) => void; onNotice: (m: string) => void }

/** Poczta wychodząca (SMTP) firmy i szablony wiadomości windykacyjnych. Hasło nigdy nie wraca z serwera. */
export function MailSettingsSection({ onError, onNotice }: SectionProps) {
  const { data, reload } = useLoad(() => api.mailSettings(), [])
  const [f, setF] = useState<MailSettingsDto | null>(null)
  const [password, setPassword] = useState('')
  const [testTo, setTestTo] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { if (data) setF(data) }, [data])
  if (!f) return null
  const set = (patch: Partial<MailSettingsDto>) => setF({ ...f, ...patch })

  async function save(e: FormEvent) {
    e.preventDefault()
    onError(''); onNotice('')
    try {
      await api.saveMailSettings({ ...f!, password: password === '' ? null : password })
      setPassword(''); onNotice('Zapisano ustawienia poczty wychodzącej'); reload()
    } catch (err) { onError((err as Error).message) }
  }

  async function clearPassword() {
    onError(''); onNotice('')
    try { await api.saveMailSettings({ ...f!, password: '' }); onNotice('Usunięto zapisane hasło SMTP'); reload() } catch (err) { onError((err as Error).message) }
  }

  async function test() {
    onError(''); onNotice('')
    setBusy(true)
    try { await api.testMail(testTo); onNotice(`Wysłano wiadomość testową na adres ${testTo}`) } catch (err) { onError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <>
      <h2>Poczta wychodząca (SMTP)</h2>
      <form className="card" onSubmit={save} aria-label="Ustawienia poczty">
        <div className="form-row">
          <label className="grow">Serwer SMTP<input value={f.host} onChange={e => set({ host: e.target.value })} required maxLength={200} placeholder="np. smtp.firma.pl" /></label>
          <label>Port SMTP<input type="number" min={1} max={65535} value={f.port} onChange={e => set({ port: Number(e.target.value) })} className="w-6" required /></label>
          <label className="inline"><input type="checkbox" checked={f.useTls} onChange={e => set({ useTls: e.target.checked })} /> Połączenie szyfrowane (TLS)</label>
        </div>
        <div className="form-row mt-3">
          <label className="grow">Login SMTP<input value={f.userName ?? ''} onChange={e => set({ userName: e.target.value })} autoComplete="off" /></label>
          <label className="grow">Hasło SMTP
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password"
              placeholder={f.hasPassword ? '(zapisane – wpisz, aby zmienić)' : ''} />
          </label>
          {f.hasPassword && <Button type="button" variant="link" onClick={clearPassword}>Usuń zapisane hasło</Button>}
        </div>
        <div className="form-row mt-3">
          <label className="grow">Adres nadawcy<input type="email" value={f.fromAddress} onChange={e => set({ fromAddress: e.target.value })} required maxLength={200} /></label>
          <label className="grow">Nazwa nadawcy<input value={f.fromName ?? ''} onChange={e => set({ fromName: e.target.value })} maxLength={200} /></label>
        </div>
        <h3>Szablony wiadomości</h3>
        <p className="muted small">Zmienne: {'{firma} {kontrahent} {faktury} {kwota} {odsetki} {razem} {termin} {data}'}.</p>
        <div className="form-row">
          <label className="grow">Temat wezwania<input value={f.reminderSubject} onChange={e => set({ reminderSubject: e.target.value })} maxLength={200} /></label>
          <label className="grow">Temat noty odsetkowej<input value={f.interestSubject} onChange={e => set({ interestSubject: e.target.value })} maxLength={200} /></label>
        </div>
        <div className="form-row mt-3">
          <label className="grow">Treść wezwania<textarea rows={6} value={f.reminderBody} onChange={e => set({ reminderBody: e.target.value })} maxLength={4000} /></label>
          <label className="grow">Treść noty odsetkowej<textarea rows={6} value={f.interestBody} onChange={e => set({ interestBody: e.target.value })} maxLength={4000} /></label>
        </div>
        <div className="form-row mt-3">
          <Button variant="primary">Zapisz ustawienia poczty</Button>
          <label className="grow">Adres testowy<input type="email" value={testTo} onChange={e => setTestTo(e.target.value)} placeholder="na jaki adres wysłać test" /></label>
          <Button type="button" loading={busy} disabled={!testTo.trim()} onClick={test}>Wyślij wiadomość testową</Button>
        </div>
      </form>
    </>
  )
}

/** Tabela stóp odsetek (rocznych, w % od daty obowiązywania) – edytowana ręcznie, bo stopy zmieniają się w czasie. */
export function InterestRatesSection({ onError, onNotice }: SectionProps) {
  const { data, reload } = useLoad(() => api.interestRates(), [])
  const [from, setFrom] = useState(todayIso())
  const [pct, setPct] = useState('')
  const [note, setNote] = useState('')

  async function add(e: FormEvent) {
    e.preventDefault()
    onError(''); onNotice('')
    const value = Number(pct.replace(',', '.'))
    if (!pct.trim() || Number.isNaN(value)) { onError('Podaj stopę odsetek, np. 11,25'); return }
    try {
      await api.addInterestRate({ validFrom: from, annualPercent: value, note: note || undefined })
      setPct(''); setNote(''); onNotice(`Dodano stopę ${pct}% obowiązującą od ${from}`); reload()
    } catch (err) { onError((err as Error).message) }
  }

  async function remove(r: InterestRateDto) {
    onError(''); onNotice('')
    try { await api.deleteInterestRate(r.id); onNotice(`Usunięto stopę obowiązującą od ${fmtDate(r.validFrom)}`); reload() } catch (err) { onError((err as Error).message) }
  }

  const columns: Column<InterestRateDto>[] = [
    { key: 'from', header: 'Obowiązuje od', render: r => fmtDate(r.validFrom), csv: r => r.validFrom.slice(0, 10) },
    { key: 'pct', header: 'Stopa roczna', align: 'right', render: r => `${r.annualPercent.toFixed(2).replace('.', ',')}%`, csv: r => r.annualPercent },
    { key: 'note', header: 'Uwagi', render: r => r.note ?? '', csv: r => r.note ?? '' },
    { key: 'actions', header: 'Akcje', fixed: true, render: r => <Button variant="link" small onClick={() => remove(r)}>Usuń</Button> },
  ]

  return (
    <>
      <h2>Stopy odsetek za opóźnienie</h2>
      <div className="card">
        <p className="muted">Odsetki naliczane są dziennie: kapitał × stopa roczna ÷ 365, od dnia następnego po terminie płatności. Aktualne stopy ustawowe ogłasza
          Ministerstwo Sprawiedliwości (odsetki ustawowe za opóźnienie) – wpisz je tutaj wraz z datą wejścia w życie; program wybiera stopę obowiązującą w każdym dniu zwłoki.</p>
        <form className="form-row" onSubmit={add} aria-label="Nowa stopa odsetek">
          <label>Obowiązuje od<input type="date" value={from} onChange={e => setFrom(e.target.value)} required /></label>
          <label>Stopa roczna (%)<input inputMode="decimal" value={pct} onChange={e => setPct(e.target.value)} placeholder="np. 11,25" className="w-6" required /></label>
          <label className="grow">Uwagi<input value={note} onChange={e => setNote(e.target.value)} maxLength={200} placeholder="np. odsetki ustawowe za opóźnienie" /></label>
          <Button variant="primary">Dodaj stopę</Button>
        </form>
        {data && <DataTable label="Stopy odsetek" columns={columns} rows={data} rowKey={r => r.id} empty="Brak stóp – bez nich nie da się naliczyć odsetek." />}
      </div>
    </>
  )
}
