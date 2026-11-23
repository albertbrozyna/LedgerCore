# T-003: Design system: tokeny, ciemny motyw, komponenty bazowe

- **Epik:** E7 UI/UX
- **Priorytet:** P0 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-002

## Cel

Spójny, profesjonalny wygląd i biblioteka komponentów zgodna z `docs/UI-GUIDELINES.md`.

## Zakres

- Tokeny w `src/design/tokens.css` (kolory, odstępy, typografia, promienie, cienie) + motyw jasny/ciemny (`prefers-color-scheme` i przełącznik zapamiętany w `localStorage`).
- Komponenty: `Button`, `Field`/`Input`/`Select`, `MoneyInput`, `DateInput`, `Modal`/`ConfirmDialog` (zamiast `confirm()`/`prompt()`), `Toast`, `Tabs`, `StatusBadge`, `EmptyState`, `Skeleton`, `PageHeader`, `Breadcrumbs`.
- Przeprojektowana nawigacja: grupy menu, zwijanie, wskaźnik bieżącej firmy i zamkniętego okresu w nagłówku.
- Przepisz istniejące strony na nowe komponenty (bez zmiany logiki); zastąp natywne `confirm()`/`prompt()` modalami.
- Strona `/design` (tylko w trybie dev) z katalogiem komponentów.

## Kryteria akceptacji

- [ ] Wszystkie strony używają komponentów z `src/design`; brak stylów „inline” poza wyjątkami.
- [ ] Ciemny motyw działa na wszystkich stronach, kontrast ≥ 4.5:1 (sprawdzone narzędziem axe).
- [ ] Testy E2E zielone; testy używające `page.once("dialog")` zaktualizowane do modali (obsługa `getByRole("dialog")`).
- [ ] Widok od 360 px do 1920 px bez poziomego przewijania strony (tabele przewijają się wewnątrz).

## Wskazówki techniczne

- Zachowaj etykiety pól i `data-testid` – to kontrakt z testami.
- Rozważ czysty CSS (bez dużych bibliotek); ewentualnie Radix UI dla dostępnych modali/popoverów.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Skróty klawiaturowe i paleta poleceń (T-005).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
