# T-010: Faktury cykliczne i konfigurowalna numeracja

- **Epik:** E3 Sprzedaż i dokumenty
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Zrobione
- **Zależności:** brak

## Cel

Powtarzalne usługi abonamentowe bez przepisywania oraz numeracja zgodna z zwyczajami klienta.

## Zakres

- Encja `RecurringInvoice` (szablon pozycji, kontrahent, częstotliwość miesięczna/kwartalna/roczna, dzień wystawienia, termin płatności w dniach, następna data, aktywna).
- Generowanie zaległych faktur za dzień (przycisk „Wygeneruj zaległe” + opcjonalna usługa tła przy starcie) z kontrolą blokady okresu i unikaniem duplikatów.
- Konfigurowalny wzór numeru sprzedaży per firma (np. `FV/{RRRR}/{NNNN}`, `{NNN}/{MM}/{RR}`), resetowanie numeracji co rok/miesiąc.

## Kryteria akceptacji

- [x] Wygenerowanie dwa razy za ten sam okres nie tworzy duplikatów (test).
- [x] Nieprawidłowy wzór numeru jest odrzucany z komunikatem; numeracja nigdy nie powtarza numeru (indeks unikalny).
- [x] Faktury wygenerowane mają audyt „cykliczna” i poprawne księgowanie.

## Wskazówki techniczne

- Wzór numeru: parser z jawnym zbiorem znaczników; testy graniczne (zmiana roku).

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wysyłka e-mail wygenerowanych faktur (łączy się z T-009).

## Dziennik zadania

