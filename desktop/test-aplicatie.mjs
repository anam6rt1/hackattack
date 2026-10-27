// Testul aplicației desktop, cap-coadă, pe stiva de test (niciodată pe cea live):
//   API-ul de test:  dotnet run --project database/tools/MarkAi.Previzualizare -- --port 5081 --e2e
//   site-ul de test: în frontend, MARKAI_DIST_DIR=.next-e2e API_INTERN_URL=http://127.0.0.1:5081 NEXT_PUBLIC_API_URL=/api npx next build
//                    apoi MARKAI_DIST_DIR=.next-e2e npx next start -p 3001 -H 127.0.0.1
//   exportul aplicației: npm run site
// Rulare: npm test (fără fereastră pe ecran: TELEBAC_ASCUNS=1, profil temporar).
// Verifică: alegerea serverului, intrarea în cont prin procesul principal (cookie-ul nu ajunge la pagină), tokenul de joc,
// descărcarea unei lecții, testul dat fără rețea, apoi trimiterea rezultatului și confirmarea lui pe server.
import { _electron as electron, request, expect } from '@playwright/test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'

const API = 'http://127.0.0.1:5081', SITE = 'http://127.0.0.1:3001', PAROLA = 'Test-demo-2026!'
const pas = (t) => console.log('✓', t)
const client = async (cine) => {
  const c = await request.newContext({ baseURL: API, extraHTTPHeaders: { 'X-MarkAi': '1' } })
  expect((await c.post('/autentificare', { data: { identificator: cine, parola: PAROLA } })).ok()).toBeTruthy()
  return c
}

const admin = await client('admin')
const lectie = await (await admin.post('/parcursuri', { data: { materieId: 1, titlu: `Desktop ${Date.now()}`, clasa: 'Bac', publicat: true, vizibilitate: 'public', organizatieId: null } })).json()
await admin.post('/cursuri', { data: { parcursId: lectie.id, titlu: 'Capitol desktop', continut: 'Text pentru aplicația desktop.', publicat: true, ordine: 1 } })
await admin.post(`/parcursuri/${lectie.id}/intrebari`, { data: { text: 'Merge desktop?', activa: true, variante: [{ text: 'Da, merge', corecta: true }, { text: 'Nu', corecta: false }] } })

const profil = mkdtempSync(path.join(tmpdir(), 'telebac-desktop-'))
const app = await electron.launch({
  executablePath: createRequire(import.meta.url)('electron'), args: [path.dirname(fileURLToPath(import.meta.url))],
  env: { ...process.env, TELEBAC_ASCUNS: '1', TELEBAC_DATE: profil },
})
try {
  const page = await app.firstWindow()
  await expect(page.getByRole('heading', { name: 'Bun venit în TeleBac' })).toBeVisible({ timeout: 20_000 }); pas('prima pornire cere serverul')
  await page.getByLabel(/Adresa serverului/).fill(SITE)
  await page.getByRole('button', { name: 'Conectează' }).click()
  await expect(page.getByLabel('Email sau utilizator')).toBeVisible({ timeout: 20_000 }); pas('serverul e păstrat')
  await page.getByLabel('Email sau utilizator').fill('george')
  await page.getByLabel('Parolă', { exact: true }).fill(PAROLA)
  await page.getByRole('button', { name: 'Intră în cont' }).click()
  await expect(page.getByRole('button', { name: 'Intră în cont' })).toBeHidden({ timeout: 20_000 }); pas('intrarea în cont, prin procesul principal')
  expect(await page.evaluate(() => document.cookie)).toBe(''); pas('cookie-ul de sesiune nu ajunge la pagină')
  const token = await page.evaluate(async () => (await (await fetch('/api/utilizatori/me/token-joc', { method: 'POST', headers: { 'X-MarkAi': '1' } })).json()).token)
  expect(token).toBeTruthy(); pas('tokenul pentru joc')

  await page.goto(`app://telebac/parcurs/?id=${lectie.id}`)
  await page.getByRole('button', { name: 'Descarcă pentru offline' }).click()
  await expect(page.getByText('Lecția e pe acest dispozitiv')).toBeVisible({ timeout: 30_000 }); pas('lecția descărcată')

  // The server becomes unreachable for the app (net.fetch in the main process ignores network emulation, but not the
  // session proxy): every request goes to a closed port, loopback included.
  await app.evaluate(({ session }) => session.defaultSession.setProxy({ proxyRules: 'http://127.0.0.1:9', proxyBypassRules: '<-loopback>' }))
  await page.goto(`app://telebac/test/?id=${lectie.id}`)
  await expect(page.getByText(/^(Ești offline|Serverul nu răspunde)\./)).toBeVisible({ timeout: 15_000 }); pas('banda de offline')
  await page.getByRole('button', { name: 'Da, merge' }).click()
  await expect(page.getByText(/serverul îl verifică și acordă XP-ul/)).toBeVisible({ timeout: 15_000 }); pas('testul notat pe dispozitiv, fără rețea')

  await app.evaluate(({ session }) => session.defaultSession.setProxy({ mode: 'direct' }))
  await page.evaluate(() => window.dispatchEvent(new Event('online')))
  await expect(page.getByText(/Rezultatele offline au ajuns pe server: 1 confirmat/)).toBeVisible({ timeout: 30_000 }); pas('rezultatul trimis la reconectare')
  const p = await (await (await client('george')).get(`/parcursuri/${lectie.id}`)).json()
  expect(p.progres?.promovatLa).toBeTruthy(); pas('serverul a înregistrat lecția ca promovată')
} finally {
  await app.close()
  rmSync(profil, { recursive: true, force: true })
}
