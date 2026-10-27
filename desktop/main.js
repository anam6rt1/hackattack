// Aplicația desktop TeleBac (Electron): același site ca în browser, ca export static (frontend/out), cu modul offline.
//   - app://telebac/...      fișierele site-ului, de pe disc (merg fără internet);
//   - app://telebac/api/...  trimise de aici serverului ales, cu sesiunea Electron: cookie-ul de cont rămâne first-party
//                             pentru server și nu ajunge niciodată la pagină (httpOnly, ca pe site);
//   - jocul (WebSocket) merge direct la server, cu un token de două minute (frontend/lib/canal-joc.js).
// Serverul se alege la prima pornire (frontend/components/ServerAplicatie.jsx) și se păstrează în config.json.

const { app, BrowserWindow, ipcMain, net, protocol, session, shell } = require('electron')
const path = require('node:path')
const fs = require('node:fs')
const { pathToFileURL } = require('node:url')

const GAZDA = 'telebac'
const ORIGINE = `app://${GAZDA}`
protocol.registerSchemesAsPrivileged([{ scheme: 'app', privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true, stream: true, codeCache: true } }])

// TELEBAC_DATE: a separate profile (config, cookies, downloaded Lecții), for the automated tests.
if (process.env.TELEBAC_DATE) app.setPath('userData', process.env.TELEBAC_DATE)

const SITE = app.isPackaged ? path.join(process.resourcesPath, 'site') : path.join(__dirname, '..', 'frontend', 'out')
const CONFIG = () => path.join(app.getPath('userData'), 'config.json')

function citesteConfig() {
  try { return JSON.parse(fs.readFileSync(CONFIG(), 'utf8')) } catch { return {} }
}
let server = null

/** Doar HTTPS (sau un server pe acest calculator, pentru dezvoltare). */
function valideaza(adresa) {
  const u = new URL(adresa)
  if (u.protocol !== 'https:' && !['localhost', '127.0.0.1'].includes(u.hostname)) throw new Error('Serverul trebuie să folosească HTTPS.')
  return u.origin
}

// Antete pe care nu le trimitem serverului: originea internă a aplicației și cookie-urile paginii (sesiunea o pune Electron).
const FARA = ['host', 'origin', 'referer', 'cookie']

async function catreServer(cerere, u) {
  if (!server) return Response.json({ mesaj: 'Alege serverul aplicației în Setări.' }, { status: 503 })
  const antete = new Headers(cerere.headers)
  for (const a of FARA) antete.delete(a)
  try {
    return await net.fetch(server + u.pathname + u.search, {
      method: cerere.method, headers: antete, body: cerere.body, duplex: 'half', credentials: 'include', redirect: 'manual', bypassCustomProtocolHandlers: true,
    })
  } catch {
    // Fără rețea: pagina primește o eroare de rețea, ca în browser, și trece pe lecțiile descărcate.
    return Response.error()
  }
}

function fisierSite(pathname) {
  const rel = decodeURIComponent(pathname).replace(/^\/+/, '')
  for (const candidat of [rel, path.join(rel, 'index.html'), `${rel}.html`]) {
    const complet = path.resolve(SITE, candidat)
    // Nimic din afara site-ului (../).
    if (!complet.startsWith(SITE + path.sep) && complet !== SITE) continue
    if (fs.existsSync(complet) && fs.statSync(complet).isFile()) return net.fetch(pathToFileURL(complet).toString(), { bypassCustomProtocolHandlers: true })
  }
  const lipsa = path.join(SITE, '404.html')
  return new Response(fs.existsSync(lipsa) ? fs.readFileSync(lipsa) : 'Pagina nu există.', { status: 404, headers: { 'content-type': 'text/html; charset=utf-8' } })
}

function fereastra() {
  const win = new BrowserWindow({
    width: 1280, height: 860, minWidth: 360, minHeight: 560, title: 'TeleBac', autoHideMenuBar: true, backgroundColor: '#163044',
    // TELEBAC_ASCUNS=1: the automated tests drive the app without showing a window on the developer's desktop.
    show: process.env.TELEBAC_ASCUNS !== '1',
    webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true, sandbox: true, nodeIntegration: false, backgroundThrottling: process.env.TELEBAC_ASCUNS !== '1' },
  })
  // Linkurile spre alte site-uri se deschid în browser; PDF-urile din lecții (blob:) într-o fereastră a aplicației.
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:/.test(url)) { shell.openExternal(url); return { action: 'deny' } }
    return { action: url.startsWith('blob:') || url.startsWith(ORIGINE) ? 'allow' : 'deny' }
  })
  win.webContents.on('will-navigate', (e, url) => {
    if (url.startsWith(ORIGINE)) return
    e.preventDefault()
    if (/^https?:/.test(url)) shell.openExternal(url)
  })
  win.loadURL(`${ORIGINE}/`)
  return win
}

if (!app.requestSingleInstanceLock()) app.quit()
app.on('second-instance', () => { const w = BrowserWindow.getAllWindows()[0]; if (w) { if (w.isMinimized()) w.restore(); w.focus() } })

app.whenReady().then(() => {
  try { server = valideaza(citesteConfig().server ?? process.env.TELEBAC_SERVER) } catch { server = null }

  protocol.handle('app', (cerere) => {
    const u = new URL(cerere.url)
    if (u.host !== GAZDA) return new Response('', { status: 404 })
    // The server is the website: its API lives under /api, as in the browser (frontend/next.config.ts).
    return u.pathname.startsWith('/api/') ? catreServer(cerere, u) : fisierSite(u.pathname)
  })

  ipcMain.on('telebac:server', (e) => { e.returnValue = server })
  ipcMain.handle('telebac:alege-server', async (_, adresa) => {
    const nou = valideaza(adresa)
    const r = await net.fetch(`${nou}/api/judete`, { headers: { 'X-MarkAi': '1' } }).catch(() => null)
    if (!r?.ok) throw new Error('Serverul nu răspunde. Verifică adresa și conexiunea la internet.')
    // Alt server, alte conturi: sesiunea vechii adrese nu mai e folosită.
    if (server && server !== nou) await session.defaultSession.clearStorageData({ origin: server, storages: ['cookies'] })
    server = nou
    fs.mkdirSync(path.dirname(CONFIG()), { recursive: true })
    fs.writeFileSync(CONFIG(), JSON.stringify({ ...citesteConfig(), server }, null, 2))
    return server
  })

  fereastra()
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) fereastra() })
})

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit() })
