// Întrebările jocului. Le poți înlocui cu cele de la P2, păstrând formatul.
//
// GRILA: t = textul, v = cele 4 variante, c = poziția variantei corecte (0, 1, 2 sau 3)
// ESTIMARI: întrebări cu răspuns numeric, folosite la egalitate în duel (câștigă cine e mai aproape)

export const GRILA = [
  { t: 'Cine este autorul nuvelei „Moara cu noroc”?', v: ['Liviu Rebreanu', 'Ioan Slavici', 'Ion Creangă', 'Mihail Sadoveanu'], c: 1 },
  { t: 'Ce meserie avea Ghiță înainte să ia hanul în arendă?', v: ['Cizmar', 'Fierar', 'Morar', 'Negustor'], c: 0 },
  { t: 'Cum se numesc cele două părți ale romanului „Ion”?', v: ['Iarna și Vara', 'Glasul pământului și Glasul iubirii', 'Satul și Orașul', 'Începutul și Sfârșitul'], c: 1 },
  { t: 'Cine este personajul principal din „Baltagul”?', v: ['Ana Baciu', 'Otilia Mărculescu', 'Vitoria Lipan', 'Cătălina'], c: 2 },
  { t: 'Cine este autorul romanului „Enigma Otiliei”?', v: ['Camil Petrescu', 'George Călinescu', 'Marin Preda', 'Liviu Rebreanu'], c: 1 },
  { t: 'Ce personaj îl înșală pe Harap-Alb și îi ia locul?', v: ['Spânul', 'Ursul', 'Împăratul Roș', 'Gerilă'], c: 0 },
  { t: 'Cine a scris poezia „Plumb”?', v: ['Tudor Arghezi', 'Lucian Blaga', 'George Bacovia', 'Ion Barbu'], c: 2 },
  { t: 'Cine este autorul romanului „Moromeții”?', v: ['Marin Preda', 'Marin Sorescu', 'Mihail Sadoveanu', 'Ioan Slavici'], c: 0 },
  { t: 'Cum se numește fata de împărat din „Luceafărul”?', v: ['Ileana', 'Cătălina', 'Ana', 'Florica'], c: 1 },
  { t: 'Cine a scris poezia „Testament”?', v: ['George Bacovia', 'Mihai Eminescu', 'Tudor Arghezi', 'Nichita Stănescu'], c: 2 },
  { t: 'Cine a scris piesa „Iona”?', v: ['Marin Sorescu', 'Camil Petrescu', 'Lucian Blaga', 'Ion Barbu'], c: 0 },
  { t: 'Cine este naratorul din „Ultima noapte de dragoste, întâia noapte de război”?', v: ['Felix Sima', 'Ștefan Gheorghidiu', 'Ilie Moromete', 'Ghiță'], c: 1 },
  { t: 'Cine a scris „Riga Crypto și lapona Enigel”?', v: ['Ion Barbu', 'Lucian Blaga', 'Tudor Arghezi', 'George Bacovia'], c: 0 },
  { t: 'Ce este Lică Sămădăul?', v: ['Jandarm', 'Hangiu', 'Șef al porcarilor', 'Preot'], c: 2 },
]

export const ESTIMARI = [
  { t: 'În ce an a apărut romanul „Ion”?', a: 1920 },
  { t: 'În ce an a apărut poemul „Luceafărul”?', a: 1883 },
  { t: 'În ce an a apărut romanul „Baltagul”?', a: 1930 },
  { t: 'În ce an a apărut romanul „Enigma Otiliei”?', a: 1938 },
  { t: 'În ce an a apărut volumul „Novele din popor”, cu „Moara cu noroc”?', a: 1881 },
]

export function amesteca(lista) {
  const copie = lista.slice()
  for (let i = copie.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[copie[i], copie[j]] = [copie[j], copie[i]]
  }
  return copie
}
