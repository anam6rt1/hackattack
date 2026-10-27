// Canalul unei camere Triviador, prin hub-ul SignalR al API-ului (/hub/joc).
// Jocul rulează pe server: aici doar trimitem intenții (atac, răspuns, estimare...) și primim starea publică
// după fiecare schimbare. Identitatea jucătorului vine din cookie-ul de autentificare, nu din browser.
// În aplicațiile desktop/mobil, WebSocket-ul pleacă din originea aplicației și nu poartă cookie-ul: cere un token de două minute,
// valabil doar pentru /hub (POST /utilizatori/me/token-joc), la fiecare (re)conectare.

import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { API, api } from '@/lib/api'
import { adresaSite, esteAplicatie } from '@/lib/platforma'

const optiuniConexiune = () => esteAplicatie()
  ? { url: adresaSite() + '/api/hub/joc', accessTokenFactory: async () => (await api('/utilizatori/me/token-joc', { metoda: 'POST' })).token }
  : { url: API + '/hub/joc', withCredentials: true }

export function deschideCanal(cod, { onStare, onConectat, onDeconectat, onEroare }) {
  const { url, ...optiuni } = optiuniConexiune()
  const conexiune = new HubConnectionBuilder()
    .withUrl(url, optiuni)
    .withAutomaticReconnect()
    // Pe măsura serverului (semnal la 5 s, deconectare după 15 s): cine pierde legătura apare repede ca plecat.
    .withKeepAliveInterval(5000)
    .withServerTimeout(15000)
    .configureLogging(LogLevel.Warning)
    .build()
  let inchis = false
  let instanta

  conexiune.on('stare', onStare)

  async function intra() {
    const actuala = await conexiune.invoke('Instanta')
    if (instanta !== undefined && instanta !== actuala) {
      const e = new Error('Serverul a repornit. Partida s-a încheiat; poți începe un joc nou.')
      e.cod = 'SERVER_RESTART'
      onDeconectat()
      onEroare(e)
      inchis = true
      await conexiune.stop()
      return
    }
    instanta = actuala
    onStare(await conexiune.invoke('Intra', cod))
    onConectat()
  }
  conexiune.onreconnecting(() => onDeconectat())
  conexiune.onreconnected(() => intra().catch(onEroare))
  conexiune.onclose?.(() => { if (!inchis) { onDeconectat(); onEroare(new Error('Conexiunea cu jocul s-a închis. Poți reveni și începe o partidă nouă.')) } })

  // Pornim la următorul tick: în dezvoltare, React montează, demontează și remontează imediat
  // componenta (Strict Mode); o conexiune oprită în timpul negocierii ar apărea ca eroare în consolă.
  const pornire = setTimeout(() => {
    conexiune
      .start()
      .then(intra)
      .catch((e) => {
        if (!inchis) onEroare(e)
      })
  }, 0)

  return {
    // Trimite o intenție serverului; mesajul unei erori (de ex. „Doar gazda poate face asta.”) ajunge la cine a apelat.
    async trimite(metoda, ...argumente) {
      if (conexiune.state !== HubConnectionState.Connected) throw new Error('Nu ești conectat la cameră.')
      try {
        await conexiune.invoke(metoda, ...argumente)
      } catch (e) {
        throw new Error((e?.message ?? '').replace(/^.*HubException: /, '') || 'Acțiunea nu a reușit.')
      }
    },
    inchide() {
      inchis = true
      clearTimeout(pornire)
      conexiune.stop()
    },
  }
}
