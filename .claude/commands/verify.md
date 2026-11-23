---
description: Pełna weryfikacja projektu (build backendu, build UI, testy E2E)
---

Uruchom pełną weryfikację LedgerCore i zgłoś wynik (nie naprawiaj niczego bez potrzeby – najpierw raport):

1. `dotnet build src\LedgerCore.sln` – oczekiwane „Liczba błędów: 0”.
2. `cd src\ledger-core-client; npm run build` – bez błędów TypeScript.
3. `cd e2e; npx playwright test` – wszystkie testy zielone (zapisz liczbę przeszłych testów).
   Przed uruchomieniem upewnij się, że żaden proces `LedgerCore.Api.exe` nie blokuje portu 5199.
4. Jeśli dodano migrację: sprawdź, że `src\LedgerCore.Infrastructure\design-time.db*` nie istnieje i że migracja zachowuje dane
   (uruchom poprzednią paczkę z `dist\LedgerCore` na tymczasowym `LEDGERCORE_DATA_DIR`, utwórz dane, uruchom nowy kod na tym samym folderze).

W podsumowaniu podaj: liczby testów, ewentualne błędy z nazwą testu i przyczyną (błąd aplikacji czy błąd testu), oraz czego nie dało się sprawdzić.
