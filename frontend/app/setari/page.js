'use client'

import { useCallback, useEffect, useRef, useState } from 'react'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { API, api, EroareApi } from '@/lib/api'
import { useSesiune } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'
import './setari.css'

const DECOR = decorSVG(13)
const ROLURI = { elev: 'Cont de elev', profesor: 'Cont de profesor', admin: 'Administrator' }
const TABS = [ ['profil', 'Date personale', 'user'], ['securitate', 'Securitatea contului', 'shield'] ]

function Icon({ name, ...props }) {
  const paths = {
    user: <><circle cx="12" cy="8" r="4" /><path d="M4 22v-3a8 8 0 0 1 16 0v3" /></>,
    shield: <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z" /><path d="m8 12 3 3 5-6" /></>,
    book: <><path d="M12 5v16M3 4h5a4 4 0 0 1 4 2 4 4 0 0 1 4-2h5v16h-5a4 4 0 0 0-4 1 4 4 0 0 0-4-1H3z" /></>,
    check: <path d="m5 12 4 4L20 5" />,
    arrow: <path d="m10 5 7 7-7 7M17 12H3" />,
    mail: <><rect x="3" y="5" width="18" height="14" rx="3" /><path d="m4 7 8 6 8-6" /></>,
    close: <path d="m6 6 12 12M6 18 18 6" />,
    lock: <><rect x="5" y="10" width="14" height="11" rx="3" /><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" /></>,
  }
  return <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>{paths[name]}</svg>
}

async function setariApi(path = '', { metoda = 'GET', corp, formular } = {}) {
  let response
  try {
    response = await fetch(`${API}/utilizatori/me/setari${path}`, {
      method: metoda, credentials: 'include',
      headers: { 'X-MarkAi-Settings': '1', ...(corp ? { 'Content-Type': 'application/json' } : {}) },
      body: formular || (corp ? JSON.stringify(corp) : undefined),
    })
  } catch { throw new EroareApi(0, 'Nu ne putem conecta la server. Încearcă din nou.') }
  const data = await response.json().catch(() => null)
  if (!response.ok) throw new EroareApi(response.status, data?.mesaj || 'Modificarea nu a putut fi salvată. Încearcă din nou.')
  return data
}

async function readSettings() {
  const [settings, available] = await Promise.all([setariApi(), api('/materii')])
  return { settings, available }
}

function Field({ label, hint, ...props }) {
  return <label className="sc-field"><span>{label}</span><input {...props} />{hint && <small>{hint}</small>}</label>
}

function CodDialog({ challenge, onClose, onConfirm, busy, error }) {
  const dialog = useRef(null)
  const [code, setCode] = useState('')
  useEffect(() => { dialog.current?.showModal() }, [])
  return <dialog ref={dialog} className="sc-dialog" onCancel={onClose} aria-labelledby="sc-dialog-title">
    <button type="button" className="sc-close" onClick={onClose} aria-label="Închide confirmarea"><Icon name="close" /></button>
    <span className="sc-modal-icon"><Icon name="mail" width="34" height="34" /></span>
    <span className="sc-kicker">VERIFICARE ÎN DOI PAȘI</span>
    <h2 id="sc-dialog-title">Încă un pas și e gata!</h2>
    <p>Am trimis un cod la <strong>{challenge.email}</strong>. Introdu-l mai jos pentru a confirma {challenge.scop === 'parola' ? 'noua parolă' : 'noua adresă de email'}.</p>
    <form onSubmit={(event) => { event.preventDefault(); onConfirm(code) }}>
      <label className="sc-field"><span>Codul de confirmare</span><input autoFocus aria-label="Cod de confirmare, 6 cifre" className="sc-code" value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required placeholder="000000" /></label>
      <small className="sc-hint">Valabil 5 minute. Verifică și folderul Spam.</small>
      {error && <div className="sc-error" role="alert">{error}</div>}
      <button className="sc-button sc-green" disabled={busy || code.length !== 6}>{busy ? 'Se verifică…' : 'Confirmă modificarea'}<Icon name="check" /></button>
      <button type="button" className="sc-text-button" onClick={onClose}>Înapoi la setări</button>
    </form>
  </dialog>
}

