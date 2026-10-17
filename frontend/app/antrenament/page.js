// TEMPORAR: pagina /antrenament. O înlocuiește P2 cu harta de capitole și nivele.
import PaginaInCurand from '@/components/PaginaInCurand'

export default function Antrenament() {
  return (
    <PaginaInCurand
      eticheta="Antrenament"
      titlu="Harta capitolelor vine aici."
      text="Fiecare operă e o insulă, cu nivele de parcurs: video, resurse de la alți elevi și un joc la final."
      seed={13}
    />
  )
}
