import { reconstruiesteDocument } from './reconstructie-document.mjs'

self.onmessage = async ({ data: fisier }) => {
  try {
    const result = await reconstruiesteDocument(new Uint8Array(await fisier.arrayBuffer()), fisier.name.split('.').pop().toLowerCase())
    if (result.bytes) self.postMessage(result, [result.bytes.buffer])
    else self.postMessage(result)
  } catch {
    self.postMessage({ motiv: 'Document protejat, invalid sau prea complex: păstrăm originalul.' })
  }
}
