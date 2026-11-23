import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'

// Build trafia do wwwroot API – aplikacja jest serwowana z jednego adresu
export default defineConfig({
  plugins: [react()],
  build: { outDir: '../LedgerCore.Api/wwwroot', emptyOutDir: true },
  server: { port: 5173, proxy: { '/api': 'http://localhost:5000', '/uploads': 'http://localhost:5000' } },
})
