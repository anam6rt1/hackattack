import { spawnSync } from 'node:child_process'

if (Number(process.versions.node.split('.')[0]) < 26) {
  console.error('Mark AI necesită Node.js 26 sau mai nou. Versiunea recomandată este în .node-version.')
  process.exit(1)
}
if (!process.env.npm_execpath) throw new Error('Pornește verificarea cu npm run verifica.')
for (const script of ['lint', 'test', 'test:optimizare', 'build']) {
  const r = spawnSync(process.execPath, [process.env.npm_execpath, 'run', script], {
    stdio: 'inherit', env: { ...process.env, NEXT_PUBLIC_API_URL: '/api' },
  })
  if (r.error) throw r.error
  if (r.status !== 0) process.exit(r.status || 1)
}
