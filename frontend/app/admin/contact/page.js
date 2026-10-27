'use client'
import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
export default function Feedback() {
  const u = usePagina('admin'), [date, setDate] = useState(null), [pagina, setPagina] = useState(1), [rezolvate, setRezolvate] = useState(false), [mesaj, setMesaj] = useState(''), [versiune, setVersiune] = useState(0)
  useEffect(() => { if (u) api(`/admin/feedback?pagina=${pagina}&rezolvate=${rezolvate}`).then(setDate).catch(e => setMesaj(e.message)) }, [u, pagina, rezolvate, versiune])
  async function rezolva(id) { try { await api(`/admin/feedback/${id}/rezolvat`, { metoda: 'POST' }); setVersiune(v => v + 1) } catch (e) { setMesaj(e.message) } }
  return <main className="tv-pagina"><h1>Feedback din pilot</h1><label><input type="checkbox" checked={rezolvate} onChange={e => { setRezolvate(e.target.checked); setPagina(1) }} /> Mesaje rezolvate</label>{date?.elemente.map(f => <article className="tv-cutie" key={f.id}><p style={{ whiteSpace: 'pre-wrap' }}>{f.text}</p><p>{f.pagina} · {new Date(f.creatLa).toLocaleString('ro-RO')}</p>{!f.rezolvatLa && <button onClick={() => rezolva(f.id)}>Marchează rezolvat</button>}</article>)}{pagina > 1 && <button onClick={() => setPagina(pagina - 1)}>Înapoi</button>}{date && pagina * 20 < date.total && <button onClick={() => setPagina(pagina + 1)}>Înainte</button>}{mesaj && <p role="status">{mesaj}</p>}</main>
}
