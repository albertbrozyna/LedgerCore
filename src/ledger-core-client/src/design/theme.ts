import { useCallback, useState } from 'react'

export type ThemeChoice = 'auto' | 'light' | 'dark'
const KEY = 'lc.theme'

export function getStoredTheme(): ThemeChoice {
  try {
    const v = localStorage.getItem(KEY)
    return v === 'light' || v === 'dark' ? v : 'auto'
  } catch { return 'auto' }
}

/** Ustawia motyw na <html> (auto = decyduje prefers-color-scheme) i zapamiętuje wybór. */
export function applyTheme(choice: ThemeChoice) {
  const root = document.documentElement
  if (choice === 'auto') root.removeAttribute('data-theme')
  else root.setAttribute('data-theme', choice)
  try { if (choice === 'auto') localStorage.removeItem(KEY); else localStorage.setItem(KEY, choice) } catch { /* tryb prywatny */ }
}

export const themeLabel: Record<ThemeChoice, string> = { auto: 'Motyw: systemowy', light: 'Motyw: jasny', dark: 'Motyw: ciemny' }

export function useTheme() {
  const [theme, setThemeState] = useState<ThemeChoice>(getStoredTheme)
  const setTheme = useCallback((t: ThemeChoice) => { applyTheme(t); setThemeState(t) }, [])
  const cycle = useCallback(() => setTheme(theme === 'auto' ? 'light' : theme === 'light' ? 'dark' : 'auto'), [theme, setTheme])
  return { theme, setTheme, cycle }
}
