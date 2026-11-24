# T-009: Wezwania do zapłaty, noty odsetkowe i wysyłka e-mail

- **Epik:** E3 Sprzedaż i dokumenty
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Zrobione
- **Zależności:** brak

## Cel

Windykacja z poziomu programu: wezwanie dla przeterminowanych faktur kontrahenta i nota odsetkowa.

## Zakres

- Konfiguracja SMTP (Administracja; hasło szyfrowane DataProtection), test wysyłki, szablony wiadomości z zmiennymi.
- Wezwanie do zapłaty: wybór faktur przeterminowanych kontrahenta, wydruk A4, wysyłka e-mail z załącznikiem PDF po stronie przeglądarki (druk) lub generowanie HTML→PDF po stronie serwera (decyzja w zadaniu).
- Nota odsetkowa: odsetki ustawowe za opóźnienie (stopa konfigurowalna z datą obowiązywania), liczone dziennie od terminu do daty obliczenia.
- Historia wysłanych wezwań na karcie faktury i kontrahenta + wpis w audycie.

## Kryteria akceptacji

- [x] Odsetki liczone wg wzoru i datowanej stopy – test jednostkowy z ręcznym przykładem (np. 1 000 zł, 45 dni).
- [x] Wezwanie obejmuje tylko faktury po terminie; opłacone/anulowane/korekty są pomijane.
- [x] Błędna konfiguracja SMTP daje czytelny komunikat, hasło nigdy nie wraca w odpowiedzi API.

## Wskazówki techniczne

- Stopy odsetek zmieniają się – trzymaj tabelę (od daty, stopa) z możliwością edycji, nie stałą w kodzie.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Automatyczne wysyłanie cyklicznych przypomnień (osobne zadanie).

## Dziennik zadania

- **Decyzja o PDF:** wezwanie i nota mają układ do druku (`/collections/notices/:id`, „Drukuj / PDF” przeglądarki), a e-mail wysyłany z programu zawiera treść jako HTML (tabela faktur) i tekst zwykły – **bez załącznika PDF** (generowanie PDF po stronie serwera wymagałoby osobnej biblioteki i nie jest potrzebne, skoro dokument jest czytelny w treści wiadomości).
- **Odsetki** (`InterestCalculator`, czysta funkcja): kapitał × stopa roczna ÷ 100 ÷ 365 za każdy dzień zwłoki; zwłoka od dnia następnego po terminie do dnia obliczenia włącznie; kapitał zmniejszają wpłaty (płatności częściowe i przedpłaty), stopa zmienia się wg tabeli z datami obowiązywania; zaokrąglenie raz, na końcu (od 0,5 w górę). Przykład z zadania: 1 000 zł, 45 dni, 11,5% → **14,18 zł**; testy z ręcznie policzonymi przypadkami (zmiana stopy w trakcie, wpłata częściowa, wpłata w dniu terminu itd.).
- **Stopy odsetek nie są zaszyte w kodzie ani zasiane przeze mnie:** nie mam jak zweryfikować aktualnych stóp ustawowych (zmieniają się), więc tabela (`InterestRate`, wspólna dla instalacji, edytowana w Administracji) startuje pusta, a przy braku stopy na dany dzień program podaje czytelny komunikat („uzupełnij tabelę stóp odsetek”). **Przed pierwszym użyciem trzeba wpisać aktualne stopy** (odsetki ustawowe za opóźnienie lub – w transakcjach handlowych – odsetki za opóźnienie w transakcjach handlowych; wybór zależy od rodzaju umowy i leży po stronie użytkownika).
- **Wezwanie obejmuje tylko faktury sprzedaży po terminie** (nieanulowane, niebędące korektami, z kwotą do zapłaty > 0 – po uwzględnieniu korekt); opłacone, anulowane, korekty i faktury zakupu są pomijane. Wezwanie może zawierać odsetki; nota odsetkowa – tylko odsetki. Termin zapłaty = dzień obliczenia + 1–90 dni.
- **Poczta:** `MailSettings` per firma (SMTP: serwer, port, TLS, login, hasło, nadawca, szablony z zmiennymi `{firma} {kontrahent} {faktury} {kwota} {odsetki} {razem} {termin} {data}`), hasło szyfrowane DataProtection (klucze w folderze danych `keys`), **nigdy nie wraca z API** (tylko flaga `hasPassword`); test wysyłki; błędy SMTP tłumaczone na polskie komunikaty (połączenie, logowanie, TLS, adres odbiorcy) bez haseł. Wysyłka nieudana zapisuje dokument w historii ze statusem „błąd wysyłki”. **Uwaga:** klucze DataProtection nie są w kopii zapasowej bazy – po przeniesieniu bazy na inny komputer hasło SMTP trzeba wpisać ponownie (program to wykrywa i prosi o ponowne wpisanie).
- **Historia:** `CollectionNotice` (migawka pozycji i treści w JSON, kwoty, kanał, status, kto wystawił) + powiązania z fakturami; widoczna na stronie windykacji kontrahenta i na karcie faktury; wpis w audycie przy każdym wystawieniu/wysyłce. Uprawnienia: wystawianie – Administrator i Księgowy, podgląd list i dokumentów – także Podgląd, ustawienia poczty i stopy – tylko Administrator.
- **UI:** „Windykacja” w menu (zadłużenie kontrahentów na wybrany dzień), strona kontrahenta (wybór faktur, rodzaj dokumentu, podgląd z edycją tematu i treści, „Zapisz i drukuj”, „Wyślij e-mail”), sekcje „Poczta wychodząca (SMTP)” i „Stopy odsetek za opóźnienie” w Administracji, historia na karcie faktury, polecenie w palecie.
- **Testy:** `InterestCalculatorTests` (6) i `CollectionsTests` (15: kryteria zadania, filtr faktur, szyfrowanie hasła, błędy SMTP, wysyłka i błędy wysyłki, szablony, stopy, izolacja firm), `09-collections.spec.ts` (9, z prawdziwym serwerem SMTP w procesie testu i wysyłką przez MailKit: komunikat o braku stóp, dodanie stopy, odsetki 14,18, wezwanie/nota/wydruk, ustawienia poczty i test wysyłki, błędny port, e-mail trafia do odbiorcy, błąd wysyłki w historii, historia na fakturze, migawka dokumentu, rola Podgląd 403, izolacja, axe w obu motywach). Weryfikacja: `dotnet build` 0 błędów, testy jednostkowe 77 + 144, Playwright 105/105, migracja na bazie ze starego kodu bez zmian w raportach.
- **Nie zrobiono:** załącznik PDF, automatyczne cykliczne przypomnienia (poza zakresem), kilka adresów odbiorców, własne szablony per kontrahent, skanowanie skrzynki (bounce), uwierzytelnianie OAuth do poczty (tylko login/hasło SMTP).
