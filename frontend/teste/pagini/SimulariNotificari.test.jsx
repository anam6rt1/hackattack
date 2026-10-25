import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import Simulari from '@/app/simulari/page'
import SetariNotificari from '@/components/SetariNotificari'
import { SesiuneProvider } from '@/lib/sesiune'
import { apiFals } from '../ajutor'

describe('simulări și notificări', () => {
  it('toate notificările sunt oprite inițial și se salvează numai alegerile utilizatorului', async () => {
    const cereri = apiFals({
      'GET /utilizatori/me/notificari': { sedinte: false, decizii: false, temeNoi: false, termeneTeme: false, pushDisponibil: false },
      'PUT /utilizatori/me/notificari': {},
    })
    const u = userEvent.setup(); render(<SetariNotificari />)
    const sedinte = await screen.findByRole('checkbox', { name: /Ședințe/ })
    for (const c of screen.getAllByRole('checkbox')) expect(c).not.toBeChecked()
    await u.click(sedinte); await u.click(screen.getByRole('button', { name: 'Salvează notificările' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Preferințele au fost salvate.')
    expect(cereri.find(c => c.metoda === 'PUT')?.corp).toEqual({ sedinte: true, decizii: false, temeNoi: false, termeneTeme: false })
  })
  it('nu arată corecturile simulării înainte de finalizare', async () => {
    const inceput = new Date().toISOString(), expira = new Date(Date.now() + 600000).toISOString()
    const s = { id: 1, total: 5, serverLa: inceput, inceputLa: inceput, expiraLa: expira, finalizatLa: null,
      intrebari: [{ id: 7, text: 'Întrebarea?', lectie: 'Lecția', tip: 'grila', variante: [{ id: 2, text: 'Varianta A' }, { id: 3, text: 'Varianta B' }], raspunsa: false }] }
    apiFals({
      'GET /utilizatori/me': { id: 1, rol: 'elev', nume: 'Elev' },
      'GET /simulari/optiuni': [{ materieId: 1, materie: 'Română', clasa: 'Bac', disponibile: 10 }],
      'GET /simulari': { total: 0, elemente: [] },
      'POST /simulari': s,
      'POST /simulari/1/raspuns': { ...s, intrebari: [{ ...s.intrebari[0], raspunsa: true, variantaAleasaId: 2 }] },
      'POST /simulari/1/finalizare': { ...s, finalizatLa: inceput, corecte: 1, notaOrientativa: 2.8, deRecapitulat: [], intrebari: [{ ...s.intrebari[0], raspunsa: true, corect: true, variantaCorectaId: 2, explicatie: 'Explicația ascunsă.' }] },
    })
    const u = userEvent.setup(); render(<SesiuneProvider><Simulari /></SesiuneProvider>)
    await screen.findByRole('option', { name: 'Română (10 întrebări)' })
    await u.selectOptions(screen.getByLabelText('Materie'), '1')
    await u.click(screen.getByRole('button', { name: 'Începe simularea' }))
    await u.click(await screen.findByRole('button', { name: 'Varianta A' }))
    expect(screen.queryByText('Explicația ascunsă.')).not.toBeInTheDocument()
    expect(screen.queryByText(/Nota orientativă:/)).not.toBeInTheDocument()
    await u.click(screen.getByRole('button', { name: 'Încheie simularea' }))
    expect(await screen.findByText(/Nota orientativă:/)).toBeInTheDocument()
    expect(screen.getByText(/Explicația ascunsă/)).toBeInTheDocument()
  })
})
