import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': '/src' },
  },
  server: {
    port: 5173,
    proxy: {
      // Proxy /api/* to the .NET backend during development.
      // In production, the backend serves the built frontend as static files.
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        secure: false,  // accept self-signed dev cert
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
    },
  },
})
