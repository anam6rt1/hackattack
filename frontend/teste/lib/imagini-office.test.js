import { describe, expect, it, vi } from 'vitest'
import { crc32 } from 'node:zlib'
import { zipSync, unzipSync, strToU8, strFromU8, zlibSync, unzlibSync } from 'fflate'
import { recomprimaPng, orientareJpeg, micsoreazaImagini } from '@/lib/imagini-office.mjs'
import { reconstruiesteDocument } from '@/lib/reconstructie-document.mjs'

const be32 = n => [n >>> 24, (n >>> 16) & 255, (n >>> 8) & 255, n & 255]
function bucata(tip, corp) {
  const t = strToU8(tip)
  return [...be32(corp.length), ...t, ...corp, ...be32(crc32(Uint8Array.from([...t, ...corp])))]
}
// A 64×64 RGB image whose pixels are stored without compression (level 0), plus a text chunk.
function pngNecomprimat({ animat = false } = {}) {
  const ihdr = [...be32(64), ...be32(64), 8, 2, 0, 0, 0]
  const randuri = new Uint8Array(64 * (1 + 64 * 3)).map((_, i) => i % 193 === 0 ? 0 : (i * 7) % 3)
  return Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, ...bucata('IHDR', ihdr), ...(animat ? bucata('acTL', [0, 0, 0, 1, 0, 0, 0, 0]) : []),
    ...bucata('tEXt', strToU8('Software\0Editor')), ...bucata('IDAT', zlibSync(randuri, { level: 0 })), ...bucata('IEND', [])])
}
function bucati(png) {
  const r = []; let i = 8
  while (i < png.length) { const n = (png[i] << 24 | png[i + 1] << 16 | png[i + 2] << 8 | png[i + 3]) >>> 0; r.push({ tip: strFromU8(png.subarray(i + 4, i + 8)), corp: png.subarray(i + 8, i + 8 + n), crc: png.subarray(i + 8 + n, i + 12 + n) }); i += 12 + n }
  return r
}
function jpegCuOrientare(orientare, le = true) {
  const v16 = n => le ? [n & 255, n >> 8] : [n >> 8, n & 255], v32 = n => le ? [...be32(n)].reverse() : be32(n)
  const tiff = [...(le ? [0x49, 0x49] : [0x4d, 0x4d]), ...v16(42), ...v32(8), ...v16(1), ...v16(0x0112), ...v16(3), ...v32(1), ...v16(orientare), 0, 0, ...v32(0)]
  const app1 = [...strToU8('Exif'), 0, 0, ...tiff]
  return Uint8Array.from([0xff, 0xd8, 0xff, 0xe1, (app1.length + 2) >> 8, (app1.length + 2) & 255, ...app1, 0xff, 0xda, 0, 2, 0xff, 0xd9])
}

describe('PNG fără pierderi', () => {
  it('recomprimă datele, păstrează pixelii și elimină textul, cu CRC valid', async () => {
    const original = pngNecomprimat(), nou = await recomprimaPng(original)
    expect(nou.length).toBeLessThan(original.length * 0.5)
    const [a, b] = [bucati(original), bucati(nou)]
    expect(b.map(x => x.tip)).toEqual(['IHDR', 'IDAT', 'IEND'])
    expect(Buffer.from(unzlibSync(b[1].corp)).equals(Buffer.from(unzlibSync(a.find(x => x.tip === 'IDAT').corp)))).toBe(true)
    for (const x of b) expect(Buffer.from(x.crc).readUInt32BE()).toBe(crc32(Uint8Array.from([...strToU8(x.tip), ...x.corp])))
  })
  it('PNG animat sau invalid rămâne neschimbat', async () => {
    const animat = pngNecomprimat({ animat: true })
    expect(await recomprimaPng(animat)).toBe(animat)
    const rupt = Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3])
    expect(await recomprimaPng(rupt)).toBe(rupt)
  })
})

