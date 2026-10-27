// Subiectele mesajelor din pagina de contact (aceleași coduri ca MesajContact.Subiecte din API).
export const SUBIECTE = [
  { id: 'problema', nume: 'Ceva nu merge' },
  { id: 'cont', nume: 'Contul meu' },
  { id: 'organizatii', nume: 'Școli și organizații' },
  { id: 'sugestie', nume: 'O idee' },
  { id: 'altceva', nume: 'Altceva' },
]
export const numeSubiect = (id) => SUBIECTE.find((s) => s.id === id)?.nume ?? id

/** Adresa paginii de contact, cu pagina de pe care se pleacă (fără parametri: pot conține tokenuri). */
export const linkContact = (de) => (de && de !== '/' && !de.startsWith('/contact') ? `/contact?de=${encodeURIComponent(de)}` : '/contact')
