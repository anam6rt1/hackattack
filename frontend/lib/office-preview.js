const elemente = (doc, tag) => [...doc.getElementsByTagNameNS('*', tag)]
const texte = doc => elemente(doc, 't').map(t => t.textContent).join('')
function parse(xml) {
  if (!xml || /<!DOCTYPE|<!ENTITY/i.test(xml)) throw new Error('Document invalid sau format neacceptat.')
  const doc = new DOMParser().parseFromString(xml, 'application/xml')
  if (doc.getElementsByTagName('parsererror').length) throw new Error('Documentul conține XML invalid.')
  return doc
}
function relatii(xml, baza) {
  const result = new Map()
  for (const r of elemente(parse(xml), 'Relationship')) {
    const path = r.getAttribute('Target') || ''
    if (r.getAttribute('TargetMode') === 'External') continue
    // Only exact whitelisted internal entries can be read; no URLs or relative traversal.
    result.set(r.getAttribute('Id'), path.startsWith('/') ? path.slice(1) : baza + path)
  }
  return result
}
export function interpreteazaOffice(xml, extensie) {
  if (extensie === 'docx') {
    const doc = parse(xml['word/document.xml'])
    return [{ titlu: 'Document Word', linii: elemente(doc, 'p').slice(0, 200).map(texte).filter(Boolean).map(t => t.slice(0, 2000)) }]
  }
  if (extensie === 'pptx') {
    const doc = parse(xml['ppt/presentation.xml'])
    const rels = relatii(xml['ppt/_rels/presentation.xml.rels'], 'ppt/')
    return elemente(doc, 'sldId').slice(0, 20).map((s, i) => {
      const id = s.getAttributeNS('http://schemas.openxmlformats.org/officeDocument/2006/relationships', 'id')
      const slide = parse(xml[rels.get(id)])
      return { titlu: `Diapozitivul ${i + 1}`, linii: elemente(slide, 'p').slice(0, 100).map(texte).filter(Boolean).map(t => t.slice(0, 2000)) }
    })
  }
  const workbook = parse(xml['xl/workbook.xml'])
  const rels = relatii(xml['xl/_rels/workbook.xml.rels'], 'xl/')
  const shared = xml['xl/sharedStrings.xml'] ? elemente(parse(xml['xl/sharedStrings.xml']), 'si').map(texte) : []
  return elemente(workbook, 'sheet').slice(0, 5).map(sheet => {
    const id = sheet.getAttributeNS('http://schemas.openxmlformats.org/officeDocument/2006/relationships', 'id')
    const doc = parse(xml[rels.get(id)])
    const randuri = elemente(doc, 'row').slice(0, 50).map(row => {
      const cells = []
      for (const c of elemente(row, 'c')) {
        const ref = c.getAttribute('r') || ''
        const col = (ref.match(/^[A-Z]+/)?.[0] || 'A').split('').reduce((n, x) => n * 26 + x.charCodeAt(0) - 64, 0) - 1
        if (col > 19) continue
        const v = elemente(c, 'v')[0]?.textContent || ''
        const type = c.getAttribute('t')
        const formula = elemente(c, 'f')[0]?.textContent
        cells[col] = String(type === 's' ? shared[Number(v)] ?? '' : type === 'inlineStr' ? texte(c) : v || (formula ? `=${formula}` : '')).slice(0, 1000)
      }
      return { numar: row.getAttribute('r'), celule: Array.from({ length: Math.min(20, cells.length) }, (_, i) => cells[i] || '') }
    })
    return { titlu: sheet.getAttribute('name'), randuri }
  })
}

export function citesteOffice(fisier, signal) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) return reject(new DOMException('Anulat', 'AbortError'))
    const worker = new Worker(new URL('./office-preview.worker.js', import.meta.url))
    const cleanup = () => { clearTimeout(timer); worker.terminate(); signal?.removeEventListener('abort', abort) }
    const fail = e => { cleanup(); reject(e) }
    const abort = () => fail(new DOMException('Anulat', 'AbortError'))
    const timer = setTimeout(() => fail(new Error('Previzualizarea a durat prea mult.')), 10000)
    signal?.addEventListener('abort', abort, { once: true })
    worker.onerror = () => fail(new Error('Previzualizarea nu este disponibilă în acest browser.'))
    worker.onmessageerror = () => fail(new Error('Documentul nu poate fi citit.'))
    worker.onmessage = ({ data }) => {
      cleanup()
      if (data.eroare) reject(new Error(data.eroare))
      else { try { resolve(interpreteazaOffice(data.xml, fisier.name.split('.').pop().toLowerCase())) } catch (e) { reject(e) } }
    }
    worker.postMessage(fisier)
  })
}
