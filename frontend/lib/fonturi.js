// Fonturile pentru paginile cu insule (joc, profesori, linia AI) și meniu.
// Câte un set pentru fiecare temă:
//   Acvatic: Fredoka (titluri) + Nunito (text)
//   Realist: Lora (titluri, cu serife) + Nunito (text)
//   Pixel:   Pixelify Sans (tot)
// Next.js le descarcă singur de la Google Fonts.

import { Fredoka, Nunito, Lora, Pixelify_Sans } from 'next/font/google'

const fredoka = Fredoka({ subsets: ['latin', 'latin-ext'], variable: '--font-titlu' })
const nunito = Nunito({ subsets: ['latin', 'latin-ext'], variable: '--font-text' })
const lora = Lora({ subsets: ['latin', 'latin-ext'], weight: ['600', '700'], variable: '--font-serif' })
const pixel = Pixelify_Sans({ subsets: ['latin', 'latin-ext'], weight: ['400', '600', '700'], variable: '--font-pixel' })

export const claseFonturi = `${fredoka.variable} ${nunito.variable} ${lora.variable} ${pixel.variable}`
