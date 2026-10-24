'use client'
// Prezentarea pentru vizitatori + intrarea în cont / crearea contului (elev sau profesor).

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { useFotografie } from '@/lib/useFotografie'
import { useIncarcare } from '@/lib/useIncarcare'
import ProgresIncarcare from '@/components/ProgresIncarcare'
import PrevizualizareFisier from '@/components/PrevizualizareFisier'
import OptimizareFotografie from '@/components/OptimizareFotografie'
import { esteProfesor, useSesiune } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'
import AlegeLoc from '@/components/AlegeLoc'
import AlegeOrganizatie, { AlegeOrganizatii } from '@/components/AlegeOrganizatie'
import AlegeMaterii from '@/components/AlegeMaterii'

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

// Pasul de după înregistrare (sau după o intrare cu un cont neconfirmat): codul primit pe email.
function ConfirmaEmail({ email, mesaj, onInapoi }) {
  const { confirmaEmail, retrimiteConfirmare } = useSesiune()
  const [eroare, setEroare] = useState('')
  const [info, setInfo] = useState(mesaj)
  const [trimite, setTrimite] = useState(false)
  const [asteapta, setAsteapta] = useState(0) // secunde până la o nouă retrimitere

  useEffect(() => {
    if (asteapta <= 0) return
    const t = setTimeout(() => setAsteapta(asteapta - 1), 1000)
    return () => clearTimeout(t)
  }, [asteapta])

  async function trimiteCod(e) {
    e.preventDefault()
    setTrimite(true)
    setEroare('')
    try {
      // După confirmare ești în cont; pagina de start te duce mai departe (sau înapoi la linkul din care ai venit).
      await confirmaEmail(email, new FormData(e.currentTarget).get('cod'))
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  async function retrimite() {
    setEroare('')
    try {
      const r = await retrimiteConfirmare(email)
      setInfo(`${r.mesaj} Codurile vechi nu mai sunt valabile.`)
      setAsteapta(60)
    } catch (err) {
      setEroare(err.message)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteCod}>
      <h3>Confirmă adresa de email</h3>
      <p className="tv-indiciu">{info}</p>
      <p className="tv-indiciu">Contul neconfirmat se șterge automat după 24 de ore.</p>
      <Camp eticheta={`Codul de 6 cifre trimis pe ${email}`} name="cod" inputMode="numeric" autoComplete="one-time-code"
        pattern="[0-9]{6}" maxLength={6} required placeholder="123456" />
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se verifică...' : 'Confirmă și intră în cont'}</button>
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-aur" onClick={retrimite} disabled={asteapta > 0}>
          {asteapta > 0 ? `Retrimite peste ${asteapta} s` : 'Retrimite codul'}
        </button>
        <button type="button" className="tv-btn tv-aur" onClick={onInapoi}>Înapoi</button>
      </div>
    </form>
  )
}

// „Am uitat parola”: un cod pe email (sau prin SMS, pe telefonul verificat al contului), apoi parola nouă.
function ParolaUitata({ onInapoi }) {
  const [pas, setPas] = useState(1)
  const [identificator, setIdentificator] = useState('')
  const [info, setInfo] = useState('')
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)

  async function cereCod(e) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    setTrimite(true)
    setEroare('')
    try {
      const r = await api('/autentificare/parola-uitata', { metoda: 'POST', corp: { identificator, canal: f.get('canal') } })
      setInfo(r.mesaj)
      setPas(2)
    } catch (err) {
      setEroare(err.message)
    } finally {
      setTrimite(false)
    }
  }

  async function schimba(e) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (f.get('parola') !== f.get('confirmare')) return setEroare('Parolele nu sunt la fel.')
    setTrimite(true)
    setEroare('')
    try {
      const r = await api('/autentificare/parola-noua', { metoda: 'POST', corp: { identificator, cod: f.get('cod'), parola: f.get('parola') } })
      onInapoi(r.mesaj)
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  // Chei diferite: altfel React refolosește câmpul „Email sau utilizator” ca „Cod” și păstrează textul scris în el.
  return pas === 1 ? (
    <form key="cere-cod" className="tv-form" onSubmit={cereCod}>
      <h3>Parolă nouă</h3>
      <p className="tv-indiciu">Îți trimitem un cod cu care alegi o parolă nouă.</p>
      <Camp eticheta="Email sau utilizator" name="identificator" autoComplete="username" required value={identificator}
        onChange={(e) => setIdentificator(e.target.value)} />
      <fieldset className="tv-eticheta-camp">
        <span>Unde primești codul</span>
        <label className="tv-bifa"><input type="radio" name="canal" value="email" defaultChecked /> Pe email</label>
        <label className="tv-bifa"><input type="radio" name="canal" value="sms" /> Prin SMS (doar dacă ai un telefon verificat în cont)</label>
      </fieldset>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se trimite...' : 'Trimite codul'}</button>
      <button type="button" className="tv-btn tv-aur" onClick={() => onInapoi()}>Înapoi la intrare</button>
    </form>
  ) : (
    <form key="parola-noua" className="tv-form" onSubmit={schimba}>
      <h3>Parolă nouă</h3>
      <p className="tv-indiciu">{info}</p>
      <Camp eticheta="Codul de 6 cifre" name="cod" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required />
      <Camp eticheta="Parola nouă (minim 8 caractere)" name="parola" type="password" autoComplete="new-password" minLength={8} required />
      <Camp eticheta="Confirmă parola nouă" name="confirmare" type="password" autoComplete="new-password" minLength={8} required />
      <p className="tv-indiciu">După schimbare, contul e scos de pe toate dispozitivele.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se schimbă...' : 'Schimbă parola'}</button>
      <button type="button" className="tv-btn tv-aur" onClick={() => setPas(1)}>Cere alt cod</button>
    </form>
  )
}

function FormularIntrare({ onConfirmare, onParolaUitata, mesaj }) {
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
      if (esteProfesor(u) && u.stareProfesor !== 'aprobat') router.push('/in-asteptare')
    } catch (err) {
      if (err.date?.cod === 'email_neconfirmat') return onConfirmare(err.date.email, err.message)
      setEroare(err.message)
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <Camp eticheta="Email sau utilizator" name="identificator" autoComplete="username" required />
      <Camp eticheta="Parolă" name="parola" type="password" autoComplete="current-password" required />
      {mesaj && <p className="tv-succes" role="status">{mesaj}</p>}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se intră...' : 'Intră în cont'}</button>
      <button type="button" className="tv-btn tv-aur" onClick={onParolaUitata}>Am uitat parola</button>
    </form>
  )
}

