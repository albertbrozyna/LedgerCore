# T-001: CI oraz testy jednostkowe domeny i aplikacji

- **Epik:** E8 Jakość
- **Priorytet:** P0 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** brak

## Cel

Szybka informacja zwrotna bez uruchamiania przeglądarki: testy jednostkowe reguł księgowych i automatyczny pipeline.

## Zakres

- Projekt `tests/LedgerCore.Domain.Tests` (xUnit): `Transaction` (bilans, min. 2 zapisy, storno), `Invoice` (VAT per pozycja, zaokrąglenia, płatności, korekty, anulowanie), `Contractor.IsValidNip`, `Account.IsDebitNormal`.
- Projekt `tests/LedgerCore.Application.Tests` na `Microsoft.EntityFrameworkCore.Sqlite` (baza w pamięci) z fałszywym `ICompanyContext`: `CloseYear`, `CreateInvoice`, `CreateCorrection`, `RecordPayment`, raporty (`TrialBalance`, `BalanceSheet`, `ProfitAndLoss`) i **izolacja firm** (dane firmy A niewidoczne w B).
- GitHub Actions `.github/workflows/ci.yml` (Windows): `dotnet build`, `dotnet test`, `npm ci && npm run build`, instalacja Playwrighta (kanał `chromium`) i `npx playwright test`; raport HTML jako artefakt.
- Dodaj projekty do `src/LedgerCore.sln` (lub osobne `tests.sln`) i opisz uruchamianie w `README.md`.

## Kryteria akceptacji

- [ ] `dotnet test` uruchamia ≥ 40 testów jednostkowych i wszystkie przechodzą.
- [ ] Test izolacji firm sprawdza co najmniej konta, księgowania, faktury i kontrahentów.
- [ ] Pipeline CI jest zielony na czystym checkout-cie (bez zależności od lokalnych plików).
- [ ] Błąd w regule księgowej (np. wyłączenie walidacji bilansu) powoduje czerwony test jednostkowy.

## Wskazówki techniczne

- Wzorzec bazy testowej: `new SqliteConnection("DataSource=:memory:")` + `Database.EnsureCreated()` (lub `Migrate()`).
- E2E w CI: ustaw `E2E_BROWSER_CHANNEL=chromium`.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Testy wydajnościowe (T-017).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
