# T-006: Szybkie księgowanie sterowane klawiaturą

- **Epik:** E7 UI/UX
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** T-003

## Cel

Wprowadzanie księgowań w tempie pracy księgowej: podpowiedzi kont, autouzupełnianie, szybkie dodawanie wierszy.

## Zakres

- Pole konta z autouzupełnianiem po kodzie i nazwie (`Combobox`), ostatnio używane konta na górze listy.
- `Enter` w ostatnim polu dodaje wiersz, `Ctrl+Enter` księguje, auto-uzupełnianie brakującej kwoty drugiej strony (różnica bilansu) po `Tab`.
- Duplikowanie księgowania („Kopiuj jako nowe”) z listy i ze szczegółów.
- Podgląd wpływu na salda wybranych kont przed zapisem (mały panel z saldami przed → po).

## Kryteria akceptacji

- [ ] Zaksięgowanie typowej operacji (2 wiersze) możliwe bez użycia myszy (test E2E tylko klawiatura).
- [ ] Podpowiedź salda zgadza się z kartą konta po zaksięgowaniu.
- [ ] „Kopiuj jako nowe” zachowuje opis i wiersze, datę ustawia na dziś.

## Wskazówki techniczne

- Endpoint z saldami wielu kont naraz (`POST /accounts/balances`), żeby nie odpytywać po jednym.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Reguły automatycznej dekretacji (osobne zadanie na przyszłość).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
