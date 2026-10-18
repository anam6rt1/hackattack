# hackattack — Mark AI

Codul si documentatia bazei de date se afla in subfolderul [database](database/README.md).

Pentru configurare si migrari: [documentatia bazei de date](database/docs/database.md).

Original concept reference: https://share.gemini.google/5D0Yw2QbMrMr

## Pornire accesibilă de pe orice dispozitiv

`./porneste-public.sh` pornește API-ul (producție, doar pe `127.0.0.1:5080`), site-ul (build de producție pe `:3000`,
care redirecționează `/api/...` spre API) și un tunel Cloudflare temporar, apoi afișează adresa publică
(se schimbă la fiecare pornire). `./porneste-public.sh stop` oprește tot.
Necesită `cloudflared` (`brew install cloudflared`) și fișierele de configurare din `~/.config/markai/`:
`db.env` (`MARKAI_CONNECTION_STRING`, obligatoriu), `jwt.env` (`MARKAI_JWT_KEY`), `ai.env` (`ANTHROPIC_API_KEY` sau
`GEMINI_API_KEY`) și `twilio.env` (`TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`). Secretele nu se pun în repo.
