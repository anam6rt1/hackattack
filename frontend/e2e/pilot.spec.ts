import { test, expect } from '@playwright/test'
import { elevNou, intraRapid, unic } from './ajutor'

test('lecția descărcată și testul ei merg offline, apoi serverul confirmă rezultatul', async ({ page, request, context }) => {
  const headers = { 'X-MarkAi': '1' }
  await request.post('/api/autentificare', { headers, data: { identificator: 'admin', parola: 'Test-demo-2026!' } })
  const lesson = await (await request.post('/api/parcursuri', { headers, data: { materieId: 1, titlu: unic('Lecție offline pilot '), clasa: 'Bac', publicat: true, vizibilitate: 'public', organizatieId: null } })).json()
  expect((await request.post('/api/cursuri', { headers, data: { parcursId: lesson.id, titlu: 'Capitol offline', continut: 'Text disponibil fără internet.', publicat: true, ordine: 1 } })).ok()).toBeTruthy()
  expect((await request.post(`/api/parcursuri/${lesson.id}/intrebari`, { headers, data: { text: 'Întrebarea offline?', activa: true, variante: [{ text: 'Corect offline', corecta: true }, { text: 'Greșit offline', corecta: false }] } })).ok()).toBeTruthy()
  // A new student: the other browser's run must not have passed this Lecție already.
  await intraRapid(page, await elevNou('offline'))

  await page.goto(`/parcurs?id=${lesson.id}`)
  await page.getByRole('button', { name: 'Descarcă pentru offline' }).click()
  await expect(page.getByText('Lecția e pe acest dispozitiv')).toBeVisible({ timeout: 30_000 })

  await context.setOffline(true)
  await page.goto(`/test?id=${lesson.id}`)
  await expect(page.getByText('Ești offline.')).toBeVisible()
  await page.getByRole('button', { name: 'Corect offline' }).click()
  await expect(page.getByText(/serverul îl verifică și acordă XP-ul/)).toBeVisible()

  await context.setOffline(false)
  await expect(page.getByText(/Rezultatele offline au ajuns pe server: 1 confirmat/)).toBeVisible({ timeout: 20_000 })
  const lectie = await (await page.request.get(`/api/parcursuri/${lesson.id}`, { headers })).json()
  expect(lectie.progres?.promovatLa).toBeTruthy()

  await page.goto('/setari')
  await page.getByRole('button', { name: 'Notificări', exact: true }).click()
  await expect(page.getByText('Dezactivat', { exact: true })).toBeVisible()
})
