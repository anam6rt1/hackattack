import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it } from 'vitest'
import SetariSumarParinte from '@/components/SetariSumarParinte'
import ConfirmareSumarParinte from '@/components/ConfirmareSumarParinte'
import StatisticiIntrebari from '@/components/StatisticiIntrebari'
import { apiFals } from '../ajutor'

it('sumarul este dezactivat și cere confirmare, fără activare automată', async () => {
  let trimis = false
  const cereri = apiFals({ 'GET /utilizatori/me/sumar-parinte': () => trimis ? { email: 'p@example.invalid', confirmatLa: null } : { email: '' }, 'PUT /utilizatori/me/sumar-parinte': () => { trimis = true; return {} } })
  const u = userEvent.setup(); render(<SetariSumarParinte />)
  await screen.findByText('Dezactivat')
  await u.type(screen.getByLabelText('Email părinte'), 'p@example.invalid'); await u.click(screen.getByRole('button', { name: 'Cere confirmarea' }))
  expect(await screen.findByText('În așteptarea confirmării părintelui')).toBeInTheDocument()
  expect(cereri.find(c => c.metoda === 'PUT').corp).toEqual({ email: 'p@example.invalid' })
})
it('deschiderea linkului nu confirmă singură părintele', async () => {
  window.history.replaceState(null, '', '/sumar-parinte#' + 'a'.repeat(64))
  const cereri = apiFals({ 'POST /sumar-parinte/confirmare': { mesaj: 'Confirmat' } })
  const u = userEvent.setup(); render(<ConfirmareSumarParinte />)
  const b = await screen.findByRole('button', { name: 'Confirm și activez sumarele' }); expect(cereri).toHaveLength(0)
  await u.click(b); expect(await screen.findByRole('status')).toHaveTextContent('Confirmat')
})
it('statisticile disting întrebările fără răspuns de cele greșite', async () => {
  apiFals({ 'GET /parcursuri/1/statistici-intrebari?pagina=1': { total: 2, elemente: [{ id: 2, text: 'Dificilă', raspunsuri: 4, corecte: 1 }, { id: 3, text: 'Nouă', raspunsuri: 0, corecte: 0 }] } })
  render(<StatisticiIntrebari parcursId={1} />)
  expect(await screen.findByText(/25% corecte/)).toBeInTheDocument(); expect(screen.getByText(/Fără răspunsuri/)).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Dificilă' })).toHaveAttribute('href', '#intrebare-2')
})
