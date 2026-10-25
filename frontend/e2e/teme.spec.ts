import { test, expect } from '@playwright/test'
import { intraRapid } from './ajutor'

test('profesorul publică tema, elevul promovează și profesorul vede rezultatul', async ({ browser, page, request }) => {
  const antete = { 'X-MarkAi': '1' }
  expect((await request.post('/api/autentificare', { headers: antete, data: { identificator: 'admin', parola: 'Test-demo-2026!' } })).ok()).toBeTruthy()
  const titlu = `Lecția temei ${Date.now()}`
  const l = await (await request.post('/api/parcursuri', { headers: antete, data: { materieId: 1, titlu, clasa: 'XII', vizibilitate: 'public', organizatieId: null } })).json()
  expect((await request.post(`/api/parcursuri/${l.id}/intrebari`, { headers: antete, data: { text: 'Cât face doi plus doi?', activa: true, explicatie: 'Două perechi.', variante: [{ text: 'Patru', corecta: true }, { text: 'Cinci', corecta: false }] } })).ok()).toBeTruthy()

  await intraRapid(page, 'irina')
  const orgs = await (await page.request.get('/api/teme/organizatii')).json()
  const org = orgs[0].id
  const elevContext = await browser.newContext()
  const elev = await elevContext.newPage()
  try {
    const username = `tema${Date.now().toString(36)}`
    const email = `${username}@example.invalid`
    expect((await elev.request.post('/api/autentificare/inregistrare', { headers: antete, data: {
      nume: 'Elev Teme', username, email, parola: 'Test-demo-2026!', organizatieId: org,
    } })).ok()).toBeTruthy()
    let cod = ''
    await expect.poll(async () => {
      const mesaje = await (await request.get('/api/admin/mesaje')).json()
      cod = mesaje.elemente.find((m: { catre: string; continut: string }) => m.catre === email)?.continut?.match(/\b\d{6}\b/)?.[0] ?? ''
      return cod
    }).not.toBe('')
    expect((await elev.request.post('/api/autentificare/confirma-email', { headers: antete, data: { email, cod } })).ok()).toBeTruthy()
    const eu = await (await elev.request.get('/api/utilizatori/me')).json()
    expect((await page.request.post(`/api/organizatii/${org}/elevi/${eu.id}/aprobare`, { headers: antete })).ok()).toBeTruthy()
    await page.goto('/teme')
    await page.getByLabel('Titlul temei').fill('Tema de recapitulare')
    const termen = new Date(Date.now() + 3 * 86400000)
    await page.getByLabel('Termen de predare').fill(termen.toISOString().slice(0, 16))
    await page.getByRole('checkbox', { name: titlu, exact: true }).check()
    await page.getByRole('button', { name: 'Publică tema', exact: true }).click()
    await expect(page.getByText('Tema a fost publicată.')).toBeVisible()
    await elev.goto('/')
    await expect(elev.getByRole('heading', { name: 'Tema de recapitulare' })).toBeVisible()
    await elev.getByRole('link', { name: 'Deschide testul', exact: true }).click()
    await Promise.all([elev.waitForResponse(r => r.url().endsWith('/raspuns') && r.status() === 200), elev.getByRole('button', { name: /Patru/ }).click()])
    await elev.goto('/teme')
    await expect(elev.getByText('Predată', { exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Rezultate Tema de recapitulare' }).click()
    const raport = page.getByRole('region', { name: 'Rezultatele temei' })
    await expect(raport.getByText(eu.nume)).toBeVisible()
    await expect(raport.getByText('1/1 lecții · Predată')).toBeVisible()
    // A later mistake enters recap; the server schedules it for tomorrow, not immediate XP farming.
    await elev.goto(`/test/${l.id}`)
    await Promise.all([elev.waitForResponse(r => r.url().endsWith('/raspuns') && r.status() === 200), elev.getByRole('button', { name: /Cinci/ }).click()])
    await elev.goto('/antrenament')
    const greseli = elev.getByRole('region', { name: 'Greșelile mele' })
    await expect(greseli.getByText('Cât face doi plus doi?', { exact: true })).toBeVisible()
    await expect(greseli.getByText(/Revine pe/)).toBeVisible()
  } finally { await elevContext.close() }
})
