import { claseFonturi } from '@/lib/fonturi'
import { NecesitaInternet } from '@/components/StareOffline'
import '../insule.css'

export default function ProfesoriLayout({ children }) {
  return <div className={claseFonturi}><NecesitaInternet ce="Rezervarea ședințelor">{children}</NecesitaInternet></div>
}
