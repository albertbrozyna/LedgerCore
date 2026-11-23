# LedgerCore

Prosty system księgowy (podwójny zapis) dla małego biura rachunkowego, instalowany lokalnie na jednym komputerze.
Bez serwera bazy danych i bez Dockera – wszystko w jednej aplikacji: .NET 10 (API) + React (interfejs) + SQLite.

## Funkcje

- **Plan kont** (domyślny plan przy pierwszym uruchomieniu; dodawanie, zmiana nazwy, dezaktywacja)
- **Księgowania** z kontrolą bilansu (Winien = Ma), automatyczna numeracja `DK/rok/0001`
- **Storno** zamiast usuwania – dziennik jest niemodyfikowalny
- **Karta konta** z saldem narastającym, **zestawienie obrotów i sald**, **rachunek wyników**, **bilans**, pulpit
- **Kontrahenci** z walidacją NIP (suma kontrolna, unikalność) i saldami należności/zobowiązań
- **Faktury sprzedaży i zakupu**: stawki 23/8/5/0/zw., numeracja `FV/rok/0001`, automatyczne księgowanie (201/202, 220 VAT), płatności częściowe i pełne, anulowanie ze stornem
- **Wiele firm (klientów biura)** w jednej instalacji: odizolowane dane, przełącznik firmy, dostęp użytkowników do wybranych firm
- **Faktury korygujące** (in minus / in plus, zmiana stawki VAT) z księgowaniem ze znakiem, wpływem na VAT i rozrachunki
- **Rejestry VAT** (sprzedaży i zakupów) oraz rozliczenie VAT należny − naliczony
- **Rozrachunki**: należności i zobowiązania z podziałem na przeterminowanie (w terminie, 1–30, 31–60, >60 dni)
- **Dane firmy** (NIP, adres, rachunek bankowy, stopka) i **wydruk faktury** z kwotą słownie (Drukuj → „Zapisz jako PDF”)
- **Wydruki raportów** z nagłówkiem firmy i okresem oraz **eksporty CSV** (dziennik, obroty i salda, rachunek wyników, bilans, rozrachunki, rejestry VAT) – otwierają się poprawnie w polskim Excelu
- **Zamknięcie okresu** (blokada księgowania wstecz) i **zamknięcie roku obrotowego** (wynik na konto 860, salda bilansowe przechodzą na nowy rok)
- **Dziennik audytu**: kto, kiedy i co zmienił
- **Import wyciągu bankowego (CSV)** z automatycznym dopasowaniem wpłat i wypłat do faktur (numer w tytule, kwota, kontrahent) i księgowaniem płatności
- **Załączniki do faktur** (PDF/PNG/JPG do 10 MB, kontrola zawartości pliku)
- **Szablony księgowań** dla powtarzalnych operacji
- **Pulpit**: wykres przychodów i kosztów z 12 miesięcy oraz kalendarz terminów podatkowych (orientacyjny)
- **Automatyczne kopie zapasowe** (codziennie, ostatnie 14) i zmiana własnego hasła
- **Użytkownicy i role**: Administrator, Księgowy, Podgląd; blokowanie kont, reset hasła, blokada po 5 błędnych logowaniach
- **Kopia zapasowa** bazy jednym kliknięciem (Administrator)

## Instalacja na komputerze biura

```powershell
powershell -ExecutionPolicy Bypass -File install\Build-Package.ps1
```

Powstaje `dist\LedgerCore-win-x64.zip` (zawiera środowisko .NET – docelowy komputer nie wymaga niczego dodatkowo).
Na komputerze biura: rozpakuj ZIP → uruchom `Install.cmd` → skrót **LedgerCore** na pulpicie.
Szczegóły dla użytkownika końcowego: [`install/INSTRUKCJA.txt`](install/INSTRUKCJA.txt).

Dane (baza SQLite, klucz JWT, wgrane pliki) leżą w `%LOCALAPPDATA%\LedgerCore\data` i przetrwają aktualizacje programu.

## Rozwój

Wymagania: .NET 10 SDK, Node.js 20+.

```powershell
# API (http://localhost:5000, dane w folderze data obok programu lub LEDGERCORE_DATA_DIR)
dotnet run --project src\LedgerCore.Api

# Interfejs z hot-reload (http://localhost:5173, proxy do API)
cd src\ledger-core-client; npm install; npm run dev
```

Swagger jest dostępny w trybie `Development` pod `/swagger`.

## Testy end-to-end

Playwright uruchamia prawdziwą aplikację na świeżej bazie (port 5199) i klika po interfejsie: kreator pierwszego
uruchomienia, logowanie, plan kont, księgowania, storno, faktury, VAT, rozrachunki, wydruki, bilans, zamknięcie
okresu i roku, role i uprawnienia, audyt, kopia zapasowa.

```powershell
cd e2e
npm install
npm run build-ui     # buduje interfejs do src\LedgerCore.Api\wwwroot
npm test
```

Testy używają zainstalowanej przeglądarki Microsoft Edge. Inna przeglądarka: `npx playwright install chromium`
i `$env:E2E_BROWSER_CHANNEL='chromium'`.

## Wielofirmowość

Każdy rekord księgowy ma identyfikator firmy, a zapytania są automatycznie filtrowane do firmy wybranej w żądaniu
(nagłówek `X-Company-Id`, sprawdzany względem uprawnień użytkownika). Dane innej firmy są „niewidoczne” także dla API.
Istniejące dane z wersji jednofirmowej są przy aktualizacji przenoszone do firmy domyślnej. Kopia zapasowa obejmuje całą
bazę (wszystkie firmy).

## Architektura

Clean Architecture: `Domain` (encje, reguły księgowe) → `Application` (CQRS: MediatR + FluentValidation) →
`Infrastructure` (EF Core + SQLite, Identity, JWT) → `Api` (Carter, serwuje też zbudowany interfejs z `wwwroot`).
Migracje bazy stosowane są automatycznie przy starcie aplikacji.

## Konfiguracja

| Ustawienie | Opis |
|---|---|
| `LEDGERCORE_DATA_DIR` / `DataDirectory` | folder danych (domyślnie `data` obok programu) |
| `ASPNETCORE_URLS` | adres nasłuchu (domyślnie `http://localhost:5000`) |
| `Jwt:SecretKey` | domyślnie generowany losowo przy pierwszym starcie i zapisywany w `data\jwt.key` |
| `Auth:RequireEmailVerification` | `true` wymaga weryfikacji e-mail (wymaga skonfigurowanego SMTP w `Email:*`); domyślnie `false` |

## Plan rozwoju

Funkcje i statusy: [`docs/CHECKLIST.md`](docs/CHECKLIST.md) · roadmapa i zadania: [`docs/ROADMAP.md`](docs/ROADMAP.md), [`docs/tasks/INDEX.md`](docs/tasks/INDEX.md) · zasady pracy (także dla Claude’a): [`CLAUDE.md`](CLAUDE.md).

## Czego jeszcze nie ma

- **KSeF (FA(3)) i JPK_V7M**: pliki XML dla urzędu wymagają oficjalnych schematów XSD Ministerstwa Finansów
  (`crd.gov.pl`) i walidacji względem nich; w środowisku, w którym powstawał projekt, serwer ten był niedostępny.
  Po dodaniu plików XSD do repozytorium można dopisać generatory i testy walidujące. Do tego czasu rejestry VAT
  (podgląd i CSV) służą jako podstawa do ręcznego wypełnienia deklaracji.
