import { unzipSync, zipSync } from 'fflate'

// Rewrites containers, never rasterizes pages or changes Office entry contents.
export async function reconstruiesteDocument(bytes, ext) {
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
    if (++count > 5000 || total > 64 * 1024 * 1024 || seen.has(f.name)) throw new Error('Arhivă prea complexă sau ambiguă.')
    seen.add(f.name)
    return true
  } })
  if (!entries['[Content_Types].xml'] || !entries[principal]) throw new Error('Document Office invalid.')
  if ([...seen].some(n => n.toLowerCase().includes('_xmlsignatures/')) ||
      Object.entries(entries).some(([n, b]) => n.endsWith('.rels') && new TextDecoder().decode(b).includes('/digital-signature/')))
    return { motiv: 'Document semnat digital: păstrăm originalul.' }
  // XML, images, relationships and embedded media remain byte-for-byte identical after decompression.
  return { bytes: zipSync(entries, { level: 9, mtime: new Date(1980, 0, 1) }) }
}
