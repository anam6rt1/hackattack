// Învelișul tuturor paginilor /joc: fonturi + stilurile cu insule.

import { claseFonturi } from '@/lib/fonturi'
import { NecesitaInternet } from '@/components/StareOffline'
import '../insule.css'

export default function JocLayout({ children }) {
  return <div className={claseFonturi}><NecesitaInternet ce="Triviador">{children}</NecesitaInternet></div>
}
