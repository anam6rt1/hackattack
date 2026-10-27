'use client'
// /contact : oricine (cu cont sau fără) scrie administratorilor platformei. Cu cont, numele și emailul vin din cont; fără cont
// se cer, ca administratorul să poată răspunde. ?de=/cale = pagina de pe care s-a venit (din subsolul site-ului), trimisă cu mesajul.
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useSearchParams } from 'next/navigation'
import { api } from '@/lib/api'
import { useSesiune } from '@/lib/sesiune'
import { useRetea } from '@/lib/useOnline'
import { SUBIECTE } from '@/lib/contact'

const MAX_TEXT = 4000

// Doar o cale de pe site (fără parametri), ca în API.
const caleSigura = (de) => (de && de.startsWith('/') && !de.startsWith('//') && de !== '/contact' ? de.split(/[?#]/)[0].slice(0, 200) : '')

export default function PaginaContact() {
  const { utilizator } = useSesiune()
  const de = caleSigura(useSearchParams().get('de'))
  const retea = useRetea()
  const [subiect, setSubiect] = useState('')
  const [text, setText] = useState('')
  const [trimis, setTrimis] = useState(false)
  const [ocupat, setOcupat] = useState(false)
  const [eroare, setEroare] = useState('')
  const [contactOperator, setContactOperator] = useState(null)

  useEffect(() => {
    api('/confidentialitate').then((c) => setContactOperator(c.contact || null), () => {})
  }, [])

  async function trimite(e) {
    e.preventDefault()
    if (!subiect) { setEroare('Alege despre ce e vorba.'); return }
    const f = new FormData(e.currentTarget)
    setOcupat(true); setEroare('')
    try {
      await api('/contact', {
        metoda: 'POST',
        corp: { subiect, text, pagina: de || null, nume: f.get('nume'), email: f.get('email'), site: f.get('site') },
      })
      setTrimis(true); setText(''); setSubiect('')
    } catch (err) {
      setEroare(err.message)
    } finally {
      setOcupat(false)
    }
  }

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Contact</span>
          <h1>Scrie-ne</h1>
          <p>Ai dat de o problemă, ai o întrebare despre cont sau vrei să aduci școala ta pe TeleBac? Mesajul ajunge la administratorii platformei.</p>
        </section>

        <div className="tv-coala">
          <div className="tv-panou">
            <section className="tv-cutie tv-panou-lat" aria-labelledby="contact-titlu">
              <h2 id="contact-titlu" className="tv-contact-titlu">{trimis ? 'Mesaj trimis' : 'Mesajul tău'}</h2>
              {trimis ? (
                <>
                  <p className="tv-succes" role="status">
                    Mulțumim! Mesajul a ajuns la administratori. {utilizator ? 'Răspunsul vine pe adresa de email a contului.' : 'Răspunsul vine pe adresa de email pe care ai scris-o.'}
                  </p>
                  <button type="button" className="tv-btn tv-aur tv-contact-buton" onClick={() => setTrimis(false)}>Trimite alt mesaj</button>
                </>
              ) : utilizator === undefined ? (
                <p className="tv-asteapta">Se încarcă...</p>
              ) : (
                <form className="tv-form" onSubmit={trimite}>
                  <fieldset className="tv-eticheta-camp">
                    <span id="contact-subiect">Despre ce e vorba?</span>
                    <div className="tv-optiuni" role="radiogroup" aria-labelledby="contact-subiect">
                      {SUBIECTE.map((s) => (
                        <label key={s.id} className="tv-optiune">
                          <input type="radio" name="subiect" value={s.id} checked={subiect === s.id} onChange={() => setSubiect(s.id)} required />
                          {s.nume}
                        </label>
                      ))}
                    </div>
                  </fieldset>

                  {utilizator ? (
                    <p className="tv-indiciu">Trimiți ca <b>{utilizator.nume}</b>{utilizator.email ? <> ({utilizator.email})</> : null}; răspunsul vine pe adresa contului.</p>
                  ) : (
                    <div className="tv-grid-2">
                      <label className="tv-eticheta-camp">
                        <span>Numele tău</span>
                        <input className="tv-camp-text" name="nume" required maxLength={120} autoComplete="name" />
                      </label>
                      <label className="tv-eticheta-camp">
                        <span>Emailul la care îți răspundem</span>
                        <input className="tv-camp-text" name="email" type="email" required maxLength={200} autoComplete="email" />
                      </label>
                    </div>
                  )}

                  <div className="tv-eticheta-camp">
                    <label className="tv-eticheta-camp">
                      <span>Mesajul</span>
                      <textarea className="tv-camp-text" name="text" required maxLength={MAX_TEXT} rows={7} value={text} onChange={(e) => setText(e.target.value)}
                        aria-describedby="contact-numar" placeholder={subiect === 'problema' ? 'Ce încercai să faci și ce s-a întâmplat? Pe ce dispozitiv?' : ''} />
                    </label>
                    <small id="contact-numar" className="tv-indiciu tv-contact-numar">{text.length} / {MAX_TEXT} caractere</small>
                  </div>

                  {/* Câmp ascuns pentru oameni: îl completează doar roboții de spam, iar serverul ignoră atunci mesajul. */}
                  <div className="tv-contact-capcana" aria-hidden="true">
                    <label>Site <input name="site" tabIndex={-1} autoComplete="off" /></label>
                  </div>

                  {de && <p className="tv-indiciu">Trimitem și pagina de pe care ai venit: <code>{de}</code></p>}
                  <p className="tv-indiciu">Nu scrie parole sau coduri primite pe email ori SMS: nu ți le cerem niciodată.</p>
                  {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
                  {retea !== 'online' && <p className="tv-eroare" role="status">Fără legătură la server mesajul nu poate fi trimis. Încearcă din nou când revine internetul.</p>}
                  <button className="tv-btn tv-contact-buton" disabled={ocupat || retea !== 'online'}>{ocupat ? 'Se trimite...' : 'Trimite mesajul'}</button>
                </form>
              )}
            </section>

            <aside className="tv-contact-lateral">
              <section className="tv-cutie">
                <h3>Poate te ajută deja</h3>
                <ul className="tv-lista-simpla">
                  {!utilizator && <li><b>Ai uitat parola?</b> Pe <Link href="/">prima pagină</Link>, la „Intră”, apasă „Am uitat parola”.</li>}
                  {!utilizator && <li><b>Nu ai primit codul de confirmare?</b> Uită-te și în Spam. Când intri în cont cu emailul neconfirmat, poți cere un cod nou.</li>}
                  {utilizator && <li><b>Datele contului</b>, emailul și telefonul se schimbă din <Link href="/setari">Setări</Link>.</li>}
                  {utilizator?.rol === 'elev' && <li><b>Fără internet?</b> Lecțiile descărcate sunt în <Link href="/descarcate">Lecții descărcate</Link>.</li>}
                  <li><b>Pentru o școală:</b> creează un cont de tip „Organizație”; un administrator îl verifică, apoi îți inviți profesorii și elevii.</li>
                </ul>
              </section>
              <section className="tv-cutie">
                <h3>Ce se întâmplă cu mesajul</h3>
                <p>Îl citesc administratorii platformei și îți răspund pe email. Numele, emailul și mesajul le folosim doar ca să-ți răspundem.</p>
                {contactOperator && <p>Poți scrie și direct la <a href={`mailto:${contactOperator}`}>{contactOperator}</a>.</p>}
              </section>
            </aside>
          </div>
        </div>
      </div>
    </main>
  )
}
