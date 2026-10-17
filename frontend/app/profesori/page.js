'use client'
// Pagina /profesori : cum câștigă bani platforma (abonament + comision).
// Totul e demonstrativ: butoanele nu procesează nicio plată.

import { useState } from 'react'
import { decorSVG } from '../joc/arta'

const DECOR = decorSVG(41)

const PLANURI = [
  {
    nume: 'Elev', cine: 'Pentru elevi', pret: '0 lei', perioada: 'pentru totdeauna', clasa: 'tv-gri', buton: 'Începe gratuit',
    beneficii: ['Toate capitolele și nivelele', 'Joc Triviador cu prietenii', 'Avatar și teme', 'Linia AI de meditații'],
  },
  {
    nume: 'Profesor Start', cine: 'Pentru profesori', pret: '49 lei', perioada: '/ lună', clasa: 'tv-albastru', buton: 'Alege Start',
    beneficii: ['Profil public cu recenzii', 'Apari în „Găsește un profesor”', 'Calendar de programări', 'Plăți prin platformă'],
  },
  {
    nume: 'Profesor Promovat', cine: 'Pentru profesori', pret: '99 lei', perioada: '/ lună', clasa: 'tv-aur', buton: 'Alege Promovat', recomandat: true,
    beneficii: ['Tot ce include Start', 'Primele locuri în căutări', 'Insignă „Recomandat”', 'Camere Triviador pentru grupele tale'],
  },
]

export default function PaginaProfesori() {
  const [planAles, setPlanAles] = useState(null)

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Pentru profesori</span>
          <h1>Elevii care se pregătesc de bac sunt deja pe insulă.</h1>
          <p>
            Platforma e gratuită pentru elevi. Profesorii își fac un profil, apar în căutări și primesc rezervări pentru
            meditații direct prin platformă.
          </p>
          <div style={{ marginTop: 20, maxWidth: 340 }}>
            <button className="tv-btn tv-aur" onClick={() => setPlanAles('Profesor Start')}>Creează profilul de profesor</button>
          </div>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          <section className="tv-planuri" aria-label="Planuri">
            {PLANURI.map((p) => (
              <div key={p.nume} className={`tv-plan ${p.recomandat ? 'tv-recomandat' : ''}`}>
                {p.recomandat && <span className="tv-eticheta-plan">Recomandat</span>}
                <span className="tv-cine">{p.cine}</span>
                <h3>{p.nume}</h3>
                <div className="tv-pret">{p.pret} <small>{p.perioada}</small></div>
                <ul>{p.beneficii.map((b) => <li key={b}>{b}</li>)}</ul>
                <button className={`tv-btn ${p.clasa}`} onClick={() => setPlanAles(p.nume)}>{p.buton}</button>
              </div>
            ))}
          </section>

          <section className="tv-sectiune">
            <h2>Cum funcționează</h2>
            <p>Elevii învață pe platformă, iar când vor ajutor suplimentar, rezervă o ședință cu un profesor.</p>
          </section>
          <div className="tv-pasi">
            <div className="tv-pas"><span>1</span><h4>Îți faci profilul</h4><p>Experiență, operele la care pregătești, prețul pe ședință și orele libere.</p></div>
            <div className="tv-pas"><span>2</span><h4>Elevii te găsesc</h4><p>Profilul tău apare elevilor care lucrează deja la capitolele pe care le predai.</p></div>
            <div className="tv-pas"><span>3</span><h4>Ședința se plătește prin platformă</h4><p>Elevul plătește la rezervare. Tu primești suma minus comisionul.</p></div>
          </div>

          <section className="tv-sectiune">
            <h2>Ce plătești, concret</h2>
            <p>Exemplu pentru un profesor cu 8 ședințe pe lună, la 80 de lei ședința.</p>
          </section>
          <div className="tv-tabel">
            <table>
              <thead>
                <tr><th>Plan</th><th>Abonament</th><th>Comision (10%)</th><th>Încasezi din ședințe</th></tr>
              </thead>
              <tbody>
                <tr><td>Profesor Start</td><td className="tv-n">49 lei</td><td className="tv-n">64 lei</td><td className="tv-n">576 lei</td></tr>
                <tr><td>Profesor Promovat</td><td className="tv-n">99 lei</td><td className="tv-n">64 lei</td><td className="tv-n">576 lei</td></tr>
              </tbody>
            </table>
          </div>

          <section className="tv-sectiune"><h2>Întrebări frecvente</h2></section>
          <div className="tv-intrebari">
            <details open>
              <summary>Elevii plătesc ceva?</summary>
              <p>Nu. Tot conținutul, jocul și linia AI sunt gratuite pentru elevi. Elevii plătesc doar ședințele pe care le rezervă cu un profesor.</p>
            </details>
            <details>
              <summary>Când se ia comisionul?</summary>
              <p>Doar la ședințele rezervate și plătite prin platformă. Dacă nu ai ședințe într-o lună, plătești doar abonamentul.</p>
            </details>
            <details>
              <summary>Pot renunța oricând?</summary>
              <p>Da. Abonamentul e lunar și îl poți opri din profilul tău.</p>
            </details>
          </div>
        </div>
        <p className="tv-nota">Prețurile sunt orientative. Butoanele nu procesează nicio plată.</p>
      </div>

      {planAles && (
        <div className="tv-fundal" onClick={() => setPlanAles(null)}>
          <div className="tv-fereastra" style={{ maxWidth: 400, textAlign: 'center', paddingTop: 28 }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ color: 'var(--maro)', fontSize: 26 }}>Ai ales {planAles}</h3>
            <p style={{ color: 'var(--gri-text)' }}>Plățile se deschid în curând. Îți trimitem un email când îți poți activa planul.</p>
            <button className="tv-btn" onClick={() => setPlanAles(null)}>Am înțeles</button>
          </div>
        </div>
      )}
    </main>
  )
}
