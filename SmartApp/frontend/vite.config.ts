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
      // Proxy /api to the backend. Default targets the `http` launch profile
      // (`dotnet run` binds http://localhost:5101 by default), which avoids the dev HTTPS
      // certificate prompt. Override with VITE_API_PROXY_TARGET (e.g. https://localhost:7123).
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5101',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
