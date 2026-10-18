# Predare pentru migrare: setarile contului

Modificarile raman pe `feature/account-settings`. Aplicarea pe server se face separat, de administrator.

## Codul C# pregatit

| Date / comportament | Implementare |
| --- | --- |
| Nume, username, email, telefon, hash parola | `src/MarkAi.Data/Entities/Utilizator.cs` |
| Descriere, fotografie, dovada calificarii, stare verificare | `src/MarkAi.Data/Entities/ProfilMentor.cs` |
| Materii predate | `src/MarkAi.Data/Entities/MentorMaterie.cs` |
| Mapari EF pentru parola si profilul profesorului | `src/MarkAi.Data/IntegrareConfiguration.cs` |
| Citire/salvare profil si documente, initiere/confirmare schimbare parola sau email | `src/MarkAi.Api/Setari/SetariContEndpoints.cs` |
| Coduri temporare si expediere SMTP | `src/MarkAi.Api/Setari/ConfirmariCont.cs` |
| Inregistrarea API-urilor | `src/MarkAi.Api/Program.cs` |

Aceste campuri exista deja in modelul EF si in migrarile versionate, inclusiv `InitialSchema` si `IntegrareFrontend`. Pagina de setari actualizeaza aceleasi inregistrari: nu introduce tabele sau coloane noi. Prin urmare nu este necesara o migrare `AccountSettings` goala. Nu modifica migrarile istorice sau snapshot-ul pentru a forta una.

Codurile de confirmare sunt temporare, in memorie, si nu necesita o tabela. Persistarea lor intre reporniri sau intre mai multe instante ar fi o extindere separata.

## Export SQL fara conexiune la server

Din directorul `database`, cu SDK .NET 10 instalat:

```powershell
dotnet tool restore
dotnet restore
New-Item -ItemType Directory -Force .local | Out-Null
# Valoare locala fictiva ceruta de fabrica EF; exportul scriptului nu deschide conexiunea.
# Ruleaza in acest terminal separat, fara credentialele serverului comun.
$env:MARKAI_CONNECTION_STRING = 'Host=127.0.0.1;Port=1;Database=offline;Username=offline'
dotnet ef migrations script --idempotent --project src/MarkAi.Data --output .local/account-settings.sql
Remove-Item Env:MARKAI_CONNECTION_STRING
```

Pe Linux, aceleasi comenzi EF, folosind:

```bash
mkdir -p .local
MARKAI_CONNECTION_STRING='Host=127.0.0.1;Port=1;Database=offline;Username=offline' \
  dotnet ef migrations script --idempotent --project src/MarkAi.Data --output .local/account-settings.sql
```

Exportul compileaza proiectul, fara sa ruleze teste sau sa aplice migrari. Scriptul include migrarile existente ale branch-ului; nu doar pagina de setari. Directorul `.local` este ignorat de Git.

## Aplicare ulterioara, de administrator

1. Foloseste acest branch si fa backup bazei tinta.
2. Verifica istoricul `mark_ai.__EFMigrationsHistory` si revizuieste scriptul exportat. Daca schema a fost creata manual, fara istoric EF, urmeaza procedura de adoptare din `database.md`; nu aplica orbește scriptul initial peste tabelele existente.
3. Configureaza `MARKAI_CONNECTION_STRING` cu datele reale doar in terminalul administrativ, fara salvarea parolei in Git.
4. Din `database`, aplica migrarile restante:

```bash
dotnet run --project tools/MarkAi.Database -- migrate
```

Daca toate migrarile existente sunt deja aplicate, aceasta functie nu cere alte schimbari de schema. Configureaza separat SMTP conform `account-settings.md`, apoi porneste API-ul.

In aceasta etapa de predare nu s-au executat teste, comenzi EF, export SQL sau migrari si nu s-a accesat baza de date. Rezultatele de testare mentionate in `account-settings.md` apartin etapei anterioare, cu baza temporara locala.
