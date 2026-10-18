import { opere } from "@/lib/materiale";
import HartaMateriale from "@/components/HartaMateriale";

export const metadata = { title: "Materiale · Română" };

export default function MaterialePage() {
  return (
    <main className="mx-auto max-w-6xl px-3 py-6 sm:px-4 sm:py-10">
      <HartaMateriale opere={opere} />
    </main>
  );
}
