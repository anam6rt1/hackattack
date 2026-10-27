import type { MetadataRoute } from 'next'
// The same for every request, so it is generated once at build time (also required by the static export of the apps).
export const dynamic = 'force-static'
export default function manifest(): MetadataRoute.Manifest {
  return { name: 'TeleBac', short_name: 'TeleBac', lang: 'ro', start_url: '/', display: 'standalone', background_color: '#f8fafc', theme_color: '#163044', icons: [{ src: '/icon-192.png', sizes: '192x192', type: 'image/png' }, { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' }] }
}
