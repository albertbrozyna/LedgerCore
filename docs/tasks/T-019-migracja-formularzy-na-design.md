# T-019: Migracja formularzy na komponenty design systemu

- **Epik:** E7 UI/UX
- **Priorytet:** P2 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** T-003

## Cel

Dokończyć T-003: strony nadal budują formularze z natywnych `<label><input/></label>` i linków z klasą `btn`. Ujednolicić je przez `Field`, `Input`, `Select`, `MoneyInput`, `DateInput` oraz nowy `LinkButton`.

## Zakres

- Zamień pola formularzy na `Field`/`Input`/`Select`; pola kwot na `MoneyInput`, daty na `DateInput` (zachowując etykiety i `aria-label`).
- Dodaj `LinkButton` (`<Link>` wyglądający jak `Button`) i użyj go zamiast `<Link className="btn …">`.
- Błędy pól (np. walidacja NIP) pokazuj przy polu (`Field error`), błąd operacji – w `ErrorBox`.
- Użyj `EmptyState` zamiast `<p className="muted">Brak …</p>`.

## Kryteria akceptacji

- [ ] Brak `<Link className="btn` i surowych `<label><input` w `src/pages` poza wyjątkami opisanymi w dzienniku.
- [ ] Etykiety i `data-testid` bez zmian; cały zestaw E2E zielony bez zmian w testach.
- [ ] Kwoty w formularzach akceptują zapis „1 234,50”.

## Poza zakresem

- Skróty klawiaturowe (T-005, T-006).

## Dziennik zadania

_(decyzje, odstępstwa, co zweryfikowano)_
