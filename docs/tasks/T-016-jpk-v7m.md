# T-016: JPK_V7M: ewidencja VAT i deklaracja – eksport XML

- **Epik:** E1 Rozliczenia PL
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Zablokowane
- **Zależności:** T-015
- **Blokada:** Brak oficjalnych XSD JPK_V7M(3) (crd.gov.pl niedostępny). Użytkownik musi dodać pliki do `schemas/jpk/` i potwierdzić obowiązującą wersję.

## Cel

Plik JPK_V7M zgodny z aktualną strukturą, zbudowany z rejestrów VAT, z walidacją XSD.

## Zakres

- Mapowanie pól K_/P_ (podstawy i VAT wg stawek, nabycia, korekty, oznaczenia GTU/procedur – zakres uzgodnić) z faktur sprzedaży/zakupu.
- Generowanie nagłówka, ewidencji i deklaracji z walidacją XSD; podgląd różnic względem rejestrów VAT w aplikacji.
- Dokładna lista obsługiwanych przypadków i wyraźna lista nieobsługiwanych (z ostrzeżeniem w UI).

## Kryteria akceptacji

- [ ] Plik dla zestawu testowych faktur przechodzi walidację XSD, a sumy w deklaracji zgadzają się z rejestrami VAT.
- [ ] Przypadki nieobsługiwane blokują eksport z komunikatem (zamiast cichego pominięcia).

## Wskazówki techniczne

- Reguły podatkowe w osobnej klasie z datą obowiązywania i źródłem.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wysyłka JPK do urzędu, JPK_FA, JPK_KR.

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
