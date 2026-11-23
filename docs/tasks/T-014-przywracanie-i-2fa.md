# T-014: Przywracanie kopii z aplikacji i uwierzytelnianie dwuskładnikowe

- **Epik:** E6 Bezpieczeństwo
- **Priorytet:** P2 · **Rozmiar:** M
- **Status:** Do zrobienia
- **Zależności:** brak

## Cel

Bezpieczne odtwarzanie danych bez ręcznego podmieniania plików oraz dodatkowa ochrona kont.

## Zakres

- Przywracanie kopii z listy w Administracji: tryb serwisowy (blokada ruchu), kopia bezpieczeństwa bieżącej bazy przed podmianą, weryfikacja nagłówka SQLite i wersji migracji, restart bazy.
- TOTP (RFC 6238): włączanie w „Moje konto” (kod QR/sekret + kody awaryjne), wymaganie kodu przy logowaniu, wyłączanie przez administratora.
- Wpisy w audycie dla obu operacji.

## Kryteria akceptacji

- [ ] Po przywróceniu kopii dane wracają do stanu z kopii (test: dodaj dane → kopia → dodaj więcej → przywróć → brak „więcej”).
- [ ] Plik, który nie jest bazą LedgerCore, jest odrzucany bez zmian w danych.
- [ ] Logowanie z 2FA wymaga poprawnego kodu; kody awaryjne działają jednorazowo; blokada po wielu błędnych kodach.

## Wskazówki techniczne

- Przywracanie wymaga zamknięcia połączeń do pliku – rozważ restart procesu przez skrypt startowy.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Logowanie przez zewnętrznego dostawcę tożsamości.

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
