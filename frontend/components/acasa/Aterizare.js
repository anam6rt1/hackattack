'use client'
// Prezentarea pentru vizitatori + intrarea în cont / crearea contului (elev sau profesor).

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { useSesiune } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'

const DECOR = decorSVG(13)

const FUNCTII = [
  { titlu: 'Materiale pe insule', text: 'Fiecare operă de bac e o insulă cu lecții scurte: video, notițe și o întrebare la final.' },
  { titlu: 'Triviador cu prietenii', text: 'Cucerește insulele colegilor răspunzând la întrebări din lecțiile parcurse.' },
  { titlu: 'Profesori, și gratuit', text: 'Rezervă o ședință cu un profesor verificat. Unii oferă și ședințe pro bono.' },
  { titlu: 'Linia AI, fără internet', text: 'Tutorele AI te sună și recapitulați împreună, chiar și fără date mobile. Apelarea unui număr de telefon va fi disponibilă în curând.' },
]

function Camp({ eticheta, ...props }) {
  return (
    <label className="tv-eticheta-camp">
      <span>{eticheta}</span>
      <input className="tv-camp-text" {...props} />
    </label>
  )
}

function FormularIntrare() {
  const { intra } = useSesiune()
  const router = useRouter()
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)

  async function trimiteFormular(e) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    setTrimite(true)
    setEroare('')
    try {
      const u = await intra(f.get('identificator'), f.get('parola'))
      if (u.rol === 'profesor' && u.stareProfesor !== 'aprobat') router.push('/in-asteptare')
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <Camp eticheta="Email sau utilizator" name="identificator" autoComplete="username" required />
      <Camp eticheta="Parolă" name="parola" type="password" autoComplete="current-password" required />
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se intră...' : 'Intră în cont'}</button>
    </form>
  )
}

function FormularElev() {
  const { inregistreazaElev } = useSesiune()
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)

  async function trimiteFormular(e) {
    e.preventDefault()
    const f = Object.fromEntries(new FormData(e.currentTarget))
    setTrimite(true)
    setEroare('')
    try {
      await inregistreazaElev({ ...f, telefon: f.telefon || null })
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <Camp eticheta="Nume și prenume" name="nume" autoComplete="name" required maxLength={120} />
      <div className="tv-grid-2">
        <Camp eticheta="Utilizator" name="username" autoComplete="username" required maxLength={40} />
        <Camp eticheta="Telefon (opțional)" name="telefon" type="tel" autoComplete="tel" placeholder="07xx xxx xxx" />
      </div>
      <Camp eticheta="Email" name="email" type="email" autoComplete="email" required />
      <Camp eticheta="Parolă (minim 8 caractere)" name="parola" type="password" autoComplete="new-password" required minLength={8} />
      <p className="tv-indiciu">Cu numărul de telefon, Linia AI te recunoaște când suni.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se creează...' : 'Creează contul de elev'}</button>
    </form>
  )
}

function FormularProfesor() {
  const { inregistreazaProfesor } = useSesiune()
  const router = useRouter()
  const [materii, setMaterii] = useState([])
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)

  useEffect(() => {
    api('/materii').then((r) => setMaterii(r.elemente)).catch((e) => setEroare(e.message))
  }, [])

  async function trimiteFormular(e) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (!f.getAll('materieIds').length) return setEroare('Alege cel puțin o materie pe care o predai.')
    setTrimite(true)
    setEroare('')
    try {
      await inregistreazaProfesor(f)
      router.push('/in-asteptare')
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <Camp eticheta="Nume și prenume" name="nume" autoComplete="name" required maxLength={120} />
      <div className="tv-grid-2">
        <Camp eticheta="Utilizator" name="username" autoComplete="username" required maxLength={40} />
        <Camp eticheta="Telefon" name="telefon" type="tel" autoComplete="tel" required placeholder="07xx xxx xxx" />
      </div>
      <Camp eticheta="Email" name="email" type="email" autoComplete="email" required />
      <Camp eticheta="Parolă (minim 8 caractere)" name="parola" type="password" autoComplete="new-password" required minLength={8} />
      <fieldset className="tv-eticheta-camp">
        <span>Materii predate</span>
        <div className="tv-optiuni">
          {materii.map((m) => (
            <label key={m.id} className="tv-optiune">
              <input type="checkbox" name="materieIds" value={m.id} /> {m.denumire}
            </label>
          ))}
        </div>
      </fieldset>
      <label className="tv-eticheta-camp">
        <span>Despre tine (apare pe profilul public)</span>
        <textarea className="tv-camp-text" name="descriere" rows={3} required maxLength={2000} placeholder="Experiență, ce pregătești, cum lucrezi cu elevii" />
      </label>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp">
          <span>Fotografie</span>
          <input className="tv-fisier" type="file" name="poza" accept="image/jpeg,image/png,image/webp" required />
        </label>
        <label className="tv-eticheta-camp">
          <span>Dovada calificării</span>
          <input className="tv-fisier" type="file" name="dovada" accept="application/pdf,image/jpeg,image/png" required />
        </label>
      </div>
      <p className="tv-indiciu">Un administrator îți verifică cererea și te sună pentru stabilirea onorariului.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn tv-aur" disabled={trimite}>{trimite ? 'Se trimite...' : 'Trimite cererea'}</button>
    </form>
  )
}

const TABURI = [
  { id: 'intrare', nume: 'Intră' },
  { id: 'elev', nume: 'Cont de elev' },
  { id: 'profesor', nume: 'Sunt profesor' },
]

export default function Aterizare() {
  const [tab, setTab] = useState('intrare')

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou tv-erou-aterizare">
          <div className="tv-aterizare">
            <div>
              <span className="tv-erou-pill">Bacul la română</span>
              <h1>Învață operele de bac, insulă cu insulă.</h1>
              <p>Lecții scurte, întrebări care te țin atent, un joc cu prietenii și profesori când ai nevoie. Gratuit pentru elevi.</p>
              <svg className="tv-decor-aterizare" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
            </div>
            <div className="tv-card">
              <div className="tv-taburi" role="tablist">
                {TABURI.map((t) => (
                  <button key={t.id} role="tab" aria-selected={tab === t.id} className={tab === t.id ? 'tv-tab-activ' : ''} onClick={() => setTab(t.id)}>
                    {t.nume}
                  </button>
                ))}
              </div>
              {tab === 'intrare' && <FormularIntrare />}
              {tab === 'elev' && <FormularElev />}
              {tab === 'profesor' && <FormularProfesor />}
            </div>
          </div>
        </section>

        <div className="tv-coala tv-coala-aterizare">
          <section className="tv-sectiune">
            <h2>Ce găsești pe insulă</h2>
          </section>
          <div className="tv-functii">
            {FUNCTII.map((f) => (
              <div key={f.titlu}>
                <h4>{f.titlu}</h4>
                <p>{f.text}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </main>
  )
}
