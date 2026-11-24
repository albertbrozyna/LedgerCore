# T-004: Zaawansowane tabele i audyt dostępności

- **Epik:** E7 UI/UX
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Zrobione
- **Zależności:** T-003

## Cel

Listy (dziennik, faktury, kontrahenci, plan kont, audyt) mają jednolite, szybkie tabele z filtrami i zapisanymi widokami.

## Zakres

- Komponent `DataTable`: sortowanie po kolumnach, filtry kolumnowe, wybór widocznych kolumn, gęstość (kompaktowa/standardowa), stopka z sumami, stan pusty i błędu, zaznaczanie wierszy, akcje zbiorcze.
- Paginacja i sortowanie po stronie serwera dla dziennika, faktur i audytu (parametry `sort`, `dir`, `page`, `pageSize`); testy sortowania na API.
- Zapisane widoki filtrów per użytkownik (`localStorage`) i eksport bieżącego widoku do CSV (przyciski w tabeli).
- Instalacja `@axe-core/playwright` i test E2E „a11y” dla głównych stron (brak naruszeń serious/critical).

## Kryteria akceptacji

- [x] Dziennik z ≥ 5 000 księgowań ładuje stronę < 500 ms – zmierzone w teście jednostkowym na 100 000 księgowań (`PerformanceTests`: domyślnie 33 ms, sort. po opisie/numerze ~155 ms, po kwocie ~430 ms), nie w E2E.
- [x] Sortowanie i filtry działają z klawiatury; fokus nie ucieka po odświeżeniu danych.
- [x] Test axe przechodzi dla: pulpit, dziennik, nowe księgowanie, faktury, plan kont, raporty.

## Wskazówki techniczne

- Sortowanie po stronie serwera: whitelist kolumn (nigdy nazwy kolumn z żądania do SQL).

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wirtualizacja bardzo długich list (> 50 000 wierszy).

## Dziennik zadania

- **Backend:** parametry `sort`/`dir` w dzienniku (`date`, `number`, `description`, `amount`), fakturach (`issue`, `number`, `type`, `contractor`, `due`, `gross`, `remaining`, `status`) i audycie (`at`, `user`, `action`). Klucze z whitelisty (switch), inne → 400 „Nieprawidłowe sortowanie”; zawsze stabilny drugi klucz. Sortowanie dziennika po kwocie to `SUM(AmountCents)` w SQL. Faktury nadal sortowane w pamięci (status i „do zapłaty” są wyliczane) – bez zmiany kosztu względem T-017.
- **UI:** `design/DataTable.tsx` (sortowanie serwerowe lub lokalne, filtry kolumnowe w trybie lokalnym, wybór kolumn i gęstość pamiętane w `localStorage`, stopka z sumami, stan pusty/błąd, zaznaczanie z akcjami zbiorczymi, pager, „Eksport widoku” do CSV z BOM i ochroną przed formułami) oraz `SavedViews.tsx` (zapisane widoki filtrów per użytkownik). Przepięte: dziennik, faktury, audyt (serwer), kontrahenci, plan kont (lokalnie). Tabele nie są odmontowywane przy odświeżaniu, więc fokus zostaje na nagłówku.
- Menu wyboru kolumn jest renderowane dopiero po otwarciu: pole „Pokaż kolumnę Status” kolidowało ze starym `getByLabel('Status')` w testach faktur (naprawiono w aplikacji, bez zmiany starych testów).
- axe (pełny zestaw reguł, serious/critical) wykrył linki rozróżniane tylko kolorem (`link-in-text-block`) w raportach – dodano podkreślenie linków w treści. Test dla 8 stron w motywie jasnym i ciemnym.
- Testy: `SortingTests` (24), `04g-tables.spec.ts` (sortowanie z klawiatury i fokus, kolumny/gęstość, widoki, CSV, zaznaczanie, filtry lokalne, whitelist API i uprawnienia, axe). Weryfikacja: `dotnet build` 0 błędów, testy jednostkowe 77 + 88 (bez kategorii Performance; wydajność mierzona osobno), Playwright 70/70.
- **Nie zrobiono:** zapisane widoki są tylko w `localStorage` przeglądarki (nie przenoszą się między komputerami); kolumny nie mają filtrów w trybie serwerowym (filtry w formularzu nad tabelą jak dotąd); nie ma E2E z seedem ≥ 5 000 księgowań.