// Câmpurile goale (de ex. localitatea neleasă) pleacă drept null, iar numerele ca numere.
const numar = (v) => (v ? Number(v) : null)

function FormularElev({ onConfirmare }) {
  const { inregistreazaElev } = useSesiune()
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)
  const [judetId, setJudetId] = useState(null)

  async function trimiteFormular(e) {
    e.preventDefault()
    const d = Object.fromEntries(new FormData(e.currentTarget))
    const f = { ...d, judetId: numar(d.judetId), localitateId: numar(d.localitateId), organizatieId: numar(d.organizatieId), clasa: d.clasa || null }
    setTrimite(true)
    setEroare('')
    try {
      const r = await inregistreazaElev(f)
      onConfirmare(r.email, r.mesaj)
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <Camp eticheta="Nume și prenume" name="nume" autoComplete="name" required maxLength={120} />
      <Camp eticheta="Utilizator" name="username" autoComplete="username" required maxLength={40} />
      <Camp eticheta="Email" name="email" type="email" autoComplete="email" required />
      <Camp eticheta="Parolă (minim 8 caractere)" name="parola" type="password" autoComplete="new-password" required minLength={8} />
      <label className="tv-eticheta-camp">
        <span>Clasa (opțional)</span>
        <select className="tv-camp-text" name="clasa" defaultValue="">
          <option value="">Nu spun</option>
          {['IX', 'X', 'XI', 'XII'].map((c) => <option key={c} value={c}>a {c}-a</option>)}
        </select>
      </label>
      <AlegeLoc onChange={(v) => setJudetId(v.judetId)} />
      <AlegeOrganizatie judetId={judetId} />
      <p className="tv-indiciu">Îți trimitem un cod pe email ca să confirmi contul. Numărul de telefon pentru Linia AI îl adaugi după, cu un cod prin SMS.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se creează...' : 'Creează contul de elev'}</button>
    </form>
  )
}

