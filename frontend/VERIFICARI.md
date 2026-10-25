# Verificări locale

Folosește Node.js 26 (versiunea fixată în `.node-version`) și .NET SDK 10 pentru server.
Node 22 instalat anterior pe calculator nu satisface cerința proiectului.

Pe acest calculator poți rula direct `./verifica-local.ps1 -Browser` din
`frontend`. Scriptul alege Node 26 din cache-ul local sau acceptă o cale prin
`-NodeExe`, folosește certificatele Windows și nu schimbă permanent mediul.
Adaugă `-Instaleaza` pentru reinstalare din lockfile.

Din `frontend`:

```sh
npm ci
npm run verifica
npx playwright install chromium
npm run test:uploads
```

`verifica` oprește execuția la prima eroare și rulează lint, testele Vitest,
testele de optimizare și compilarea de producție. `test:uploads` compilează și
verifică într-un browser real previzualizarea Word/PowerPoint/Excel/PDF,
optimizarea fotografiilor și anularea/reîncercarea. Aceste teste folosesc un API
simulat și nu accesează baza de date. Testele end-to-end existente, cu API .NET
și PostgreSQL temporar, rămân disponibile prin `npm run test:e2e` și necesită Docker.

Pe Windows, dacă instalarea npm raportează `UNABLE_TO_VERIFY_LEAF_SIGNATURE`
într-o rețea cu certificate instalate în Windows, Node 26 poate utiliza magazinul
de certificate al sistemului:

```powershell
$env:NODE_USE_SYSTEM_CA = '1'
npm ci
```

Verificarea HTTPS rămâne activă. Nu folosi `strict-ssl=false`.

Din `database`:

```sh
dotnet test --project tests/MarkAi.Tests.Unit/MarkAi.Tests.Unit.csproj
```

Suportul XLSX necesită migrarea `20261024083000_FisiereExcel` înainte de folosirea
într-o bază existentă, prin mecanismul de migrare al proiectului. Migrarea nu
șterge documente; revenirea la versiunea precedentă este refuzată dacă există
materiale XLSX. Nu aplica migrarea pe o bază reală ca parte a testelor.
