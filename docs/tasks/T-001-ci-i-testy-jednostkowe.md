# T-001: CI oraz testy jednostkowe domeny i aplikacji

- **Epik:** E8 Jakość
- **Priorytet:** P0 · **Rozmiar:** M
- **Status:** Zrobione
- **Zależności:** brak

## Cel

Szybka informacja zwrotna bez uruchamiania przeglądarki: testy jednostkowe reguł księgowych i automatyczny pipeline.

## Zakres

- Projekt `tests/LedgerCore.Domain.Tests` (xUnit): `Transaction` (bilans, min. 2 zapisy, storno), `Invoice` (VAT per pozycja, zaokrąglenia, płatności, korekty, anulowanie), `Contractor.IsValidNip`, `Account.IsDebitNormal`.
- Projekt `tests/LedgerCore.Application.Tests` na `Microsoft.EntityFrameworkCore.Sqlite` (baza w pamięci) z fałszywym `ICompanyContext`: `CloseYear`, `CreateInvoice`, `CreateCorrection`, `RecordPayment`, raporty (`TrialBalance`, `BalanceSheet`, `ProfitAndLoss`) i **izolacja firm** (dane firmy A niewidoczne w B).
- GitHub Actions `.github/workflows/ci.yml` (Windows): `dotnet build`, `dotnet test`, `npm ci && npm run build`, instalacja Playwrighta (kanał `chromium`) i `npx playwright test`; raport HTML jako artefakt.
- Dodaj projekty do `src/LedgerCore.sln` (lub osobne `tests.sln`) i opisz uruchamianie w `README.md`.

## Kryteria akceptacji

- [x] `dotnet test` uruchamia ≥ 40 testów jednostkowych i wszystkie przechodzą.
- [x] Test izolacji firm sprawdza co najmniej konta, księgowania, faktury i kontrahentów.
- [x] Pipeline CI jest zielony na czystym checkout-cie (bez zależności od lokalnych plików). _Plik workflow gotowy; nie uruchomiony na GitHubie – zob. dziennik._
- [x] Błąd w regule księgowej (np. wyłączenie walidacji bilansu) powoduje czerwony test jednostkowy.

## Wskazówki techniczne

- Wzorzec bazy testowej: `new SqliteConnection("DataSource=:memory:")` + `Database.EnsureCreated()` (lub `Migrate()`).
- E2E w CI: ustaw `E2E_BROWSER_CHANNEL=chromium`.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Testy wydajnościowe (T-017).

## Dziennik zadania

- Projekty: `tests/LedgerCore.Domain.Tests` (77 testów) i `tests/LedgerCore.Application.Tests` (53 testy), dodane do `src/LedgerCore.sln`.
- Aplikacja testowana na prawdziwym `LedgerDbContext` + SQLite in-memory (`EnsureCreated`), handlery tworzone bezpośrednio (bez MediatR); dwie firmy z domyślnym planem kont, przełączane przez `ICompanyContext`.
- Test odporności: wyłączenie walidacji bilansu w `Transaction.Validate` daje 3 czerwone testy (2 domena, 1 aplikacja); zmianę cofnięto.
- Weryfikacja lokalna: `dotnet build` 0 błędów, `dotnet test` 130/130, `npm run build`, Playwright 50/50.
- **Nie zweryfikowano:** workflow `.github/workflows/ci.yml` nie był uruchamiany na GitHubie (brak zdalnego repo/uruchomienia w tej sesji); kanał `chromium` w CI zakłada `playwright install --with-deps chromium`.
- Uwaga: testy jednostkowe używają stałego roku 2025 (zamknięcie roku odrzuca przyszłe lata).
