'use client'
// Panoul din dreapta hărții: camera (înainte de start) sau jucătorii + jurnalul (în timpul jocului).

import { QRCodeSVG } from 'qrcode.react'
import Avatar from './Avatar'
import { INSULE, insula, CETATI, CULORI } from './date-harta'

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

export function PanouCamera({ cod, link, online, euId, esteGazda, onStart, insule = INSULE, materie, gata = true }) {
  return (
    <div className="tv-card">
      <div className="tv-camera-sus">
        <div>
          <div className="tv-mic">Camera{materie ? ` · ${materie}` : ''}</div>
          <div className="tv-cod">{cod}</div>
        </div>
        {link && (
          <div className="tv-qr">
            <QRCodeSVG value={link} size={70} fgColor="#6b3410" />
          </div>
        )}
      </div>

      <div className="tv-jucatori">
        {online.slice(0, 3).map((j, i) => (
          <div key={j.id} className="tv-jucator tv-intra">
            <Avatar nume={j.nume} culoare={CULORI[i]} />
            <span className="tv-nume">
              {j.nume}
              {j.id === euId && ' (tu)'}
              <small>Cetatea: {insula(CETATI[i], insule).nume}</small>
            </span>
          </div>
        ))}
        {online.length > 3 && <p className="tv-mic">Camera e plină: joacă primii 3. Ceilalți privesc.</p>}
      </div>

      <ul className="tv-reguli">
        <li><i>1</i><span>La rândul tău, alegi o insulă legată printr-un pod de insulele tale.</span></li>
        <li><i>2</i><span>Insulă liberă: răspunzi corect la întrebare și e a ta.</span></li>
        <li><i>3</i><span>Insula altcuiva: duel. La egalitate, câștigă cine ghicește mai aproape răspunsul numeric.</span></li>
      </ul>

      {esteGazda ? (
        <button className="tv-btn" onClick={onStart} disabled={online.length < 2 || !gata}>
          {!gata ? 'Se pregătesc întrebările...' : online.length < 2 ? 'Se așteaptă jucătorii...' : 'Pornește bătălia'}
        </button>
      ) : (
        <p className="tv-asteapta">Așteaptă ca gazda să pornească bătălia...</p>
      )}
    </div>
  )
}

export function PanouJoc({ stare, euId, runde }) {
  const curent = stare.jucatori[stare.tura]
  const numar = (id) => Object.values(stare.proprietari).filter((p) => p === id).length
  const eRandulMeu = curent?.id === euId
  const terminat = stare.faza === 'final'

  let indiciu = ''
  if (terminat) indiciu = 'Bătălia s-a terminat.'
  else if (stare.faza === 'alegere') indiciu = eRandulMeu ? 'Alege o insulă cu marginea aurie.' : `${curent.nume} își alege ținta...`
  else indiciu = 'Duel în desfășurare...'

  return (
    <>
      <div className="tv-card">
        <div className="tv-tura">
          <span className="tv-runda">{Math.min(stare.runda, runde)}</span>
          <div>
            <div className="tv-mic">Runda {Math.min(stare.runda, runde)} din {runde}</div>
            <h3>{terminat ? 'Final' : eRandulMeu ? 'Rândul tău' : `Rândul lui ${curent.nume}`}</h3>
            <span className="tv-indiciu">{indiciu}</span>
          </div>
        </div>
        <div className="tv-jucatori">
          {stare.jucatori.map((j) => (
            <div key={j.id} className={`tv-jucator ${j.id === curent?.id && !terminat ? 'tv-randul' : ''}`}>
              <Avatar nume={j.nume} culoare={j.culoare} />
              <span className="tv-nume">
                {j.nume}
                {j.id === euId && ' (tu)'}
                <small>{numar(j.id) * 300} puncte</small>
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
