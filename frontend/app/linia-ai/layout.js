import { claseFonturi } from '@/lib/fonturi'
import { NecesitaInternet } from '@/components/StareOffline'
import '../insule.css'

export default function LiniaLayout({ children }) {
  return <div className={claseFonturi}><NecesitaInternet ce="Linia AI">{children}</NecesitaInternet></div>
}
