# T-005: Globalna wyszukiwarka, paleta poleceń i skróty klawiaturowe

- **Epik:** E7 UI/UX
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** T-003

## Cel

Szybka nawigacja: `Ctrl+K` znajduje konta, kontrahentów, faktury, księgowania i polecenia.

## Zakres

- Endpoint `GET /api/v1/search?q=` (w bieżącej firmie): konta (kod/nazwa), kontrahenci (nazwa/NIP), faktury (numer), księgowania (numer/opis); limit 5 na kategorię.
- Paleta poleceń w UI (`Ctrl+K`): wyniki z kategoriami + polecenia („Nowe księgowanie”, „Nowa faktura sprzedaży”, „Przełącz firmę …”, „Raporty”).
- Skróty z `docs/UI-GUIDELINES.md` (`N`, `F`, `?`, `Alt+↑/↓` firma), okno pomocy ze skrótami; wyłączone podczas pisania w polach.

## Kryteria akceptacji

- [ ] Wyniki dotyczą wyłącznie bieżącej firmy (test izolacji).
- [ ] `Ctrl+K` → wpisanie numeru faktury → `Enter` otwiera fakturę (E2E).
- [ ] Skróty nie reagują w polach tekstowych; `?` otwiera pomoc.

## Wskazówki techniczne

- Wyszukiwanie przez `EF.Functions.Like` z parametryzacją; wartości użytkownika nigdy bez parametrów.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wyszukiwanie pełnotekstowe po załącznikach.

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
