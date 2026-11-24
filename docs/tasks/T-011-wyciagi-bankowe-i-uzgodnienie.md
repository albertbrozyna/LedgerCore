# T-011: Wyciągi bankowe: więcej formatów, deduplikacja, reguły, uzgodnienie

- **Epik:** E4 Bank
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Zrobione
- **Zależności:** T-001

## Cel

Import wyciągu ma być bezpieczny (bez dublowania), obsługiwać typowe formaty PL i pozwalać uzgodnić saldo z bankiem.

## Zakres

- Serwerowy zapis zaimportowanych operacji (`BankTransaction`: konto, data, kwota, tytuł, kontrahent, skrót unikalny, status: nowa/zaksięgowana/pominięta) i **deduplikacja** po skrócie (data+kwota+tytuł+numer rachunku).
- Parsery: CSV (obecny), MT940, Elixir-0, oraz profile CSV popularnych banków (wybór profilu przy imporcie).
- Reguły dopasowania zapisywane per firma (np. „tytuł zawiera X → konto/kontrahent Y”), także księgowanie bezpośrednie na konto (opłaty bankowe, odsetki).
- Uzgodnienie: saldo końcowe wyciągu vs saldo konta w księdze na dzień, lista różnic.

## Kryteria akceptacji

- [x] Ponowny import tego samego pliku nie tworzy drugiego zestawu operacji ani drugich płatności (test).
- [x] Dla MT940 i Elixir-0 istnieją pliki testowe i testy parsera (kwoty, daty, tytuły z polskimi znakami).
- [x] Reguła „opłata bankowa” księguje operację na konto kosztowe bez faktury.
- [x] Raport uzgodnienia pokazuje zgodność lub różnicę z listą niezaksięgowanych operacji.

## Wskazówki techniczne

- Zacznij od modelu i deduplikacji, parsery dodawaj jako wtyczki o wspólnym interfejsie.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Integracje bankowe online (PSD2).

## Dziennik zadania

