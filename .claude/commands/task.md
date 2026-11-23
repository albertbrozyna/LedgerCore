---
description: Wykonaj zadanie z docs/tasks (np. /task T-003) zgodnie z procesem projektu
argument-hint: "[ID zadania, np. T-003] (puste = wybierz następne z INDEX.md)"
---

Pracujesz nad projektem LedgerCore. Wykonaj zadanie: **$ARGUMENTS** (jeśli puste – wybierz pierwsze zadanie z `docs/tasks/INDEX.md`
o najwyższym priorytecie, które ma status „Do zrobienia” i spełnione zależności; powiedz użytkownikowi, które wybrałeś i dlaczego).

1. Przeczytaj: `CLAUDE.md`, `docs/tasks/README.md`, plik zadania, `docs/ARCHITECTURE.md` (oraz `docs/UI-GUIDELINES.md`, jeśli zadanie dotyka UI).
   Jeśli zadanie jest **Zablokowane** albo brakuje danych/decyzji – zatrzymaj się i zapytaj użytkownika.
2. Ustaw status „W toku” w `docs/tasks/INDEX.md` i w pliku zadania. Rozpisz kroki w liście zadań (domena → migracja → backend → UI → testy → dokumentacja).
3. Zaimplementuj zgodnie z zasadami z `CLAUDE.md` (firmy `ICompanyOwned`, audyt, blokada okresu, uprawnienia, polskie komunikaty, migracje bez utraty danych).
4. Napisz testy E2E (i jednostkowe, jeśli istnieje projekt testowy) dla ścieżki szczęśliwej, walidacji, roli „Podgląd” i izolacji firm.
   Zwróć uwagę na kolejność plików E2E (wzorzec nazw w `CLAUDE.md`).
5. Uruchom pełną weryfikację jak w `/verify`. Wszystko musi być zielone; nie osłabiaj testów, żeby przeszły.
6. Zaznacz kryteria akceptacji w pliku zadania, uzupełnij „Dziennik zadania”, ustaw status „Zrobione”, zaktualizuj `docs/CHECKLIST.md`
   oraz `README.md`/`install/INSTRUKCJA.txt`, jeśli zmienia się użytkowanie.
7. **Nie commituj**, dopóki użytkownik o to nie poprosi (wtedy: `T-0XX: opis` + akapit co i dlaczego).
8. Zakończ uczciwym podsumowaniem: co zrobione, co zweryfikowane (z wynikami testów), co pominięto/ryzyka, jakie nowe zadania warto dopisać.
