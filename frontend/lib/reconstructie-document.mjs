import { unzipSync, zipSync } from 'fflate'
import { esteImagineOffice, micsoreazaImagini } from './imagini-office.mjs'

// PDF: structural rewrite only, pages are never rasterized. Office: text and XML stay byte-for-byte identical; the images in
// the media folders are made smaller (see imagini-office.mjs), with `codeaza` (the browser's encoder) when it is available.
export async function reconstruiesteDocument(bytes, ext, { codeaza } = {}) {
  if (ext === 'pdf') {
    const { PDFDocument, PDFName } = await import('pdf-lib')
    const pdf = await PDFDocument.load(bytes, { updateMetadata: false, throwOnInvalidObject: true })
    // Rewriting invalidates digital signatures. Preserve forms, permissions and signatures conservatively.
    if (pdf.isEncrypted || pdf.catalog.has(PDFName.of('AcroForm')) || pdf.catalog.has(PDFName.of('Perms')))
      return { motiv: 'Document cu formulare, semnături sau protecție: păstrăm originalul.' }
    if (pdf.getPageCount() > 500) return { motiv: 'Peste 500 de pagini: păstrăm originalul.' }
    const output = await pdf.save({ useObjectStreams: true, addDefaultPage: false, updateFieldAppearances: false })
    return { bytes: output }
  }
  const principal = { docx: 'word/document.xml', pptx: 'ppt/presentation.xml', xlsx: 'xl/workbook.xml' }[ext]
  if (!principal) return { motiv: 'Acest format nu are optimizare locală disponibilă.' }
  let total = 0, count = 0
  const seen = new Set()
  const entries = unzipSync(bytes, { filter: f => {
    total += f.originalSize
    if (++count > 5000 || total > 160 * 1024 * 1024 || seen.has(f.name)) throw new Error('Arhivă prea complexă sau ambiguă.')
    seen.add(f.name)
    return true
  } })
  if (!entries['[Content_Types].xml'] || !entries[principal]) throw new Error('Document Office invalid.')
  if ([...seen].some(n => n.toLowerCase().includes('_xmlsignatures/')) ||
      Object.entries(entries).some(([n, b]) => n.endsWith('.rels') && new TextDecoder().decode(b).includes('/digital-signature/')))
    return { motiv: 'Document semnat digital: păstrăm originalul.' }
  await micsoreazaImagini(entries, codeaza)
  // JPEG and PNG are compressed already: storing them as they are is as small and much faster than deflating them again.
  const arhiva = Object.fromEntries(Object.entries(entries).map(([n, b]) => [n, esteImagineOffice(n) ? [b, { level: 0 }] : b]))
  return { bytes: zipSync(arhiva, { level: 9, mtime: new Date(1980, 0, 1) }) }
}