function FormularProfesor({ onConfirmare }) {
  const [materii, setMaterii] = useState([])
  const [organizatii, setOrganizatii] = useState([])
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)
  const [judetId, setJudetId] = useState(null)
  const [files, setFiles] = useState({})
  const fotografie = useFotografie(files.poza, false)
  const upload = useIncarcare()

  async function trimiteFormular(e) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (!f.getAll('materieIds').length) return setEroare('Alege cel puțin o materie pe care o predai.')
    if (!f.getAll('organizatieIds').length) return setEroare('Alege cel puțin o organizație în care predai.')
    if (!f.get('localitateId')) f.delete('localitateId')
    setTrimite(true)
    setEroare('')
    try {
      if (fotografie.inLucru) throw new Error('Așteaptă pregătirea fotografiei.')
      if (fotografie.fisier) f.set('poza', fotografie.fisier)
      if (f.get('poza')?.size > 5 * 1024 * 1024) throw new Error('Fotografia depășește limita de 5 MB.')
      if (f.get('dovada')?.size > 10 * 1024 * 1024) throw new Error('Dovada depășește limita de 10 MB.')
      const r = await upload.trimite('/autentificare/inregistrare-profesor', f)
      onConfirmare(r.email, r.mesaj)
    } catch (err) {
      setEroare(err.name === 'AbortError' ? 'Încărcare anulată. Poți reîncerca.' : err.message)
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
      <AlegeMaterii alese={materii} onChange={setMaterii} name="materieIds" />
      <label className="tv-eticheta-camp">
        <span>Despre tine (apare pe profilul public)</span>
        <textarea className="tv-camp-text" name="descriere" rows={3} required maxLength={2000} placeholder="Experiență, ce pregătești, cum lucrezi cu elevii" />
      </label>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp">
          <span>Fotografie</span>
          <input className="tv-fisier" type="file" name="poza" accept="image/jpeg,image/png,image/webp" required disabled={trimite} onChange={e => setFiles(f => ({ ...f, poza: e.target.files[0] }))} />
        </label>
        <label className="tv-eticheta-camp">
          <span>Dovada calificării</span>
          <input className="tv-fisier" type="file" name="dovada" accept="application/pdf,image/jpeg,image/png" required disabled={trimite} onChange={e => setFiles(f => ({ ...f, dovada: e.target.files[0] }))} />
        </label>
      </div>
      <OptimizareFotografie fisier={fotografie.fisier} inLucru={fotografie.inLucru} />
      <PrevizualizareFisier fisier={files.dovada} eticheta="Dovada calificării" />
      <ProgresIncarcare progres={upload.progres} onAnuleaza={upload.anuleaza} />
      <AlegeLoc obligatoriu onChange={(v) => setJudetId(v.judetId)} />
      <AlegeOrganizatii judetId={judetId} alese={organizatii} onChange={setOrganizatii} />
      <p className="tv-indiciu">Administratorii organizațiilor alese îți verifică cererea. Dacă nu ești aprobat în 7 zile, contul se șterge automat.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn tv-aur" disabled={trimite || fotografie.inLucru}>{trimite ? 'Se trimite...' : 'Trimite cererea'}</button>
    </form>
  )
}

