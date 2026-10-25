import { offlineRead, offlineSave, offlineRemove, offlineClear } from './offline-store.js'
const container = document.querySelector('#lectii'), status = document.querySelector('#status')
function element(tag, text, parent) { const e = document.createElement(tag); e.textContent = text; parent.append(e); return e }
async function request(path, body) {
  const r = await fetch('/api' + path, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', 'X-MarkAi': '1' }, body: body ? JSON.stringify(body) : undefined, credentials: 'same-origin', cache: 'no-store' })
  const data = await r.json().catch(() => ({}))
  if (!r.ok) throw new Error(data.mesaj || data.detail || 'Intră în cont și verifică accesul la lecție înainte de sincronizare.')
  return data
}
async function render() {
  container.replaceChildren()
  const rows = await offlineRead()
  if (!rows.length) element('p', 'Nu există lecții salvate pe acest dispozitiv.', container)
  for (const row of rows) {
    const article = element('article', '', container)
    element('h2', row.titlu, article); element('p', 'Salvată: ' + new Date(row.salvataLa).toLocaleString('ro-RO'), article)
    for (const c of row.capitole) { element('h3', c.titlu, article); element('p', c.text, article) }
    if (row.quiz) {
      element('h3', 'Test pregătit', article)
      for (const q of row.quiz.intrebari) {
        const label = element('label', q.text, article), select = element('select', '', label)
        element('option', 'Alege răspunsul', select).value = ''
        for (const v of q.variante) element('option', v.text, select).value = String(v.id)
        select.value = String(row.raspunsuri[q.id] ?? '')
        select.onchange = async () => { row.raspunsuri[q.id] = Number(select.value) || null; try { await offlineSave(row); status.textContent = 'Răspuns păstrat pe dispozitiv. Nu este încă verificat.' } catch(e) { status.textContent = e.message } }
      }
    }
    const remove = element('button', 'Elimină copia locală', article)
    remove.onclick = async () => { await offlineRemove(row.id); await render() }
  }
}
let syncing = false
async function sync() {
  if (syncing) return
  syncing = true
  try {
    const rows = await offlineRead()
    if (!rows.some(row => row.quiz && Object.values(row.raspunsuri).some(Boolean))) return
    const me = await request('/utilizatori/me')
    for (const row of rows) {
      if (!row.quiz) continue
      if (row.uid !== me.id) throw new Error('Intră în contul care a salvat lecția.')
      const state = await request(`/parcursuri/${row.id}/quiz/${row.quiz.id}/stare`)
      if (state.finalizataLa) { row.quiz = null; row.raspunsuri = {}; await offlineSave(row); continue }
      for (const q of row.quiz.intrebari) {
        const variantaId = row.raspunsuri[q.id]
        if (!variantaId) continue
        const accepted = state.raspunsuri.find(a => a.intrebareId === q.id)?.variantaId
        if (accepted) {
          delete row.raspunsuri[q.id]; row.quiz.intrebari = row.quiz.intrebari.filter(x => x.id !== q.id); await offlineSave(row); continue
        }
        // Server is authoritative; no XP or completion is computed locally.
        const result = await request(`/parcursuri/${row.id}/quiz/${row.quiz.id}/raspuns`, { intrebareId: q.id, variantaId })
        delete row.raspunsuri[q.id]; row.quiz.intrebari = row.quiz.intrebari.filter(x => x.id !== q.id)
        if (result.final) { row.quiz = null; status.textContent = result.final.promovat ? 'Test verificat: promovat.' : 'Test verificat. Poți relua testul în aplicație.' }
        await offlineSave(row)
        if (!row.quiz) break
      }
    }
    await render()
    status.textContent ||= 'Răspunsurile completate au fost sincronizate.'
  } catch (e) { status.textContent = e.message + ' Răspunsurile nesincronizate rămân pe dispozitiv. Poți continua testul și în aplicație.' }
  finally { syncing = false }
}
document.querySelector('#sync').onclick = sync
document.querySelector('#clear').onclick = async () => { if (confirm('Ștergi lecțiile și răspunsurile păstrate pe acest dispozitiv?')) { await offlineClear(); await render() } }
window.addEventListener('online', sync)
render().then(() => { if (navigator.onLine) return sync() }).catch(e => { status.textContent = e.message })
