'use client'
// Pagina /linia-ai : prezintă linia telefonică „Meditații la 0 MB” (Twilio + Claude).
// Linia reală se construiește separat; aici e doar prezentarea + un exemplu de apel.

import { useEffect, useRef, useState } from 'react'
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

export default function PaginaLiniaAI() {
  const [vizibile, setVizibile] = useState(APEL.length) // la încărcare se vede tot apelul
  const [secunde, setSecunde] = useState(102)
  const timere = useRef([])

  useEffect(() => () => timere.current.forEach(clearTimeout), [])

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
          <h1>Fără internet? Sună și învață cu tutorele AI.</h1>
          <p>
            Elevii fără date mobile sau fără laptop pot recapitula pentru bac printr-un apel telefonic obișnuit. Tutorele îi
            recunoaște după număr și continuă de unde au rămas pe site.
          </p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          <div className="tv-status">
            <b>În pregătire.</b>
            <span>Numărul liniei va fi anunțat la lansare. Până atunci, poți asculta mai jos un exemplu de apel.</span>
          </div>

          <div className="tv-apel-grid">
            <div style={{ display: 'grid', gap: 28, minWidth: 0 }}>
              <section className="tv-sectiune">
                <h2>Cum funcționează</h2>
                <p>Același profil de elev, pe trei canale: site, apel și SMS.</p>
              </section>
              <div className="tv-pasi tv-o-coloana">
                <div className="tv-pas"><span>1</span><h4>Suni de pe numărul tău</h4><p>Nu ai nevoie de cont sau parolă la telefon. Tutorele te recunoaște după numărul salvat în profil.</p></div>
                <div className="tv-pas"><span>2</span><h4>Vorbești cu tutorele</h4><p>Îți pune întrebări scurte despre opera la care ai rămas și te ajută să ajungi singur la răspuns.</p></div>
                <div className="tv-pas"><span>3</span><h4>Primești rezumatul prin SMS</h4><p>La final, ideile principale îți vin pe telefon, iar progresul apare în profilul tău de pe site.</p></div>
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
                    <small>Apel în curs · {ceas}</small>
                  </div>
                  <span className="tv-badge0">Apel GSM · 0 MB date</span>
                  <div className="tv-unda" aria-hidden="true">
                    {Array.from({ length: 22 }, (_, i) => <i key={i} style={{ animationDelay: (i % 7) * -0.13 + 's' }} />)}
                  </div>
                  <div className="tv-bule">
                    {APEL.slice(0, vizibile).map(([cine, text], i) => (
                      <div key={i} className={`tv-bula tv-bula-${cine}`}>
                        {cine === 'sms' && <b>SMS · Tutorele de Bac</b>}
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
            <div><h4>Faci un test oral rapid</h4><p>Cinci întrebări de tip grilă, citite la telefon, cu scorul trimis prin SMS.</p></div>
            <div><h4>Continui de unde ai rămas</h4><p>Ce faci la telefon apare în profil, iar pe site continui de la același pas.</p></div>
          </div>

          <section className="tv-sectiune">
            <h2>Ce se întâmplă în spate</h2>
            <p>Drumul unui apel, de la telefonul elevului până la tutore și înapoi.</p>
          </section>
          <div className="tv-flux-wrap">
            <div className="tv-flux">
              <div className="tv-nod"><b>Telefonul elevului</b><small>apel sau SMS, fără date</small></div>
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
        <p className="tv-nota">Conversația din telefon e un exemplu scris pentru demo.</p>
      </div>
    </main>
  )
}
