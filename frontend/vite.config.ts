import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Proxy /api/* to the .NET backend during development.
      // In production, the backend serves the built frontend as static files.
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        secure: false,  // accept self-signed dev cert
        // The backend talks to the proxy over HTTPS, so it marks the session cookie
        // `Secure`. The browser, however, sees the dev server over plain http://localhost:5173
        // and silently drops `Secure` cookies on an insecure origin — which would break login
        // locally. Strip the `Secure` attribute from Set-Cookie for the dev proxy only;
        // production is genuinely HTTPS and same-origin, so the cookie stays Secure there.
        configure: proxy => {
          proxy.on('proxyRes', proxyRes => {
            const setCookie = proxyRes.headers['set-cookie']
            if (setCookie) {
              proxyRes.headers['set-cookie'] = setCookie.map(cookie =>
                cookie.replace(/;\s*Secure/gi, ''),
              )
            }
          })
        },
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
