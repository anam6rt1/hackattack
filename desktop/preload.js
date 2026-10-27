// Puntea dintre pagină și procesul principal: doar adresa serverului și schimbarea ei. Pagina rămâne izolată (contextIsolation,
// sandbox); frontend/lib/platforma.js recunoaște aplicația desktop după window.telebacDesktop.
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('telebacDesktop', {
  server: ipcRenderer.sendSync('telebac:server'),
  alegeServer: (adresa) => ipcRenderer.invoke('telebac:alege-server', adresa),
})
