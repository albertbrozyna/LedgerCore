# T-009: Wezwania do zapłaty, noty odsetkowe i wysyłka e-mail

- **Epik:** E3 Sprzedaż i dokumenty
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** brak

## Cel

Windykacja z poziomu programu: wezwanie dla przeterminowanych faktur kontrahenta i nota odsetkowa.

## Zakres

- Konfiguracja SMTP (Administracja; hasło szyfrowane DataProtection), test wysyłki, szablony wiadomości z zmiennymi.
- Wezwanie do zapłaty: wybór faktur przeterminowanych kontrahenta, wydruk A4, wysyłka e-mail z załącznikiem PDF po stronie przeglądarki (druk) lub generowanie HTML→PDF po stronie serwera (decyzja w zadaniu).
- Nota odsetkowa: odsetki ustawowe za opóźnienie (stopa konfigurowalna z datą obowiązywania), liczone dziennie od terminu do daty obliczenia.
- Historia wysłanych wezwań na karcie faktury i kontrahenta + wpis w audycie.

## Kryteria akceptacji

- [ ] Odsetki liczone wg wzoru i datowanej stopy – test jednostkowy z ręcznym przykładem (np. 1 000 zł, 45 dni).
- [ ] Wezwanie obejmuje tylko faktury po terminie; opłacone/anulowane/korekty są pomijane.
- [ ] Błędna konfiguracja SMTP daje czytelny komunikat, hasło nigdy nie wraca w odpowiedzi API.

## Wskazówki techniczne

- Stopy odsetek zmieniają się – trzymaj tabelę (od daty, stopa) z możliwością edycji, nie stałą w kodzie.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Automatyczne wysyłanie cyklicznych przypomnień (osobne zadanie).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
