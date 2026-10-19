# hackattack — Mark AI

Codul si documentatia bazei de date se afla in subfolderul [database](database/README.md).

Pentru configurare si migrari: [documentatia bazei de date](database/docs/database.md).

Original concept reference: https://share.gemini.google/5D0Yw2QbMrMr

## Pornire accesibilă de pe orice dispozitiv

`./porneste-public.sh` pornește API-ul (producție, doar pe `127.0.0.1:5080`), site-ul (build de producție pe `:3000`,
care redirecționează `/api/...` spre API) și un tunel Cloudflare temporar, apoi afișează adresa publică
(se schimbă la fiecare pornire). `./porneste-public.sh stop` oprește tot.
Necesită `cloudflared` (`brew install cloudflared`).

`./porneste-local.sh` pornește aceleași servicii fără tunel, doar pe `http://localhost:3000` (Linia AI la telefon
rămâne oprită, fiindcă Twilio are nevoie de o adresă publică). `./porneste-local.sh stop` oprește tot.

Toate secretele și setările locale stau într-un singur fișier, `~/.config/markai/markai.env` (chmod 600), niciodată în repo.
Pornește de la [markai.env.example](markai.env.example): obligatorii sunt `MARKAI_CONNECTION_STRING` și `MARKAI_JWT_KEY`;
scripturile se opresc și spun ce lipsește.
