# T-012: Zadania i terminy klientów (workflow biura)

- **Epik:** E5 Praca biura
- **Priorytet:** P1 · **Rozmiar:** L
- **Status:** Do zrobienia
- **Zależności:** T-003

## Cel

Biuro widzi, co jest do zrobienia u którego klienta i w jakim terminie – zamiast notatek w arkuszu.

## Zakres

- Encja `OfficeTask` (firma, tytuł, opis, termin, status: do zrobienia/w toku/zrobione, przypisany użytkownik, typ: VAT/JPK, ZUS, PIT/CIT, bilans, inne, powtarzalność).
- Generator terminów podatkowych dla firmy na wybrany okres wg trybu rozliczeń (zasady w jednym miejscu, z datą obowiązywania i uwagą o weryfikacji).
- Widok „Moje zadania” (wszystkie firmy użytkownika, filtr po statusie/terminie/kliencie), widok tablicy per firma, powiadomienie o zbliżających się terminach na pulpicie.
- Komentarze do zadań i wpis w audycie zmian statusu.

## Kryteria akceptacji

- [ ] Użytkownik widzi wyłącznie zadania firm, do których ma dostęp (test uprawnień i izolacji).
- [ ] Generator nie duplikuje już istniejących zadań tego samego typu i okresu.
- [ ] Zaległe zadania są oznaczone, a pulpit pokazuje ich liczbę.

## Wskazówki techniczne

- Terminy przesuwaj na dzień roboczy (sobota/niedziela); dodaj listę świąt jako dane, nie kod.

## Testy

- E2E w `e2e/tests/` (uwaga na kolejność plików – zob. `CLAUDE.md`): ścieżka szczęśliwa, walidacje, rola „Podgląd”, izolacja firm.
- Testy jednostkowe reguł (jeśli istnieje projekt testowy z T-001).

## Poza zakresem

- Powiadomienia e-mail (łączy się z T-009).

## Dziennik zadania

_(uzupełnij podczas pracy: decyzje, odstępstwa, co zweryfikowano)_
