import type { Metadata } from "next";
import { Suspense } from "react";
import "./globals.css";
import "./insule.css";
import Meniu from "@/components/Meniu";
import { TemaSite } from "@/components/ProfilTeme";
import Furnizori from "@/components/Furnizori";
import Incarcare from "@/components/Incarcare";
import { claseFonturi } from "@/lib/fonturi";
import { SCRIPT_TEMA } from "@/lib/tema-sezon";

export const metadata: Metadata = {
  title: "Bac la Română",
  description: "Învață pentru bacul la română jucându-te.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ro" data-tema="vara-zi" suppressHydrationWarning>
      <head>
        {/* Alege tema (anotimp + zi/noapte) înainte de prima afișare, ca să nu clipească. */}
        <script dangerouslySetInnerHTML={{ __html: SCRIPT_TEMA }} />
      </head>
      <body className={claseFonturi}>
        <Furnizori><TemaSite>
          {/* Meniul și paginile citesc adresa (usePathname/useParams), deci se randează în browser */}
          <Suspense fallback={<header className="mn-bara" />}>
            <Meniu />
          </Suspense>
          <Suspense fallback={<Incarcare />}>{children}</Suspense>
        </TemaSite></Furnizori>
      </body>
    </html>
  );
}
