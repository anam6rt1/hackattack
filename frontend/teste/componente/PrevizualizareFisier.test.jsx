import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import PrevizualizareFisier from '@/components/PrevizualizareFisier'

vi.mock('pdfjs-dist', () => ({ PDFWorker: { create: vi.fn() }, getDocument: vi.fn() }))
afterEach(() => vi.unstubAllGlobals())
it('PDF folosește o adresă locală, revocată la închiderea previzualizării', async () => {
  const create = vi.fn(() => 'blob:local-pdf'), revoke = vi.fn()
  vi.stubGlobal('URL', class extends URL { static createObjectURL = create; static revokeObjectURL = revoke })
  const u = userEvent.setup(), f = new File(['%PDF-1.7'], 'curs.pdf', { type: 'application/pdf' })
  render(<PrevizualizareFisier fisier={f} />)
  expect(create).not.toHaveBeenCalled()
  await u.click(screen.getByRole('button', { name: /Previzualizează/ }))
  expect(screen.getByRole('link', { name: /Deschide previzualizarea/ })).toHaveAttribute('href', 'blob:local-pdf')
  await u.click(screen.getByRole('button', { name: /Ascunde/ }))
  expect(revoke).toHaveBeenCalledWith('blob:local-pdf')
})
it('explică formatele vechi fără a trimite documentul unui serviciu extern', async () => {
  render(<PrevizualizareFisier fisier={new File(['xls'], 'vechi.xls')} />)
  await userEvent.click(screen.getByRole('button', { name: /Previzualizează/ }))
  expect(screen.getByText(/salvează o copie/)).toHaveTextContent('.xlsx')
})
