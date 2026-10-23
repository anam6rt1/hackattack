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

Dacă după o actualizare de pachete `npm run build` cade cu `Can't resolve '@vercel/turbopack-next/internal/font/google/font'`
(„next/font/google queries have exactly one entry”), cache-ul Turbopack e vechi: `rm -rf .next/cache` și build din nou.

## Teste în browser (Playwright: Chromium, Firefox, WebKit)

```
npx playwright install chromium firefox   # o dată (~250 MB în ~/.cache/ms-playwright)
npm run test:e2e                          # Chromium + Firefox, ~4 min; pornește și oprește singur serverele de test
npm run test:e2e:webkit                   # WebKit (motorul din Safari), în containerul oficial Playwright
npx playwright test e2e/lectie.spec.ts --project=firefox --headed   # un fișier, într-un browser, cu fereastră
```

WebKit-ul Playwright pentru Linux cere biblioteci din Ubuntu (`libicu74`, `libjpeg-turbo8`) pe care Fedora și Bazzite nu le
au, așa că `test:e2e:webkit` pornește serverele de test pe calculator și rulează doar testele WebKit în
`mcr.microsoft.com/playwright` (aceeași versiune ca `@playwright/test`, ~2 GB la prima rulare, cu podman). În CI fiecare browser
rulează direct pe Ubuntu, într-un job separat.

Testele (`e2e/`) nu ating site-ul sau baza reală: Playwright pornește API-ul de test (`database/tools/MarkAi.Previzualizare --e2e`,
127.0.0.1:5081, cu PostgreSQL-ul lui în Testcontainers — pe Bazzite prin socket-ul podman) și un build separat al site-ului
(`.next-e2e`, 127.0.0.1:3001, cu `/api` îndreptat spre API-ul de test). Înainte de teste, `e2e/pregatire.ts` verifică asta (baza de
test are doar 2 județe) și adaugă lecțiile, întrebările și PDF-ul folosite. Codurile de pe email și SMS sunt citite din lista
„Mesaje trimise” a administratorului (furnizorul de test nu trimite nimic). La un eșec, `npx playwright show-report`.
