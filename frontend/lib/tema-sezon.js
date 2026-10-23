// Temele site-ului: anotimpul și momentul zilei (zi/noapte).
// „Automat” alege tema după ceasul elevului; altfel rămâne tema aleasă.
// Separat de temă există stilul desenelor (Acvatic, Realist, Pixel), pus pe <html data-theme="...">:
// stilul schimbă forma (fonturi, carduri, insule), iar tema anotimpului îi dă culorile și decorul.
// Același cod (în variantă text) rulează și în <head>, înainte de prima afișare, ca să nu „clipească” tema.

export const ANOTIMPURI = ['primavara', 'vara', 'toamna', 'iarna']
export const MOMENTE = ['zi', 'noapte']
export const CHEIE = 'tema-preferinta' // 'auto' sau codul temei, ex. 'iarna-noapte'

export const NUME_ANOTIMP = { primavara: 'Primăvară', vara: 'Vară', toamna: 'Toamnă', iarna: 'Iarnă' }
export const TEME = ANOTIMPURI.flatMap((a) => MOMENTE.map((m) => ({ cod: `${a}-${m}`, nume: `${NUME_ANOTIMP[a]} · ${m}` })))

export const STILURI = [
  { id: 'acvatic', nume: 'Acvatic', descriere: 'Lume acvatică, cu bule și peștișori' },
  { id: 'realist', nume: 'Realist', descriere: 'Apă, lumină și nori ca în realitate' },
  { id: 'pixel', nume: 'Pixel', descriere: 'Joc retro pe 16 biți' },
]
export const CHEIE_STIL = 'bac_tema'
// În baza de date stilul e „interfața”; Acvatic este interfața „insule” de la început.
export const stilDinInterfata = (cod) => (cod === 'insule' ? 'acvatic' : STILURI.some((s) => s.id === cod) ? cod : null)
export const interfataDinStil = (stil) => (stil === 'acvatic' ? 'insule' : stil)

export function citesteStil() {
  try {
    const s = localStorage.getItem(CHEIE_STIL)
    return STILURI.some((x) => x.id === s) ? s : 'acvatic'
  } catch {
    return 'acvatic'
  }
}

export function aplicaStil(stil) {
  document.documentElement.dataset.theme = STILURI.some((x) => x.id === stil) ? stil : 'acvatic'
}

export function temaAutomata(data = new Date()) {
  const luna = data.getMonth() // 0 = ianuarie
  const anotimp = luna >= 2 && luna <= 4 ? 'primavara' : luna >= 5 && luna <= 7 ? 'vara' : luna >= 8 && luna <= 10 ? 'toamna' : 'iarna'
  const ora = data.getHours()
  return `${anotimp}-${ora >= 7 && ora < 20 ? 'zi' : 'noapte'}`
}

export function citestePreferinta() {
  try {
    return localStorage.getItem(CHEIE) || 'auto'
  } catch {
    return 'auto'
  }
}

export function aplicaTema(preferinta) {
  const cod = preferinta && preferinta !== 'auto' && TEME.some((t) => t.cod === preferinta) ? preferinta : temaAutomata()
  document.documentElement.dataset.tema = cod
  return cod
}

// Rulează în <head> (vezi app/layout.tsx); trebuie să rămână echivalent cu funcțiile de mai sus.
export const SCRIPT_TEMA = `(function(){try{var p=localStorage.getItem('${CHEIE}')||'auto';var d=new Date(),l=d.getMonth(),o=d.getHours();var a=l>=2&&l<=4?'primavara':l>=5&&l<=7?'vara':l>=8&&l<=10?'toamna':'iarna';var t=/^(primavara|vara|toamna|iarna)-(zi|noapte)$/.test(p)?p:a+'-'+(o>=7&&o<20?'zi':'noapte');document.documentElement.setAttribute('data-tema',t);var s=localStorage.getItem('${CHEIE_STIL}');document.documentElement.setAttribute('data-theme',/^(acvatic|realist|pixel)$/.test(s)?s:'acvatic')}catch(e){}})()`
