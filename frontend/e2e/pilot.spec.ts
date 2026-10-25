import { test, expect } from '@playwright/test'
import { intraRapid } from './ajutor'

test('lecția și testul funcționează offline, apoi răspunsul este verificat pe server', async ({ page, request, context }) => {
  const headers = { 'X-MarkAi': '1' }
  await request.post('/api/autentificare', { headers, data: { identificator: 'admin', parola: 'Test-demo-2026!' } })
  const lesson = await (await request.post('/api/parcursuri', { headers, data: { materieId: 1, titlu: 'Lecție offline pilot', clasa: 'Bac', publicat: true, vizibilitate: 'public', organizatieId: null } })).json()
  expect((await request.post('/api/cursuri', { headers, data: { parcursId: lesson.id, titlu: 'Capitol offline', continut: 'Text disponibil fără internet.', publicat: true, ordine: 1 } })).ok()).toBeTruthy()
  expect((await request.post(`/api/parcursuri/${lesson.id}/intrebari`, { headers, data: { text: 'Întrebarea offline?', activa: true, variante: [{ text: 'Corect offline', corecta: true }, { text: 'Greșit offline', corecta: false }] } })).ok()).toBeTruthy()
  await intraRapid(page, 'george')
  await page.goto(`/parcurs/${lesson.id}`)
  await page.getByRole('checkbox', { name: /Pregătește și testul/ }).check()
  await page.getByRole('button', { name: 'Salvează pe acest dispozitiv' }).click()
  await expect(page.getByText('Lecția a fost salvată pe acest dispozitiv.')).toBeVisible()
  await page.goto('/offline.html')
  await expect(page.getByText('Text disponibil fără internet.')).toBeVisible()
  await context.setOffline(true)
  await page.reload()
  await expect(page.getByText('Text disponibil fără internet.')).toBeVisible()
  await page.getByRole('combobox').selectOption({ label: 'Corect offline' })
  await expect(page.getByRole('status')).toHaveText('Răspuns păstrat pe dispozitiv. Nu este încă verificat.')
  await context.setOffline(false)
  await expect(page.getByRole('status')).toHaveText('Test verificat: promovat.')
  await page.goto('/setari')
  await page.getByRole('button', { name: 'Notificări', exact: true }).click()
  await expect(page.getByText('Dezactivat', { exact: true })).toBeVisible()
})
