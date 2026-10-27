import { unzlibSync, zlibSync, strFromU8, strToU8 } from 'fflate'

// Images inside DOCX/PPTX/XLSX: owner decision (plan 4) — always make them smaller, even with some loss of quality:
// longest side at most 1920 px, opaque photos stored as JPEG (quality 0.85). Diagrams, text and transparent images stay PNG.
export const LATURA_MAXIMA = 1920
export const CALITATE_JPEG = 0.85

const SEMNATURA_PNG = [137, 80, 78, 71, 13, 10, 26, 10]
const CRC = new Uint32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0 })
function crc32(bytes) { let c = 0xffffffff; for (const b of bytes) c = CRC[(c ^ b) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0 }
const u32 = (b, i) => ((b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]) >>> 0

export const estePng = b => b.length > 33 && SEMNATURA_PNG.every((x, i) => b[i] === x)
export const esteJpeg = b => b.length > 4 && b[0] === 0xff && b[1] === 0xd8

/** The PNG colour type (3 = palette: a diagram or drawing, never turned into JPEG). */
export const tipCuloarePng = b => b[25]

/**
 * Lossless: the same pixels and filters, only the compressed data (IDAT) redone at level 9 and text/EXIF chunks dropped.
 * Animated PNGs and anything unexpected come back unchanged.
 */
// Level 9 (fflate) shrinks diagrams ~20% more than the browser's native stream (a fixed, lower level) but is 5–10× slower, so
// it runs only while the document's budget of raw pixels lasts; the rest goes through the native stream.
export const bugetNivel9 = () => ({ octeti: 16 * 1024 * 1024 })
const nativ = () => typeof CompressionStream === 'function' && typeof DecompressionStream === 'function'
async function prinFlux(bytes, flux) {
  return new Uint8Array(await new Response(new Blob([bytes]).stream().pipeThrough(flux)).arrayBuffer())
}
async function redeflate(date, buget) {
  const brut = nativ() ? await prinFlux(date, new DecompressionStream('deflate')).catch(() => unzlibSync(date)) : unzlibSync(date)
  if (!nativ() || brut.length <= buget.octeti) { buget.octeti -= brut.length; return zlibSync(brut, { level: 9 }) }
  return prinFlux(brut, new CompressionStream('deflate'))
}

export async function recomprimaPng(bytes, buget = bugetNivel9()) {
  if (!estePng(bytes)) return bytes
  const pastrate = new Set(['IHDR', 'PLTE', 'tRNS', 'iCCP', 'sRGB', 'gAMA', 'cHRM', 'pHYs', 'sBIT', 'IEND'])
  const bucati = [], date = []
  let poz = 8, ihdr = null
  try {
    while (poz + 12 <= bytes.length) {
      const n = u32(bytes, poz), tip = String.fromCharCode(...bytes.subarray(poz + 4, poz + 8)), corp = bytes.subarray(poz + 8, poz + 8 + n)
      poz += 12 + n
      if (tip === 'acTL') return bytes
      if (tip === 'IHDR') ihdr = corp
      if (tip === 'IDAT') { if (!date.length) bucati.push(['IDAT', null]); date.push(corp); continue }
      if (pastrate.has(tip)) bucati.push([tip, corp])
      if (tip === 'IEND') break
    }
    if (!ihdr || !date.length) return bytes
    const vechi = new Uint8Array(date.reduce((s, d) => s + d.length, 0))
    date.reduce((o, d) => (vechi.set(d, o), o + d.length), 0)
    const nou = await redeflate(vechi, buget)
    const idat = nou.length < vechi.length ? nou : vechi
    const parti = [Uint8Array.from(SEMNATURA_PNG)]
    for (const [tip, corp] of bucati) {
      const c = corp ?? idat, cap = new Uint8Array(8 + c.length)
      cap.set([c.length >>> 24, (c.length >>> 16) & 255, (c.length >>> 8) & 255, c.length & 255]); cap.set(strToU8(tip), 4); cap.set(c, 8)
      const crc = crc32(cap.subarray(4))
      parti.push(cap, Uint8Array.from([crc >>> 24, (crc >>> 16) & 255, (crc >>> 8) & 255, crc & 255]))
    }
    const rezultat = new Uint8Array(parti.reduce((s, p) => s + p.length, 0))
    parti.reduce((o, p) => (rezultat.set(p, o), o + p.length), 0)
    return rezultat.length < bytes.length ? rezultat : bytes
  } catch {
    return bytes
  }
}

/** EXIF orientation of a JPEG (1 = as stored). Office and browsers disagree on rotated photos, so those are left alone. */
export function orientareJpeg(b) {
  let i = 2
  while (i + 4 < b.length && b[i] === 0xff) {
    const marker = b[i + 1], n = (b[i + 2] << 8) | b[i + 3]
    if (marker === 0xda) break
    if (marker === 0xe1 && strFromU8(b.subarray(i + 4, i + 8)) === 'Exif') {
      const t = i + 10, le = b[t] === 0x49
      const r16 = o => le ? b[t + o] | (b[t + o + 1] << 8) : (b[t + o] << 8) | b[t + o + 1]
      const r32 = o => le ? (b[t + o] | (b[t + o + 1] << 8) | (b[t + o + 2] << 16) | (b[t + o + 3] << 24)) >>> 0 : u32(b, t + o)
      const ifd = r32(4), intrari = r16(ifd)
      for (let k = 0; k < intrari; k++) if (r16(ifd + 2 + k * 12) === 0x0112) return r16(ifd + 2 + k * 12 + 8)
      return 1
    }
    i += 2 + n
  }
  return 1
}

const extensie = nume => nume.slice(nume.lastIndexOf('.') + 1).toLowerCase()
export const esteImagineOffice = nume => /\/media\/[^/]+\.(png|jpe?g)$/i.test(nume)

/**
 * Makes every image in a media folder (ppt/media, word/media, xl/media) smaller. `codeaza(bytes, { png, jpeg })` is the browser's encoder (see the worker); without it
 * (Node, old browsers) only the lossless PNG step runs. Returns the renames (image1.png → image1.jpeg) the caller must apply.
 */
export async function micsoreazaImagini(entries, codeaza) {
  const redenumiri = new Map(), buget = bugetNivel9()
  for (const nume of Object.keys(entries).filter(esteImagineOffice)) {
    let date = entries[nume]
    const ext = extensie(nume), png = ext === 'png'
    if (png ? !estePng(date) : !esteJpeg(date)) continue
    let nou = null
    if (codeaza && (png || orientareJpeg(date) === 1)) {
      try { nou = await codeaza(date, { png, paleta: png && tipCuloarePng(date) === 3, buget }) } catch { nou = null }
    }
    if (nou && nou.jpeg && png && nou.date.length < date.length) {
      let tinta = nume.replace(/\.png$/i, '.jpeg')
      for (let k = 2; entries[tinta] || [...redenumiri.values()].includes(tinta); k++) tinta = nume.replace(/\.png$/i, `-${k}.jpeg`)
      redenumiri.set(nume, tinta)
      delete entries[nume]
      entries[tinta] = nou.date
      continue
    }
    // The lossless pass only for a PNG that stays at full size (a downscaled one comes back from the encoder already redone):
    // re-deflating a large image in JavaScript is the slowest step, so it never runs on pixels that are about to be replaced.
    if (nou && !!nou.jpeg === !png && nou.date.length < date.length) date = nou.date
    else if (png) date = await recomprimaPng(date, buget)
    entries[nume] = date
  }
  if (redenumiri.size) actualizeazaReferinte(entries, redenumiri)
  return redenumiri
}

// "ppt/slides/_rels/slide1.xml.rels" describes "ppt/slides/slide1.xml": its relative targets start from "ppt/slides/".
function directorSursa(rels) {
  const parti = rels.split('/'); parti.splice(-2, 2)
  return parti
}
function rezolva(baza, tinta) {
  const parti = tinta.startsWith('/') ? [] : [...baza]
  for (const p of tinta.replace(/^\//, '').split('/')) { if (p === '..') parti.pop(); else if (p && p !== '.') parti.push(p) }
  return parti.join('/')
}

/** Points every relationship and the content types at the renamed images. */
export function actualizeazaReferinte(entries, redenumiri) {
  for (const nume of Object.keys(entries).filter(n => n.endsWith('.rels'))) {
    const baza = directorSursa(nume), xml = strFromU8(entries[nume])
    const nou = xml.replace(/<Relationship\b[^>]*>/g, rel => {
      if (/TargetMode="External"/.test(rel)) return rel
      return rel.replace(/Target="([^"]+)"/, (tot, tinta) => {
        const redenumit = redenumiri.get(rezolva(baza, decodeURI(tinta)))
        if (!redenumit) return tot
        const vechi = tinta.slice(tinta.lastIndexOf('/') + 1)
        return `Target="${tinta.slice(0, tinta.length - vechi.length)}${encodeURI(redenumit.slice(redenumit.lastIndexOf('/') + 1))}"`
      })
    })
    if (nou !== xml) entries[nume] = strToU8(nou)
  }
  const tipuri = '[Content_Types].xml'
  if (!entries[tipuri]) return
  let xml = strFromU8(entries[tipuri])
  for (const [vechi, nou] of redenumiri)
    xml = xml.replace(new RegExp(`<Override\\b[^>]*PartName="/${vechi.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}"[^>]*>`, 'g'),
      o => o.replace(/PartName="[^"]+"/, `PartName="/${nou}"`).replace(/ContentType="[^"]+"/, 'ContentType="image/jpeg"'))
  if (!/<Default\b[^>]*Extension="jpeg"/i.test(xml)) xml = xml.replace(/<\/Types>\s*$/, '<Default Extension="jpeg" ContentType="image/jpeg"/></Types>')
  entries[tipuri] = strToU8(xml)
}

