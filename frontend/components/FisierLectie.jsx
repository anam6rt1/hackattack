'use client'
// Un fișier al lecției: PDF-ul se deschide într-un tab nou, Word/PowerPoint/Excel se descarcă. Dacă lecția e descărcată,
// se folosește copia de pe dispozitiv (merge și fără internet, și e instant).
import { useEffect, useState } from 'react'
import { API, urlApi } from '@/lib/api'
import { platforma } from '@/lib/platforma'
import { marimeFisier } from '@/lib/marime-fisier'

export default function FisierLectie({ fisier: f }) {
  const [local, setLocal] = useState(null)
  useEffect(() => {
    let url = null, activ = true
    import('@/lib/offline/depozit').then(async ({ areDateOffline }) => {
      if (!areDateOffline()) return
      const blob = await (await import('@/lib/offline/pachete')).fisierDescarcat(f.id)
      if (blob && activ) { url = URL.createObjectURL(blob); setLocal(url) }
    })
    return () => { activ = false; if (url) URL.revokeObjectURL(url) }
  }, [f.id])
  // Pe telefon, un link de descărcare nu deschide nimic în aplicație: fișierul merge la o aplicație potrivită (PDF, Office).
  async function peMobil(e) {
    if (platforma() !== 'mobil') return
    e.preventDefault()
    const blob = local ? await (await fetch(local)).blob()
      : await (await fetch(`${API}/fisiere/${f.id}?descarca=true`, { credentials: 'include', headers: { 'X-MarkAi': '1' } })).blob()
    const { deschideFisierPeMobil } = await import('@/lib/mobil')
    await deschideFisierPeMobil(blob, f.nume).catch(() => {})
  }
  return (
    <a
      className="tv-rand"
      onClick={peMobil}
      href={local ?? urlApi(`/fisiere/${f.id}`)}
      {...(f.tip === 'pdf' ? { target: '_blank', rel: 'noreferrer' } : { download: f.nume })}
    >
      <span className="tv-pastila tv-pastila-aur">{f.tip.toUpperCase()}</span>
      <div><b>{f.nume}</b><small>{marimeFisier(f.marimeOcteti)} · {f.tip === 'pdf' ? 'se deschide' : 'se descarcă'}{local ? ' · de pe dispozitiv' : ''}</small></div>
    </a>
  )
}
