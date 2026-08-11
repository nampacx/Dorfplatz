import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Local dev: proxy /api and /.auth to the SWA CLI emulator (port 4280).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:7071',
      '/.auth': 'http://localhost:4280',
    },
  },
})
