# Architektura i mechanizmy, które trzeba respektować

## Warstwy

`Domain` (encje i reguły) ← `Application` (przypadki użycia) ← `Infrastructure` (EF/SQLite, Identity, JWT) ← `Api` (HTTP).
Domena nie zna EF ani HTTP. Handlery Application używają `IAppDbContext` (zaimplementowanego przez `LedgerDbContext`).

## Wielofirmowość (najważniejszy mechanizm)

- `Company` = klient biura. Rekordy księgowe implementują `ICompanyOwned` (`CompanyId`).
- `LedgerDbContext` dla każdej encji `ICompanyOwned` ustawia **globalny filtr zapytań** `CompanyId == CurrentCompanyId`
  i indeks po `CompanyId`; przy zapisie (`StampCompany`) wszystkie nowe rekordy dostają firmę z `ICompanyContext`.
- `CompanyContextMiddleware` ustala firmę z nagłówka `X-Company-Id` po sprawdzeniu uprawnień
  (`CompanyAccess.Accessible`: Administrator → wszystkie aktywne firmy, reszta → przypisane w `UserCompanies`).
  Obcy identyfikator → 403. Endpointy `/api/v1/auth/*` i `/api/v1/companies/mine` są wyjątkiem (działają z nieaktualnym nagłówkiem).
- Dane globalne (bez `ICompanyOwned`): `Users`, `Companies`, `UserCompanies`, tokeny. Kopia zapasowa obejmuje całą bazę.
- Nowa encja księgowa: dodaj `: ICompanyOwned` + `public Guid CompanyId { get; private set; }`, unikalne indeksy z `CompanyId`,
  migrację. Nie używaj `IgnoreQueryFilters()` w kodzie biznesowym.
- UI zapamiętuje firmę w `localStorage` (`lc.company`) i dodaje nagłówek do każdego żądania (`api.ts`).

## Księgowość

- `Transaction` (nagłówek, numer `DK/rok/0001`) + `JournalEntry` (konto, kwota, strona Wn/Ma). `Transaction.Validate()` wymaga ≥ 2 zapisów i równości stron.
- Storno = nowa transakcja z odwróconymi stronami (`CreateReversal`). Brak edycji/usuwania.
- Znaki konta: aktywa/koszty – saldo po stronie Wn; pozostałe po Ma (`Account.IsDebitNormal`, `LedgerQueries.Natural`).
- Zamknięcie roku: transakcje `ZR/rok/0001` zerują konta wynikowe, wynik trafia na konto 860; raporty wyników i pulpit
  **pomijają** transakcje o numerze zaczynającym się od `ZR/`.
- Blokada okresu (`PeriodLock`, jeden wiersz na firmę): `PeriodGuard.EnsureOpen` przed każdym księgowaniem z datą.
- Raporty liczą się w pamięci z `LedgerQueries.LoadEntries` (SQLite nie sumuje `decimal`) – patrz T-017 dla skalowania.

## Faktury

- `Invoice` (sprzedaż/zakup), `InvoiceLine` (netto, stawka, VAT liczony per pozycja z zaokrągleniem), `Payment`.
- Księgowanie automatyczne: sprzedaż Wn 201 brutto / Ma przychód netto + Ma 220 VAT; zakup odwrotnie na 202/koszt/220.
  Wymagane konta systemowe po kodzie: 201, 202, 220, 700, 402 (`SystemAccounts`).
- Korekty: `Invoice` z `CorrectedInvoiceId`, pozycje = różnice (mogą być ujemne), księgowane ze znakiem (`AddSigned`),
  `CorrectedAmount` na fakturze korygowanej zmienia `Remaining`. Korekta sama nie podlega płatności.
- Faktury i płatności mają własne księgowania – `ReverseTransaction` je odrzuca (anulowanie przez `CancelInvoice`).

## Bezpieczeństwo i audyt

- JWT (Identity), role `Admin` / `Accountant` / `Viewer`; polityki `Policies.AdminOnly`, `Policies.CanWrite`; domyślnie każdy endpoint wymaga logowania.
- Klucz JWT generowany przy pierwszym starcie (`data/jwt.key`). Blokada konta po 5 błędnych logowaniach.
- `AuditLog.Add` zapisuje wpis razem z operacją (przy jej `SaveChanges`) w bieżącej firmie.
- Załączniki: pliki w `data/attachments/<guid>`; walidacja rozszerzenia **i sygnatury** pliku; limit 10 MB.

## Dane i pliki

Folder danych: `LEDGERCORE_DATA_DIR` / `DataDirectory` (domyślnie `data` obok programu; po instalacji `%LOCALAPPDATA%\LedgerCore\data`):
`ledgercore.db`, `jwt.key`, `attachments/`, `uploads/`, `backups/`. Automatyczna kopia raz dziennie (14 ostatnich).

## Błędy HTTP

`GlobalExceptionHandler`: `DomainException`/`ArgumentException` → 400, `UnauthorizedAccessException` → 401, reszta → 500
(szczegóły tylko w logu). Odpowiedź: `ProblemDetails` z `detail` po polsku – UI wyświetla `detail`.
