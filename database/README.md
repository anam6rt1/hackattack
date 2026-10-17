# Mark AI — baza de date

Tot codul bazei de date este in acest subfolder:

- `src/MarkAi.Data`: entitati C#, DbContext, configurari, migrari si regulile trivia.
- `tools/MarkAi.Database`: adoptare schema existenta, migrari si verificari de integrare.
- `tools/*.sql`: verificari de structura si integritate a datelor.
- `docs/database.md`: configurare Windows/Linux/Bazzite si comenzi de lucru.
- `docs/verification.md`: rezultatele verificarii initiale.
- `MarkAi.slnx`, `global.json`, `dotnet-tools.json`: solutia, SDK-ul si unealta EF.

Ruleaza comenzile .NET din acest director:

```bash
cd database
dotnet tool restore
dotnet restore
dotnet build
```

Pentru conexiune si migrari vezi [documentatia completa](docs/database.md).
Pachetele NuGet si rezultatele compilarii nu se incarca in Git; fisierele `packages.lock.json` se pastreaza.
