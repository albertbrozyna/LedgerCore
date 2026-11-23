import { useState } from 'react'
import {
  Badge, Breadcrumbs, Button, DateInput, EmptyState, ErrorBox, Field, Input, MoneyInput, Notice, PageHeader, Select, Skeleton,
  StatusBadge, Tabs, themeLabel, useDialogs, useTheme, useToast,
} from '../design'

const tokens = ['bg', 'surface', 'surface-2', 'text', 'muted', 'border', 'primary', 'danger', 'ok', 'warn']

/** Katalog komponentów – dostępny tylko w trybie deweloperskim pod /design. */
export function DesignCatalogPage() {
  const { theme, cycle } = useTheme()
  const { confirm, prompt } = useDialogs()
  const toast = useToast()
  const [tab, setTab] = useState('a')
  const [money, setMoney] = useState('1 234,5')
  const [date, setDate] = useState('')
  const [last, setLast] = useState('')
  return (
    <>
      <Breadcrumbs items={[{ label: 'Pulpit', to: '/dashboard' }, { label: 'Katalog komponentów' }]} />
      <PageHeader title="Katalog komponentów">
        <Button onClick={cycle}>{themeLabel[theme]}</Button>
      </PageHeader>

      <h2>Tokeny kolorów</h2>
      <div className="swatches">
        {tokens.map(t => <div key={t} className="swatch"><i className={`sw-${t}`} />{t}</div>)}
      </div>

      <h2>Przyciski</h2>
      <div className="stack">
        <Button variant="primary">Główny</Button><Button>Drugorzędny</Button><Button variant="danger">Niebezpieczny</Button>
        <Button variant="link">Link</Button><Button small>Mały</Button><Button loading>W toku</Button><Button disabled>Wyłączony</Button>
      </div>

      <h2>Pola formularza</h2>
      <div className="form-row">
        <Field label="Tekst" hint="Podpowiedź pod polem"><Input defaultValue="Przykład" /></Field>
        <Field label="Z błędem" error="To pole jest wymagane"><Input /></Field>
        <Field label="Lista"><Select><option>Pierwsza</option><option>Druga</option></Select></Field>
        <Field label="Kwota (MoneyInput)"><MoneyInput value={money} onChange={setMoney} /></Field>
        <Field label="Data (DateInput, klawisz t = dziś)"><DateInput value={date} onChange={setDate} /></Field>
      </div>

      <h2>Odznaki i status</h2>
      <div className="stack">
        <Badge>Neutralna</Badge><Badge tone="info">Info</Badge><Badge tone="ok">OK</Badge><Badge tone="warn">Uwaga</Badge><Badge tone="danger">Błąd</Badge>
        <StatusBadge status="unpaid">Nieopłacona</StatusBadge><StatusBadge status="overdue">Po terminie</StatusBadge>
        <StatusBadge status="paid">Opłacona</StatusBadge><StatusBadge status="canceled">Anulowana</StatusBadge>
      </div>

      <h2>Zakładki</h2>
      <Tabs label="Przykładowe zakładki" value={tab} onChange={setTab} tabs={[{ value: 'a', label: 'Pierwsza' }, { value: 'b', label: 'Druga' }]} />

      <h2>Komunikaty, stan pusty, szkielet</h2>
      <ErrorBox message="Przykładowy błąd operacji" />
      <Notice message="Przykładowe potwierdzenie" />
      <EmptyState action={<Button variant="primary">Dodaj pierwszy</Button>}>Brak danych do wyświetlenia.</EmptyState>
      <Skeleton />

      <h2>Okna dialogowe i powiadomienia</h2>
      <div className="stack">
        <Button onClick={async () => setLast(`confirm: ${await confirm({ title: 'Czy na pewno?', message: 'Opis skutków operacji.', danger: true, confirmLabel: 'Usuń' })}`)}>Potwierdzenie</Button>
        <Button onClick={async () => setLast(`prompt: ${await prompt({ title: 'Podaj wartość', label: 'Wartość' })}`)}>Zapytanie</Button>
        <Button onClick={() => toast('Zapisano zmiany')}>Toast</Button>
        <span className="muted" data-testid="catalog-last">{last}</span>
      </div>
    </>
  )
}
