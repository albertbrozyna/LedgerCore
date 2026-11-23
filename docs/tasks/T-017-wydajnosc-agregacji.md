# T-017: Wydajność: agregacje w SQL i skalowanie raportów

- **Epik:** E8 Jakość
- **Priorytet:** P0 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-001

## Cel

Raporty i pulpit mają działać szybko na dziesiątkach/setkach tysięcy zapisów (dziś `LedgerQueries.LoadEntries` ładuje wszystko do pamięci).

## Zakres

- Pomiar: generator danych (np. 200 000 zapisów, 50 kont, 5 lat) jako test wydajnościowy w `tests/` + wyniki bazowe w dokumentacji zadania.
- Rozwiązanie sum po stronie SQL (np. kwoty jako liczba groszy `long` w kolumnie pomocniczej albo tabela sald okresowych aktualizowana przy księgowaniu) – wybór uzasadnij pomiarem.
- Przepisz `TrialBalance`, `AccountLedger` (paginacja), `ProfitAndLoss`, `BalanceSheet`, `Dashboard`, listę kont na nowe agregacje; zachowaj wyniki identyczne co do grosza (porównanie ze starą implementacją w teście).
- Indeksy pod zapytania (konto+data, firma+data) potwierdzone planem zapytań.

## Kryteria akceptacji

- [ ] Dla 200 000 zapisów: pulpit < 1 s, zestawienie obrotów rok < 1 s, karta konta (strona 50) < 300 ms na typowym laptopie.
- [ ] Wszystkie dotychczasowe testy E2E i jednostkowe zielone (wyniki liczbowe identyczne).
- [ ] Migracja przelicza istniejące dane bez utraty i jest sprawdzona na starej bazie.

## Wskazówki techniczne

- Dziś: SQLite nie wspiera SUM na `decimal` – stąd ładowanie do pamięci. Nie wprowadzaj `double`.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Zmiana silnika bazy.

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