export default function SetariCont() {
  const { utilizator, reincarca } = useSesiune()
  const router = useRouter()
  const [data, setData] = useState(null)
  const [form, setForm] = useState(null)
  const [subjects, setSubjects] = useState([])
  const [tab, setTab] = useState('profil')
  const [error, setError] = useState('')
  const [toast, setToast] = useState(null)
  const [busy, setBusy] = useState(false)
  const [challenge, setChallenge] = useState(null)
  const [otpError, setOtpError] = useState('')
  const [scop, setScop] = useState('parola')
  const [files, setFiles] = useState({})
  const [fileKey, setFileKey] = useState(0)
  const securityForm = useRef(null)

  const receive = useCallback(({ settings, available }) => {
    setData(settings)
    setForm({ nume: settings.utilizator.nume, username: settings.utilizator.username,
      telefon: settings.utilizator.telefon || '', descriere: settings.profesor?.descriere || '', materieIds: settings.profesor?.materieIds || [] })
    setSubjects(available.elemente)
  }, [])
  const load = useCallback(async () => receive(await readSettings()), [receive])
  useEffect(() => {
    if (utilizator === null) router.replace('/')
    if (utilizator?.id) readSettings().then(receive).catch(e => setError(e.message))
  }, [utilizator, router, receive])

  useEffect(() => {
    if (!toast) return
    const timer = setTimeout(() => setToast(null), 6500)
    return () => clearTimeout(timer)
  }, [toast])

  function notify(message) { setToast(message); setError('') }
  function update(key, value) { setForm(previous => ({ ...previous, [key]: value })) }
  function changeTab(value) { setTab(value); setError('') }

  async function save(event) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const payload = { nume: form.nume, username: form.username, telefon: form.telefon }
      if (utilizator.rol === 'profesor') { payload.descriere = form.descriere; payload.materieIds = form.materieIds }
      const result = await setariApi('', { metoda: 'PATCH', corp: payload })
      await load(); await reincarca(); notify(result.mesaj)
    } catch (e) { setError(e.message) } finally { setBusy(false) }
  }

  async function saveFiles(event) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const body = new FormData()
      if (files.poza) body.append('poza', files.poza)
      if (files.dovada) body.append('dovada', files.dovada)
      const result = await setariApi('/documente', { metoda: 'POST', formular: body })
      setFiles({}); setFileKey(k => k + 1); await load(); await reincarca(); notify(result.mesaj)
    } catch (e) { setError(e.message) } finally { setBusy(false) }
  }

  async function requestCode(event) {
    event.preventDefault(); setError('')
    const body = new FormData(event.currentTarget)
    if (scop === 'parola' && body.get('valoareNoua') !== body.get('confirmare')) { setError('Parolele noi nu coincid.'); return }
    setBusy(true)
    try {
      const result = await setariApi('/confirmari', { metoda: 'POST', corp: { scop, parolaActuala: body.get('actuala'), valoareNoua: body.get('valoareNoua') } })
      securityForm.current.reset(); setOtpError(''); setChallenge({ ...result, scop, email: data.utilizator.email })
      notify('Codul de confirmare a fost trimis. Verifică emailul.')
    } catch (e) { setError(e.message) } finally { setBusy(false) }
  }

  async function confirm(code) {
    setBusy(true); setOtpError('')
    try {
      const result = await setariApi('/confirmari/finalizeaza', { metoda: 'POST', corp: { id: challenge.id, cod: code } })
      setChallenge(null); await load(); await reincarca(); notify(result.mesaj)
    } catch (e) { setOtpError(e.message) } finally { setBusy(false) }
  }

  const teacher = utilizator?.rol === 'profesor'
  const tabs = teacher ? [...TABS, ['profesor', 'Profil de profesor', 'book']] : TABS

  return <main className="tv-pagina sc-page">
    <div className="sc-shell">
      <Link href="/" className="sc-back">← Înapoi la insula ta</Link>
      <header className="sc-hero">
        <div><span className="sc-label"><Icon name="shield" /> Colțul tău personal</span><h1>Contul tău.<br />În ritmul tău.</h1><p>O mică oprire pentru lucrurile importante.<br />Actualizează-ți profilul și păstrează-ți contul în siguranță.</p></div>
        <div className="sc-island" aria-hidden="true"><div dangerouslySetInnerHTML={{ __html: DECOR }} /><span><Icon name="lock" width="58" height="58" /></span><i>Totul la locul lui.</i></div>
      </header>

      {utilizator === undefined || (utilizator && !form && !error) ? <div className="sc-card sc-loading" role="status">Pregătim setările contului…</div> : null}
      {error && !form && <div className="sc-card sc-error" role="alert">{error}<button type="button" className="sc-text-button" onClick={() => load().then(() => setError('')).catch(e => setError(e.message))}>Încearcă din nou</button></div>}
      {utilizator && form && <div className="sc-grid">
        <aside className="sc-sidebar">
          <div className="sc-card sc-profile"><span className="sc-avatar">{utilizator.nume?.charAt(0).toUpperCase()}</span><h2>{utilizator.nume}</h2><span className="sc-role">{ROLURI[utilizator.rol]}</span><p>Un profil actualizat,<br />un pas înainte.</p></div>
          <nav className="sc-card sc-tabs" aria-label="Secțiuni setări">{tabs.map(([key, title, icon]) => <button type="button" key={key} onClick={() => changeTab(key)} className={tab === key ? 'sc-selected' : ''} aria-current={tab === key ? 'page' : undefined}><Icon name={icon} />{title}<Icon name="arrow" width="18" /></button>)}</nav>
          <div className="sc-aside-note"><Icon name="lock" /><p>Datele tale sunt personale.<br />Rolul contului este gestionat de echipa noastră.</p></div>
        </aside>

        <section className="sc-card sc-content" aria-labelledby="sc-section-title">
          <div className="sc-section-heading"><span className="sc-section-icon"><Icon name={tab === 'profil' ? 'user' : tab === 'profesor' ? 'book' : 'shield'} /></span><div><span className="sc-kicker">SETĂRILE CONTULUI</span><h2 id="sc-section-title">{tabs.find(t => t[0] === tab)?.[1]}</h2></div></div>
          {tab === 'profil' && <><p className="sc-intro">Așa te cunoaștem pe insulă. Poți actualiza oricând aceste informații.</p><form className="sc-form" onSubmit={save}>
            <Field label="Nume și prenume" name="nume" value={form.nume} onChange={e => update('nume', e.target.value)} autoComplete="name" maxLength={120} required />
            <div className="sc-two"><Field label="Utilizator" name="username" value={form.username} onChange={e => update('username', e.target.value)} autoComplete="username" maxLength={40} required /><Field label={teacher ? 'Telefon' : 'Telefon (opțional)'} name="telefon" type="tel" value={form.telefon} onChange={e => update('telefon', e.target.value)} placeholder="07xx xxx xxx" autoComplete="tel" required={teacher} /></div>
            <div className="sc-email-line"><Field label="Email" type="email" value={data.utilizator.email} readOnly /><button type="button" className="sc-text-button" onClick={() => { setScop('email'); changeTab('securitate') }}>Schimbă emailul <Icon name="arrow" width="16" /></button></div>
            <div className="sc-info"><Icon name="shield" /><p>{teacher ? 'Telefonul ne ajută să păstrăm legătura pentru verificarea profilului și ședințele tale.' : 'Cu numărul de telefon, Linia AI te poate recunoaște când suni.'}</p></div>
            {teacher && <p className="sc-hint">Materiile predate și documentele se actualizează din secțiunea „Profil de profesor”.</p>}
            {error && <div className="sc-error" role="alert">{error}</div>}
            <div className="sc-footer"><span><Icon name="lock" width="16" /> Doar tu poți edita aceste date</span><button className="sc-button sc-green" disabled={busy}>{busy ? 'Se salvează…' : 'Salvează modificările'}<Icon name="check" /></button></div>
          </form></>}

          {tab === 'securitate' && <><p className="sc-intro">Tu ai cheia contului. Pentru modificările importante, verificăm că ești chiar tu.</p>
            <div className="sc-security-note"><span><Icon name="shield" /></span><div><strong>Un pas în plus. Mai multă liniște.</strong><p>Parola actuală + un cod pe emailul contului. Codul este valabil 5 minute și se folosește o singură dată.</p></div><span className="sc-small-badge">2 pași</span></div>
            <div className="sc-security-tabs" aria-label="Tip modificare"><button type="button" className={scop === 'parola' ? 'sc-selected' : ''} onClick={() => { setScop('parola'); setError('') }}>Schimbă parola</button><button type="button" className={scop === 'email' ? 'sc-selected' : ''} onClick={() => { setScop('email'); setError('') }}>Schimbă emailul</button></div>
            <form ref={securityForm} className="sc-form" onSubmit={requestCode} key={scop}>
              <Field label="Parola actuală" name="actuala" type="password" autoComplete="current-password" required maxLength={128} />
              <Field label={scop === 'parola' ? 'Parolă nouă' : 'Email nou'} name="valoareNoua" type={scop === 'parola' ? 'password' : 'email'} autoComplete={scop === 'parola' ? 'new-password' : 'email'} minLength={scop === 'parola' ? 8 : undefined} maxLength={scop === 'parola' ? 128 : 200} required hint={scop === 'parola' ? 'Cel puțin 8 caractere. Alege o parolă pe care nu o folosești în altă parte.' : 'Codul de confirmare va ajunge la adresa actuală, nu la cea nouă.'} />
              {scop === 'parola' && <Field label="Confirmă parola nouă" name="confirmare" type="password" autoComplete="new-password" minLength={8} maxLength={128} required />}
              {error && <div className="sc-error" role="alert">{error}</div>}
              <div className="sc-footer"><span><Icon name="mail" width="16" /> Confirmare prin email</span><button className="sc-button sc-gold" disabled={busy}>{busy ? 'Se trimite codul…' : 'Trimite codul de confirmare'}<Icon name="arrow" /></button></div>
            </form></>}

          {tab === 'profesor' && teacher && <><p className="sc-intro">Ajută elevii să te cunoască. Aceste informații apar pe profilul tău de profesor.</p>
            <div className="sc-status"><Icon name="shield" /><span>{data.profesor?.stareVerificare === 'aprobat' ? 'Profil verificat de echipa noastră' : 'Profilul este în curs de verificare sau necesită actualizare'}</span></div>
            <form className="sc-form" onSubmit={save}><fieldset className="sc-subjects"><legend>Materii predate</legend>{subjects.map(subject => <label key={subject.id}><input type="checkbox" checked={form.materieIds.includes(subject.id)} onChange={e => update('materieIds', e.target.checked ? [...form.materieIds, subject.id] : form.materieIds.filter(id => id !== subject.id))} />{subject.denumire}</label>)}</fieldset>
              <label className="sc-field"><span>Despre tine <small>(apare pe profilul public)</small></span><textarea name="descriere" value={form.descriere} onChange={e => update('descriere', e.target.value)} required maxLength={2000} rows={5} placeholder="Experiență, ce pregătești, cum lucrezi cu elevii" /></label>
              <p className="sc-hint">Schimbarea materiilor predate solicită o nouă verificare a profilului.</p>
              {error && <div className="sc-error" role="alert">{error}</div>}
              <button className="sc-button sc-green" disabled={busy || form.materieIds.length === 0}>{busy ? 'Se salvează…' : 'Salvează profilul de profesor'}<Icon name="check" /></button>
            </form>
            <form className="sc-files-form" onSubmit={saveFiles}><h3>Fotografie și calificare</h3><p className="sc-hint">Păstrăm fișierele actuale dacă nu alegi altele noi.</p><div className="sc-two" key={fileKey}>
              <label className="sc-upload"><Icon name="user" /><strong>Fotografie</strong><small>{data.profesor?.arePoza ? 'Ai deja o fotografie' : 'Adaugă o fotografie'} · JPG, PNG, WebP · max. 5 MB</small><input aria-label="Fotografie" type="file" accept="image/jpeg,image/png,image/webp" onChange={e => setFiles(f => ({ ...f, poza: e.target.files[0] }))} /></label>
              <label className="sc-upload"><Icon name="book" /><strong>Dovada calificării</strong><small>{data.profesor?.areDovada ? 'Document existent' : 'Adaugă o diplomă sau un certificat'} · PDF sau imagine · max. 10 MB</small><input aria-label="Dovada calificării" type="file" accept="application/pdf,image/jpeg,image/png,image/webp" onChange={e => setFiles(f => ({ ...f, dovada: e.target.files[0] }))} /></label>
            </div><p className="sc-hint">O dovadă nouă este verificată de un administrator înainte de reactivarea profilului.</p><button className="sc-button sc-gold" disabled={busy || (!files.poza && !files.dovada)}>{busy ? 'Se încarcă…' : 'Actualizează fișierele'}<Icon name="arrow" /></button></form>
          </>}
        </section>
      </div>}
      <p className="sc-bottom">Fiecare pas mic contează. Inclusiv cel pentru siguranța ta. <span>✦</span></p>
    </div>
    {toast && <div className="sc-toast" role="status" aria-live="polite"><span><Icon name="check" /></span><div><strong>Totul e în regulă!</strong><p>{toast}</p></div><button type="button" aria-label="Închide notificarea" onClick={() => setToast(null)}><Icon name="close" width="18" /></button></div>}
    {challenge && <CodDialog key={challenge.id} challenge={challenge} onClose={() => { setChallenge(null); setOtpError('') }} onConfirm={confirm} busy={busy} error={otpError} />}
  </main>
}


