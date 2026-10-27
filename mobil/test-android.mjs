// Testul aplicației Android, cap-coadă, pe un emulator sau telefon conectat prin adb și pe stiva de test (niciodată live):
//   API-ul de test pe 5081 și site-ul de test pe 3001 (vezi desktop/test-aplicatie.mjs), apoi `npm run android:apk`.
// Rulare: npm run test:android. Telefonul ajunge la site prin `adb reverse tcp:3001` (doar build-ul debug permite HTTP local).
// Verifică: alegerea serverului, intrarea în cont prin HTTP-ul nativ, descărcarea unei lecții, testul fără server,
// apoi trimiterea rezultatului și confirmarea lui pe server.
import { _android as android, request, expect } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const API = 'http://127.0.0.1:5081', PAROLA = 'Test-demo-2026!', PACHET = 'ro.telebac.app'
const APK = path.join(path.dirname(fileURLToPath(import.meta.url)), 'android/app/build/outputs/apk/debug/app-debug.apk')
const adb = (...a) => execFileSync('adb', a, { encoding: 'utf8' })
const pas = (t) => console.log('✓', t)
const client = async (cine) => {
  const c = await request.newContext({ baseURL: API, extraHTTPHeaders: { 'X-MarkAi': '1' } })
  expect((await c.post('/autentificare', { data: { identificator: cine, parola: PAROLA } })).ok()).toBeTruthy()
  return c
}

const admin = await client('admin')
const lectie = await (await admin.post('/parcursuri', { data: { materieId: 1, titlu: `Android ${Date.now()}`, clasa: 'Bac', publicat: true, vizibilitate: 'public', organizatieId: null } })).json()
await admin.post('/cursuri', { data: { parcursId: lectie.id, titlu: 'Capitol Android', continut: 'Text pentru telefon.', publicat: true, ordine: 1 } })
await admin.post(`/parcursuri/${lectie.id}/intrebari`, { data: { text: 'Merge pe telefon?', activa: true, variante: [{ text: 'Da, merge', corecta: true }, { text: 'Nu', corecta: false }] } })

const [device] = await android.devices()
if (!device) throw new Error('Niciun dispozitiv Android conectat (adb devices).')
adb('reverse', 'tcp:3001', 'tcp:3001')
await device.installApk(APK)
await device.shell(`pm clear ${PACHET}`)
await device.shell(`am start -n ${PACHET}/.MainActivity`)
const page = await (await device.webView({ pkg: PACHET }, { timeout: 60_000 })).page()
try {
  await expect(page.getByRole('heading', { name: 'Bun venit în TeleBac' })).toBeVisible({ timeout: 30_000 }); pas('prima pornire cere serverul')
  await page.getByLabel(/Adresa serverului/).fill('http://127.0.0.1:3001')
  await page.getByRole('button', { name: 'Conectează' }).click()
  await expect(page.getByLabel('Email sau utilizator')).toBeVisible({ timeout: 30_000 }); pas('serverul e păstrat')
  await page.getByLabel('Email sau utilizator').fill('george')
  await page.getByLabel('Parolă', { exact: true }).fill(PAROLA)
  await page.getByRole('button', { name: 'Intră în cont' }).click()
  await expect(page.getByRole('button', { name: 'Intră în cont' })).toBeHidden({ timeout: 30_000 }); pas('intrarea în cont, prin HTTP-ul nativ')

  await page.goto(`https://localhost/parcurs/?id=${lectie.id}`)
  await page.getByRole('button', { name: 'Descarcă pentru offline' }).click()
  await expect(page.getByText('Lecția e pe acest dispozitiv')).toBeVisible({ timeout: 60_000 }); pas('lecția descărcată')

  adb('reverse', '--remove', 'tcp:3001')
  await page.goto(`https://localhost/test/?id=${lectie.id}`)
  await expect(page.getByText(/^(Ești offline|Serverul nu răspunde)\./)).toBeVisible({ timeout: 30_000 }); pas('banda de offline')
  await page.getByRole('button', { name: 'Da, merge' }).click()
  await expect(page.getByText(/serverul îl verifică și acordă XP-ul/)).toBeVisible({ timeout: 30_000 }); pas('testul notat pe telefon, fără server')

  adb('reverse', 'tcp:3001', 'tcp:3001')
  await page.evaluate(() => window.dispatchEvent(new Event('online')))
  await expect(page.getByText(/Rezultatele offline au ajuns pe server: 1 confirmat/)).toBeVisible({ timeout: 45_000 }); pas('rezultatul trimis la reconectare')
  const p = await (await (await client('george')).get(`/parcursuri/${lectie.id}`)).json()
  expect(p.progres?.promovatLa).toBeTruthy(); pas('serverul a înregistrat lecția ca promovată')
} finally {
  adb('reverse', '--remove-all')
  await device.close()
}
