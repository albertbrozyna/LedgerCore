# T-007: Księga przychodów i rozchodów (KPiR) oraz ewidencja ryczałtu

- **Epik:** E1 Rozliczenia PL
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-001

## Cel

Większość małych klientów biura to osoby prowadzące KPiR lub ryczałt – trzeba je obsłużyć obok pełnej księgowości.

## Zakres

- Tryb księgowości firmy (`Company.AccountingMode`: `FullBooks` / `Kpir` / `Ryczalt`) ustawiany przy tworzeniu firmy; migracja z domyślnym `FullBooks`.
- Ewidencja KPiR: kolumny wg wzoru (lp., data zdarzenia, nr dowodu, kontrahent, opis, przychód: wartość sprzedanych towarów i usług / pozostałe, koszty: zakup towarów, uboczne koszty zakupu, wynagrodzenia, pozostałe wydatki, B+R), sumy miesięczne i narastające, eksport CSV i druk.
- Dokumenty (faktury sprzedaży/zakupu) w trybie KPiR zapisują się do ewidencji bez księgowania na konta; w trybie ryczałtu – ewidencja przychodów ze stawkami ryczałtu.
- Zestawienia: przychód/koszt/dochód narastająco, zaliczka na podatek (informacyjnie).

## Kryteria akceptacji

- [ ] Firma w trybie KPiR nie widzi planu kont/bilansu w menu (tylko właściwe pozycje), firma `FullBooks` zachowuje pełny interfejs.
- [ ] Sumy KPiR zgadzają się z fakturami (test: 3 faktury sprzedaży i 2 zakupu → oczekiwane sumy w kolumnach).
- [ ] Wydruk KPiR ma nagłówek firmy i okres; eksport CSV otwiera się w Excelu.
- [ ] Zmiana trybu istniejącej firmy z danymi jest blokowana lub wymaga potwierdzenia z opisem skutków.

## Wskazówki techniczne

- Zacznij od analizy przepisów (rozporządzenie o KPiR) i opisz źródło w komentarzu; reguły trzymaj w osobnej klasie.
- Nie dubluj faktur – ewidencja to widok nad `Invoice`, nie kopia.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Deklaracje PIT i JPK_PKPIR (wymagają XSD).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
