// TEMPORAR: pagina /materiale. O înlocuiește colegul care face resursele.
import PaginaInCurand from '@/components/PaginaInCurand'

export default function Materiale() {
  return (
    <PaginaInCurand
      eticheta="Materiale"
      titlu="Rezumate, scheme și eseuri de la alți elevi."
      text="Aici vor apărea materialele postate de elevi și studenți, împărțite pe opere."
      seed={29}
    />
  )
}