- **Numeracja:** wzór per firma (`Company.SalesNumberPattern` + `SalesNumberReset`: co rok / co miesiąc / nigdy). Parser `NumberPattern` (domena) ma jawny zbiór znaczników `{RRRR} {RR} {MM} {N…}` (1–8 liter N = min. liczba cyfr); odrzuca nieznane znaczniki, brak lub powtórzenie numeru kolejnego, powtórzony rok/miesiąc, niedozwolone znaki, nawiasy bez pary, wzór dłuższy niż 40 znaków oraz wzór, który przy wybranym zerowaniu powtarzałby numery (rok wymagany przy zerowaniu rocznym, rok i miesiąc przy miesięcznym). Domyślny wzór `FV/{RRRR}/{NNNN}` = dotychczasowy format, więc istniejące firmy działają bez zmian.
- **Następny numer** = maks. numer kolejny wśród istniejących faktur sprzedaży pasujących do wzoru w okresie numeracji (rok/miesiąc dopasowywane do daty tylko tam, gdzie następuje reset) + 1; zmiana wzoru „w locie” kontynuuje numerację w nowym wzorze, a stare numery zostają. Korekty zostają przy własnym prefiksie `KFV/rok/` (poza zakresem zadania).
- **Unikalność:** filtrowany unikalny indeks `(CompanyId, Number) WHERE Type = Sales` (zakupy mogą powtarzać numer u różnych dostawców). Migracja najpierw przemianowuje ewentualne duplikaty numerów sprzedaży w istniejącej bazie (dopisek `-dup-xxxx` do późniejszej faktury), żeby indeks nie zablokował aktualizacji – w praktyce duplikatów nie ma, bo stara numeracja była ciągła.
- **Faktury cykliczne:** `RecurringInvoice` + `RecurringInvoiceLine` (`ICompanyOwned`): nazwa, nabywca, częstotliwość (miesiąc/kwartał/rok), dzień miesiąca (31 → ostatni dzień krótszego miesiąca, rok przestępny sprawdzony), termin płatności w dniach, następna data, opcjonalne zakończenie (po ostatnim okresie szablon sam się dezaktywuje), konto przychodu / rodzaj KPiR / stawka ryczałtu (walidowane już przy zapisie szablonu). Generator woła zwykłe `CreateInvoice` – więc księgowanie, VAT, audyt i blokada okresu działają identycznie, także w KPiR/ryczałcie. Data wystawienia i sprzedaży = data z harmonogramu.
- **Brak duplikatów (dwa zabezpieczenia):** generator pomija okres, dla którego faktura już istnieje (i tylko przesuwa harmonogram – działa też po ręcznym cofnięciu daty), a baza ma unikalny indeks `(CompanyId, RecurringInvoiceId, RecurringPeriod)`.
- **Błędy generowania:** okres zamknięty, nieaktywny kontrahent czy brak konta wstrzymują tylko dany szablon (komunikat w oknie wyniku, „następna faktura” zostaje, więc po odblokowaniu zaległe się wystawią); pozostałe szablony generują się dalej. Limit 60 okresów na szablon na jedno uruchomienie.
- **Audyt:** akcja „Faktura cykliczna” przy dodaniu/zmianie/wstrzymaniu/usunięciu szablonu i przy każdej wygenerowanej fakturze (użytkownik albo „system” przy starcie); dodatkowo zwykły wpis „Faktura”.
- **Usługa tła:** `RecurringHostedService` generuje zaległe faktury dla aktywnych firm po starcie, ale tylko gdy `RecurringInvoices:GenerateOnStartup=true` (domyślnie **wyłączone** – faktury powstające bez udziału człowieka to decyzja użytkownika). Nie ma testu E2E tej usługi (E2E startuje z domyślną konfiguracją); logikę pokrywają testy generatora, a samo uruchomienie usługi sprawdzono tylko przez kompilację i przegląd.
- **UI:** strona „Abonamenty” (`/recurring`; nazwa w menu bez słowa „Faktury”, żeby nie dublować linku „Faktury” w istniejących testach): lista, formularz szablonu, „Generuj do dnia” + „Wygeneruj zaległe”, wstrzymaj/wznów/wygeneruj z szablonu/usuń; sekcja „Numeracja faktur sprzedaży” w Administracji z podglądem następnego numeru na żywo i komunikatami błędów. Uprawnienia: odczyt wszyscy, zapis szablonów i generowanie – Administrator i Księgowy, wzór numeracji – tylko Administrator.
- **Testy:** `NumberPatternTests` + `RecurringInvoiceTests` (domena: 14 metod, w tym przypadki Theory), `RecurringTests` (16: zaległe okresy i ponowne uruchomienie, cofnięcie harmonogramu, unikalny indeks, księgowanie i audyt, okres zamknięty, wstrzymany/przyszły szablon, data zakończenia, pojedynczy szablon, izolacja firm, walidacja, usuwanie, własny wzór i reset miesięczny, odrzucenie błędnego wzoru, podgląd, numeracja per firma, unikalność numerów sprzedaży), `10-recurring-numbering.spec.ts` (12, w tym axe w obu motywach: szablon i generowanie, księgowanie i audyt, wstrzymanie/usunięcie, walidacja API, zamknięty okres, wzór z podglądem i błędami, numeracja per firma, rola Podgląd 403, edycja). Weryfikacja: `dotnet build` 0 błędów, testy jednostkowe 103 + 160, Playwright 117/117 (jedno wcześniejsze przejście miało jednorazową awarię w teście 05 – przy ponownym uruchomieniu całości zielone), migracja na bazie ze starego kodu sprawdzona skryptem `e2e/migration-check-t010.mjs` (faktury i numeracja zachowane, wzór domyślny, numeracja kontynuowana, generowanie po migracji).
- **Nie zrobiono / do rozważenia:** numeracja faktur korygujących i zakupów konfigurowana wzorem, wysyłka wygenerowanych faktur e-mailem (poza zakresem; łączy się z T-009), podgląd „co zostanie wygenerowane” przed uruchomieniem, szablony z zmiennymi w opisach (np. „abonament {miesiąc}”), automatyczne generowanie okresowe w trakcie pracy aplikacji (teraz: przycisk lub start).
