import PaginaInCurand from "@/components/PaginaInCurand";

export default function Acasa() {
  return (
    <PaginaInCurand
      eticheta="Acasă"
      titlu="Bine ai venit pe insula bacului."
      text="Alege din meniu: antrenament pe capitole, materiale, Triviador cu prietenii sau linia AI."
      seed={13}
    />
  );
}