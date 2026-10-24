import { describe, expect, it } from 'vitest'
import { PDFDocument } from 'pdf-lib'
import { zipSync, unzipSync, strToU8 } from 'fflate'
import { reconstruiesteDocument } from '@/lib/reconstructie-document.mjs'
import { marimeFisier } from '@/lib/marime-fisier'

describe('reconstrucția fără reducerea calității', () => {
  it('PDF păstrează paginile și metadatele, cu structură comprimată', async () => {
    const pdf = await PDFDocument.create()
    pdf.setTitle('Material academic'); pdf.setAuthor('Profesor')
    for (let i = 0; i < 30; i++) pdf.addPage([600, 800]).drawText(`Pagina ${i + 1}: ecuatii si diagrame`)
    const original = await pdf.save({ useObjectStreams: false })
    const r = await reconstruiesteDocument(original, 'pdf')
    expect(r.bytes.length).toBeLessThan(original.length)
    const reparsed = await PDFDocument.load(r.bytes)
    expect(reparsed.getPageCount()).toBe(30)
    expect(reparsed.getTitle()).toBe('Material academic')
    expect(reparsed.getAuthor()).toBe('Profesor')
    expect(reparsed.getPage(0).getSize()).toEqual({ width: 600, height: 800 })
  })
  it('PDF cu formulare rămâne original', async () => {
    const pdf = await PDFDocument.create(); const page = pdf.addPage()
    pdf.getForm().createTextField('Nume').addToPage(page)
    const r = await reconstruiesteDocument(await pdf.save(), 'pdf')
    expect(r.bytes).toBeUndefined()
    expect(r.motiv).toMatch(/formulare/)
  })
  it.each([['docx', 'word/document.xml'], ['pptx', 'ppt/presentation.xml'], ['xlsx', 'xl/workbook.xml']])('%s păstrează fiecare fișier intern octet cu octet', async (ext, principal) => {
    const entries = { '[Content_Types].xml': strToU8('<Types/>'), [principal]: strToU8('<document>' + 'Continut '.repeat(10000) + '</document>'), 'media/imagine.bin': Uint8Array.from([0, 255, 7, 42]) }
    const original = zipSync(entries, { level: 0 })
    const r = await reconstruiesteDocument(original, ext)
    expect(r.bytes.length).toBeLessThan(original.length)
    const extracted = unzipSync(r.bytes)
    expect(Object.keys(extracted).sort()).toEqual(Object.keys(entries).sort())
    for (const name of Object.keys(entries)) expect(Buffer.from(extracted[name]).equals(Buffer.from(entries[name]))).toBe(true)
  })
  it('Office semnat digital și arhivele prea mari nu sunt rescrise', async () => {
    const entries = { '[Content_Types].xml': strToU8('<Types/>'), 'word/document.xml': strToU8('<document/>'), '_xmlsignatures/sig.xml': strToU8('<signature/>') }
    expect((await reconstruiesteDocument(zipSync(entries), 'docx')).motiv).toMatch(/semnat/)
    const big = zipSync({ 'word/document.xml': new Uint8Array(65 * 1024 * 1024) })
    await expect(reconstruiesteDocument(big, 'docx')).rejects.toThrow(/complexă/)
  })
  it('dimensiunea nu ascunde economii prin rotunjire', () => {
    expect(marimeFisier(1301840)).toContain('1,24 MiB')
    expect(marimeFisier(1301840)).toContain('1.301.840 octeți')
    expect(marimeFisier(878801)).toContain('878.801 octeți')
  })
})
