import { spawnSync } from 'node:child_process'

const env = { ...process.env, NEXT_PUBLIC_API_URL: '/api' }
for (const args of [[process.env.npm_execpath, 'run', 'build'], ['node_modules/@playwright/test/cli.js', 'test', '--config', 'playwright.upload.config.ts']]) {
  const result = spawnSync(process.execPath, args, { stdio: 'inherit', env })
  if (result.error) throw result.error
  if (result.status !== 0) process.exit(result.status || 1)
}
