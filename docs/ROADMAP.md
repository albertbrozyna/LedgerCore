# Roadmapa LedgerCore

Wizja: **najwygodniejszy lokalny program dla małego biura rachunkowego w Polsce** – obsługa wielu klientów, pełna księgowość i
uproszczone ewidencje, faktury z VAT, rozrachunki, bank, raporty i druki, szybki interfejs sterowany z klawiatury,
prosta instalacja i bezpieczne dane (kopie, audyt, role).

Zasady: małe zadania (rozmiar S/M/L), każde z testami E2E i dokumentacją, brak „wielkich wybuchów”, migracje zawsze
z zachowaniem danych. Zadania: `docs/tasks/INDEX.md`. Funkcje i statusy: `docs/CHECKLIST.md`.

## Stan obecny (zrobione)

Księga główna i raporty (obroty/salda, wynik, bilans), faktury sprzedaży/zakupu z VAT, korekty, płatności, rejestry VAT, rozrachunki,
import wyciągu CSV, załączniki, szablony księgowań, zamknięcie okresu i roku, audyt, role, kopie zapasowe, wydruki,
**wielofirmowość**, paczka instalacyjna, 50 testów E2E.

## Epiki i kolejność

| Epik | Cel | Zadania | Priorytet |
|---|---|---|---|
| **E8 Jakość i fundament** | Bezpieczne tempo rozwoju: CI, testy jednostkowe, porządek w kodzie, wydajność | T-001, T-002, T-017 | P0 – najpierw |
| **E7 UI/UX „super”** | Design system, tabele, skróty, szybkie księgowanie, a11y | T-003, T-004, T-005, T-006 | P0/P1 – równolegle z E8 |
| **E1 Rozliczenia PL** | KPiR i ryczałt (najczęstsze formy klientów biura), KSeF, JPK | T-007, T-015*, T-016* | P1 (*zablokowane XSD) |
| **E3 Sprzedaż i dokumenty** | Wezwania, odsetki, e-mail, faktury cykliczne, numeracja | T-009, T-010 | P1 |
| **E4 Bank** | Formaty wyciągów, deduplikacja, reguły, uzgodnienie | T-011 | P1 |
| **E5 Praca biura** | Zadania i terminy klientów, raporty zbiorcze, eksport/import firmy | T-012, T-013 | P1/P2 |
| **E2 Księga rozszerzona** | Środki trwałe, waluty | T-008, T-018 | P2 |
| **E6 Bezpieczeństwo i operacje** | Przywracanie kopii, 2FA | T-014 | P2 |

## Kamienie milowe

- **M1 – Fundament i design (T-001, T-002, T-003, T-017):** CI zielone, testy jednostkowe domeny, jeden klient API,
  design system z ciemnym motywem, raporty skalujące się na dziesiątki tysięcy zapisów.
- **M2 – Praca „bez myszy” (T-004, T-005, T-006):** tabele z filtrami i sortowaniem, paleta poleceń, szybkie księgowanie z klawiatury.
- **M3 – Klienci biura (T-007, T-012, T-013):** KPiR/ryczałt, zadania i terminy klientów, raporty zbiorcze, eksport/import firmy.
- **M4 – Sprzedaż i bank (T-009, T-010, T-011):** wezwania/odsetki/e-mail, faktury cykliczne, lepszy import bankowy z uzgodnieniem.
- **M5 – Księga rozszerzona i bezpieczeństwo (T-008, T-018, T-014).**
- **M6 – Urzędowe pliki (T-015, T-016):** po dostarczeniu oficjalnych XSD (patrz niżej).

## Zależności zewnętrzne i ryzyka

- **KSeF FA(3) i JPK_V7M** wymagają oficjalnych schematów XSD MF. Środowisko pracy nie ma dostępu do `crd.gov.pl`.
  Zanim zaczniesz T-015/T-016, użytkownik musi dodać pliki XSD do `schemas/` (i potwierdzić wersję struktur). Bez walidacji XSD nie generujemy plików dla urzędu.
- **Prawo podatkowe zmienia się** (stawki, terminy, struktury). Reguły podatkowe trzymaj w jednym miejscu (osobne klasy/konfiguracja),
  opisuj datą obowiązywania i źródłem, a w UI zaznaczaj, że narzędzie wspiera, ale nie zastępuje księgowego.
- **Skalowanie raportów:** obecne raporty liczą w pamięci – T-017 przed większymi klientami (setki tysięcy zapisów).
- **Licencja MediatR 14:** do użytku komercyjnego wymagany klucz licencyjny – rozstrzygnąć przed wdrożeniem u klientów (albo zostać przy wersji 12.x).

## Definicja ukończenia (DoD) – dla każdego zadania

1. Spełnione wszystkie kryteria akceptacji z pliku zadania.
2. `dotnet build` bez błędów, `npm run build` bez błędów, **pełny** zestaw `npx playwright test` zielony (w tym nowe testy).
3. Migracja (jeśli jest) zachowuje dane i jest sprawdzona na bazie z poprzedniej wersji.
4. Obsłużone: uprawnienia (Podgląd nie zapisuje), izolacja firm, blokada okresu, wpis w audycie, komunikaty po polsku.
5. UI zgodne z `docs/UI-GUIDELINES.md` (klawiatura, a11y, druk jeśli dotyczy).
6. Zaktualizowane: `docs/tasks/INDEX.md`, `docs/CHECKLIST.md`, `install/INSTRUKCJA.txt`/`README.md` gdy zmienia się użytkowanie.
7. Podsumowanie mówi uczciwie, co zweryfikowano, a czego nie.
