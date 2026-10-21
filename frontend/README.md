# Frontend — Bac la Română

Next.js 16 (App Router), JavaScript, Tailwind + stilul „insule” (`app/insule.css`).

## Pornire

1. Pornește API-ul (vezi `database/docs/api.md`), implicit pe `http://localhost:5080`.
2. Creează `frontend/.env.local`:

   ```
   NEXT_PUBLIC_API_URL=http://localhost:5080
   ```

3. `npm install` și `npm run dev`, apoi deschide http://localhost:3000.

## Structură

- `lib/api.js` — clientul API (cookie de sesiune, erori cu mesaj în română).
- `lib/sesiune.js` — cine e logat (`useSesiune`) și protecția paginilor pe rol (`usePagina`).
- `lib/tema-sezon.js`, `components/Tema.js` — temele anotimp × zi/noapte (automat sau alese din meniul contului).
- `lib/canal-joc.js` — canalul SignalR al camerelor Triviador.
- `app/` — paginile: `/` (prezentare + cont, sau panoul elevului/profesorului/adminului), `/materiale`, `/materiale/[id]` (harta),
  `/nivel/[id]` (întrebarea lecției), `/antrenament`, `/joc`, `/profesori`, `/linia-ai`, `/admin/continut`, `/admin/profesori`, `/in-asteptare`.
- `data/*.json` — conținutul inițial, importat în baza de date de comanda `seed`.

## Teste (Vitest + React Testing Library)

```
npm test            # o dată (~8 s)
npm run test:watch  # la fiecare salvare
```

Testele stau în `teste/` (`lib/`, `componente/`, `pagini/`), rulează în jsdom și nu ating niciun server: `teste/ajutor.js`
înlocuiește `fetch` cu un API fals (`apiFals({ "GET /cale": raspuns })`) și ține un router fals, iar `teste/setup.js`
înlocuiește `next/navigation` și `next/link`. Testele din browser (Playwright) și cele ale API-ului (`database/docs/teste.md`) sunt separate.
