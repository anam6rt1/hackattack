'use client'
// Pagina /linia-ai : linia telefonică „Meditații la 0 MB” (Twilio + Claude).
// Arată numărul liniei, dacă numărul elevului e salvat în profil (așa îl recunoaște tutorele)
// și apelurile făcute; conversația aleasă apare în telefonul din dreapta.

import { useEffect, useRef, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import { usePagina, useSesiune } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import { decorSVG } from '../joc/arta'

const DECOR = decorSVG(77)

const APEL = [
  ['ai', 'Salut, Ana! Data trecută ai rămas la „Moara cu noroc”, la caracterizarea lui Ghiță. Continuăm de acolo?'],
  ['eu', 'Da. Știu că la început e harnic.'],
  ['ai', 'Corect. Și ce se schimbă la el după ce apare Lică la han?'],
  ['eu', 'Devine lacom și se îndepărtează de Ana.'],
  ['ai', 'Exact. Poți să-mi dai o scenă din text care arată asta?'],
  ['eu', 'Când nu-i mai spune Anei ce vorbește cu Lică.'],
  ['ai', 'Foarte bine. Îți trimit acum rezumatul prin SMS.'],
  ['sms', 'Ghiță: harnic la început, apoi lacom. Cauza: întâlnirea cu Lică. Scenă-cheie: își ascunde înțelegerile cu Lică de Ana. Data viitoare: finalul nuvelei.'],
]

// Mesajele din API → bulele din telefon
const tipBula = (m) => (m.tip === 'sms' ? 'sms' : m.expeditor === 'elev' ? 'eu' : 'ai')

function Telefonul({ utilizator, linie, onSalvat }) {
  const { reincarca } = useSesiune()
  const [telefon, setTelefon] = useState(utilizator.telefon ?? '')
  const [mesaj, setMesaj] = useState(null)
  const [codTrimis, setCodTrimis] = useState(null) // numărul la care a plecat codul prin SMS
  const [cod, setCod] = useState('')
  const [lucreaza, setLucreaza] = useState(false)
  const [apel, setApel] = useState(null) // { ok, text } după apăsarea „Sună-mă”
  const [suna, setSuna] = useState(false)

  // Pasul 1: codul pleacă prin SMS la numărul scris. Pasul 2: codul confirmă că numărul e al tău și îl salvează.
  async function cereCod(e) {
    e.preventDefault()
    setLucreaza(true)
    try {
      const r = await api('/utilizatori/me/setari/telefon/cod', { metoda: 'POST', corp: { telefon } })
      setCodTrimis(r.telefon)
      setCod('')
      setMesaj({ ok: true, text: r.mesaj })
    } catch (err) {
      setMesaj({ ok: false, text: err.message })
    } finally {
      setLucreaza(false)
    }
  }

  async function confirmaCod(e) {
    e.preventDefault()
    setLucreaza(true)
    try {
      const r = await api('/utilizatori/me/setari/telefon/confirma', { metoda: 'POST', corp: { telefon: codTrimis, cod } })
      setCodTrimis(null)
      await reincarca()
      onSalvat()
      setMesaj({ ok: true, text: r.mesaj })
    } catch (err) {
      setMesaj({ ok: false, text: err.message })
    } finally {
      setLucreaza(false)
    }
  }

  // Tutorele te sună pe numărul salvat; apelul apare apoi în „Apelurile tale”
  async function sunaMa() {
    setSuna(true)
    setApel(null)
    try {
      const r = await api('/linia-ai/suna-ma', { metoda: 'POST' })
      setApel({ ok: true, text: r.mesaj })
      setTimeout(onSalvat, 15000)
    } catch (err) {
      setApel({ ok: false, text: err.message })
    } finally {
      setSuna(false)
    }
  }

  return (
    <div className="tv-panou">
      <div className="tv-cutie">
        <div className="tv-mic">Vorbește cu tutorele</div>
        {utilizator.rol === 'elev' ? (
          <>
            <p>Apasă butonul și tutorele te sună pe numărul tău de telefon. Nu ai nevoie de date mobile.</p>
            <button className="tv-btn" onClick={sunaMa} disabled={suna || !linie.poateSuna || !linie.telefonVerificat}>
              {suna ? 'Se inițiază apelul...' : 'Sună-mă'}
            </button>
            {!linie.telefonVerificat && <p className="tv-indiciu">Verifică mai întâi numărul tău de telefon (mai jos), cu codul primit prin SMS.</p>}
            {linie.telefonVerificat && !linie.poateSuna && <p className="tv-indiciu">Apelurile nu sunt încă pornite pe server.</p>}
            {apel && <p className={apel.ok ? 'tv-succes' : 'tv-eroare'} role="status">{apel.text}</p>}
          </>
        ) : (
          <p>Elevii pot cere aici ca tutorele să îi sune.</p>
        )}
        <p className="tv-indiciu">Aplicația este în curs de dezvoltare, vom oferi posibilitatea de a suna la un număr de telefon în curând.</p>
      </div>
      {utilizator.rol === 'elev' && (
        <form className="tv-cutie tv-panou-lat tv-form" onSubmit={codTrimis ? confirmaCod : cereCod}>
          <h3>Numărul tău de telefon</h3>
          <p>
            {linie.telefonVerificat
              ? `Tutorele te sună pe ${linie.telefonulMeu} (verificat) când apeși „Sună-mă”. Apelarea liniei de pe acest număr va fi disponibilă în curând.`
              : linie.telefonulMeu
                ? `Numărul ${linie.telefonulMeu} nu e verificat încă. Primești un cod prin SMS ca să confirmi că e al tău.`
                : 'Adaugă numărul tău. Primești un cod prin SMS ca să confirmi că e al tău; abia apoi tutorele te poate suna.'}
          </p>
          {codTrimis ? (
            <div className="tv-grid-2">
              <input className="tv-camp-text" inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={cod}
                onChange={(e) => setCod(e.target.value)} placeholder="Codul din SMS" aria-label="Codul primit prin SMS" />
              <button className="tv-btn" disabled={lucreaza || cod.trim().length !== 6}>Confirmă</button>
            </div>
          ) : (
            <div className="tv-grid-2">
              <input className="tv-camp-text" type="tel" value={telefon} onChange={(e) => setTelefon(e.target.value)} placeholder="07xx xxx xxx" aria-label="Numărul tău de telefon" />
              <button className="tv-btn" disabled={lucreaza || !telefon.trim()}>{linie.telefonVerificat ? 'Schimbă numărul' : 'Trimite codul prin SMS'}</button>
            </div>
          )}
          {codTrimis && <button type="button" className="tv-btn tv-aur" onClick={() => setCodTrimis(null)}>Alt număr / retrimite</button>}
          {mesaj && <p className={mesaj.ok ? 'tv-succes' : 'tv-eroare'}>{mesaj.text}</p>}
        </form>
      )}
    </div>
  )
}

// Combinații gata făcute; aceleași idei le înțelege și tutorele când i le ceri în apel
const PRESETURI = [
  { nume: 'Normală', voce: 'ioana', viteza: 100, ton: 0 },
  { nume: 'Bunică', voce: 'ioana', viteza: 80, ton: -8 },
  { nume: 'Tânără, vioaie', voce: 'ioana', viteza: 110, ton: 8 },
  { nume: 'Rar și clar', voce: 'carmen', viteza: 80, ton: 0 },
  { nume: 'Mai gravă', voce: 'maria', viteza: 95, ton: -18 },
]

function VoceaTutorelui({ linie, onSalvat }) {
  const [setari, setSetari] = useState(linie.voce)
  const [mesaj, setMesaj] = useState(null)
  const schimbat = setari.voce !== linie.voce.voce || setari.viteza !== linie.voce.viteza || setari.ton !== linie.voce.ton

  async function salveaza() {
    try {
      await api('/utilizatori/me/voce-tutor', { metoda: 'PUT', corp: setari })
      onSalvat()
      setMesaj({ ok: true, text: 'Vocea a fost salvată. O vei auzi la următorul apel.' })
    } catch (e) {
      setMesaj({ ok: false, text: e.message })
    }
  }

  return (
    <section className="tv-cutie tv-form">
      <h3>Vocea tutorelui</h3>
      <p>
        Poți schimba vocea și în timpul apelului, spunând de exemplu „vorbește mai rar”, „vorbește ca o bunică” sau „mai repede, te rog”.
        Pentru limba română există doar voci feminine; le poți face mai grave sau mai subțiri din ton.
      </p>
      <div className="tv-optiuni">
        {PRESETURI.map((p) => (
          <button key={p.nume} type="button" className="tv-optiune" onClick={() => setSetari({ voce: p.voce, viteza: p.viteza, ton: p.ton })}>
            {p.nume}
          </button>
        ))}
      </div>
      <fieldset className="tv-eticheta-camp">
        <span>Vocea</span>
        <div className="tv-optiuni">
          {linie.voci.map((v) => (
            <label key={v.cod} className="tv-optiune">
              <input type="radio" name="voce" checked={setari.voce === v.cod} onChange={() => setSetari({ ...setari, voce: v.cod })} />
              {v.nume} <small style={{ color: 'var(--gri-text)' }}>· {v.descriere}</small>
            </label>
          ))}
        </div>
      </fieldset>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp">
          <span>Viteza: {setari.viteza}%{setari.viteza < 100 ? ' (mai rar)' : setari.viteza > 100 ? ' (mai repede)' : ''}</span>
          <input type="range" min={70} max={130} step={5} value={setari.viteza} onChange={(e) => setSetari({ ...setari, viteza: Number(e.target.value) })} />
        </label>
        <label className="tv-eticheta-camp">
          <span>Ton: {setari.ton > 0 ? '+' : ''}{setari.ton}%{setari.ton < 0 ? ' (mai grav)' : setari.ton > 0 ? ' (mai subțire)' : ''}</span>
          <input type="range" min={-20} max={20} step={2} value={setari.ton} onChange={(e) => setSetari({ ...setari, ton: Number(e.target.value) })} />
        </label>
      </div>
      <button className="tv-btn" onClick={salveaza} disabled={!schimbat}>Salvează vocea</button>
      {mesaj && <p className={mesaj.ok ? 'tv-succes' : 'tv-eroare'}>{mesaj.text}</p>}
    </section>
  )
}

// Conversația completă a unui apel, într-o fereastră peste pagină
function JurnalApel({ apel, mesaje, onInchide }) {
  useEffect(() => {
    const tasta = (e) => e.key === 'Escape' && onInchide()
    window.addEventListener('keydown', tasta)
    return () => window.removeEventListener('keydown', tasta)
  }, [onInchide])

  const durata = apel.finalizatLa ? Math.round((new Date(apel.finalizatLa) - new Date(apel.inceputLa)) / 1000) : null
  const detalii = [`${mesaje.length} mesaje`, durata !== null && `${Math.floor(durata / 60)} min ${durata % 60} s`, apel.status === 'in_curs' && 'în curs']
    .filter(Boolean).join(' · ')

  return (
    <div className="tv-fundal" onClick={onInchide}>
      <div className="tv-fereastra" role="dialog" aria-modal="true" aria-labelledby="jurnal-titlu" onClick={(e) => e.stopPropagation()}>
        <div className="tv-eticheta-sus">
          <b id="jurnal-titlu">Apel din {dataOra(apel.inceputLa)}</b>
          <small>{apel.curs ? `${apel.curs} · ` : ''}{detalii}</small>
        </div>
        {mesaje.length ? (
          <div className="tv-jurnal-apel">
            {mesaje.map((m) => (
              <div key={m.id} className={`tv-bula tv-bula-${tipBula(m)}`}>
                <small>{tipBula(m) === 'sms' ? 'SMS' : m.expeditor === 'elev' ? 'Tu' : 'Tutorele'} · {ora(m.creatLa)}</small>
                {m.continut}
              </div>
            ))}
          </div>
        ) : (
          <p className="tv-gol">Apelul nu are mesaje înregistrate.</p>
        )}
        <button className="tv-btn tv-gri" style={{ width: '100%', marginTop: 16 }} onClick={onInchide} autoFocus>Închide</button>
      </div>
    </div>
  )
}

const ora = (d) => new Date(d).toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit', second: '2-digit' })

export default function PaginaLiniaAI() {
  const utilizator = usePagina('elev', 'profesor')
  const [linie, setLinie] = useState(null)
  const [apeluri, setApeluri] = useState([])
  const [ales, setAles] = useState(null) // { apel, mesaje } afișat în fereastra cu jurnalul
  const [eroareJurnal, setEroareJurnal] = useState(null)
  const [vizibile, setVizibile] = useState(APEL.length) // la încărcare se vede tot apelul
  const [secunde, setSecunde] = useState(102)
  const timere = useRef([])

  useEffect(() => () => timere.current.forEach(clearTimeout), [])

  function incarca() {
    api('/linia-ai').then(setLinie).catch(() => setLinie({ numar: '', telefonulMeu: null }))
    api('/sesiuni?canal=telefon').then((r) => setApeluri(r.elemente)).catch(() => {})
  }
  useEffect(() => {
    if (utilizator) incarca()
  }, [utilizator])

  async function deschide(apel) {
    setEroareJurnal(null)
    try {
      const r = await api(`/sesiuni/${apel.id}/mesaje`)
      setAles({ apel, mesaje: r.elemente })
    } catch (e) {
      setEroareJurnal(e.message)
    }
  }

  if (!utilizator || !linie) return <Incarcare />
  const bule = APEL.slice(0, vizibile)

  function reda() {
    timere.current.forEach(clearTimeout)
    timere.current = []
    setVizibile(0)
    setSecunde(0)
    APEL.forEach((_, i) => timere.current.push(setTimeout(() => setVizibile(i + 1), 600 + i * 1600)))
    for (let s = 1; s <= Math.ceil((APEL.length * 1600) / 1000); s++) {
      timere.current.push(setTimeout(() => setSecunde(s), s * 1000))
    }
  }

  const ceas = `${String(Math.floor(secunde / 60)).padStart(2, '0')}:${String(secunde % 60).padStart(2, '0')}`

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Meditații la 0 MB</span>
          <h1>Fără internet? Învață la telefon cu tutorele AI.</h1>
          <p>
            Elevii fără date mobile sau fără laptop pot recapitula pentru bac printr-un apel telefonic obișnuit. Tutorele îi
            sună pe numărul salvat și continuă de unde au rămas pe site.
          </p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          {ales && <JurnalApel apel={ales.apel} mesaje={ales.mesaje} onInchide={() => setAles(null)} />}
          <Telefonul utilizator={utilizator} linie={linie} onSalvat={incarca} />

          {linie.voce && utilizator.rol === 'elev' && <VoceaTutorelui key={JSON.stringify(linie.voce)} linie={linie} onSalvat={incarca} />}

          <section className="tv-cutie">
            <h3>Apelurile tale</h3>
            {apeluri.length ? (
              <ul className="tv-lista">
                {apeluri.map((a) => (
                  <li key={a.id}>
                    <button className={`tv-rand tv-rand-buton ${ales?.apel.id === a.id ? 'tv-rand-activ' : ''}`} onClick={() => deschide(a)}>
                      <div>
                        <b>{dataOra(a.inceputLa)}{a.curs ? ` · ${a.curs}` : ''}</b>
                        <small>{a.previzualizare ?? 'Fără mesaje înregistrate'} · {a.numarMesaje} mesaje</small>
                      </div>
                      <span className={`tv-pastila ${a.status === 'in_curs' ? 'tv-pastila-aur' : 'tv-pastila-verde'}`}>{a.status === 'in_curs' ? 'În curs' : 'Încheiat'}</span>
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="tv-gol">Nu ai sunat încă. Conversațiile tale cu tutorele vor apărea aici.</p>
            )}
            {eroareJurnal && <p className="tv-eroare" role="alert">{eroareJurnal}</p>}
          </section>

          <div className="tv-apel-grid">
            <div style={{ display: 'grid', gap: 28, minWidth: 0 }}>
              <section className="tv-sectiune">
                <h2>Cum funcționează</h2>
                <p>Același profil de elev, pe site și la telefon. Rezumatul prin SMS este încă în dezvoltare.</p>
              </section>
              <div className="tv-pasi tv-o-coloana">
                <div className="tv-pas"><span>1</span><h4>Apeși „Sună-mă”</h4><p>Tutorele te sună pe numărul salvat în profil. La telefon nu ai nevoie de cont sau parolă.</p></div>
                <div className="tv-pas"><span>2</span><h4>Vorbești cu tutorele</h4><p>Îți pune întrebări scurte despre opera la care ai rămas și te ajută să ajungi singur la răspuns.</p></div>
                <div className="tv-pas"><span>3</span><h4>Regăsești apelul pe site</h4><p>Conversația apare în „Apelurile tale”. Dacă apelul se încheie înainte să terminați, continuați data viitoare.</p></div>
              </div>
            </div>

            <div>
              <div className="tv-telefon">
                <div className="tv-telefon-in">
                  <div className="tv-apel-cap">
                    <span>
                      <svg viewBox="0 0 40 40" width="34" height="34" aria-hidden="true">
                        <ellipse cx="20" cy="26" rx="14" ry="8" fill="#f1d9a0" />
                        <ellipse cx="20" cy="24" rx="10" ry="6" fill="#6cc04a" />
                        <path d="M20 8v15" stroke="#6b3410" strokeWidth="2.4" />
                        <path d="M20 8l9 3.5-9 3.5z" fill="#e2463b" />
                      </svg>
                    </span>
                    <b>Tutorele de Bac</b>
                    <small>Apel de exemplu · {ceas}</small>
                  </div>
                  <span className="tv-badge0">Apel GSM · 0 MB date</span>
                  <div className="tv-unda" aria-hidden="true">
                    {Array.from({ length: 22 }, (_, i) => <i key={i} style={{ animationDelay: (i % 7) * -0.13 + 's' }} />)}
                  </div>
                  <div className="tv-bule">
                    {bule.map(([cine, text], i) => (
                      <div key={i} className={`tv-bula tv-bula-${cine}`}>
                        {cine === 'sms' && <b>SMS · funcție în dezvoltare</b>}
                        {text}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
              <div style={{ marginTop: 14 }}>
                <button className="tv-btn tv-aur" onClick={reda}>Ascultă exemplul de apel</button>
              </div>
            </div>
          </div>

          <section className="tv-sectiune"><h2>Ce poți face la telefon</h2></section>
          <div className="tv-functii">
            <div><h4>Recapitulezi o operă</h4><p>Tema, personajele, momentele subiectului, pe rând, cu întrebări scurte.</p></div>
            <div><h4>Exersezi caracterizarea</h4><p>Spui trăsăturile unui personaj, iar tutorele îți cere exemple din text.</p></div>
            <div><h4>Faci un test oral rapid <span className="tv-pastila tv-pastila-aur">în dezvoltare</span></h4><p>Cinci întrebări de tip grilă, citite la telefon, cu scorul trimis prin SMS. Această funcție nu este încă disponibilă.</p></div>
            <div><h4>Continui de unde ai rămas</h4><p>Ce faci la telefon apare în profil, iar pe site continui de la același pas.</p></div>
          </div>

          <section className="tv-sectiune">
            <h2>Ce se întâmplă în spate</h2>
            <p>Drumul unui apel, de la telefonul elevului până la tutore și înapoi.</p>
          </section>
          <div className="tv-flux-wrap">
            <div className="tv-flux">
              <div className="tv-nod"><b>Telefonul elevului</b><small>apel, fără date (SMS în dezvoltare)</small></div>
              <span className="tv-sageata">→</span>
              <div className="tv-nod"><b>Twilio</b><small>numărul liniei și sunetul apelului</small></div>
              <span className="tv-sageata">→</span>
              <div className="tv-nod"><b>Serverul nostru</b><small>recunoaște elevul după număr</small></div>
              <span className="tv-sageata">⇄</span>
              <div className="tv-nod tv-nod-ai"><b>Claude</b><small>tutorele care răspunde</small></div>
              <span className="tv-sageata">→</span>
              <div className="tv-nod"><b>Profilul elevului</b><small>progresul apare pe site</small></div>
            </div>
          </div>
        </div>
        <p className="tv-nota">Până alegi un apel din listă, telefonul arată o conversație exemplu.</p>
      </div>
    </main>
  )
}
