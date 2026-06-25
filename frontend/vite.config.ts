import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg'],
      manifest: {
        name: 'Caixa Diário',
        short_name: 'Caixa Diário',
        description: 'Gestão financeira simplificada',
        theme_color: '#863bff',
        background_color: '#0d0d0d',
        display: 'standalone',
        scope: '/',
        start_url: '/',
        orientation: 'portrait',
        icons: [
          {
            src: 'favicon.svg',
            sizes: 'any',
            type: 'image/svg+xml',
            purpose: 'any',
          },
        ],
      },
      workbox: {
        // Precache todos os assets estáticos do build
        globPatterns: ['**/*.{js,css,html,svg,ico,woff,woff2}'],
        // SPA fallback para rotas do React Router (offline)
        navigateFallback: 'index.html',
        // Não aplicar SW fallback em rotas de API
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            // Chamadas de API sempre via rede — dados financeiros nunca cacheados
            urlPattern: /\/api\//,
            handler: 'NetworkOnly',
          },
        ],
        cleanupOutdatedCaches: true,
      },
      // Não ativa SW em modo dev (PWA requer build de produção)
      devOptions: { enabled: false },
    }),
  ],
  server: {
    proxy: {
      '/api': 'http://localhost:5131',
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
    coverage: {
      provider: 'v8',
      thresholds: { lines: 80, functions: 80, branches: 80, statements: 80 },
      exclude: [
        'src/main.tsx',
        'src/types.ts',
        'src/setupTests.ts',
        '**/*.css',
        'src/styles/**',
      ],
      reporter: ['text', 'html'],
    },
  },
  build: {
    outDir: '../CaixaDiario.API/wwwroot',
    emptyOutDir: true,
  },
})
