import type { Metadata } from "next";
import { Suspense } from "react";
import "./globals.css";
import "./insule.css";
import Meniu from "@/components/Meniu";
import Furnizori from "@/components/Furnizori";
import Incarcare from "@/components/Incarcare";
import StareOffline from "@/components/StareOffline";
import PoartaServer from "@/components/ServerAplicatie";
import IntegrareMobil from "@/components/IntegrareMobil";
import SubsolSite from "@/components/SubsolSite";
import { claseFonturi } from "@/lib/fonturi";
import { SCRIPT_TEMA } from "@/lib/tema-sezon";

export const metadata: Metadata = {
  title: "Bac la Română",
  description: "Învață pentru bacul la română jucându-te.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    // Fonturile stau pe <html>, ca variabilele lor să poată fi folosite și de stilurile puse pe <html data-theme>.
    <html lang="ro" data-tema="vara-zi" data-theme="acvatic" className={claseFonturi} suppressHydrationWarning>
      <head>
        {/* Alege tema (anotimp + zi/noapte) și stilul înainte de prima afișare, ca să nu clipească. */}
        <script dangerouslySetInnerHTML={{ __html: SCRIPT_TEMA }} />
      </head>
      <body>
        <Furnizori>
          {/* Meniul și paginile citesc adresa (usePathname/useParams), deci se randează în browser */}
          <Suspense fallback={<header className="mn-bara" />}>
            <Meniu />
          </Suspense>
          <StareOffline />
          <IntegrareMobil />
          <Suspense fallback={<Incarcare />}><PoartaServer>{children}</PoartaServer></Suspense>
          <SubsolSite />
        </Furnizori>
      </body>
    </html>
  );
}
