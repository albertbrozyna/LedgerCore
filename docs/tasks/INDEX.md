# Indeks zadań

Statusy: **Do zrobienia** · **W toku** · **Zablokowane** · **Zrobione**. Po zakończeniu zadania zmień status tutaj **i** w pliku zadania.
Wybieraj zadania od góry (priorytet), pod warunkiem że wszystkie zależności są **Zrobione**. Proces: [`README.md`](README.md).

| ID | Zadanie | Epik | Prio | Rozmiar | Zależności | Status |
|---|---|---|---|---|---|---|
| [T-001](T-001-ci-i-testy-jednostkowe.md) | CI oraz testy jednostkowe domeny i aplikacji | E8 Jakość | P0 | M | — | Zrobione |
| [T-002](T-002-refaktor-klienta-api.md) | Jeden spójny klient API i typy w UI | E8 Jakość | P0 | M | — | Zrobione |
| [T-003](T-003-design-system.md) | Design system: tokeny, ciemny motyw, komponenty bazowe | E7 UI/UX | P0 | L | T-002 | Zrobione |
| [T-017](T-017-wydajnosc-agregacji.md) | Wydajność: agregacje w SQL i skalowanie raportów | E8 Jakość | P0 | L | T-001 | Zrobione |
| [T-004](T-004-tabele-i-dostepnosc.md) | Zaawansowane tabele i audyt dostępności | E7 UI/UX | P1 | L | T-003 | Do zrobienia |
| [T-005](T-005-wyszukiwarka-i-skroty.md) | Globalna wyszukiwarka, paleta poleceń i skróty klawiaturowe | E7 UI/UX | P1 | M | T-003 | Do zrobienia |
| [T-006](T-006-szybkie-ksiegowanie.md) | Szybkie księgowanie sterowane klawiaturą | E7 UI/UX | P1 | M | T-003 | Do zrobienia |
| [T-007](T-007-kpir-i-ryczalt.md) | Księga przychodów i rozchodów (KPiR) oraz ewidencja ryczałtu | E1 Rozliczenia PL | P1 | L | T-001 | Do zrobienia |
| [T-009](T-009-wezwania-odsetki-email.md) | Wezwania do zapłaty, noty odsetkowe i wysyłka e-mail | E3 Sprzedaż i dokumenty | P1 | M | — | Do zrobienia |
| [T-010](T-010-faktury-cykliczne-numeracja.md) | Faktury cykliczne i konfigurowalna numeracja | E3 Sprzedaż i dokumenty | P1 | M | — | Do zrobienia |
| [T-011](T-011-wyciagi-bankowe-i-uzgodnienie.md) | Wyciągi bankowe: więcej formatów, deduplikacja, reguły, uzgodnienie | E4 Bank | P1 | L | T-001 | Do zrobienia |
| [T-012](T-012-zadania-i-terminy-klientow.md) | Zadania i terminy klientów (workflow biura) | E5 Praca biura | P1 | L | T-003 | Do zrobienia |
| [T-015](T-015-ksef-fa3.md) | KSeF: generowanie faktur FA(3) i eksport | E1 Rozliczenia PL | P1 | L | T-001 | Zablokowane |
| [T-016](T-016-jpk-v7m.md) | JPK_V7M: ewidencja VAT i deklaracja – eksport XML | E1 Rozliczenia PL | P1 | L | T-015 | Zablokowane |
| [T-008](T-008-srodki-trwale.md) | Środki trwałe i amortyzacja | E2 Księga rozszerzona | P2 | L | T-001 | Do zrobienia |
| [T-013](T-013-raporty-zbiorcze-i-eksport-firmy.md) | Raporty zbiorcze po firmach oraz eksport/import pojedynczej firmy | E5 Praca biura | P2 | L | T-001 | Do zrobienia |
| [T-014](T-014-przywracanie-i-2fa.md) | Przywracanie kopii z aplikacji i uwierzytelnianie dwuskładnikowe | E6 Bezpieczeństwo | P2 | M | — | Do zrobienia |
| [T-018](T-018-waluty-i-kursy.md) | Waluty obce i kursy NBP | E2 Księga rozszerzona | P2 | L | T-001 | Do zrobienia |
| [T-019](T-019-migracja-formularzy-na-design.md) | Migracja formularzy na komponenty design systemu | E7 UI/UX | P2 | M | T-003 | Do zrobienia |

## Sugerowana kolejność

1. **T-001** (CI + testy jednostkowe) i **T-002** (porządek w kliencie API) – bezpieczny fundament, można równolegle.
2. **T-003** (design system) → **T-004**, **T-005**, **T-006** (tabele, wyszukiwarka, szybkie księgowanie).
3. **T-017** (wydajność) zanim biuro wgra duże dane; **T-007** (KPiR/ryczałt) – największa wartość dla typowych klientów.
4. **T-009**, **T-010**, **T-011**, **T-012**, **T-013**; później **T-008**, **T-018**, **T-014**.
5. **T-015/T-016** dopiero po dostarczeniu XSD.