- **Model:** `BankStatement` (plik, salda z MT940), `BankTransaction` (Nowa / Zaksięgowana / Pominięta, powiązanie z fakturą albo księgowaniem reguły), `BankRule` – wszystko `ICompanyOwned`. Parsowanie przeniesione z przeglądarki na serwer (jedna implementacja, testowalna); klient zachował tylko dopasowanie do faktur (`matchRow`) i wybór kodowania pliku (UTF-8, w razie błędu Windows-1250).
- **Deduplikacja:** skrót SHA-256 z rachunku firmy, daty, kwoty, znormalizowanego tytułu (same litery i cyfry), rachunku kontrahenta i numeru egzemplarza w pliku; unikalny indeks (firma, rachunek, skrót). Numer egzemplarza sprawia, że dwie identyczne operacje tego samego dnia są dwiema operacjami, a ponowny import (także nakładającego się wyciągu) niczego nie dubluje. Operacje zaksięgowane przy ponownym imporcie wracają oznaczone „duplikat (zaksięgowana)” i nie da się ich zaksięgować drugi raz.
- **Parsery** (wspólny interfejs `IBankParser`): CSV (separator i nagłówek wykrywane, wstęp przed nagłówkiem pomijany, profile mBank/PKO BP/ING), MT940 (:25:, :60F:, :61:, :86: także strukturalne ~20…~38, :62F:, storna RC/RD), Elixir-0 (kierunek z porównania rachunków z numerem rachunku firmy). Pliki testowe: `tests/LedgerCore.Application.Tests/TestData/wyciag.mt940` i `wyciag.elixir` (z polskimi znakami), używane też w E2E.
- **Uczciwie o profilach CSV i formatach:** nie miałem prawdziwych plików z tych banków – profile i parsery oparłem na znanych strukturach (nagłówki, MT940 z polskim polem :86:, układ pól Elixir-0) i na własnych próbkach. Profil „Automatyczny” działa na typowych nagłówkach (data/kwota/tytuł/kontrahent/rachunek); **przed produkcyjnym użyciem warto sprawdzić import na własnym wyciągu każdego banku** (szczególnie Elixir-0 – kierunek ustalany wg rachunku, obsługiwany tylko typ 110). Kodowanie ISO-8859-2/CP852 (rzadkie) nie jest obsługiwane automatycznie.
- **Reguły:** „tekst w opisie lub u kontrahenta” (bez rozróżniania wielkości liter, min. 3 znaki) + kierunek (dowolny/wpływ/wypłata) + konto; wpływ Wn bank / Ma konto, wypłata Wn konto / Ma bank; pierwsza pasująca reguła w kolejności dodania; stosowane przy imporcie (opcja) lub przyciskiem. Błąd (np. zamknięty okres, nieaktywne konto) nie przerywa importu – operacja zostaje „nowa”, a komunikat trafia do użytkownika. **Nie zrobiono:** reguły wskazującej kontrahenta (podpowiedź faktury) – tylko księgowanie na konto.
- **Uzgodnienie:** saldo konta w księdze na dzień (domyślnie data salda z ostatniego wyciągu MT940, inaczej dziś) vs saldo końcowe z pliku MT940 albo wpisane ręcznie; wynik: zgodne / różnica, lista niezaksięgowanych operacji do tego dnia i różnica po ich uwzględnieniu. Historia zaimportowanych wyciągów (nowe/duplikaty).
- **Księgowanie do faktury** przez istniejące `RecordPayment` (kwota ograniczona do pozostałej na fakturze; wpływ tylko do sprzedaży, wypłata tylko do zakupu); import i księgowanie tylko w pełnej księgowości (KPiR/ryczałt nie mają kont). Uprawnienia: odczyt wszyscy, import/reguły/księgowanie – Administrator i Księgowy.
- **Zmiana zachowania:** import wymaga wyboru konta już przy „Analizuj” (operacje są zapisywane na koncie), a wyciąg jest zapisywany od razu po analizie – dopasowanie do faktur nadal wymaga zatwierdzenia „Zaksięguj zaznaczone”.
- **Błąd z T-010 znaleziony przy okazji:** podgląd numeracji w Administracji po 250 ms czyścił wspólny komunikat błędu strony (np. błąd NIP znikał) – to powodowało sporadyczne awarie testów 05 i 09. Sekcja numeracji ma teraz własny komunikat błędu.
- **Testy:** `BankParserTests` i `BankTests` (42: kwoty/daty, MT940 z saldami i stornami, Elixir, CSV z wstępem/cudzysłowami/ING, deduplikacja i nakładające się wyciągi, unikalny indeks, reguły i kierunki, zamknięty okres, księgowanie do faktur, pominięcie/przywrócenie, uzgodnienie, walidacja, izolacja firm), `11-bank-statements.spec.ts` (13 w tym axe w obu motywach: reguła w UI, import MT940 z regułą i płatnościami, ponowny import bez duplikatów i drugich płatności, uzgodnienie zgodne, CSV w Windows-1250 z różnicą i listą niezaksięgowanych, pomiń/przywróć, Elixir-0 przez API, błędy UI, usunięcie reguły, rola Podgląd 403 i izolacja). Weryfikacja: `dotnet build` 0 błędów, testy jednostkowe 103 + 201, Playwright 130/130, migracja na bazie ze starego kodu sprawdzona (`e2e/migration-check-t010.mjs` obejmuje też import wyciągu). W jednym z przebiegów proces roboczy Playwrighta zakończył się awarią systemową (kod 3221226505) – niezwiązane ze zmianą, przy ponownym uruchomieniu zielono.
- **Nie zrobiono / do rozważenia:** wiele walut na wyciągu, import z kilku rachunków z jednego pliku MT940, podpowiadanie kontrahenta regułą, ręczne cofanie zaksięgowania operacji, eksport niedopasowanych operacji.
