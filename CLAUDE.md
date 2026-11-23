# LedgerCore – instrukcja dla Claude’a

Lokalny system księgowy dla małego biura rachunkowego (wiele firm-klientów w jednej instalacji). Działa na jednym
komputerze z Windows: .NET 10 (API) + React (UI serwowany z `wwwroot`) + SQLite. Bez Dockera i bez serwera bazy.
Wszystkie teksty dla użytkownika, komunikaty błędów i dokumentacja są **po polsku**.

**Zanim zaczniesz zadanie przeczytaj:** `docs/tasks/README.md` (proces), plik zadania w `docs/tasks/`,
`docs/ARCHITECTURE.md` (mechanizmy, które trzeba respektować) i – przy pracy nad UI – `docs/UI-GUIDELINES.md`.
Plan i kolejność: `docs/ROADMAP.md`. Lista funkcji ze statusami: `docs/CHECKLIST.md`.

## Układ repozytorium

```
src/LedgerCore.Domain          encje, reguły księgowe (bez zależności od EF/HTTP)
src/LedgerCore.Application     CQRS: MediatR + FluentValidation; handlery używają IAppDbContext
src/LedgerCore.Infrastructure  EF Core + SQLite, Identity, JWT, migracje (Migrations/)
src/LedgerCore.Api             Carter (endpointy), middleware, Program.cs, serwuje wwwroot
src/ledger-core-client         React + TypeScript + Vite (build → src/LedgerCore.Api/wwwroot)
e2e                            Playwright – testy end-to-end na prawdziwej aplikacji
install                        Build-Package.ps1, Install.cmd/ps1, Start-LedgerCore.cmd, INSTRUKCJA.txt
docs                           ROADMAP, CHECKLIST, ARCHITECTURE, UI-GUIDELINES, tasks/
```
(`LedgerCore.AppHost` i `LedgerCore.ServiceDefaults` to pozostałość po szkielecie – nie rozwijamy ich.)

## Komendy (PowerShell, katalog główny repo)

| Co | Komenda |
|---|---|
| Build backendu | `dotnet build src\LedgerCore.sln` (ma kończyć się „Liczba błędów: 0”) |
| Build UI | `cd src\ledger-core-client; npm run build` (typy TS + bundle do `wwwroot`) |
| Uruchomienie lokalne | `dotnet run --project src\LedgerCore.Api` → http://localhost:5000 |
| UI z hot-reload | `cd src\ledger-core-client; npm run dev` (proxy do API) |
| Nowa migracja | `dotnet ef migrations add Nazwa --project src\LedgerCore.Infrastructure --output-dir Migrations`, potem **usuń** `src\LedgerCore.Infrastructure\design-time.db*` i przebuduj |
| Testy E2E | `cd e2e; npm run build-ui; npx playwright test` (sam startuje API na porcie 5199 ze świeżą bazą) |
| Paczka instalacyjna | `powershell -ExecutionPolicy Bypass -File install\Build-Package.ps1` → `dist\LedgerCore-win-x64.zip` |

**E2E nie buduje UI.** Po zmianie czegokolwiek w `src\ledger-core-client` zbuduj UI (`npm run build`), inaczej testy
sprawdzą stary interfejs. Po zmianie backendu zbuduj `dotnet build` (webServer Playwrighta używa `-c Release`).

## Zasady implementacji

**Backend**
- Funkcja = statyczna klasa w `Application/Features/<Obszar>/` z `Command/Query` (record), `Validator` i `Handler`.
  Błędy biznesowe: `throw new DomainValidationException("komunikat po polsku")` → HTTP 400 z `detail`.
  Walidacja kształtu danych: FluentValidation (zwraca 400 z listą błędów).
- Endpointy: moduły Carter w `Api/Features/...`, grupy przez `app.MapVersionedGroup("nazwa")` (`/api/v1/nazwa`).
  Domyślnie każdy endpoint wymaga logowania. Zapis: `.RequireAuthorization(Policies.CanWrite)`, administracja: `Policies.AdminOnly`.
- **Każda nowa encja z danymi księgowymi implementuje `ICompanyOwned`** (pole `CompanyId`) – filtr zapytań i ustawianie
  firmy przy zapisie dzieją się automatycznie. Unikalne indeksy muszą zawierać `CompanyId`. Zob. `docs/ARCHITECTURE.md`.
- Każda operacja zmieniająca dane woła `AuditLog.Add(akcja, opis, ct)`; każde księgowanie z datą woła
  `PeriodGuard.EnsureOpen(db, data, ct)` (blokada okresu).
- Kwoty: `decimal` (18,2), nigdy `double`. SQLite nie sumuje `decimal` w SQL – używaj `LedgerQueries.Sums` (sumy `AmountCents` w groszach liczone w SQL;
  `LoadEntries` w pamięci tylko dla eksportów i testów referencyjnych).
