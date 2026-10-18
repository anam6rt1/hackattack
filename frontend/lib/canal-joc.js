// Canalul unei camere Triviador, prin hub-ul SignalR al API-ului (/hub/joc).
// Înlocuiește canalul Supabase Realtime: aceeași idee de „broadcast” (stare, actiune) și „presence”.
// Identitatea jucătorului vine din cookie-ul de autentificare, nu din browser.

import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { API } from '@/lib/api'

export function deschideCanal(cod, gazda, { onPrezenta, onIntrat, onStare, onActiune, onConectat, onDeconectat, onEroare }) {
  const conexiune = new HubConnectionBuilder()
    .withUrl(API + '/hub/joc', { withCredentials: true })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
  let inchis = false

  conexiune.on('prezenta', onPrezenta)
  conexiune.on('intrat', onIntrat)
  conexiune.on('stare', onStare)
  conexiune.on('actiune', onActiune)

  async function intra() {
    onPrezenta(await conexiune.invoke('Intra', cod, gazda))
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
    trimite(eveniment, payload) {
      if (conexiune.state === HubConnectionState.Connected) conexiune.invoke('Trimite', eveniment, payload).catch(() => {})
    },
    inchide() {
      inchis = true
      clearTimeout(pornire)
      conexiune.stop()
    },
  }
}
