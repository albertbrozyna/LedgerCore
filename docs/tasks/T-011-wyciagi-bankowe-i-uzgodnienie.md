# T-011: Wyciągi bankowe: więcej formatów, deduplikacja, reguły, uzgodnienie

- **Epik:** E4 Bank
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-001

## Cel

Import wyciągu ma być bezpieczny (bez dublowania), obsługiwać typowe formaty PL i pozwalać uzgodnić saldo z bankiem.

## Zakres

- Serwerowy zapis zaimportowanych operacji (`BankTransaction`: konto, data, kwota, tytuł, kontrahent, skrót unikalny, status: nowa/zaksięgowana/pominięta) i **deduplikacja** po skrócie (data+kwota+tytuł+numer rachunku).
- Parsery: CSV (obecny), MT940, Elixir-0, oraz profile CSV popularnych banków (wybór profilu przy imporcie).
- Reguły dopasowania zapisywane per firma (np. „tytuł zawiera X → konto/kontrahent Y”), także księgowanie bezpośrednie na konto (opłaty bankowe, odsetki).
- Uzgodnienie: saldo końcowe wyciągu vs saldo konta w księdze na dzień, lista różnic.

## Kryteria akceptacji

- [ ] Ponowny import tego samego pliku nie tworzy drugiego zestawu operacji ani drugich płatności (test).
- [ ] Dla MT940 i Elixir-0 istnieją pliki testowe i testy parsera (kwoty, daty, tytuły z polskimi znakami).
- [ ] Reguła „opłata bankowa” księguje operację na konto kosztowe bez faktury.
- [ ] Raport uzgodnienia pokazuje zgodność lub różnicę z listą niezaksięgowanych operacji.

## Wskazówki techniczne

- Zacznij od modelu i deduplikacji, parsery dodawaj jako wtyczki o wspólnym interfejsie.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Integracje bankowe online (PSD2).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