- Księgowania tworzy się wyłącznie przez `Transaction` (domena pilnuje bilansu Winien = Ma). Nie ma kasowania
  księgowań – tylko storno. Faktury księgują się automatycznie; ich księgowań nie wolno stornować ręcznie.
- Migracje muszą **zachowywać istniejące dane** (patrz `…_MultiCompany.cs` jako wzór). Testuj migrację na bazie
  z poprzedniej paczki (`dist\LedgerCore`, uruchom stary exe, utwórz dane, odpal nowy kod na tym samym `LEDGERCORE_DATA_DIR`).

**Frontend**
- Strony w `src/pages`, wspólne klocki w `src/ui.tsx`, klient API w `src/api.ts`, formaty/etykiety w `src/format.ts`.
- Błędy: `<ErrorBox>` (`role="alert"`), potwierdzenia: `<Notice>` (`role="status"`). Teksty po polsku.
- Etykiety pól (`<label>`) i `aria-label` są kontraktem z testami – nie zmieniaj ich bez aktualizacji testów.
  Stabilne dane w testach oznaczaj `data-testid`.
- Kwoty formatuj `fmtMoney` (pl-PL). Uwaga: `Intl` dla pl-PL **nie grupuje** liczb 4-cyfrowych („1230,50”, ale „12 345,00”).

**Testy (obowiązkowe dla każdej funkcji)**
- Każde zadanie kończy się testami E2E w `e2e/tests/` (scenariusz szczęśliwy + błędy + uprawnienia: Podgląd 403 + izolacja firm).
- Specyfikacje współdzielą jedną bazę i uruchamiają się **po kolei według nazwy pliku**. Znaczenie numerów:
  `01` setup/logowanie, `02` księga, `03` użytkownicy, `04…` faktury/dodatki, `05` **zamyka rok i blokuje firmę nr 1**,
  `06` wielofirmowość. Nowy test wymagający księgowania w pierwszej firmie musi mieć nazwę sortującą się **przed `05`**
  (np. `04d-…`); inaczej używaj nowej firmy. Nie psuj stanu, na którym polegają późniejsze pliki.
- `getByLabel('Od')` łapie też inne etykiety – używaj `{ exact: true }`. Po filtrach lista może chwilę pokazywać stary stan –
  asercje „zniknęło” ustawiaj przed asercjami „pojawiło się”, żeby uniknąć naruszeń strict mode.
- Nie osłabiaj ani nie usuwaj istniejących testów, żeby „przeszły”. Błąd w teście naprawiaj tylko, gdy to test jest błędny
  (i napisz o tym wprost); błąd aplikacji naprawiaj w kodzie.

## Pułapki środowiska

- **Nie używaj długich heredoców w Bash** (Polskie znaki/apostrofy potrafią zepsuć parsowanie). Pliki twórz narzędziem Write.
  Do edycji wielu miejsc napisz krótki skrypt `.mjs` (Write) i uruchom `node`. `python` nie jest dostępny.
- Pliki `.ps1` muszą być zapisane jako UTF-8 **z BOM** (Windows PowerShell 5.1). `README.md` i dokumenty – UTF-8 bez BOM.
- Narzędzie Write zachowuje kodowanie istniejącego pliku (np. UTF-16) – przy dziwnych znakach usuń plik i zapisz od nowa.
- `crd.gov.pl` (oficjalne XSD MF) jest niedostępny – **nie generuj niezwalidowanego XML-a dla urzędu** (KSeF/JPK; zob. T-015/T-016).
- Kasuj tymczasowe bazy po testach ręcznych (`/tmp/lc-*`), zabij procesy `LedgerCore.Api.exe` (`taskkill //F //IM LedgerCore.Api.exe`).

## Jak pracować (skrót; pełny proces w `docs/tasks/README.md`)

1. Wybierz zadanie z `docs/tasks/INDEX.md` (najwyższy priorytet, zależności spełnione) albo to wskazane przez użytkownika.
2. Rozpisz kroki w liście zadań, zaimplementuj backend → migrację → UI → testy E2E → dokumentację.
3. Uruchom **pełny** zestaw: `dotnet build`, `npm run build`, `npx playwright test` – wszystko ma być zielone.
4. Zaktualizuj status w `docs/tasks/INDEX.md` i pliku zadania, `docs/CHECKLIST.md`, `install/INSTRUKCJA.txt`/`README.md` (jeśli zmienia się użycie).
5. Commit **tylko na prośbę użytkownika**, jeden commit na zadanie, komunikat: `T-0XX: krótki opis` + akapit „co i dlaczego”.
6. W podsumowaniu napisz uczciwie, co zrobione, co zweryfikowane testami, a czego nie sprawdzono lub nie obsłużono.
