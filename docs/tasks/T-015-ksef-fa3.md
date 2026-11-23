# T-015: KSeF: generowanie faktur FA(3) i eksport

- **Epik:** E1 Rozliczenia PL
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Zablokowane
- **Zależności:** T-001
- **Blokada:** Brak oficjalnych schematów XSD FA(3) (crd.gov.pl niedostępny w środowisku). Użytkownik musi dodać pliki do `schemas/ksef/` i potwierdzić wersję struktury.

## Cel

Wystawianie faktur ustrukturyzowanych zgodnych z FA(3) z walidacją względem oficjalnego schematu.

## Zakres

- Generator XML z modelu `Invoice` (sprzedawca z danych firmy, nabywca, pozycje, stawki VAT, podsumowania, daty, płatność) wraz z korektami.
- **Walidacja XSD w teście i w czasie działania** (`XmlSchemaSet`) – plik, który nie przechodzi walidacji, nie jest oferowany do pobrania.
- Eksport pliku XML z karty faktury; (osobne, późniejsze zadanie: wysyłka przez API KSeF z tokenem/certyfikatem – wymaga środowiska testowego MF).

## Kryteria akceptacji

- [ ] Każda faktura przykładowa (zwykła, wielostawkowa, korekta in minus/plus, zakup) generuje XML zwalidowany przez XSD.
- [ ] Brak wymaganych danych (NIP sprzedawcy/nabywcy, adres) daje czytelny komunikat zamiast niepoprawnego pliku.
- [ ] Dokumentacja opisuje zakres (co jest wspierane, czego nie) i wersję schematu.

## Wskazówki techniczne

- Nie zaczynaj bez XSD. Zapytaj użytkownika o pliki i o potrzebny tryb (offline/online).

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Wysyłka online do KSeF, pobieranie UPO.

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
