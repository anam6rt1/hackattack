import { createECDH } from 'node:crypto'
import { writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
const [cale, subject] = process.argv.slice(2)
if (!cale || !subject || !/^(mailto:|https:\/\/)/.test(subject)) throw new Error('Utilizare: node tools/creeaza-chei-push.mjs <fișier în afara proiectului> <mailto:contact sau https://site>')
const ecdh = createECDH('prime256v1'); ecdh.generateKeys()
writeFileSync(resolve(cale), JSON.stringify({ subject, publicKey: ecdh.getPublicKey().toString('base64url'), privateKey: ecdh.getPrivateKey().toString('base64url') }), { flag: 'wx', mode: 0o600 })
console.log('Cheile push au fost salvate. Nu publica fișierul și păstrează-l la repornire.')
