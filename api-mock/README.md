# Mock API pentru frontend

> API-ul real există acum în `database/src/MarkAi.Api` (vezi `database/docs/api.md`); contractul generat din cod este la `/openapi/v1.json`. Acest mock rămâne doar ca referință istorică.

Contractul `openapi.yaml` descrie API-ul demonstrativ pentru utilizatori, materii si materiale, grafuri de invatare, tutoring, rezervari, sesiuni, progres, aspect si trivia. Rutele si campurile JSON sunt in romana fara diacritice.

## Pornire rapida

Necesita Node.js si npm. Din acest director ruleaza:

```bash
npx @stoplight/prism-cli mock -h 0.0.0.0 openapi.yaml
```

Mock-ul porneste implicit la `http://localhost:4010`. Colegii din aceeasi retea pot folosi `http://IP-UL-CALCULATORULUI:4010`; pentru acces din afara retelei folositi un tunel numai pentru portul mock-ului. Nu contine conexiune la baza de date.

Prism returneaza exemplele declarate in contract. Pentru interfata de documentare, incarcati `openapi.yaml` in Swagger Editor sau Swagger UI. In aplicatia frontend, configurati adresa de baza printr-o variabila de mediu, de exemplu `VITE_API_BASE_URL=http://localhost:4010`.

## Note pentru echipa

- Raspunsurile sunt date statice de demo; operatiile de scriere nu persista modificari si autentificarea nu este reala.
- Listele folosesc forma `{ "elemente": [...], "total": n }`.
- Resursele au nume romanesti fara diacritice, de exemplu `/utilizatori`, `/materii`, `/cursuri`, `/sesiuni` si `/realizari`.
- Contractul este o prima versiune pentru integrarea ecranelor; ajustati-l impreuna cu echipa inainte de implementarea backendului.
