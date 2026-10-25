'use client'
import PrevizualizareFisier from './PrevizualizareFisier'

export default function OptimizareFotografie({ fisier, inLucru }) {
  if (inLucru) return <p role="status">Se pregătește fotografia…</p>
  return <PrevizualizareFisier fisier={fisier} eticheta="Previzualizează fotografia" />
}
