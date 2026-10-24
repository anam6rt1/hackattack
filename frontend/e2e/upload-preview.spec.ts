import { test, expect, type Page } from '@playwright/test'
import { zipSync, strToU8 } from 'fflate'
import { PDFDocument } from 'pdf-lib'
import { readFile } from 'node:fs/promises'

const rel = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
const lectie = { id: 1, materieId: 1, materie: 'Română', titlu: 'Lecție de verificare', clasa: 'XII', vizibilitate: 'public', publicat: true, ordine: 1,
  autorId: 2, autor: 'Irina', organizatieId: 1, organizatie: 'Liceu', partajari: [], numarCapitole: 0, numarIntrebari: 0, poatePublicaPublic: true, poateMuta: true, organizatiiPosibile: [] }
const office = (name: string, xml: Record<string, string>) => ({ name, mimeType: 'application/octet-stream', buffer: Buffer.from(zipSync(Object.fromEntries(Object.entries(xml).map(([k, v]) => [k, strToU8(v)])))) })
function pdf() {
  const stream = 'BT /F1 14 Tf 20 250 Td (Previzualizare PDF) Tj ET\n'
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>', '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${Buffer.byteLength(stream)} >>\nstream\n${stream}endstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ]
  let text = '%PDF-1.4\n'
  const offsets = [0]
  objects.forEach((o, i) => { offsets.push(Buffer.byteLength(text)); text += `${i + 1} 0 obj\n${o}\nendobj\n` })
  const start = Buffer.byteLength(text)
  text += `xref\n0 6\n0000000000 65535 f \n${offsets.slice(1).map(o => `${String(o).padStart(10, '0')} 00000 n \n`).join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${start}\n%%EOF`
  return Buffer.from(text)
}

async function apiTest(page: Page, profesor = true) {
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace('/api', '')
    if (path === '/fisiere/previzualizare') return route.fulfill({ body: pdf(), contentType: 'application/pdf' })
    const data: Record<string, unknown> = {
      '/utilizatori/me': profesor ? { id: 2, nume: 'Irina', rol: 'profesor', stareProfesor: 'aprobat', organizatii: [] } : null,
      '/materii': { elemente: [{ id: 1, denumire: 'Română' }] },
      '/profesori/me': { profil: { materii: [{ id: 1, denumire: 'Română' }] } },
      '/continut/parcursuri': { elemente: [lectie], total: 1 },
      '/continut/parcursuri/1/fisiere': { elemente: [], maxMb: 20 },
      '/parcursuri/1/generare': { activ: false },
    }
    await route.fulfill({ status: path === '/utilizatori/me' && !profesor ? 401 : 200, json: data[path] ?? { elemente: [], total: 0 } })
  })
}
async function editor(page: Page) {
  await apiTest(page)
  await page.goto('/admin/continut')
  await page.getByRole('button', { name: /Lecție de verificare/ }).click()
}

test('Word, Excel și PowerPoint folosesc același vizualizator de pagini ca PDF-ul', async ({ page }) => {
  await editor(page)
  const files = [
    office('curs.docx', { 'word/document.xml': '<document><p><t>Text Word verificat</t></p></document>' }),
    office('curs.pptx', {
      'ppt/presentation.xml': `<presentation xmlns:r="${rel}"><sldId r:id="s"/></presentation>`,
      'ppt/_rels/presentation.xml.rels': '<Relationships><Relationship Id="s" Target="slides/slide1.xml"/></Relationships>',
      'ppt/slides/slide1.xml': '<sld><p><t>Text diapozitiv verificat</t></p></sld>',
    }),
    office('tabel.xlsx', {
      'xl/workbook.xml': `<workbook xmlns:r="${rel}"><sheet name="Buget" r:id="s"/></workbook>`,
      'xl/_rels/workbook.xml.rels': '<Relationships><Relationship Id="s" Target="worksheets/sheet1.xml"/></Relationships>',
      'xl/worksheets/sheet1.xml': '<worksheet><row r="1"><c r="A1" t="inlineStr"><is><t>Venit verificat</t></is></c><c r="C1"><v>42</v></c></row></worksheet>',
    }),
    { name: 'curs.pdf', mimeType: 'application/pdf', buffer: pdf() },
  ]
  let uploads = 0
  page.on('request', r => { if (r.method() === 'POST') uploads++ })
  await page.locator('input[type=file]').setInputFiles(files)
  for (const f of files) await page.getByRole('button', { name: `Previzualizează: ${f.name}`, exact: true }).click()
  for (const name of ['curs.docx.pdf', 'curs.pptx.pdf', 'tabel.xlsx.pdf', 'curs.pdf']) {
    await expect(page.getByRole('img', { name: `Pagina 1 din ${name}`, exact: true })).toBeVisible({ timeout: 20000 })
  }
  await expect(page.getByText('Text Word verificat', { exact: true })).toHaveCount(0)
  await expect(page.getByRole('table')).toHaveCount(0)
  expect(uploads).toBe(3)
  await page.getByRole('heading', { name: 'Fișierele lecției' }).scrollIntoViewIfNeeded()
  await page.screenshot({ path: 'test-results/previzualizare-documente.png', fullPage: true })
})

