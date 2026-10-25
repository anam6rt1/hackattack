import { defineConfig, devices } from '@playwright/test'

// Isolated local stack: never reuse the owner's preview API/database.
export default defineConfig({
  testDir: 'e2e', testMatch: ['teme.spec.ts', 'simulari.spec.ts', 'pilot.spec.ts'], timeout: 60000, workers: 1, retries: 0,
  use: { baseURL: 'http://127.0.0.1:3003', locale: 'ro-RO', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    { command: 'dotnet run --project ../database/tools/MarkAi.Previzualizare -- --port 5083 --e2e', url: 'http://127.0.0.1:5083/materii', timeout: 180000, reuseExistingServer: false },
    { command: 'node node_modules/next/dist/bin/next build && node node_modules/next/dist/bin/next start -p 3003 -H 127.0.0.1', url: 'http://127.0.0.1:3003/api/materii', timeout: 180000, reuseExistingServer: false,
      env: { MARKAI_DIST_DIR: '.next-teme-e2e', API_INTERN_URL: 'http://127.0.0.1:5083', NEXT_PUBLIC_API_URL: '/api' } },
  ],
})
