# Wytyczne UI („super” interfejs dla księgowych)

Cel: szybka, czytelna i bezpieczna praca ludzi, którzy spędzają w programie osiem godzin dziennie i wpisują setki dokumentów.
Interfejs ma być **gęsty informacyjnie, przewidywalny i obsługiwany z klawiatury**, a nie „efektowny”.

## Zasady

1. **Klawiatura przede wszystkim.** Każda częsta czynność (nowe księgowanie, dodanie pozycji, zapis, szukanie) ma skrót
   i sensowną kolejność `Tab`. Formularze księgowe obsługuje się bez myszy; `Enter` zatwierdza, `Esc` anuluje.
2. **Stan zawsze widoczny.** Przy każdej operacji użytkownik widzi: w której jest firmie, jaki okres jest zamknięty,
   czy księgowanie jest zbilansowane, czy zapis się powiódł. Żadnych „cichych” porażek.
3. **Błędy wskazują rozwiązanie.** Komunikat po polsku mówi, co jest nie tak i co zrobić („Okres jest zamknięty do …”,
   a nie „Błąd 400”). Błąd pola stoi przy polu, błąd operacji w `ErrorBox` nad formularzem.
4. **Niszczące czynności wymagają potwierdzenia** z opisem skutków i – gdzie się da – możliwości cofnięcia (storno zamiast usuwania).
5. **Liczby są wyrównane do prawej, w czcionce o stałej szerokości cyfr** (`font-variant-numeric: tabular-nums`), kwoty
   zawsze `fmtMoney`, ujemne wyraźnie oznaczone, zero w tabelach obrotów jako puste (`fmtMoneyOrDash`).
6. **Gęstość konfigurowalna.** Tryb „kompaktowy” dla tabel (więcej wierszy), domyślny dla pozostałych.
7. **Dostępność (WCAG AA):** kontrast ≥ 4.5:1, widoczny focus, etykiety dla każdego pola, role ARIA (`alert`, `status`, `tablist`),
   pełna obsługa klawiaturą, brak informacji przekazywanej wyłącznie kolorem (status ma tekst + kolor).
8. **Responsywność:** użytkowanie docelowe to laptop/monitor, ale układ nie może się łamać od 360 px (menu zwijane, tabele przewijane).
9. **Druk to pełnoprawny widok:** każdy raport i dokument ma układ do druku (`@media print`, nagłówek firmy, okres, data wydruku).
10. **Wydajność odczuwalna:** szkielety ładowania zamiast „Ładowanie…”, paginacja po stronie serwera dla list > 100, debounce wyszukiwania.

## System projektowy (docelowo w `src/ledger-core-client/src/design/`)

- **Tokeny** (CSS custom properties): kolory (bg, surface, text, muted, border, primary, danger, ok, warn), odstępy (4/8/12/16/24),
  promienie, cienie, typografia (skala 12/13/14/16/20/24). Jasny i ciemny motyw (`prefers-color-scheme` + przełącznik zapamiętany lokalnie).
- **Komponenty bazowe:** `Button` (primary/secondary/danger/link, loading), `Field` (label + input + hint + error), `Select`, `MoneyInput`
  (parsuje „1 234,50”), `DateInput` (skróty: `t` = dziś, `+7`), `Table` (sortowanie, filtry, wybór kolumn, paginacja, stopka z sumami, stan pusty),
  `Modal`/`ConfirmDialog`, `Toast`, `Tabs`, `Badge`/`StatusBadge`, `EmptyState`, `Skeleton`, `PageHeader`, `Breadcrumbs`.
- **Wzorce stron:** lista (filtry → tabela → paginacja → akcje zbiorcze), szczegóły (nagłówek + karty + oś zdarzeń), formularz wierszowy
  (jak księgowanie/faktura: nagłówek + tabela pozycji + podsumowanie na dole „przyklejone”), raport (parametry → wynik → druk/CSV).
- **Nawigacja:** lewe menu pogrupowane (Księgowość / Sprzedaż i zakupy / Raporty / Biuro / Administracja), przełącznik firmy na górze,
  globalna wyszukiwarka i paleta poleceń (`Ctrl+K`), okruszki na stronach szczegółów.

## Skróty klawiaturowe (docelowe)

`Ctrl+K` paleta/wyszukiwarka · `N` nowe księgowanie · `F` nowa faktura · `Ctrl+S` zapisz · `Esc` zamknij/anuluj ·
`Alt+↑/↓` poprzednia/następna firma · `?` pomoc ze skrótami. Skróty nie mogą działać podczas pisania w polach tekstowych.

## Kontrakt z testami

- Etykiety pól i przycisków są wykorzystywane przez `getByLabel`/`getByRole` w Playwright – zmiana tekstu = zmiana testów w tym samym zadaniu.
- Dane do asercji (kwoty, statusy) mają `data-testid`; nie polegaj na pozycji w DOM.
- Każda nowa strona ma test E2E: ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm (jeśli dotyczy), druk (`page.emulateMedia({ media: 'print' })`).
- Audyt dostępności: dodaj `@axe-core/playwright` (zadanie T-004) i sprawdzaj kluczowe strony pod kątem naruszeń „serious/critical”.
