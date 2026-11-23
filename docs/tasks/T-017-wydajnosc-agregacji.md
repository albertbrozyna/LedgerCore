# T-017: Wydajność: agregacje w SQL i skalowanie raportów

- **Epik:** E8 Jakość
- **Priorytet:** P0 · **Rozmiar:** L
- **Status:** Zrobione
- **Zależności:** T-001

## Cel

Raporty i pulpit mają działać szybko na dziesiątkach/setkach tysięcy zapisów (dziś `LedgerQueries.LoadEntries` ładuje wszystko do pamięci).

## Zakres

- Pomiar: generator danych (np. 200 000 zapisów, 50 kont, 5 lat) jako test wydajnościowy w `tests/` + wyniki bazowe w dokumentacji zadania.
- Rozwiązanie sum po stronie SQL (np. kwoty jako liczba groszy `long` w kolumnie pomocniczej albo tabela sald okresowych aktualizowana przy księgowaniu) – wybór uzasadnij pomiarem.
- Przepisz `TrialBalance`, `AccountLedger` (paginacja), `ProfitAndLoss`, `BalanceSheet`, `Dashboard`, listę kont na nowe agregacje; zachowaj wyniki identyczne co do grosza (porównanie ze starą implementacją w teście).
- Indeksy pod zapytania (konto+data, firma+data) potwierdzone planem zapytań.

## Kryteria akceptacji

- [x] Dla 200 000 zapisów: pulpit < 1 s, zestawienie obrotów rok < 1 s, karta konta (strona 50) < 300 ms na typowym laptopie.
- [x] Wszystkie dotychczasowe testy E2E i jednostkowe zielone (wyniki liczbowe identyczne).
- [x] Migracja przelicza istniejące dane bez utraty i jest sprawdzona na starej bazie.

## Wskazówki techniczne

- Dziś: SQLite nie wspiera SUM na `decimal` – stąd ładowanie do pamięci. Nie wprowadzaj `double`.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Zmiana silnika bazy.

## Dziennik zadania

### Pomiar (200 000 zapisów, 100 000 księgowań, 5 lat, 50 kont, plik SQLite, najlepszy z 3 przebiegów)

| Operacja | Przed | Po | Limit |
|---|---|---|---|
| Pulpit | 2120 ms | 317 ms | 1000 ms |
| Zestawienie obrotów (rok) | 2268 ms | 286 ms | 1000 ms |
| Karta konta (strona 50) | 402 ms (całość) | 38 ms | 300 ms |
| Rachunek wyników + bilans | 3041 ms | 440 ms | 1000 ms |
| Lista kont z saldami | 1751 ms | 201 ms | 1000 ms |

Test: `tests/LedgerCore.Application.Tests/PerformanceTests.cs` (`[Trait("Category","Performance")]`, generator `PerfData.cs`; na CI limity ×3). Uruchomienie samego pomiaru: `dotnet test testsLedgerCore.Application.Tests --filter Category=Performance --logger "console;verbosity=detailed"`. Pomiar „przed” wykonano na starej implementacji (ładowanie wszystkich zapisów do pamięci).

### Decyzje
- **Wybór rozwiązania:** zamiast tabeli sald okresowych (którą trzeba utrzymywać przy każdym księgowaniu i przy migracjach) – kolumny pomocnicze w `journalentries`: `AmountCents` (long, grosze), `Date` (kopia daty księgowania) i `IsClosing` (księgowanie `ZR/…`). SQLite sumuje liczby całkowite dokładnie, więc `SUM(AmountCents)` jest bez błędów zaokrągleń, a `decimal` pozostaje źródłem prawdy w domenie (`Amount`). Pomiar pokazał 5–10× przyspieszenie, więc tabela sald okresowych nie była potrzebna.
- Indeksy pokrywające: `(CompanyId, Date, AccountId, Side, AmountCents)` dla raportów w zakresie dat i `(CompanyId, AccountId, Date, Side, AmountCents)` dla karty konta oraz `transactions(CompanyId, Date)`; test sprawdza `EXPLAIN QUERY PLAN` (COVERING INDEX).
- `LedgerQueries.Sums` zwraca obroty per konto liczone w SQL; `TrialBalance`, `AccountLedger`, `ProfitAndLoss`, `BalanceSheet`, `Dashboard`, `ListAccounts` i `CloseYear` przepisane. `LoadEntries` zostaje tylko dla eksportu dziennika (CSV) i jako referencja w testach.
- `AccountLedger`: opcjonalne `page`/`pageSize` (API `?page=&pageSize=`, domyślnie 50, max 500); bez `page` zwraca wszystkie wiersze. Saldo początkowe, sumy i saldo końcowe dotyczą całego okresu; saldo na początku strony = saldo otwarcia + suma wierszy poprzedzających. Odpowiedź ma nowe pola `totalCount`, `page`, `pageSize`. UI: karta konta stronicuje po 100 wierszy.
- Migracja `JournalEntryAggregates` uzupełnia kolumny z istniejących danych (grosze z `Amount`, data i znacznik zamknięcia z nagłówka księgowania).

### Weryfikacja
- Testy równoważności (`AggregationEquivalenceTests`): wyniki nowych raportów co do grosza równe liczeniu na `decimal` z `LoadEntries` – dla zakresów dat, z księgowaniem zamykającym rok, 3-wierszowymi zapisami i danymi drugiej firmy; strony karty konta składają się na pełną kartę z identycznymi saldami.
- Migracja sprawdzona na bazie utworzonej przez poprzedni kod (HEAD przed T-017: transakcje, faktura, zamknięcie roku 2024) – skrypt `e2e/migration-check.mjs`: konta, zestawienie obrotów, rachunek wyników, bilans i pulpit identyczne przed i po migracji.
- `dotnet build` 0 błędów, `dotnet test` 77 + 69, `npm run build`, Playwright 60/60 (nowy `04f-ledger-paging.spec.ts`).
- **Uwagi:** pulpit nadal wczytuje faktury do pamięci (`Remaining` jest liczone, nie kolumną) – przy dziesiątkach tysięcy faktur to będzie następne wąskie gardło (do rozważenia osobno). Poza tym uruchamianie `LedgerCore.Api.exe` z innego katalogu niż jego własny nie wczytuje `appsettings.json` (token JWT wygasa od razu) – istniejące zachowanie, tylko zauważone; skrypt migracji ustawia `cwd`.
