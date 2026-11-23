# T-008: Środki trwałe i amortyzacja

- **Epik:** E2 Księga rozszerzona
- **Priorytet:** P2 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-001

## Cel

Rejestr środków trwałych z planem amortyzacji i automatycznym księgowaniem odpisów.

## Zakres

- Encje `FixedAsset` (nazwa, nr inwentarzowy, data przyjęcia, wartość początkowa, stawka/okres, metoda: liniowa, konto środka, konto umorzenia, konto kosztu) i `DepreciationEntry`.
- Rejestr + formularz przyjęcia (opcjonalnie z faktury zakupu) + likwidacja/sprzedaż.
- Generowanie odpisów miesięcznych za wskazany okres (jedno księgowanie zbiorcze), blokada podwójnego naliczenia, zgodność z blokadą okresu.
- Raport „Plan amortyzacji” i „Wartość netto środków trwałych” na dzień.

## Kryteria akceptacji

- [ ] Odpisy dla N środków za miesiąc księgują się jednym dokumentem Wn koszt / Ma umorzenie i zgadzają się z planem (test z ręcznie wyliczonymi kwotami).
- [ ] Drugie naliczenie tego samego miesiąca jest odrzucone; w zamkniętym okresie – odrzucone.
- [ ] Środek w pełni umorzony przestaje być naliczany; zaokrąglenia nie powodują nadmiernego umorzenia (ostatni odpis koryguje różnicę).

## Wskazówki techniczne

- Dodaj domyślne konta (013/071/401-amortyzacja) do planu kont nowych firm i opcjonalny „dopisz brakujące konta” dla istniejących.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Amortyzacja degresywna, amortyzacja jednorazowa podatkowa (kolejne zadania).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