describe('orientarea EXIF', () => {
  it.each([[6, true], [3, false], [1, true]])('citește orientarea %i', (o, le) => expect(orientareJpeg(jpegCuOrientare(o, le))).toBe(o))
  it('fără EXIF este 1', () => expect(orientareJpeg(Uint8Array.from([0xff, 0xd8, 0xff, 0xda, 0, 2]))).toBe(1))
})

const tipuri = '<?xml version="1.0"?><Types xmlns="x"><Default Extension="png" ContentType="image/png"/><Override PartName="/ppt/media/image2.png" ContentType="image/png"/></Types>'
const relsSlide = '<Relationships><Relationship Id="rId1" Target="../media/image1.png"/><Relationship Id="rId2" Target="/ppt/media/image2.png"/><Relationship Id="rId3" TargetMode="External" Target="../media/image1.png"/><Relationship Id="rId4" Target="../slideLayouts/slideLayout1.xml"/></Relationships>'

describe('imaginile din Office', () => {
  it('fotografia PNG devine JPEG, iar relațiile și tipurile o urmează', async () => {
    const entries = { '[Content_Types].xml': strToU8(tipuri), 'ppt/slides/_rels/slide1.xml.rels': strToU8(relsSlide),
      'ppt/media/image1.png': pngNecomprimat(), 'ppt/media/image2.png': pngNecomprimat(), 'ppt/media/image1.jpeg': Uint8Array.from([0xff, 0xd8, 0xff, 0xd9]) }
    const codeaza = vi.fn(async (_b, { png }) => png ? { date: Uint8Array.from([0xff, 0xd8, 1, 0xff, 0xd9]), jpeg: true } : null)
    const redenumiri = await micsoreazaImagini(entries, codeaza)
    expect(Object.fromEntries(redenumiri)).toEqual({ 'ppt/media/image1.png': 'ppt/media/image1-2.jpeg', 'ppt/media/image2.png': 'ppt/media/image2.jpeg' })
    expect(entries['ppt/media/image1.png']).toBeUndefined()
    const rels = strFromU8(entries['ppt/slides/_rels/slide1.xml.rels'])
    expect(rels).toContain('Id="rId1" Target="../media/image1-2.jpeg"')
    expect(rels).toContain('Id="rId2" Target="/ppt/media/image2.jpeg"')
    expect(rels).toContain('TargetMode="External" Target="../media/image1.png"')
    expect(rels).toContain('Target="../slideLayouts/slideLayout1.xml"')
    const t = strFromU8(entries['[Content_Types].xml'])
    expect(t).toContain('<Override PartName="/ppt/media/image2.jpeg" ContentType="image/jpeg"/>')
    expect(t).toContain('<Default Extension="jpeg" ContentType="image/jpeg"/></Types>')
  })
  it('JPEG rotit prin EXIF nu este atins, iar rezultatul mai mare este ignorat', async () => {
    const rotit = jpegCuOrientare(6), entries = { 'word/media/a.jpg': rotit, 'word/media/b.png': pngNecomprimat() }
    const codeaza = vi.fn(async () => ({ date: new Uint8Array(1_000_000), jpeg: false }))
    await micsoreazaImagini(entries, codeaza)
    expect(entries['word/media/a.jpg']).toBe(rotit)
    expect(codeaza).toHaveBeenCalledOnce()
    expect(entries['word/media/b.png'].length).toBeLessThan(pngNecomprimat().length)
  })
  it('fără encoder rulează doar pasul fără pierderi, iar documentul rămâne valid', async () => {
    const xml = '<w:document>' + 'Text '.repeat(2000) + '</w:document>'
    const original = zipSync({ '[Content_Types].xml': strToU8(tipuri), 'word/document.xml': strToU8(xml), 'word/media/image1.png': pngNecomprimat() }, { level: 0 })
    const r = await reconstruiesteDocument(original, 'docx')
    const e = unzipSync(r.bytes)
    expect(strFromU8(e['word/document.xml'])).toBe(xml)
    expect(e['word/media/image1.png'].length).toBeLessThan(pngNecomprimat().length)
  })
})
