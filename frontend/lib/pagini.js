// Câte elemente arată o pagină din fiecare listă a site-ului. Fiecare listă, fiecare tab al ei și fiecare rol care o vede are
// constanta lui, ca aspectul unei pagini să poată fi ajustat fără să schimbe altele. Serverul primește numărul ca `marime`
// și are propriile limite (database/src/MarkAi.Api/Infrastructura/Pagini.cs: implicit și maxim 100 pe pagină).

// Profesori — elevul caută un profesor.
export const PROFESORI_PE_PAGINA_ELEV = 6

// Panoul organizației (administratorul ei), pe taburi.
export const CERERI_PE_PAGINA_ORGANIZATIE = 8
export const PROFESORI_PE_PAGINA_ORGANIZATIE = 8
export const ELEVI_PE_PAGINA_ORGANIZATIE = 12

// Panoul organizației (un profesor al ei): elevii care așteaptă confirmarea.
export const ELEVI_DE_CONFIRMAT_PE_PAGINA_PROFESOR = 8

// Admin → Organizații, pe taburi.
export const ORGANIZATII_IN_ASTEPTARE_PE_PAGINA_ADMIN = 8
export const ORGANIZATII_APROBATE_PE_PAGINA_ADMIN = 8
export const ORGANIZATII_RESPINSE_PE_PAGINA_ADMIN = 8

// Admin → Profesori, pe taburi.
export const PROFESORI_IN_ASTEPTARE_PE_PAGINA_ADMIN = 6
export const PROFESORI_APROBATI_PE_PAGINA_ADMIN = 6
export const PROFESORI_RESPINSI_PE_PAGINA_ADMIN = 6

// Conținut — lista de lecții din editor.
export const LECTII_PE_PAGINA_EDITOR_PROFESOR = 8
export const LECTII_PE_PAGINA_EDITOR_ADMIN = 8

// Acasă (elev) — progresul pe materii (doar materiile în care a deschis o lecție).
export const PROGRES_MATERII_PE_PAGINA_ACASA_ELEV = 4

// Materiale — lista materiilor.
export const MATERII_PE_PAGINA_MATERIALE_ELEV = 6
export const MATERII_PE_PAGINA_MATERIALE_ADMIN = 6

// Materiale — căutarea: lecțiile găsite (carduri) și capitolele găsite (rânduri), fiecare cu paginile lui.
export const LECTII_PE_PAGINA_CAUTARE = 9
export const CAPITOLE_PE_PAGINA_CAUTARE = 10

// Materiale → o materie: lecțiile ei.
export const LECTII_PE_PAGINA_MATERIE_ELEV = 9
export const LECTII_PE_PAGINA_MATERIE_ADMIN = 9

/** `?…&pagina=p&marime=n` pentru o listă (pagina 1 nu se mai scrie). */
export function cuPagina(url, pagina, marime) {
  const sep = url.includes('?') ? '&' : '?'
  return `${url}${sep}${pagina > 1 ? `pagina=${pagina}&` : ''}marime=${marime}`
}
