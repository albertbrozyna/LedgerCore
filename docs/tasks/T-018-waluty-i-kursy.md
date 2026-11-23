# T-018: Waluty obce i kursy NBP

- **Epik:** E2 Księga rozszerzona
- **Priorytet:** P2 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-001

## Cel

Faktury i płatności w walutach obcych z przeliczeniem na PLN po kursie z dnia roboczego poprzedzającego i różnicami kursowymi.

## Zakres

- Waluta na fakturze i płatności, kurs (ręczny lub import z tabeli A NBP – opcjonalnie online, z buforem w bazie), przeliczenie VAT na PLN.
- Księgowanie w PLN z zachowaniem kwoty w walucie; różnice kursowe przy rozliczeniu płatności (konta 755/759 w domyślnym planie).
- Rozrachunki i rejestry VAT w PLN, widok w walucie na karcie faktury.

## Kryteria akceptacji

- [ ] Faktura EUR 1 000 po kursie 4,30 księguje 4 300 zł; zapłata po 4,40 tworzy różnicę kursową 100 zł dodatnią (test).
- [ ] Brak kursu na dzień blokuje wystawienie z czytelnym komunikatem lub pozwala wpisać kurs ręcznie.
- [ ] Dotychczasowe faktury (PLN) działają bez zmian.

## Wskazówki techniczne

- Reguła kursu: ostatni dzień roboczy poprzedzający dzień wystawienia/sprzedaży – zapisz źródło w komentarzu i w danych kursu.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wycena bilansowa rozrachunków walutowych na koniec roku (kolejne zadanie).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