test('fotografia este afișată o singură dată, în varianta pregătită', async ({ page }) => {
  await apiTest(page, false)
  await page.goto('/')
  await page.getByRole('tab', { name: 'Sunt profesor' }).click()
  const png = await page.evaluate(() => {
    const c = document.createElement('canvas'); c.width = 900; c.height = 900
    const ctx = c.getContext('2d')!, image = ctx.createImageData(900, 900)
    let seed = 123
    for (let i = 0; i < image.data.length; i += 4) {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0
      const noise = (seed >>> 24) % 16, pixel = i / 4
      image.data[i] = (pixel % 900) / 4 + noise
      image.data[i + 1] = Math.floor(pixel / 900) / 4 + noise
      image.data[i + 2] = 100 + noise
      image.data[i + 3] = 255
    }
    ctx.putImageData(image, 0, 0)
    return c.toDataURL('image/png').split(',')[1]
  })
  await page.locator('input[name=poza]').setInputFiles({ name: 'portret.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') })
  await page.getByRole('button', { name: /Previzualizează fotografia: portret.webp/ }).click()
  await expect(page.getByAltText('Previzualizare: portret.webp')).toBeVisible()
  await expect(page.getByRole('button', { name: /portret.png/ })).toHaveCount(0)
  await expect(page.getByText(/Economisești|se optimizează pe dispozitiv/)).toHaveCount(0)

})

test('arată progresul, anularea păstrează selecția și permite reîncercarea', async ({ page }) => {
  await editor(page)
  let release: () => void = () => {}
  const gate = new Promise<void>(r => { release = r })
  let cereri = 0
  await page.route('**/api/parcursuri/1/fisiere', async route => {
    cereri++
    if (cereri === 1) await gate
    await route.fulfill({ status: 201, json: { id: 1 } }).catch(() => {})
  })
  await page.locator('input[type=file]').setInputFiles({ name: 'curs.pdf', mimeType: 'application/pdf', buffer: pdf() })
  await page.getByRole('button', { name: 'Încarcă fișierele selectate' }).click()
  await expect(page.getByRole('progressbar')).toBeVisible()
  await page.getByRole('button', { name: 'Anulează încărcarea' }).click()
  await expect(page.getByText(/Încărcare anulată/)).toBeVisible()
  release()
  await expect(page.getByRole('button', { name: 'Încarcă fișierele selectate' })).toBeEnabled()
  await page.getByRole('button', { name: 'Încarcă fișierele selectate' }).click()
  await expect(page.getByRole('button', { name: 'Încarcă fișierele selectate' })).toHaveCount(0)
  expect(cereri).toBe(2)
})

test('documentele deja salvate se previzualizează prin API-ul autentificat', async ({ page }) => {
  await apiTest(page)
  const doc = office('salvat.docx', { 'word/document.xml': '<document><p><t>Document salvat verificat</t></p></document>' })
  await page.route('**/api/continut/parcursuri/1/fisiere', route => route.fulfill({ json: { maxMb: 20, elemente: [{ id: 9, nume: doc.name, tip: 'docx', marimeOcteti: doc.buffer.length, stareText: 'gata', caractereText: 26 }] } }))
  await page.route('**/api/fisiere/9/previzualizare', route => route.fulfill({ body: pdf(), contentType: 'application/pdf' }))
  await page.goto('/admin/continut')
  await page.getByRole('button', { name: /Lecție de verificare/ }).click()
  await page.getByRole('button', { name: 'Previzualizează documentul' }).click()
  await expect(page.getByRole('img', { name: 'Pagina 1 din salvat.docx' })).toBeVisible()
  await page.getByRole('button', { name: 'Închide documentul' }).click()
  await expect(page.getByRole('img', { name: 'Pagina 1 din salvat.docx' })).toHaveCount(0)
})

test('setările profesorului permit previzualizarea dovezii și anularea uploadului', async ({ page }) => {
  await apiTest(page)
  await page.route('**/api/utilizatori/me/setari', route => route.fulfill({ json: {
    utilizator: { id: 2, nume: 'Irina', username: 'irina', email: 'irina@example.test', telefon: '' },
    profesor: { descriere: 'Profesor', materieIds: [1], stareVerificare: 'aprobat', arePoza: true, areDovada: true },
  } }))
  let release: () => void = () => {}
  const gate = new Promise<void>(r => { release = r })
  await page.route('**/api/utilizatori/me/setari/documente', async route => {
    await gate
    await route.fulfill({ json: { mesaj: 'Salvat' } }).catch(() => {})
  })
  await page.goto('/setari')
  await page.getByRole('button', { name: 'Profil de profesor', exact: true }).click()
  await page.getByLabel('Dovada calificării', { exact: true }).setInputFiles({ name: 'dovada.pdf', mimeType: 'application/pdf', buffer: pdf() })
  await page.getByRole('button', { name: 'Dovada calificării: dovada.pdf', exact: true }).click()
  await expect(page.getByRole('img', { name: 'Pagina 1 din dovada.pdf' })).toBeVisible()
  await page.getByRole('button', { name: 'Actualizează fișierele' }).click()
  await expect(page.getByRole('progressbar')).toBeVisible()
  await page.getByRole('button', { name: 'Anulează încărcarea' }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'Încărcare anulată' })).toBeVisible()
  release()
  await expect(page.getByRole('button', { name: 'Actualizează fișierele' })).toBeEnabled()
})

