# T-010: Faktury cykliczne i konfigurowalna numeracja

- **Epik:** E3 Sprzedaż i dokumenty
- **Priorytet:** P1 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** brak

## Cel

Powtarzalne usługi abonamentowe bez przepisywania oraz numeracja zgodna z zwyczajami klienta.

## Zakres

- Encja `RecurringInvoice` (szablon pozycji, kontrahent, częstotliwość miesięczna/kwartalna/roczna, dzień wystawienia, termin płatności w dniach, następna data, aktywna).
- Generowanie zaległych faktur za dzień (przycisk „Wygeneruj zaległe” + opcjonalna usługa tła przy starcie) z kontrolą blokady okresu i unikaniem duplikatów.
- Konfigurowalny wzór numeru sprzedaży per firma (np. `FV/{RRRR}/{NNNN}`, `{NNN}/{MM}/{RR}`), resetowanie numeracji co rok/miesiąc.

## Kryteria akceptacji

- [ ] Wygenerowanie dwa razy za ten sam okres nie tworzy duplikatów (test).
- [ ] Nieprawidłowy wzór numeru jest odrzucany z komunikatem; numeracja nigdy nie powtarza numeru (indeks unikalny).
- [ ] Faktury wygenerowane mają audyt „cykliczna” i poprawne księgowanie.

## Wskazówki techniczne

- Wzór numeru: parser z jawnym zbiorem znaczników; testy graniczne (zmiana roku).

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wysyłka e-mail wygenerowanych faktur (łączy się z T-009).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
