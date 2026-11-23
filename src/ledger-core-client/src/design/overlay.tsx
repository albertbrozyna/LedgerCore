import { createContext, useCallback, useContext, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Button, Field } from './controls'

/** Modal oparty o natywny <dialog> (focus trap, Esc, rola `dialog`). */
export function Modal({ title, onClose, children, footer }: { title: string; onClose: () => void; children: ReactNode; footer?: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const d = ref.current
    if (d && !d.open) d.showModal()
    return () => { if (d?.open) d.close() }
  }, [])
  return (
    <dialog ref={ref} className="modal" aria-labelledby="modal-title" onCancel={e => { e.preventDefault(); onClose() }}
      onClick={e => { if (e.target === ref.current) onClose() }}>
      <div className="modal-body">
        <h2 id="modal-title" className="modal-title">{title}</h2>
        {children}
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </dialog>
  )
}

export interface ConfirmOptions { title: string; message?: ReactNode; confirmLabel?: string; cancelLabel?: string; danger?: boolean }
export interface PromptOptions {
  title: string; label: string; message?: ReactNode; initial?: string; type?: 'text' | 'password'
  confirmLabel?: string; hint?: string
}

type Pending =
  | { kind: 'confirm'; opts: ConfirmOptions; resolve: (v: boolean) => void }
  | { kind: 'prompt'; opts: PromptOptions; resolve: (v: string | null) => void }

interface DialogApi {
  confirm: (o: ConfirmOptions) => Promise<boolean>
  prompt: (o: PromptOptions) => Promise<string | null>
}
const DialogContext = createContext<DialogApi | null>(null)

export function useDialogs(): DialogApi {
  const c = useContext(DialogContext)
  if (!c) throw new Error('useDialogs wymaga DialogProvider')
  return c
}

function PromptForm({ opts, onDone }: { opts: PromptOptions; onDone: (v: string | null) => void }) {
  const [value, setValue] = useState(opts.initial ?? '')
  const submit = (e: FormEvent) => { e.preventDefault(); onDone(value) }
  return (
    <form onSubmit={submit}>
      {opts.message && <p>{opts.message}</p>}
      <Field label={opts.label} hint={opts.hint}>
        <input autoFocus type={opts.type ?? 'text'} value={value} onChange={e => setValue(e.target.value)} />
      </Field>
      <div className="modal-footer">
        <Button type="button" onClick={() => onDone(null)}>Anuluj</Button>
        <Button type="submit" variant="primary">{opts.confirmLabel ?? 'Zapisz'}</Button>
      </div>
    </form>
  )
}

/** Zamiennik natywnych `confirm()` / `prompt()`: asynchroniczne okna dialogowe w stylu aplikacji. */
export function DialogProvider({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<Pending | null>(null)
  const confirm = useCallback((opts: ConfirmOptions) => new Promise<boolean>(resolve => setPending({ kind: 'confirm', opts, resolve })), [])
  const prompt = useCallback((opts: PromptOptions) => new Promise<string | null>(resolve => setPending({ kind: 'prompt', opts, resolve })), [])

  function finish(value: boolean | string | null) {
    if (!pending) return
    if (pending.kind === 'confirm') pending.resolve(value === true)
    else pending.resolve(typeof value === 'string' ? value : null)
    setPending(null)
  }

  return (
    <DialogContext.Provider value={{ confirm, prompt }}>
      {children}
      {pending?.kind === 'confirm' && (
        <Modal title={pending.opts.title} onClose={() => finish(false)}
          footer={<>
            <Button autoFocus onClick={() => finish(false)}>{pending.opts.cancelLabel ?? 'Wróć'}</Button>
            <Button variant={pending.opts.danger ? 'danger' : 'primary'} onClick={() => finish(true)}>{pending.opts.confirmLabel ?? 'Potwierdź'}</Button>
          </>}>
          {pending.opts.message && <p>{pending.opts.message}</p>}
        </Modal>
      )}
      {pending?.kind === 'prompt' && (
        <Modal title={pending.opts.title} onClose={() => finish(null)}>
          <PromptForm opts={pending.opts} onDone={finish} />
        </Modal>
      )}
    </DialogContext.Provider>
  )
}

// ---- Toast ----
interface ToastItem { id: number; message: string; tone: 'ok' | 'error' }
const ToastContext = createContext<((message: string, tone?: 'ok' | 'error') => void) | null>(null)

export function useToast() {
  const c = useContext(ToastContext)
  if (!c) throw new Error('useToast wymaga ToastProvider')
  return c
}

/** Krótkie powiadomienia w rogu ekranu (znikają po kilku sekundach). Nie używają roli `status`, by nie dublować <Notice>. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])
  const seq = useRef(0)
  const show = useCallback((message: string, tone: 'ok' | 'error' = 'ok') => {
    const id = ++seq.current
    setItems(x => [...x, { id, message, tone }])
    setTimeout(() => setItems(x => x.filter(t => t.id !== id)), 5000)
  }, [])
  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className="toasts no-print" aria-live="polite">
        {items.map(t => <div key={t.id} className={`toast toast-${t.tone}`} data-testid="toast">{t.message}</div>)}
      </div>
    </ToastContext.Provider>
  )
}
