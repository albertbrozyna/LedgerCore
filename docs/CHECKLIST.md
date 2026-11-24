# Checklista: mocny program księgowy dla małego biura

Plan prac i zadania: [`ROADMAP.md`](ROADMAP.md), [`tasks/INDEX.md`](tasks/INDEX.md).

Legenda: ✅ zrobione · ⬜ później · ⛔ zablokowane zewnętrznie

## Księga główna
- ✅ Plan kont (dodawanie, dezaktywacja), zasada podwójnego zapisu, kontrola bilansu
- ✅ Dziennik księgowań z filtrami, wyszukiwaniem i numeracją
- ✅ Storno zamiast usuwania, niemodyfikowalny dziennik
- ✅ Karta konta z saldem narastającym, zestawienie obrotów i sald
- ✅ Rachunek wyników, bilans
- ✅ Zamknięcie okresu i roku obrotowego, salda otwarcia nowego roku
- ✅ Szablony księgowań (powtarzalne operacje jednym kliknięciem)
- ⬜ Konta wielowalutowe i kursy walut
- ⬜ Środki trwałe i amortyzacja
- ⬜ Budżet vs wykonanie

## Faktury i VAT
- ✅ Faktury sprzedaży i zakupu, stawki VAT, automatyczne księgowanie
- ✅ Płatności częściowe i pełne, anulowanie faktury
- ✅ Rejestry VAT, rozliczenie VAT należny/naliczony
- ✅ Wydruk faktury (PDF), kwota słownie
- ✅ Załączniki do faktur (skany/PDF dostawców)
- ✅ Faktury korygujące (in minus i in plus, zmiana stawki VAT, księgowanie ze znakiem, wpływ na VAT i rozrachunki)
- ⬜ Wezwania do zapłaty, noty odsetkowe
- ⛔ KSeF (FA(3)) i JPK_V7M – wymagają oficjalnych XSD (brak dostępu do crd.gov.pl)

## Rozrachunki i bank
- ✅ Rozrachunki z wiekowaniem, salda kontrahentów
- ✅ Import wyciągu bankowego (CSV) z automatycznym dopasowaniem płatności do faktur
- ⬜ Karta kontrahenta (historia rozliczeń)

## Kontrola i bezpieczeństwo
- ✅ Role (Administrator / Księgowy / Podgląd), blokada kont, blokada po błędnych logowaniach
- ✅ Dziennik audytu
- ✅ Kopia zapasowa na żądanie
- ✅ Automatyczne kopie zapasowe (codziennie, z rotacją) i lista kopii w aplikacji
- ✅ Zmiana własnego hasła
- ⬜ Uwierzytelnianie dwuskładnikowe
- ⬜ Przywracanie kopii z poziomu aplikacji

## Pulpit i organizacja pracy
- ✅ Pulpit: salda, wynik miesiąca, rozrachunki, ostatnie księgowania
- ✅ Wykres przychodów i kosztów w ujęciu miesięcznym
- ✅ Kalendarz terminów podatkowych (VAT, ZUS, zaliczki) na pulpicie

## Wielofirmowość (biuro obsługuje wielu klientów)
- ✅ Wiele firm w jednej instalacji: osobny plan kont, księgowania, faktury, kontrahenci, raporty, zamknięcia okresów i audyt dla każdej firmy
- ✅ Przełącznik firmy, zarządzanie firmami (dodawanie, zmiana nazwy, dezaktywacja)
- ✅ Dostęp użytkowników do wybranych firm (Administrator widzi wszystkie)
- ✅ Migracja istniejących danych jednofirmowych do "firmy domyślnej"
- ⬜ Konsolidacja i raporty zbiorcze po wszystkich firmach
- ⬜ Kopia/eksport pojedynczej firmy (obecnie kopia obejmuje całą bazę ze wszystkimi firmami)

## Jakość
- ✅ Testy jednostkowe domeny i aplikacji (xUnit, SQLite w pamięci), w tym izolacja firm
- ✅ Pipeline CI (GitHub Actions): build, testy jednostkowe, build UI, E2E
- ✅ Design system: tokeny, ciemny motyw, komponenty bazowe, modale zamiast okien przeglądarki, nawigacja grupowana
- ✅ Raporty i pulpit liczone w SQL (200 000 zapisów: pulpit ~0,3 s), karta konta ze stronicowaniem
- ✅ Tabele: sortowanie po stronie serwera, filtry, wybór kolumn, zapisane widoki, eksport widoku CSV, audyt dostępności (axe)
- ✅ Globalna wyszukiwarka i paleta poleceń (Ctrl+K), skróty klawiaturowe z pomocą (N, F, ?, Alt+↑/↓)
- ✅ Szybkie księgowanie z klawiatury: autouzupełnianie kont, Ctrl+Enter, podgląd sald, „Kopiuj jako nowe”
- ✅ KPiR i ryczałt: tryb ewidencji firmy, księga przychodów i rozchodów (kolumny 7–17), ewidencja przychodów ryczałtu, eksport CSV i druk
