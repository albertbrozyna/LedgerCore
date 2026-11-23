# Jak pracować zadaniami

Każde zadanie to osobny plik `T-0XX-nazwa.md` z celem, zakresem, kryteriami akceptacji i wskazówkami. Lista i statusy: [`INDEX.md`](INDEX.md).
Ogólny plan: [`../ROADMAP.md`](../ROADMAP.md). Zasady techniczne: [`../../CLAUDE.md`](../../CLAUDE.md).

## Cykl zadania

1. **Wybór.** Weź zadanie wskazane przez użytkownika albo pierwsze z indeksu o najwyższym priorytecie, którego zależności są zrobione
   i które nie jest zablokowane. Zmień status na **W toku** (indeks + plik zadania).
2. **Zrozumienie.** Przeczytaj plik zadania, `CLAUDE.md`, `docs/ARCHITECTURE.md` (i `UI-GUIDELINES.md` przy UI) oraz kod, którego dotyka zadanie.
   Jeśli kryterium jest niejednoznaczne albo brakuje danych/decyzji (np. pliki XSD, przepisy) – **zapytaj użytkownika**, zamiast zgadywać.
3. **Plan.** Rozpisz kroki w liście zadań (np. domena → migracja → handlery → endpointy → UI → testy → dokumentacja). Duże zadania (rozmiar L)
   podziel na etapy, z których każdy kończy się działającym, przetestowanym stanem.
4. **Implementacja** zgodnie z zasadami z `CLAUDE.md`. Małe, spójne zmiany; bez „przy okazji” refaktorów poza zakresem
   (zauważone rzeczy zapisz w dzienniku zadania lub jako nowe zadanie).
5. **Testy.** Napisz testy równolegle z kodem (E2E + jednostkowe). Uruchom **pełny** zestaw: `dotnet build`, `npm run build`, `npx playwright test`.
   Nie kończ zadania z czerwonym testem ani z wyłączonym/osłabionym testem.
6. **Weryfikacja ręczna** kluczowego przepływu (uruchom aplikację, kliknij; dla zmian migracji sprawdź na starej bazie).
7. **Dokumentacja.** Zaznacz kryteria akceptacji (`- [x]`), uzupełnij „Dziennik zadania”, zaktualizuj `CHECKLIST.md`, `README.md`, `INSTRUKCJA.txt` (dla użytkownika końcowego).
8. **Zamknięcie.** Status **Zrobione** w indeksie i w pliku. Jeśli użytkownik prosi – commit `T-0XX: opis`. Podsumowanie: co zrobione, co przetestowane,
   co świadomie pominięto, nowe zadania wynikające z pracy.

## Zasady

- Jedno zadanie naraz na jednej gałęzi/katalogu roboczym; równoległe zadania tylko gdy dotykają różnych plików (osobne worktree).
- Nie zmieniaj zakresu po cichu – jeśli zadanie rośnie, wydziel nowe i wpisz je do indeksu.
- Zadanie **Zablokowane** wolno ruszyć dopiero po usunięciu blokady opisanej w pliku (np. dostarczeniu XSD).
- Priorytety: **P0** fundament/ryzyko, **P1** główna wartość, **P2** ważne uzupełnienia, **P3** miłe do mieć.
- Rozmiary: **S** ≤ pół dnia, **M** ~1 dzień, **L** kilka dni (dziel na etapy).

## Szablon nowego zadania

Skopiuj `TEMPLATE.md`, nadaj kolejny numer, dopisz wiersz w `INDEX.md`.
