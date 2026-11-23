# T-004: Zaawansowane tabele i audyt dostępności

- **Epik:** E7 UI/UX
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-003

## Cel

Listy (dziennik, faktury, kontrahenci, plan kont, audyt) mają jednolite, szybkie tabele z filtrami i zapisanymi widokami.

## Zakres

- Komponent `DataTable`: sortowanie po kolumnach, filtry kolumnowe, wybór widocznych kolumn, gęstość (kompaktowa/standardowa), stopka z sumami, stan pusty i błędu, zaznaczanie wierszy, akcje zbiorcze.
- Paginacja i sortowanie po stronie serwera dla dziennika, faktur i audytu (parametry `sort`, `dir`, `page`, `pageSize`); testy sortowania na API.
- Zapisane widoki filtrów per użytkownik (`localStorage`) i eksport bieżącego widoku do CSV (przyciski w tabeli).
- Instalacja `@axe-core/playwright` i test E2E „a11y” dla głównych stron (brak naruszeń serious/critical).

## Kryteria akceptacji

- [ ] Dziennik z ≥ 5 000 księgowań (seed w teście) ładuje stronę < 500 ms (po stronie serwera paginacja).
- [ ] Sortowanie i filtry działają z klawiatury; fokus nie ucieka po odświeżeniu danych.
- [ ] Test axe przechodzi dla: pulpit, dziennik, nowe księgowanie, faktury, plan kont, raporty.

## Wskazówki techniczne

- Sortowanie po stronie serwera: whitelist kolumn (nigdy nazwy kolumn z żądania do SQL).

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wirtualizacja bardzo długich list (> 50 000 wierszy).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
