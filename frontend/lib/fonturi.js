// Fonturile site-ului. Next.js le descarcă singur de la Google Fonts la build.
// Baloo 2 și Nunito au toate diacriticele românești (ă â î ș ț); Fredoka, folosit înainte, nu avea ă, ș, ț
// și le desena cu alt font, așa că în titluri literele acestea arătau diferit.
// Stilurile din profil își aduc fontul lor: Realist = Lora (titluri, cu serife), Pixel = Pixelify Sans (tot).

import { Baloo_2, Lora, Nunito, Pixelify_Sans } from 'next/font/google'

const baloo = Baloo_2({ subsets: ['latin', 'latin-ext'], variable: '--font-titlu' })
const nunito = Nunito({ subsets: ['latin', 'latin-ext'], variable: '--font-text' })
const lora = Lora({ subsets: ['latin', 'latin-ext'], weight: ['600', '700'], variable: '--font-serif' })
const pixel = Pixelify_Sans({ subsets: ['latin', 'latin-ext'], weight: ['400', '600', '700'], variable: '--font-pixel' })

export const claseFonturi = `${baloo.variable} ${nunito.variable} ${lora.variable} ${pixel.variable}`
