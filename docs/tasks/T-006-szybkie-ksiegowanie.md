# T-006: Szybkie księgowanie sterowane klawiaturą

- **Epik:** E7 UI/UX
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Zrobione
- **Zależności:** T-003

## Cel

Wprowadzanie księgowań w tempie pracy księgowej: podpowiedzi kont, autouzupełnianie, szybkie dodawanie wierszy.

## Zakres

- Pole konta z autouzupełnianiem po kodzie i nazwie (`Combobox`), ostatnio używane konta na górze listy.
- `Enter` w ostatnim polu dodaje wiersz, `Ctrl+Enter` księguje, auto-uzupełnianie brakującej kwoty drugiej strony (różnica bilansu) po `Tab`.
- Duplikowanie księgowania („Kopiuj jako nowe”) z listy i ze szczegółów.
- Podgląd wpływu na salda wybranych kont przed zapisem (mały panel z saldami przed → po).

## Kryteria akceptacji

- [x] Zaksięgowanie typowej operacji (2 wiersze) możliwe bez użycia myszy (test E2E tylko klawiatura).
- [x] Podpowiedź salda zgadza się z kartą konta po zaksięgowaniu.
- [x] „Kopiuj jako nowe” zachowuje opis i wiersze, datę ustawia na dziś.

## Wskazówki techniczne

- Endpoint z saldami wielu kont naraz (`POST /accounts/balances`), żeby nie odpytywać po jednym.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Reguły automatycznej dekretacji (osobne zadanie na przyszłość).

## Dziennik zadania

- **Backend:** `POST /api/v1/accounts/balances` (`AccountBalances`): salda do 50 kont naraz w znaku naturalnym konta (te same liczby co plan kont i karta konta); konta spoza bieżącej firmy są pomijane, > 50 → 400. `LedgerQueries.SumsFor` – sumy dla wskazanych kont. Testy: `AccountBalancesTests` (3).
- **UI:** `design/Combobox.tsx` (ARIA combobox/listbox, filtr po kodzie i nazwie bez względu na wielkość liter i polskie znaki, kod zaczynający się od wpisanego tekstu wyżej, strzałki, Enter/Tab zatwierdzają, Esc przywraca, lista otwiera się po fokusie). Formularz księgowania przeniesiony do `pages/NewTransaction.tsx`: „Ostatnio używane” konta (8, per firma, `localStorage`) na górze listy; Enter w polu kwoty przechodzi dalej, w ostatnim polu ostatniego wiersza dodaje wiersz; Ctrl+Enter księguje (przy niezbilansowanym pokazuje błąd); Enter w polach nie wysyła już formularza; wybór konta w pustym wierszu uzupełnia kwotę drugiej strony różnicą bilansu; panel „Wpływ na salda” (przed → zmiana → po, z `data-testid` `preview-*-<kod>`); „Kopiuj jako nowe” na szczegółach księgowania i w kolumnie akcji dziennika (`/transactions/new?copy=<id>`: opis i wiersze zachowane, data = dziś, komunikat o skopiowaniu).
- **Zmiana kontraktu z testami (świadoma):** pole konta nie jest już `<select>`, więc `selectOption` przestało działać. Etykieta „Konto w wierszu N” zostaje (combobox), a wybór w testach idzie przez nowy helper `pickAccount` (`helpers.ts`, `02-accounting`, `04b-extras`). Asercje bez zmian; to konsekwencja wymaganego przez zadanie autouzupełniania.
- **Testy:** `04i-quick-posting.spec.ts` (księgowanie 2 wierszy samą klawiaturą, Enter dodaje wiersz, Ctrl+Enter bez bilansu, autouzupełnianie w obie strony, ostatnio używane, podgląd sald = saldo końcowe karty konta po zaksięgowaniu, kopiowanie ze szczegółów i z listy, rola Podgląd, limit 50 kont w API). Weryfikacja: `dotnet build` 0 błędów, Playwright 88/88.
- **Do wiedzy:** „ostatnio używane” są pamiętane tylko w przeglądarce (nie przenoszą się między komputerami); test 04i księguje drobne kwoty Kasa↔Kapitał przed plikiem 05, więc zmienia dane pierwszej firmy; podgląd sald nie uwzględnia blokady okresu (to robi serwer przy zapisie); izolację firm dla `/accounts/balances` pokrywają testy jednostkowe, nie E2E.
