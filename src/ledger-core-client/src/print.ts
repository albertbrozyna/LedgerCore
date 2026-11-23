// Pomocnicze funkcje do wydruków i eksportów (bez zależności zewnętrznych).

const ones = ['', 'jeden', 'dwa', 'trzy', 'cztery', 'pięć', 'sześć', 'siedem', 'osiem', 'dziewięć', 'dziesięć', 'jedenaście',
  'dwanaście', 'trzynaście', 'czternaście', 'piętnaście', 'szesnaście', 'siedemnaście', 'osiemnaście', 'dziewiętnaście']
const tens = ['', '', 'dwadzieścia', 'trzydzieści', 'czterdzieści', 'pięćdziesiąt', 'sześćdziesiąt', 'siedemdziesiąt', 'osiemdziesiąt', 'dziewięćdziesiąt']
const hundreds = ['', 'sto', 'dwieście', 'trzysta', 'czterysta', 'pięćset', 'sześćset', 'siedemset', 'osiemset', 'dziewięćset']

function below1000(n: number): string {
  const parts: string[] = []
  parts.push(hundreds[Math.floor(n / 100)])
  const r = n % 100
  if (r < 20) parts.push(ones[r])
  else parts.push(tens[Math.floor(r / 10)], ones[r % 10])
  return parts.filter(Boolean).join(' ')
}

/** Polska odmiana rzeczownika po liczbie: 1 → form1, 2-4 (poza 12-14) → form2, reszta → form5. */
function plural(n: number, f1: string, f2: string, f5: string): string {
  if (n === 1) return f1
  const last = n % 10, last2 = n % 100
  return last >= 2 && last <= 4 && !(last2 >= 12 && last2 <= 14) ? f2 : f5
}

export function integerInWords(n: number): string {
  if (n === 0) return 'zero'
  const groups: [number, string, string, string][] = [
    [1_000_000_000, 'miliard', 'miliardy', 'miliardów'],
    [1_000_000, 'milion', 'miliony', 'milionów'],
    [1_000, 'tysiąc', 'tysiące', 'tysięcy'],
  ]
  const out: string[] = []
  let rest = n
  for (const [size, f1, f2, f5] of groups) {
    const q = Math.floor(rest / size)
    if (q > 0) {
      out.push(q === 1 ? f1 : `${below1000(q)} ${plural(q, f1, f2, f5)}`)
      rest %= size
    }
  }
  if (rest > 0) out.push(below1000(rest))
  return out.join(' ')
}

/** 1335.00 → "jeden tysiąc trzysta trzydzieści pięć złotych 00/100" */
export function amountInWords(amount: number): string {
  const cents = Math.round(amount * 100)
  const zl = Math.floor(cents / 100)
  const gr = String(cents % 100).padStart(2, '0')
  return `${integerInWords(zl)} ${plural(zl, 'złoty', 'złote', 'złotych')} ${gr}/100`
}

/** Eksport tabeli do CSV zgodnego z polskim Excelem (BOM, średnik, przecinek dziesiętny). */
export function downloadCsv(filename: string, header: string[], rows: (string | number)[][]) {
  const cell = (v: string | number) => {
    if (typeof v === 'number') return v.toFixed(2).replace('.', ',')
    let s = v
    if (s.length > 0 && '=+-@'.includes(s[0])) s = "'" + s // ochrona przed wstrzyknięciem formuł
    return `"${s.replace(/"/g, '""')}"`
  }
  const text = [header.map(h => `"${h}"`).join(';'), ...rows.map(r => r.map(cell).join(';'))].join('\r\n')
  const blob = new Blob(['﻿' + text], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = filename; a.click()
  URL.revokeObjectURL(url)
}
