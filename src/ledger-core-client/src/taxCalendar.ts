// Orientacyjny kalendarz terminów podatkowych. Terminy zależą od formy opodatkowania i rodzaju działalności –
// aplikacja podpowiada najczęstsze (VAT/JPK_V7 do 25., zaliczki PIT/CIT i składki ZUS do 20.) i przesuwa je na
// najbliższy dzień roboczy (weekendy; święta nie są uwzględniane).

export interface Deadline { date: string; label: string; detail: string }

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

export function nextBusinessDay(d: Date): Date {
  const x = new Date(d)
  while (x.getDay() === 0 || x.getDay() === 6) x.setDate(x.getDate() + 1)
  return x
}

const monthName = (d: Date) => d.toLocaleDateString('pl-PL', { month: 'long', year: 'numeric' })

export function upcomingDeadlines(today: Date, horizonDays = 45): Deadline[] {
  const out: Deadline[] = []
  const end = new Date(today); end.setDate(end.getDate() + horizonDays)
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  for (let m = 0; m < 3; m++) {
    const first = new Date(today.getFullYear(), today.getMonth() + m, 1)
    const prev = new Date(first.getFullYear(), first.getMonth() - 1, 1)
    const items: [number, string, string][] = [
      [25, 'JPK_V7 / VAT', `rozliczenie VAT za ${monthName(prev)}`],
      [20, 'Zaliczka PIT/CIT', `zaliczka miesięczna za ${monthName(prev)}`],
      [20, 'Składki ZUS', `składki za ${monthName(prev)} (dla niektórych płatników termin 5. lub 15.)`],
    ]
    for (const [day, label, detail] of items) {
      const d = nextBusinessDay(new Date(first.getFullYear(), first.getMonth(), day))
      if (d >= start && d <= end) out.push({ date: iso(d), label, detail })
    }
  }
  return out.sort((a, b) => a.date.localeCompare(b.date))
}
