// app/nivel/[id]/page.js — pagina unui nivel, ex. /nivel/harap-1
import IntrebareNivel from "@/components/IntrebareNivel";

export default async function PaginaNivel({ params }) {
  const { id } = await params;
  return <IntrebareNivel nivelId={id} />;
}
