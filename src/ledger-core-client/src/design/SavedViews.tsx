import { useState } from 'react'
import { Button } from './controls'
import { useDialogs } from './overlay'

interface Stored<S> { name: string; state: S }

function read<S>(key: string): Stored<S>[] {
  try { return JSON.parse(localStorage.getItem(`lc.views.${key}`) ?? '[]') } catch { return [] }
}

/**
 * Zapisane widoki filtrów (np. „Faktury po terminie”) w localStorage. `storageKey` powinien zawierać identyfikator
 * użytkownika (widoki są per użytkownik) i nazwę listy. `state` to bieżące filtry, `onApply` je przywraca.
 */
export function SavedViews<S>({ storageKey, state, onApply }: { storageKey: string; state: S; onApply: (s: S) => void }) {
  const { prompt, confirm } = useDialogs()
  const [views, setViews] = useState<Stored<S>[]>(() => read<S>(storageKey))
  const [current, setCurrent] = useState('')

  function persist(next: Stored<S>[]) {
    setViews(next)
    try { localStorage.setItem(`lc.views.${storageKey}`, JSON.stringify(next)) } catch { /* tryb prywatny */ }
  }

  async function save() {
    const name = (await prompt({ title: 'Zapisz widok filtrów', label: 'Nazwa widoku', initial: current }))?.trim()
    if (!name) return
    persist([...views.filter(v => v.name !== name), { name, state }])
    setCurrent(name)
  }

  async function remove() {
    if (!current) return
    if (!await confirm({ title: 'Usunąć zapisany widok?', message: `Widok „${current}” zostanie usunięty z tej przeglądarki.`, confirmLabel: 'Usuń', danger: true })) return
    persist(views.filter(v => v.name !== current))
    setCurrent('')
  }

  return (
    <div className="saved-views no-print">
      <label>Zapisane widoki
        <select value={current} onChange={e => {
          setCurrent(e.target.value)
          const v = views.find(x => x.name === e.target.value)
          if (v) onApply(v.state)
        }}>
          <option value="">— bieżące filtry —</option>
          {views.map(v => <option key={v.name} value={v.name}>{v.name}</option>)}
        </select>
      </label>
      <Button small onClick={save}>Zapisz widok</Button>
      {current && <Button small variant="link" onClick={remove}>Usuń widok</Button>}
    </div>
  )
}