test('rescrie PDF-ul local, arată octeții exacți și trimite efectiv varianta mai mică', async ({ page }) => {
  const sample = process.env.MARKAI_COMPRESSION_SAMPLE
  let original: Uint8Array
  if (sample) original = new Uint8Array(await readFile(sample))
  else {
    const doc = await PDFDocument.create()
    for (let i = 0; i < 120; i++) doc.addPage().drawText(`Lectia ${i + 1}`)
    original = await doc.save({ useObjectStreams: false })
  }
  const pages = (await PDFDocument.load(original)).getPageCount()
  await editor(page)
  const uploads: Uint8Array[] = []
  await page.route('**/api/parcursuri/1/fisiere', async route => {
    const request = route.request()
    const form = await new Request('http://fixture.test', { method: 'POST',
      headers: { 'content-type': request.headers()['content-type'] }, body: new Uint8Array(request.postDataBuffer()!).buffer }).formData()
    uploads.push(new Uint8Array(await (form.get('fisier') as File).arrayBuffer()))
    await route.fulfill({ status: 201, json: { id: 1 } })
  })
  await page.locator('input[type=file]').setInputFiles({ name: 'audit.pdf', mimeType: 'application/pdf', buffer: Buffer.from(original) })
  await expect(page.getByRole('button', { name: 'Previzualizează: audit.pdf', exact: true })).toBeVisible({ timeout: 30000 })
  await expect(page.getByRole('button', { name: /Previzualizează.*audit.pdf/ })).toHaveCount(1)
  await expect(page.getByRole('checkbox', { name: /Păstrează documentele originale/ })).toHaveCount(0)
  await expect(page.getByText(/Optimizăm documentele|Economie:|De trimis:|Original:/)).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Încarcă fișierele selectate' })).toBeEnabled({ timeout: 30000 })
  await page.getByRole('heading', { name: 'Fișierele lecției' }).scrollIntoViewIfNeeded()
  await page.getByRole('heading', { name: 'Fișierele lecției' }).locator('..').screenshot({ path: 'test-results/selectie-fisier.png' })
  await page.getByRole('button', { name: 'Încarcă fișierele selectate' }).click()
  await expect(page.getByRole('button', { name: 'Încarcă fișierele selectate' })).toHaveCount(0)
  const saved = uploads[0]
  expect(saved).toBeDefined()
  expect(saved!.length).toBeLessThan(original.length * 0.95)
  expect((await PDFDocument.load(saved!)).getPageCount()).toBe(pages)
  if (sample) { expect(original.length).toBe(1301840); expect(saved!.length).toBe(878801) }
})

test('recomprimă Office păstrând octeții fiecărui fișier intern', async ({ page }) => {
  await editor(page)
  const entries = { '[Content_Types].xml': strToU8('<Types/>'), 'word/document.xml': strToU8('<document><p><t>' + 'Text verificat '.repeat(6000) + '</t></p></document>'), 'word/media/imagine.bin': Uint8Array.from([0, 127, 255]) }
  const original = zipSync(entries, { level: 0 })
  const uploads: Uint8Array[] = []
  await page.route('**/api/parcursuri/1/fisiere', async route => {
    const request = route.request()
    const form = await new Request('http://fixture.test', { method: 'POST', headers: { 'content-type': request.headers()['content-type'] }, body: new Uint8Array(request.postDataBuffer()!).buffer }).formData()
    uploads.push(new Uint8Array(await (form.get('fisier') as File).arrayBuffer()))
    await route.fulfill({ status: 201, json: { id: 1 } })
  })
  await page.locator('input[type=file]').setInputFiles({ name: 'audit.docx', mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', buffer: Buffer.from(original) })
  await expect(page.getByRole('button', { name: 'Previzualizează: audit.docx', exact: true })).toBeVisible({ timeout: 30000 })
  await page.getByRole('button', { name: 'Încarcă fișierele selectate' }).click()
  await expect(page.getByRole('button', { name: 'Încarcă fișierele selectate' })).toHaveCount(0)
  const { unzipSync } = await import('fflate')
  const saved = uploads[0]
  expect(saved).toBeDefined()
  const extracted = unzipSync(saved)
  expect(Object.keys(extracted).sort()).toEqual(Object.keys(entries).sort())
  for (const [name, originalBytes] of Object.entries(entries)) expect(Buffer.from(extracted[name]).equals(Buffer.from(originalBytes))).toBe(true)
})
