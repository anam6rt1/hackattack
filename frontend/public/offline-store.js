const DB = 'telebac-offline-v1'
async function open() {
  return new Promise((resolve, reject) => {
    const r = indexedDB.open(DB, 1)
    r.onupgradeneeded = () => r.result.createObjectStore('lectii', { keyPath: 'id' })
    r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error)
  })
}
export async function offlineRead() {
  const db = await open()
  try { return await new Promise((resolve, reject) => { const r = db.transaction('lectii').objectStore('lectii').getAll(); r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error) }) } finally { db.close() }
}
async function write(action) {
  const db = await open()
  try { await new Promise((resolve, reject) => { const tx = db.transaction('lectii', 'readwrite'); action(tx.objectStore('lectii')); tx.oncomplete = resolve; tx.onerror = () => reject(tx.error); tx.onabort = () => reject(tx.error) }) } finally { db.close() }
}
export async function offlineSave(row) {
  const rows = await offlineRead()
  const other = rows.filter(r => r.id !== row.id)
  if (other.length >= 10 || new Blob([JSON.stringify([...other, row])]).size > 2000000) throw new Error('Poți păstra cel mult 10 lecții și 2 MB de text. Elimină o lecție salvată.')
  await write(s => s.put(row))
}
export function offlineRemove(id) { return write(s => s.delete(id)) }
export function offlineClear() { return write(s => s.clear()) }
