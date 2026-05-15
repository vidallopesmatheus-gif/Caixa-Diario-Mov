import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
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
