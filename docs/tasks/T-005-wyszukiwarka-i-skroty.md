# T-005: Globalna wyszukiwarka, paleta poleceń i skróty klawiaturowe

- **Epik:** E7 UI/UX
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Zrobione
- **Zależności:** T-003

## Cel

Szybka nawigacja: `Ctrl+K` znajduje konta, kontrahentów, faktury, księgowania i polecenia.

## Zakres

- Endpoint `GET /api/v1/search?q=` (w bieżącej firmie): konta (kod/nazwa), kontrahenci (nazwa/NIP), faktury (numer), księgowania (numer/opis); limit 5 na kategorię.
- Paleta poleceń w UI (`Ctrl+K`): wyniki z kategoriami + polecenia („Nowe księgowanie”, „Nowa faktura sprzedaży”, „Przełącz firmę …”, „Raporty”).
- Skróty z `docs/UI-GUIDELINES.md` (`N`, `F`, `?`, `Alt+↑/↓` firma), okno pomocy ze skrótami; wyłączone podczas pisania w polach.

## Kryteria akceptacji

- [x] Wyniki dotyczą wyłącznie bieżącej firmy (test izolacji).
- [x] `Ctrl+K` → wpisanie numeru faktury → `Enter` otwiera fakturę (E2E).
- [x] Skróty nie reagują w polach tekstowych; `?` otwiera pomoc.

## Wskazówki techniczne

- Wyszukiwanie przez `EF.Functions.Like` z parametryzacją; wartości użytkownika nigdy bez parametrów.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wyszukiwanie pełnotekstowe po załącznikach.

## Dziennik zadania

- **Backend:** `GET /api/v1/search?q=` (`Application/Features/Search/GlobalSearch.cs`, endpoint w `Api/Features/Search`) – konta (kod/nazwa), kontrahenci (nazwa, NIP także bez separatorów), faktury (numer, z nazwą kontrahenta i kwotą), księgowania (numer/opis); limit 5 na kategorię, minimum 2 znaki, maksimum 100. Wzorzec LIKE jest parametryzowany, a `%`, `_` i `\` z zapytania są escapowane (testowane: „10%” znajduje dosłowny procent, „%%” nic nie zwraca). Dostępne dla każdej zalogowanej roli (tylko odczyt); firma wynika z nagłówka `X-Company-Id`, obca firma → 403.
- **UI:** `CommandPalette.tsx` (okno `<dialog>`, combobox + listbox z `aria-activedescendant`, strzałki/Enter/Esc, wyszukiwanie z opóźnieniem 150 ms i odrzucaniem spóźnionych odpowiedzi; wyniki jednego zapytania nie są stosowane do innego tekstu), polecenia filtrowane bez względu na wielkość liter i polskie znaki, „Przełącz firmę: …”, polecenia zapisu tylko dla Administratora/Księgowego, polecenia administracyjne tylko dla Administratora. `shortcuts.tsx`: Ctrl+K (działa też w polach, przełącza okno), N, F, ?, Alt+↑/↓; jednoklawiszowe skróty wyłączone w polach input/select/textarea/contenteditable i przy otwartym oknie dialogowym. Przyciski „Wyszukiwarka Ctrl+K” i „?” na górnym pasku. Okno pomocy ze skrótami.
- Testy: `SearchTests` (14, w tym izolacja firm, limit, NIP, wildcardy), `04h-search-shortcuts.spec.ts` (Ctrl+K → numer faktury → Enter, grupy wyników, polecenia i nawigacja strzałkami, skróty N/F/?, brak reakcji w polach, rola Podgląd, API 401/krótkie zapytanie), `07-search-companies.spec.ts` (izolacja firm w API i w palecie, „Przełącz firmę”, Alt+↑/↓). Weryfikacja: `dotnet build` 0 błędów, testy jednostkowe 77 + 102, Playwright 81/81.
- Uwaga do testów: użytkownik „Podgląd” z testu 03 jest później awansowany na Księgowego, więc test roli Podgląd tworzy własnego użytkownika (olga.skroty@biuro.test).
- **Nie zrobiono / ryzyka:** przełączenie firmy (Alt+↑/↓ i polecenie) przeładowuje stronę (jak lista „Firma”), więc niezapisane dane formularza przepadają; wyszukiwanie nie obejmuje opisów pozycji faktur ani nazw kontrahentów po numerze faktury; skrót „N”/„F” nie rozpoznaje układów klawiatury innych niż łaciński.
