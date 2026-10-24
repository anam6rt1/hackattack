'use client'

export default function OptimizareFotografie({ original, onChange, disabled, mesaj }) {
  return <div>
    <label><input type="checkbox" checked={original} disabled={disabled} onChange={e => onChange(e.target.checked)} /> Păstrează fotografia originală</label>
    <p className="tv-indiciu">Altfel, fotografia se optimizează pe dispozitivul tău: WebP, maximum 2048 px. Se trimite varianta optimizată numai dacă este mai mică. Dovada calificării se păstrează integral.</p>
    {mesaj && <p role="status" aria-live="polite">{mesaj}</p>}
  </div>
}
