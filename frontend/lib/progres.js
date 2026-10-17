// lib/progres.js — progresul elevului (localStorage pentru demo; P1 îl poate muta în Supabase)
//  • nivele terminate: ["harap-1", ...]  → un nivel e terminat când răspunzi corect la întrebare (cerculețul „play”)
//  • activități parcurse: { "harap-1": { video: true, materiale: true } }

const CHEIE_NIVELE = "progres-nivele";
const CHEIE_ACTIVITATI = "progres-activitati";

// copie în memorie: dacă browserul blochează localStorage (mod privat, iframe), progresul merge măcar până la reîncărcare
const memorie = {};

function citeste(cheie, implicit) {
  try {
    const v = localStorage.getItem(cheie);
    if (v !== null) return JSON.parse(v);
  } catch {}
  return memorie[cheie] ?? implicit;
}

function scrie(cheie, valoare) {
  memorie[cheie] = valoare;
  try {
    localStorage.setItem(cheie, JSON.stringify(valoare));
  } catch {}
}

export function citesteProgres() {
  return citeste(CHEIE_NIVELE, []);
}

export function marcheazaNivelTerminat(nivelId) {
  const p = citesteProgres();
  if (!p.includes(nivelId)) scrie(CHEIE_NIVELE, [...p, nivelId]);
}

export function citesteActivitati() {
  return citeste(CHEIE_ACTIVITATI, {});
}

// tip: "video" | "materiale"
export function marcheazaActivitate(nivelId, tip) {
  const a = citesteActivitati();
  a[nivelId] = { ...(a[nivelId] ?? {}), [tip]: true };
  scrie(CHEIE_ACTIVITATI, a);
}

export function reseteazaProgres() {
  delete memorie[CHEIE_NIVELE];
  delete memorie[CHEIE_ACTIVITATI];
  try {
    localStorage.removeItem(CHEIE_NIVELE);
    localStorage.removeItem(CHEIE_ACTIVITATI);
  } catch {}
}
