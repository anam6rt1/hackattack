'use client'
// Panoul din dreapta hărții: camera (înainte de start) sau jucătorii + jurnalul (în timpul jocului).
// Tot ce apare aici vine din starea trimisă de server.

import { QRCodeSVG } from 'qrcode.react'
import Avatar from './Avatar'
import { insula } from './date-harta'

const NIVELURI = [['usor', 'Ușor'], ['mediu', 'Mediu'], ['greu', 'Greu']]

function IconInsula({ culoare }) {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <ellipse cx="10" cy="13" rx="9" ry="5" fill="#f1d9a0" />
      <ellipse cx="10" cy="11.5" rx="6.5" ry="3.6" fill="#6cc04a" />
      <path d="M10 3v8" stroke="#6b3410" strokeWidth="1.6" />
      <path d="M10 3l6 2.2-6 2.2z" fill={culoare} />
    </svg>
  )
}

export function PanouCamera({ stare, euId, link, onPorneste, onAdaugaBot, onScoateBot }) {
  const esteGazda = stare.gazdaId === euId
  const locuriLibere = stare.maxJucatori - stare.jucatori.length
  return (
    <div className="tv-card">
      <div className="tv-camera-sus">
        <div>
          <div className="tv-mic">Camera · {stare.materie}</div>
          <div className="tv-cod">{stare.cod}</div>
          <div className="tv-mic">{stare.harta.insule.length} insule · {stare.maxJucatori} locuri</div>
        </div>
        {link && (
          <div className="tv-qr">
            <QRCodeSVG value={link} size={70} fgColor="#6b3410" />
          </div>
        )}
      </div>

      <div className="tv-jucatori">
        {stare.jucatori.map((j) => (
          <div key={j.id} className="tv-jucator tv-intra">
            <Avatar nume={j.nume} culoare={j.culoare} />
            <span className="tv-nume">
              {j.nume}
              {j.id === euId && ' (tu)'}
              {j.id === stare.gazdaId && ' · gazdă'}
              {j.cetate && <small>Cetatea: {insula(j.cetate, stare.harta)?.nume}</small>}
            </span>
            {j.bot && esteGazda && <button type="button" className="tv-scoate" onClick={() => onScoateBot(j.id)}>Scoate</button>}
          </div>
        ))}
        {stare.spectatori.length > 0 && (
          <p className="tv-mic">Privesc: {stare.spectatori.map((s) => s.nume).join(', ')} (camera e plină).</p>
        )}
      </div>

      {esteGazda && locuriLibere > 0 && (
        <div className="tv-formular">
          <div className="tv-mic">Adaugă un robot pe un loc liber</div>
          <div className="tv-boti">
            {NIVELURI.map(([cod, nume]) => (
              <button key={cod} type="button" className="tv-btn tv-aur" onClick={() => onAdaugaBot(cod)}>{nume}</button>
            ))}
          </div>
          <small className="tv-mic">Într-un joc cu roboți primești cu 30% mai puțin XP.</small>
        </div>
      )}

      {stare.jurnal.length > 0 && (
        <ul className="tv-jurnal" aria-live="polite">
          {stare.jurnal.slice(0, 3).map((r, i) => (
            <li key={i}>
              <span className="tv-punct" style={{ background: r.culoare }} />
              {r.text}
            </li>
          ))}
        </ul>
      )}

      <ul className="tv-reguli">
        <li><i>1</i><span>La rândul tău, alegi o insulă legată printr-un pod de insulele tale.</span></li>
        <li><i>2</i><span>Insulă liberă: răspunzi corect la întrebare și e a ta.</span></li>
        <li><i>3</i><span>Insula altcuiva: duel. La egalitate, câștigă cine ghicește mai aproape răspunsul numeric.</span></li>
        <li><i>4</i><span>La final: 10 XP pentru fiecare insulă, plus 50 XP pentru câștigător.</span></li>
      </ul>

      {esteGazda ? (
        <button className="tv-btn" onClick={onPorneste} disabled={stare.jucatori.length < 2}>
          {stare.jucatori.length < 2 ? 'Așteaptă un jucător sau adaugă un robot' : 'Pornește bătălia'}
        </button>
      ) : (
        <p className="tv-asteapta">Așteaptă ca gazda să pornească bătălia...</p>
      )}
    </div>
  )
}

export function PanouJoc({ stare, euId }) {
  const curent = stare.faza === 'final' ? null : stare.jucatori[stare.tura]
  const numar = (id) => Object.values(stare.proprietari).filter((p) => p === id).length
  const eRandulMeu = curent?.id === euId
  const terminat = stare.faza === 'final'
  const runda = Math.min(stare.runda, stare.runde)

  let indiciu = ''
  if (terminat) indiciu = 'Bătălia s-a terminat.'
  else if (stare.faza === 'alegere') indiciu = eRandulMeu ? 'Alege o insulă cu marginea aurie.' : `${curent.nume} își alege ținta...`
  else indiciu = 'Duel în desfășurare...'

  return (
    <>
      <div className="tv-card">
        <div className="tv-tura">
          <span className="tv-runda">{runda}</span>
          <div>
            <div className="tv-mic">Runda {runda} din {stare.runde}</div>
            <h3>{terminat ? 'Final' : eRandulMeu ? 'Rândul tău' : `Rândul lui ${curent.nume}`}</h3>
            <span className="tv-indiciu">{indiciu}</span>
          </div>
        </div>
        <div className="tv-jucatori">
          {stare.jucatori.map((j) => (
            <div key={j.id} className={`tv-jucator ${j.id === curent?.id ? 'tv-randul' : ''}`}>
              <Avatar nume={j.nume} culoare={j.culoare} />
              <span className="tv-nume">
                {j.nume}
                {j.id === euId && ' (tu)'}
                <small>{j.bot ? 'robot' : j.conectat ? (numar(j.id) === 1 ? '1 insulă' : `${numar(j.id)} insule`) : 'deconectat'}</small>
              </span>
              <span className="tv-numar">
                <IconInsula culoare={j.culoare} />
                {numar(j.id)}
              </span>
            </div>
          ))}
        </div>
      </div>

      <div className="tv-card">
        <div className="tv-mic">Jurnalul bătăliei</div>
        <ul className="tv-jurnal">
          {stare.jurnal.map((r, i) => (
            <li key={i}>
              <span className="tv-punct" style={{ background: r.culoare }} />
              {r.text}
            </li>
          ))}
        </ul>
      </div>
    </>
  )
}
