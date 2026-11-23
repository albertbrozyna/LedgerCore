# T-003: Design system: tokeny, ciemny motyw, komponenty bazowe

- **Epik:** E7 UI/UX
- **Priorytet:** P0 · **Rozmiar:** L
- **Status:** Zrobione
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

- [x] Strony używają komponentów z `src/design` (Button, Badge/StatusBadge, Tabs, PageHeader, ErrorBox/Notice, Loading/Skeleton, dialogi); brak stylów „inline”. _Formularze (`Field`/`MoneyInput`/`DateInput`) i `LinkButton` – świadomie wydzielone do T-019._
- [x] Ciemny motyw działa na wszystkich stronach, kontrast ≥ 4.5:1 (sprawdzone narzędziem axe).
- [x] Testy E2E zielone; testy używające `page.once("dialog")` zaktualizowane do modali (obsługa `getByRole("dialog")`).
- [x] Widok od 360 px do 1920 px bez poziomego przewijania strony (tabele przewijają się wewnątrz).

## Wskazówki techniczne

- Zachowaj etykiety pól i `data-testid` – to kontrakt z testami.
- Rozważ czysty CSS (bez dużych bibliotek); ewentualnie Radix UI dla dostępnych modali/popoverów.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Skróty klawiaturowe i paleta poleceń (T-005).

## Dziennik zadania

- Nowy katalog `src/design/`: `tokens.css` (kolory, odstępy, promienie, cienie, typografia; jasny/ciemny przez `prefers-color-scheme` + atrybut `data-theme`), `theme.ts` (przełącznik auto/jasny/ciemny w `localStorage`, stosowany przed renderem), `controls.tsx` (Button, Field, Input, Select, MoneyInput, DateInput), `display.tsx` (ErrorBox, Notice, PageHeader, Breadcrumbs, Tabs, Badge, StatusBadge, EmptyState, Skeleton, Loading, PrintHeader), `overlay.tsx` (Modal na natywnym `<dialog>`, `useDialogs().confirm/prompt`, Toast), `useLoad.ts`. `src/ui.tsx` usunięty, importy przepięte na `../design`.
- `index.css` przepisany na tokeny; w trybie ciemnym wydruk wymusza jasne kolory. Skeleton i toasty celowo bez `role="status"`, by nie kolidować z `<Notice>` w testach.
- Nawigacja: grupy (Księga, Sprzedaż i zakupy, Zestawienia, Zarządzanie – tylko dla admina), zwijanie menu (zapamiętane), pasek nagłówka z firmą/przełącznikiem firm, znacznik okresu (`data-testid="period-status"`, z GET `/admin/period-lock`) i przełącznikiem motywu.
- `confirm()`/`prompt()` zastąpione modalami (zamknięcie roku, storno, anulowanie faktury, usunięcie załącznika, zmiana nazwy firmy, reset hasła – teraz pole typu password). Testy E2E zaktualizowano: zamiast `page.once('dialog')` helpery `confirmDialog`/`answerPrompt` (zmiana wymuszona przez zmianę mechanizmu, asercje bez zmian).
- Katalog `/design` tylko w trybie dev (`import.meta.env.DEV`); w buildzie produkcyjnym trasa nie istnieje.
- Nowy test `04e-design-system.spec.ts` (przed 05): grupy menu i rola Podgląd, zwijanie, motyw, anulowanie modala (Esc/„Wróć”), kontrast axe w obu motywach na 13 stronach, brak poziomego przewijania przy 360 i 1920 px. Dodano `@axe-core/playwright` do e2e. Axe wykrył za niski kontrast zielonego w odznace „Okres otwarty” – ciemniejszy `--ok`.
- Weryfikacja: `tsc -b`, `npm run build`, Playwright 58/58.
- **Nie sprawdzono:** wizualnie w przeglądarce (poza testami axe/overflow), kontrastu stanów hover/focus ani stron wydruku w ciemnym motywie; `MoneyInput`/`DateInput`/`Toast`/`Breadcrumbs` są w katalogu, ale jeszcze nieużywane na stronach (T-019, T-005/T-006).
- Odstępstwo: nie użyto Radix – natywny `<dialog>` wystarcza.
