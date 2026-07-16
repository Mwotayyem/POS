import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

// The API base URL is read from VITE_API_BASE_URL at build/dev time (see .env).
// In dev, requests to /api are proxied to the backend to avoid CORS during local work.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
