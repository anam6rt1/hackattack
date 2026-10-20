// Canalul unei camere Triviador, prin hub-ul SignalR al API-ului (/hub/joc).
// Jocul rulează pe server: aici doar trimitem intenții (atac, răspuns, estimare...) și primim starea publică
// după fiecare schimbare. Identitatea jucătorului vine din cookie-ul de autentificare, nu din browser.

import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { API } from '@/lib/api'

export function deschideCanal(cod, { onStare, onConectat, onDeconectat, onEroare }) {
  const conexiune = new HubConnectionBuilder()
    .withUrl(API + '/hub/joc', { withCredentials: true })
    .withAutomaticReconnect()
    // Pe măsura serverului (semnal la 5 s, deconectare după 15 s): cine pierde legătura apare repede ca plecat.
    .withKeepAliveInterval(5000)
    .withServerTimeout(15000)
    .configureLogging(LogLevel.Warning)
    .build()
  let inchis = false

  conexiune.on('stare', onStare)

  async function intra() {
    onStare(await conexiune.invoke('Intra', cod))
    onConectat()
  }
  conexiune.onreconnecting(() => onDeconectat())
  conexiune.onreconnected(() => intra().catch(onEroare))

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
