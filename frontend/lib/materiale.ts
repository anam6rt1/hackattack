// Toate datele paginii de materiale stau aici.
// Ca să adaugi un PDF:
//   1. pune fișierul în  public/materiale/<slug-opera>/  (ex. public/materiale/harap-alb/rezumat.pdf)
//   2. adaugă-l în lista `materiale` a părții potrivite: { titlu: "...", fisier: "/materiale/harap-alb/rezumat.pdf" }
// O parte fără materiale apare automat ca „În curând”.

export type Material = {
  titlu: string;
  fisier: string; // cale din /public sau URL (ex. Supabase Storage)
};

export type Parte = {
  id: string;
  titlu: string;
  descriere: string;
  materiale: Material[];
};

export type Opera = {
  slug: string;
  titlu: string;
  autor: string;
  an: string;
  gen: string;
  descriere: string;
  emoji: string;
  culoare: string; // culoarea de accent a cardului
  parti: Parte[];
};

// Părțile standard, la fel pentru fiecare operă
function partiStandard(): Parte[] {
  return [
    { id: "prezentare", titlu: "Prezentare generală", descriere: "Context, curent literar, încadrare în gen și specie.", materiale: [] },
    { id: "rezumat", titlu: "Rezumat", descriere: "Firul narativ / ideile poetice, pe scurt.", materiale: [] },
    { id: "personaje", titlu: "Personaje", descriere: "Caracterizări și relații între personaje.", materiale: [] },
    { id: "teme", titlu: "Teme și motive", descriere: "Tema, viziunea despre lume, motive literare.", materiale: [] },
    { id: "bac", titlu: "Subiecte de bac", descriere: "Eseuri model și cerințe din anii trecuți.", materiale: [] },
  ];
}

export const opere: Opera[] = [
  {
    slug: "harap-alb",
    titlu: "Povestea lui Harap-Alb",
    autor: "Ion Creangă",
    an: "1877",
    gen: "Epic · Basm cult",
    descriere: "Drumul de maturizare al fiului de crai, de la naivitate la înțelepciune.",
    emoji: "🐎",
    culoare: "#e0a526",
    parti: partiStandard(),
  },
  {
    slug: "moara-cu-noroc",
    titlu: "Moara cu noroc",
    autor: "Ioan Slavici",
    an: "1881",
    gen: "Epic · Nuvelă psihologică",
    descriere: "Dezumanizarea lui Ghiță sub puterea banului și a lui Lică Sămădăul.",
    emoji: "⚙️",
    culoare: "#b5543a",
    parti: partiStandard(),
  },
  {
    slug: "luceafarul",
    titlu: "Luceafărul",
    autor: "Mihai Eminescu",
    an: "1883",
    gen: "Liric · Poem romantic",
    descriere: "Condiția geniului, între iubire și nemurire.",
    emoji: "⭐",
    culoare: "#4a6fd1",
    parti: partiStandard(),
  },
];

export function getOpera(slug: string) {
  return opere.find((o) => o.slug === slug);
}

export function numarMateriale(opera: Opera) {
  return opera.parti.reduce((s, p) => s + p.materiale.length, 0);
}
