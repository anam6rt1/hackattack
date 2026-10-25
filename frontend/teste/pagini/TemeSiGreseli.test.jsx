import { render, screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import TemeleMele from '@/components/TemeleMele'
import TemeProfesor from '@/components/TemeProfesor'
import GreselileMele from '@/components/GreselileMele'
import { apiFals, raspuns } from '../ajutor'

describe('teme și recapitulare', () => {
  it('arată tema predată cu întârziere și legătura către lecție', async () => {
    apiFals({ 'GET /teme': { total: 1, elemente: [{ id: 1, titlu: 'Tema de luni', organizatie: 'Liceul', termenLa: '2026-01-01T12:00:00Z', lectii: [{ id: 2, titlu: 'Romanul', promovatLa: '2026-01-02T12:00:00Z' }] }] } })
    render(<TemeleMele />)
    expect(await screen.findByText('Predată cu întârziere')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Romanul' })).toHaveAttribute('href', '/parcurs/2')
  })
  it('arată erorile de încărcare a temelor', async () => {
    apiFals({ 'GET /teme': raspuns(503, { mesaj: 'Temele nu sunt disponibile.' }) })
    render(<TemeleMele />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Temele nu sunt disponibile.')
  })
  it('profesorul publică o temă pentru organizație', async () => {
    const cereri = apiFals({
      'GET /teme/organizatii': [{ id: 1, nume: 'Liceul', admin: true }],
      'GET /organizatii/1/teme': { total: 0, elemente: [] },
      'GET /organizatii/1/teme/lectii': { total: 1, elemente: [{ id: 4, titlu: 'Romanul' }] },
      'GET /organizatii/1/teme/grupe': { total: 0, elemente: [] },
      'GET /organizatii/1/teme/elevi': { total: 0, elemente: [] },
      'POST /organizatii/1/teme': { id: 10 },
    })
    const u = userEvent.setup(); render(<TemeProfesor />)
    await u.type(await screen.findByLabelText('Titlul temei'), 'Recapitulare')
    await u.type(screen.getByLabelText('Termen de predare'), '2026-12-10T18:00')
    await u.click(await screen.findByRole('checkbox', { name: 'Romanul' }))
    await u.click(screen.getByRole('button', { name: 'Publică tema' }))
    expect(await screen.findByText('Tema a fost publicată.')).toBeInTheDocument()
    expect(cereri.find(c => c.metoda === 'POST')).toMatchObject({ cale: '/organizatii/1/teme', corp: { titlu: 'Recapitulare', parcursuriIds: [4], toataOrganizatia: true, grupeIds: [] } })
  })
  it('recapitulează fără a afișa soluția înainte de răspuns', async () => {
    apiFals({
      'GET /greseli': { total: 1, scadente: 1, elemente: [{ intrebareId: 7, text: 'Capitala?', lectie: 'Geografie', scadentaLa: '2020-01-01', corecteConsecutive: 0 }] },
      'POST /greseli/7/incepe': { token: 'token', intrebare: { id: 7, text: 'Capitala?', variante: [{ id: 1, text: 'București' }, { id: 2, text: 'Brașov' }] } },
      'POST /greseli/7/raspuns': { corect: true, variantaCorectaId: 1, explicatie: 'Explicația.', rezolvata: true, corecteConsecutive: 2 },
    })
    const u = userEvent.setup(); render(<GreselileMele />)
    await u.click(await screen.findByRole('button', { name: 'Recapitulează' }))
    expect(screen.queryByText('Explicația.')).not.toBeInTheDocument()
    await u.click(await screen.findByRole('button', { name: 'București' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Întrebarea a ieșit din listă.')
    await waitFor(() => expect(within(screen.getByRole('button', { name: 'București ✓' })).getByText('București ✓')).toBeInTheDocument())
  })
})
