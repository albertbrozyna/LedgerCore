import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { applyTheme, getStoredTheme } from './design'

applyTheme(getStoredTheme()) // zanim React narysuje UI – bez „mignięcia” motywu

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
