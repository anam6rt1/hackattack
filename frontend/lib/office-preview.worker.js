import { unzipSync, strFromU8 } from 'fflate'

self.onmessage = async ({ data: fisier }) => {
  try {
    if (fisier.size > 20 * 1024 * 1024) throw new Error('Previzualizarea locală este limitată la 20 MB.')
    let total = 0, intrari = 0
    const fisiere = unzipSync(new Uint8Array(await fisier.arrayBuffer()), { filter: f => {
      if (++intrari > 5000) throw new Error('Documentul conține prea multe elemente.')
      if (!/^(word\/document\.xml|ppt\/(presentation\.xml|_rels\/presentation\.xml.rels|slides\/slide\d+\.xml)|xl\/(workbook\.xml|_rels\/workbook\.xml.rels|sharedStrings\.xml|worksheets\/sheet\d+\.xml))$/.test(f.name)) return false
      total += f.originalSize
      if (f.originalSize > 2 * 1024 * 1024 || total > 8 * 1024 * 1024) throw new Error('Documentul este prea complex pentru previzualizarea locală.')
      return true
    } })
    self.postMessage({ xml: Object.fromEntries(Object.entries(fisiere).map(([n, b]) => [n, strFromU8(b)])) })
  } catch (e) { self.postMessage({ eroare: e.message || 'Documentul nu poate fi citit.' }) }
}