// Photos have many colours and no transparency; diagrams, charts and text have few colours or a transparent background.
function esteFotografie(px) {
  for (let i = 3; i < px.length; i += 4) if (px[i] !== 255) return false
  const pas = Math.max(1, Math.floor(px.length / 4 / 65536)), culori = new Set()
  for (let i = 0; i < px.length && culori.size < 1024; i += 4 * pas) culori.add((px[i] << 16) | (px[i + 1] << 8) | px[i + 2])
  return culori.size >= 1024
}

/** The browser encoder for micsoreazaImagini (runs in the document worker: OffscreenCanvas, no page thread). */
export async function codeazaCuCanvas(bytes, { png, paleta, buget }) {
  if (typeof OffscreenCanvas === 'undefined' || typeof createImageBitmap === 'undefined') return null
  const imagine = await createImageBitmap(new Blob([bytes], { type: png ? 'image/png' : 'image/jpeg' }))
  try {
    const { width: l, height: h } = imagine
    if (l * h > 40_000_000) return null
    const raport = Math.min(1, LATURA_MAXIMA / Math.max(l, h))
    if (raport === 1 && paleta) return null
    const canvas = new OffscreenCanvas(Math.max(1, Math.round(l * raport)), Math.max(1, Math.round(h * raport)))
    const ctx = canvas.getContext('2d', { willReadFrequently: png })
    ctx.imageSmoothingQuality = 'high'
    ctx.drawImage(imagine, 0, 0, canvas.width, canvas.height)
    const codeaza = async (type, quality) => new Uint8Array(await (await canvas.convertToBlob({ type, quality })).arrayBuffer())
    if (!png) {
      const jpeg = await codeaza('image/jpeg', CALITATE_JPEG)
      // Same size: re-encoding an already compressed JPEG only pays off when the gain is clear.
      return raport < 1 || jpeg.length < bytes.length * 0.9 ? { date: jpeg, jpeg: true } : null
    }
    if (!paleta && esteFotografie(ctx.getImageData(0, 0, canvas.width, canvas.height).data)) return { date: await codeaza('image/jpeg', CALITATE_JPEG), jpeg: true }
    return raport < 1 ? { date: await recomprimaPng(await codeaza('image/png'), buget), jpeg: false } : null
  } finally {
    imagine.close()
  }
}