// Cine conduce o școală sau un centru: contul și organizația așteaptă aprobarea unui administrator al platformei.
function FormularOrganizatie({ onConfirmare }) {
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)

  async function trimiteFormular(e) {
    e.preventDefault()
    const d = Object.fromEntries(new FormData(e.currentTarget))
    setTrimite(true)
    setEroare('')
    try {
      const r = await api('/autentificare/inregistrare-organizatie', {
        metoda: 'POST', corp: { ...d, judetId: numar(d.judetId), localitateId: numar(d.localitateId) },
      })
      onConfirmare(r.email, r.mesaj)
    } catch (err) {
      setEroare(err.message)
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <Camp eticheta="Numele organizației (școală, centru)" name="numeOrganizatie" required maxLength={200} />
      <Camp eticheta="Adresa" name="adresa" required maxLength={300} />
      <AlegeLoc obligatoriu />
      <Camp eticheta="Numele tău (administratorul organizației)" name="nume" autoComplete="name" required maxLength={120} />
      <div className="tv-grid-2">
        <Camp eticheta="Utilizator" name="username" autoComplete="username" required maxLength={40} />
        <Camp eticheta="Telefon" name="telefon" type="tel" autoComplete="tel" required placeholder="07xx xxx xxx" />
      </div>
      <Camp eticheta="Email" name="email" type="email" autoComplete="email" required />
      <Camp eticheta="Parolă (minim 8 caractere)" name="parola" type="password" autoComplete="new-password" required minLength={8} />
      <p className="tv-indiciu">
        Un administrator al platformei verifică organizația. După aprobare, primești profesori și elevi în ea și poți preda și tu.
        Dacă nu e aprobată în 7 zile, contul se șterge automat.
      </p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn tv-aur" disabled={trimite}>{trimite ? 'Se trimite...' : 'Trimite cererea'}</button>
    </form>
  )
}

const TABURI = [
  { id: 'intrare', nume: 'Intră' },
  { id: 'elev', nume: 'Cont de elev' },
  { id: 'profesor', nume: 'Sunt profesor' },
  { id: 'organizatie', nume: 'Organizație' },
]

export default function Aterizare() {
  const [tab, setTab] = useState('intrare')
  const [confirmare, setConfirmare] = useState(null) // { email, mesaj } cât timp se așteaptă codul de pe email
  const [parolaUitata, setParolaUitata] = useState(false)
  const [mesajIntrare, setMesajIntrare] = useState('') // de ex. „Parola a fost schimbată…”
  const laConfirmare = (email, mesaj) => setConfirmare({ email, mesaj })

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
              {confirmare ? (
                <ConfirmaEmail email={confirmare.email} mesaj={confirmare.mesaj} onInapoi={() => setConfirmare(null)} />
              ) : parolaUitata ? (
                <ParolaUitata onInapoi={(mesaj) => { setParolaUitata(false); setTab('intrare'); setMesajIntrare(mesaj ?? '') }} />
              ) : (
              <>
              <div className="tv-taburi" role="tablist">
                {TABURI.map((t) => (
                  <button key={t.id} role="tab" aria-selected={tab === t.id} className={tab === t.id ? 'tv-tab-activ' : ''} onClick={() => setTab(t.id)}>
                    {t.nume}
                  </button>
                ))}
              </div>
              {tab === 'intrare' && <FormularIntrare onConfirmare={laConfirmare} onParolaUitata={() => setParolaUitata(true)} mesaj={mesajIntrare} />}
              {tab === 'elev' && <FormularElev onConfirmare={laConfirmare} />}
              {tab === 'profesor' && <FormularProfesor onConfirmare={laConfirmare} />}
              {tab === 'organizatie' && <FormularOrganizatie onConfirmare={laConfirmare} />}
              </>
              )}
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
