# T-002: Jeden spójny klient API i typy w UI

- **Epik:** E8 Jakość
- **Priorytet:** P0 · **Rozmiar:** M
- **Status:** Zrobione
- **Zależności:** brak

## Cel

Posprzątać `src/api.ts` (obecnie obiekty `api`, `apiX`, `apiY`, `apiZ`, `apiC` dopisywane warstwami) i przygotować grunt pod design system.

## Zakres

- Podziel klienta na moduły domenowe w `src/api/`: `http.ts` (fetch, tokeny, odświeżanie, nagłówek firmy, pobieranie plików), `accounting.ts`, `invoicing.ts`, `companies.ts`, `admin.ts`, `reports.ts`, `types.ts`; eksport przez `src/api/index.ts` jako jeden obiekt `api`.
- Zamień wszystkie importy w `src/pages/*` i `src/*.tsx`; usuń `apiX/apiY/apiZ/apiC`.
- Wspólny hook `useLoad` rozszerz o `refetch`, anulowanie przy odmontowaniu i unikanie wyścigów (zmienne `id` w deps).
- Ujednolić obsługę błędów: jedna klasa `ApiError` z `status` i `detail`; `ErrorBox` pokazuje `detail`.

## Kryteria akceptacji

- [x] Brak zmian w zachowaniu UI: `npm run build` i **cały** zestaw E2E zielony bez zmian w testach.
- [x] Brak plików > 300 linii w `src/api/`; brak zduplikowanych typów.
- [x] Odpowiedzi z 401 nadal wylogowują, 403 firmy pokazuje czytelny komunikat.

## Wskazówki techniczne

- Wyścig zauważony wcześniej: `reload()` po `navigate` pobierał dane starego `id` – hook ma ignorować spóźnione odpowiedzi.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Zmiana wyglądu (T-003).

## Dziennik zadania

- `src/api.ts` zastąpiony katalogiem `src/api/`: `http.ts`, `types.ts`, `accounting.ts`, `invoicing.ts`, `reports.ts`, `companies.ts`, `admin.ts`, `index.ts` (największy plik 120 linii). Jeden obiekt `api`; `apiX/Y/Z/C` usunięte, importy w stronach zamienione skryptem.
- `ApiError` ma teraz `detail` i `status`; 403 bez treści daje komunikat „Brak uprawnień do tej operacji lub do wybranej firmy”, 404 – „Nie znaleziono żądanego zasobu”.
- `useLoad`: dodano `refetch` (alias `reload` zostaje), numerowanie żądań ignoruje spóźnione odpowiedzi po zmianie deps/odmontowaniu; `fn` trzymane w refie.
- Upload załącznika korzysta ze wspólnych `authHeaders`/`upload` (dotąd duplikat logiki nagłówków).
- Weryfikacja: `tsc -b` i `npm run build` czyste, Playwright 50/50 bez zmian w testach. `eslint` ma 5 błędów istniejących przed zmianą (m.in. setState w efekcie) – nie dotyczy zakresu.
- Nie dodano osobnego testu E2E na wyścig w `useLoad` ani na 403 – brak zmian zachowania; do rozważenia w T-003/T-004.
