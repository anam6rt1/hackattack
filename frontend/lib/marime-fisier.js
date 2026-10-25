export function marimeFisier(octeti) {
  const n = new Intl.NumberFormat('ro-RO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  const exact = new Intl.NumberFormat('ro-RO').format(octeti)
  return `${n.format(octeti / 1024 / 1024)} MiB (${exact} octeți)`
}
