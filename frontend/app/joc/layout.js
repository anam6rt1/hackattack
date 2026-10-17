// Învelișul tuturor paginilor /joc: fonturi + stilurile cu insule.

import { claseFonturi } from '@/lib/fonturi'
import '../insule.css'

export default function JocLayout({ children }) {
  return <div className={claseFonturi}>{children}</div>
}
