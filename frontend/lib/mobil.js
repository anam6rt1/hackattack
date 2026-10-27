// Ce are în plus aplicația mobilă (Capacitor) față de site. Pluginurile native se folosesc prin window.Capacitor.Plugins,
// înregistrate de aplicație (mobil/package.json), ca frontendul să nu depindă de pachetele Capacitor.
import { platforma } from './platforma'

const plugin = (nume) => window.Capacitor?.Plugins?.[nume]

/** Butonul „înapoi” de pe Android: înapoi în aplicație; din pagina de start, iese. */
export function pornesteMobil() {
  if (platforma() !== 'mobil') return () => {}
  const asculta = plugin('App')?.addListener('backButton', ({ canGoBack }) => (canGoBack ? window.history.back() : plugin('App').exitApp()))
  return () => { asculta?.then?.((a) => a.remove()) }
}

const base64 = (blob) => new Promise((resolve, reject) => {
  const r = new FileReader()
  r.onload = () => resolve(String(r.result).split(',')[1])
  r.onerror = () => reject(r.error)
  r.readAsDataURL(blob)
})

/** Un fișier al lecției pe telefon: îl scrie în cache-ul aplicației și îl dă unei aplicații (PDF, Office) prin meniul de partajare. */
export async function deschideFisierPeMobil(blob, nume) {
  const { Filesystem, Share } = window.Capacitor.Plugins
  const sigur = nume.replace(/[^\p{L}\p{N}._ -]/gu, '_')
  const { uri } = await Filesystem.writeFile({ path: `fisiere/${sigur}`, data: await base64(blob), directory: 'CACHE', recursive: true })
  await Share.share({ title: nume, url: uri, dialogTitle: 'Deschide cu' })
}
