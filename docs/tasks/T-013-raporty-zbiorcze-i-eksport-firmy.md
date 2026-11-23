# T-013: Raporty zbiorcze po firmach oraz eksport/import pojedynczej firmy

- **Epik:** E5 Praca biura
- **Priorytet:** P2 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-001

## Cel

Przegląd portfela klientów i możliwość przeniesienia danych jednej firmy (archiwum, zmiana biura).

## Zakres

- Raport zbiorczy (Administrator/uprawnione osoby): per firma przychody, koszty, wynik, należności i zobowiązania po terminie, status zamknięcia okresu – tylko firmy dostępne użytkownikowi.
- Eksport firmy do pliku (`.lcexport`, zip z JSON/CSV: konta, księgowania, kontrahenci, faktury, płatności, załączniki, szablony) z wersją formatu.
- Import do nowej firmy z walidacją spójności (bilans, unikalność, odwołania) i raportem błędów; dane trafiają do wybranej, nowej firmy.

## Kryteria akceptacji

- [ ] Eksport → import tworzy firmę o identycznych saldach, numeracji i raportach (test porównawczy trial balance).
- [ ] Import uszkodzonego pliku niczego nie zmienia (transakcja) i zwraca czytelny błąd.
- [ ] Raport zbiorczy nie ujawnia danych firm bez uprawnień.

## Wskazówki techniczne

- Identyfikatory mapuj na nowe GUID-y przy imporcie, zachowując relacje.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Konsolidacja kapitałowa (łączenie sprawozdań grup).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
