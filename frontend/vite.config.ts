/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// In dev the SPA talks to the API through this proxy, so the browser only ever sees one origin (no CORS).
// In Docker, nginx does the same job in front of the built assets.
export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    port: 5173,
    proxy: { '/api': { target: process.env.API_URL ?? 'http://localhost:5000', changeOrigin: true } },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    restoreMocks: true,
  },
})
