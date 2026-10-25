// Decodarea și re-encodarea rulează în afara firului interfeței.
self.onmessage = async ({ data: fisier }) => {
  let imagine
  try {
    imagine = await createImageBitmap(fisier)
    if (imagine.width * imagine.height > 24_000_000) throw new Error('Imagine prea mare')
    const raport = Math.min(1, 2048 / Math.max(imagine.width, imagine.height))
    const canvas = new OffscreenCanvas(Math.max(1, Math.round(imagine.width * raport)), Math.max(1, Math.round(imagine.height * raport)))
    const context = canvas.getContext('2d')
    context.drawImage(imagine, 0, 0, canvas.width, canvas.height)
    const blob = await canvas.convertToBlob({ type: 'image/webp', quality: 0.88 })
    self.postMessage({ blob })
  } catch {
    self.postMessage({ blob: null })
  } finally {
    imagine?.close()
  }
}
