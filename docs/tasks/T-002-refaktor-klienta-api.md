# T-002: Jeden spójny klient API i typy w UI

- **Epik:** E8 Jakość
- **Priorytet:** P0 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** brak

## Cel

Posprzątać `src/api.ts` (obecnie obiekty `api`, `apiX`, `apiY`, `apiZ`, `apiC` dopisywane warstwami) i przygotować grunt pod design system.

## Zakres

- Podziel klienta na moduły domenowe w `src/api/`: `http.ts` (fetch, tokeny, odświeżanie, nagłówek firmy, pobieranie plików), `accounting.ts`, `invoicing.ts`, `companies.ts`, `admin.ts`, `reports.ts`, `types.ts`; eksport przez `src/api/index.ts` jako jeden obiekt `api`.
- Zamień wszystkie importy w `src/pages/*` i `src/*.tsx`; usuń `apiX/apiY/apiZ/apiC`.
- Wspólny hook `useLoad` rozszerz o `refetch`, anulowanie przy odmontowaniu i unikanie wyścigów (zmienne `id` w deps).
- Ujednolić obsługę błędów: jedna klasa `ApiError` z `status` i `detail`; `ErrorBox` pokazuje `detail`.

## Kryteria akceptacji

- [ ] Brak zmian w zachowaniu UI: `npm run build` i **cały** zestaw E2E zielony bez zmian w testach.
- [ ] Brak plików > 300 linii w `src/api/`; brak zduplikowanych typów.
- [ ] Odpowiedzi z 401 nadal wylogowują, 403 firmy pokazuje czytelny komunikat.

## Wskazówki techniczne

- Wyścig zauważony wcześniej: `reload()` po `navigate` pobierał dane starego `id` – hook ma ignorować spóźnione odpowiedzi.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Zmiana wyglądu (T-003).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
