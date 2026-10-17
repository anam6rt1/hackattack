import HartaInsule from "@/components/HartaInsule";
import { T } from "@/lib/tema";

export default function PaginaHarta() {
  return (
    <main className={`min-h-screen p-4 ${T.pagina}`}>
      <HartaInsule />
    </main>
  );
}
