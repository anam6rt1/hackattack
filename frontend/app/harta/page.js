// /harta s-a mutat la /antrenament. Pagina asta doar redirecționează linkurile vechi.
import { redirect } from 'next/navigation'

export default function HartaVeche() {
  redirect('/antrenament')
}