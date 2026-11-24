# T-007: Księga przychodów i rozchodów (KPiR) oraz ewidencja ryczałtu

- **Epik:** E1 Rozliczenia PL
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Zrobione
- **Zależności:** T-001

## Cel

Większość małych klientów biura to osoby prowadzące KPiR lub ryczałt – trzeba je obsłużyć obok pełnej księgowości.

## Zakres

- Tryb księgowości firmy (`Company.AccountingMode`: `FullBooks` / `Kpir` / `Ryczalt`) ustawiany przy tworzeniu firmy; migracja z domyślnym `FullBooks`.
- Ewidencja KPiR: kolumny wg wzoru (lp., data zdarzenia, nr dowodu, kontrahent, opis, przychód: wartość sprzedanych towarów i usług / pozostałe, koszty: zakup towarów, uboczne koszty zakupu, wynagrodzenia, pozostałe wydatki, B+R), sumy miesięczne i narastające, eksport CSV i druk.
- Dokumenty (faktury sprzedaży/zakupu) w trybie KPiR zapisują się do ewidencji bez księgowania na konta; w trybie ryczałtu – ewidencja przychodów ze stawkami ryczałtu.
- Zestawienia: przychód/koszt/dochód narastająco, zaliczka na podatek (informacyjnie).

## Kryteria akceptacji

- [x] Firma w trybie KPiR nie widzi planu kont/bilansu w menu (tylko właściwe pozycje), firma `FullBooks` zachowuje pełny interfejs.
- [x] Sumy KPiR zgadzają się z fakturami (test: 3 faktury sprzedaży i 2 zakupu → oczekiwane sumy w kolumnach).
- [x] Wydruk KPiR ma nagłówek firmy i okres; eksport CSV otwiera się w Excelu.
- [x] Zmiana trybu istniejącej firmy z danymi jest blokowana lub wymaga potwierdzenia z opisem skutków.

## Wskazówki techniczne

- Zacznij od analizy przepisów (rozporządzenie o KPiR) i opisz źródło w komentarzu; reguły trzymaj w osobnej klasie.
- Nie dubluj faktur – ewidencja to widok nad `Invoice`, nie kopia.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Deklaracje PIT i JPK_PKPIR (wymagają XSD).

## Dziennik zadania

- **Tryb firmy:** `Company.AccountingMode` (`FullBooks` / `Kpir` / `Ryczalt`), migracja `AccountingModes` (kolumna z domyślną wartością 0 = pełna księgowość; sprawdzona na bazie z poprzedniego kodu: konta, obroty, wyniki, bilans, pulpit identyczne). Tryb ustawia się przy tworzeniu firmy (Firmy → „Tryb ewidencji”; KPiR i ryczałt nie dostają planu kont) albo w Administracji **tylko dla firmy bez faktur i księgowań** – inaczej 400 „załóż nową firmę” (świadoma decyzja: dane z jednego trybu nie przeliczają się bezpiecznie na inny, więc zamiast potwierdzenia z opisem skutków jest blokada).
- **Ewidencja jako widok nad `Invoice`** (`Features/Registers`): faktura w KPiR/ryczałcie ma `KpirKind` (KPiR) lub `RyczaltRate` (ryczałt, sprzedaż) i **nie tworzy księgowania** (`TransactionId = null`, `ResultAccountId = Guid.Empty`); płatności bez konta (`AccountId/TransactionId = Guid.Empty`, w UI „—”); anulowanie bez storna; korekty ze znakiem w dacie wystawienia korekty. `CreateTransaction`, `ReverseTransaction` i `CloseYear` odrzucają firmy poza pełną księgowością (komunikat z nazwą trybu).
- **KPiR:** kolumny wg wzoru rozporządzenia (7, 8, 9 – przychody; 10, 11, 12, 13, 14 – wydatki; 16 – B+R; 17 – uwagi), lp. liczone od początku roku, sumy miesięczne i narastające, dochód oraz informacyjna zaliczka wg 19% (forma opodatkowania podatnika nie jest przechowywana). Domyślnie sprzedaż → kol. 7, zakup → kol. 13; wybór rodzaju w formularzu faktury. Data zdarzenia: data sprzedaży (sprzedaż), data wystawienia (zakup, korekty). **Ryczałt:** przychody netto wg stawek (2, 3, 5,5, 8,5, 10, 12, 12,5, 14, 15, 17, 20%), podatek informacyjnie od sum wg stawek, zaokrąglony do pełnych zł; zakupy nie wchodzą do ewidencji.
- **UI:** strona „Ewidencja” (KPiR / ryczałt) z wyborem roku i miesięcy, tabelą (DataTable: kolumny, eksport CSV z BOM i średnikami, druk z nagłówkiem firmy i okresem), podsumowaniem miesięcznym; menu, trasy, skróty i paleta ukrywają plan kont, dziennik, nowe księgowanie i import wyciągu poza pełną księgowością (trasy przekierowują na pulpit); pulpit pokazuje tylko rozrachunki i terminy; raporty – tylko rejestry VAT i rozrachunki; administracja – bez zamknięcia roku.
- **Testy:** `RegistersTests` (18; m.in. 3 sprzedaże + 2 zakupy → oczekiwane sumy, brak księgowań, B+R, daty zdarzeń i lp., miesiące narastająco, korekty/anulowanie, blokady operacji na kontach, ryczałt: stawki i zaokrąglenia, izolacja firm, zmiana trybu), `08-kpir-ryczalt.spec.ts` (menu i przekierowania, sumy w UI, CSV, druk, faktura i płatność bez konta, ryczałt, zmiana trybu z potwierdzeniem i blokadą, izolacja, rola Podgląd, axe w obu motywach). Weryfikacja: `dotnet build` 0 błędów, testy jednostkowe 77 + 123, Playwright 96/96.
- **Uproszczenia / nie zrobiono (do rozważenia jako osobne zadania):** brak ręcznych wpisów KPiR bez faktury (wynagrodzenia kol. 12, składki, opłaty bankowe, remanent), brak podziału kosztów na pozycje faktury, VAT niepodlegający odliczeniu nie zwiększa kosztu (kwoty netto), brak eksportu JPK_PKPIR/PIT (poza zakresem – wymaga XSD), zaliczka tylko informacyjna wg 19%, brak rozróżnienia kasowej metody rozliczania przychodów ryczałtu. Import wyciągu bankowego i szablony księgowań są dostępne tylko w pełnej księgowości.
