// Fonturile pentru paginile cu stilul „insule” (joc, profesori, linia AI).
// Next.js le descarcă singur de la Google Fonts la build.

import { Fredoka, Nunito } from 'next/font/google'

const fredoka = Fredoka({ subsets: ['latin', 'latin-ext'], variable: '--font-titlu' })
const nunito = Nunito({ subsets: ['latin', 'latin-ext'], variable: '--font-text' })

export const claseFonturi = `${fredoka.variable} ${nunito.variable}`
