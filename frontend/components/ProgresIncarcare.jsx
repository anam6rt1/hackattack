'use client'
export default function ProgresIncarcare({ progres, onAnuleaza }) {
  if (!progres) return null
  return <div className="tv-form" aria-label="Încărcare fișier">
    <p role="status">{progres.asteapta ? 'Transfer terminat. Serverul verifică fișierul…' : progres.procent == null ? 'Se transferă fișierul…' : `Se transferă fișierul: ${progres.procent}%`}</p>
    <progress aria-label="Progresul încărcării" max="100" value={progres.procent ?? undefined} style={{ width: '100%' }} />
    <button type="button" className="tv-pastila" onClick={onAnuleaza}>Anulează încărcarea</button>
    <small>Anularea oprește transferul. Dacă serverul a primit deja fișierul, verifică lista înainte de a reîncerca.</small>
  </div>
}
