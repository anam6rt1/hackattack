// lib/tema.js — clasele Tailwind care folosesc culorile temei (variabilele CSS definite de P1).
// Fiecare variabilă are după virgulă culoarea actuală, folosită dacă tema nu o definește.
// Dacă P1 folosește alte nume de variabile, se schimbă DOAR aici.
//
//   --bg          fundalul paginii
//   --card-bg     fundalul cardurilor / ferestrelor
//   --text        culoarea textului (și baza pentru nuanțele semitransparente)
//   --accent      culoarea butoanelor principale
//   --accent-text textul de pe butoanele principale

export const T = {
  pagina: "bg-[var(--bg,#0b3550)] text-[var(--text,#ffffff)]",
  card: "bg-[var(--card-bg,#10405f)] text-[var(--text,#ffffff)]",
  // suprafețe și contururi semitransparente, calculate din culoarea textului: merg și pe teme deschise, și pe teme închise
  suprafata: "bg-[color-mix(in_srgb,var(--text,#ffffff)_7%,transparent)]",
  suprafataHover: "hover:bg-[color-mix(in_srgb,var(--text,#ffffff)_14%,transparent)]",
  suprafataSlaba: "bg-[color-mix(in_srgb,var(--text,#ffffff)_4%,transparent)]",
  contur: "ring-[color-mix(in_srgb,var(--text,#ffffff)_16%,transparent)]",
  conturSlab: "ring-[color-mix(in_srgb,var(--text,#ffffff)_10%,transparent)]",
  textSecundar: "text-[color-mix(in_srgb,var(--text,#ffffff)_72%,transparent)]",
  textSlab: "text-[color-mix(in_srgb,var(--text,#ffffff)_55%,transparent)]",
  accent: "bg-[var(--accent,#fbbf24)] text-[var(--accent-text,#451a03)] hover:brightness-110",
  accentText: "text-[var(--accent,#fbbf24)]",
};
