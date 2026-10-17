// app/nivel/[id]/page.js — pagina unui nivel, ex. /nivel/harap-1
import IntrebareNivel from "@/components/IntrebareNivel";
import INTREBARI from "@/data/intrebari.json";

// spune dinainte ce nivele există (cerut de Cache Components din Next.js 16)
export function generateStaticParams() {
  return Object.keys(INTREBARI).map((id) => ({ id }));
}

export default async function PaginaNivel({ params }) {
  const { id } = await params;
  return <IntrebareNivel nivelId={id} />;
}
