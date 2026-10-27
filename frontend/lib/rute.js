// Adresele paginilor care primesc un identificator. Sunt cu parametri (?id=7), nu cu segmente (/parcurs/7): aplicația desktop și
// cea mobilă folosesc un export static al site-ului, care nu poate avea pagini pentru identificatori necunoscuți la build.
// Adresele vechi (/parcurs/7, /test/7, /materiale/3, /joc/ABC234) rămân valabile pe site: page.web.js le redirecționează.
export const rute = {
  parcurs: (id) => `/parcurs?id=${id}`,
  test: (id) => `/test?id=${id}`,
  materie: (id) => `/materiale/materie?id=${id}`,
  camera: (cod) => `/joc/camera?cod=${cod}`,
}
