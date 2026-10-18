import type { Metadata } from "next";
import "./globals.css";
import Meniu from "@/components/Meniu";
import { TemaSite } from "@/components/ProfilTeme";

export const metadata: Metadata = {
  title: "Bac la Română",
  description: "Învață pentru bacul la română jucându-te.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ro" suppressHydrationWarning>
      <body>
        <TemaSite>
          <Meniu />
          {children}
        </TemaSite>
      </body>
    </html>
  );
}