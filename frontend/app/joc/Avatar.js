// Avatar TEMPORAR: un cerc în culoarea jucătorului, cu inițiala.
// Când P1 termină editorul de avatar, înlocuiești conținutul acestui fișier cu avatarul lui.

export default function Avatar({ nume = '?', culoare = '#1b8ad3', marime = 38 }) {
  return (
    <span
      className="tv-avatar"
      style={{ width: marime, height: marime, fontSize: marime * 0.45, background: culoare, boxShadow: `0 0 0 2px ${culoare}` }}
    >
      {nume.charAt(0).toUpperCase()}
    </span>
  )
}
