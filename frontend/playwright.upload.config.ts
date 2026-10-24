import { defineConfig, devices } from '@playwright/test'

// Frontend-only browser checks. All API calls are intercepted by fixtures; no database is touched.
export default defineConfig({
  testDir: 'e2e', testMatch: 'upload-preview.spec.ts', timeout: 45000,
  use: { baseURL: 'http://127.0.0.1:3002', locale: 'ro-RO', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'node node_modules/next/dist/bin/next start -p 3002 -H 127.0.0.1', url: 'http://127.0.0.1:3002', reuseExistingServer: false, timeout: 30000 },
})
