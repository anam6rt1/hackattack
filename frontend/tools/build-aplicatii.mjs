// Exportul static pentru aplicațiile desktop și mobil (frontend/out), pe orice sistem: variabilele se pun aici, nu în linia de
// comandă (`VAR=1 comanda` nu merge în cmd-ul Windows). Vezi next.config.ts (MARKAI_EXPORT) și lib/platforma.js.
import { spawnSync } from 'node:child_process'

const r = spawnSync('npx', ['next', 'build'], {
  stdio: 'inherit', shell: process.platform === 'win32',
  env: { ...process.env, MARKAI_EXPORT: '1', NEXT_PUBLIC_API_URL: '/api' },
})
process.exit(r.status ?? 1)
