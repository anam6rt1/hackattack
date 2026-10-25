# Baza de date Mark AI — Code First

Proiectul foloseste .NET 10, Entity Framework Core 10 si Npgsql, pe Windows/Linux din VS Code sau terminal.
Modelul C# si migrarile din `src/MarkAi.Data` reprezinta sursa structurii de acum inainte.
PostgreSQL ramane in containerul Bazzite; Tailscale Serve directioneaza `100.80.30.123:5432` catre `127.0.0.1:5432` pe server.

## Pornire

Instaleaza SDK-ul .NET 10. Pe gazda Bazzite:

```bash
sudo rpm-ostree install --idempotent dotnet-sdk-10.0 git
# Salveaza lucrul si reporneste dupa finalizarea instalarii:
systemctl reboot
```

Din subfolderul `database` al repository-ului, dupa restart:

```bash
cd database
dotnet --info
dotnet tool restore
dotnet restore
dotnet build
```

Pentru C# in VS Code foloseste extensia Microsoft C# Dev Kit. Extensia Microsoft PostgreSQL permite explorarea datelor.
Nu este necesar Visual Studio, un server PostgreSQL local sau Distrobox.

## Conexiune fara parola in Git

Pe Linux:

```bash
export MARKAI_CONNECTION_STRING='Host=100.80.30.123;Port=5432;Database=postgres;Username=postgres'
read -rs -p 'Parola PostgreSQL: ' PGPASSWORD
echo
export PGPASSWORD
```

Pe Windows PowerShell:

```powershell
$env:MARKAI_CONNECTION_STRING = 'Host=100.80.30.123;Port=5432;Database=postgres;Username=postgres'
$credential = Get-Credential -UserName postgres -Message 'Parola PostgreSQL'
$env:PGPASSWORD = $credential.GetNetworkCredential().Password
```

Foloseste contul primit de la administrator. Contul de migrari are nevoie de DDL; verificarea necesita si CREATEDB pentru bazele temporare.
Nu folosi contul postgres sau accesul direct la DB in clientul web/joc; backendul trebuie sa autentifice utilizatorii si sa foloseasca un cont cu permisiuni limitate.

## Migrari

- `SchemaInitiala` (2026-10-20): o singura migrare cu toata schema, dupa refacerea „Lecție” (cele 13 migrari din hackathon au fost comasate;
  baza reala a fost reconstruita de la zero). Contine si indexurile case-insensitive `lower(email)`/`lower(username)` si extensia `pg_trgm`
  (indexuri GIN pe coloanele `cautare`). Istoricul se afla in `mark_ai.__EFMigrationsHistory`.
- Datele de referinta (roluri, inclusiv `organizatie`, niveluri XP, tarif, teme, stiluri) nu sunt in migrari, ci in `seed`.

O baza noua, de la zero:

```bash
dotnet run --project tools/MarkAi.Database -- migrate
dotnet run --project tools/MarkAi.Database -- seed-localitati      # judete si localitati (SIRUTA)
dotnet run --project tools/MarkAi.Database -- seed                 # materii, Lectii, capitole, intrebari, teme
dotnet run --project tools/MarkAi.Database -- date-test          # DOAR baze de test/demo: conturi si continut de test
```

`date-test` (continutul in `tools/MarkAi.Database/date/date-test.json`) adauga, cu parola `MARKAI_PAROLA_TEST` (implicit „HackAttack”)
si adrese @hackattack.com confirmate: admin, organizatie (admin „Organizație Demo”), profesor, elev; un profesor pe materie
(profesor-matematica, -romana, -fizica, -geografie, -istorie); elevii elev1–elev20 (1–16 in organizatie, 17–18 cu cerere in asteptare,
19–20 fara organizatie); 5 materii cu Lectii pe clase (IX–XII), capitole, intrebari si fisiere (la Istorie). Ce exista deja nu se
adauga din nou. Fisierele merg in folderul de stocare al API-ului (implicit `src/MarkAi.Api/uploads`, sau primul argument).

Pentru modificari viitoare, editeaza entitatile si configurarea, apoi:

```bash
dotnet ef migrations add NumeModificare --project src/MarkAi.Data
dotnet ef migrations script --idempotent --project src/MarkAi.Data --output migration-review.sql
# Revizuieste scriptul si fa backup inainte de aplicare pe serverul comun.
dotnet run --project tools/MarkAi.Database -- migrate
```

Aceiasi pasi, din radacina repo-ului, cu `./dbmg.sh` (`help` le listeaza): `check` (e nevoie de o migrare?), `create Nume`,
`status`, `apply [--dry-run]` si `remove` (doar o migrare neaplicata). `apply` lucreaza pe baza din `~/.config/markai/markai.env`:
refuza SQL-ul care pierde date (DROP TABLE/COLUMN/SCHEMA, DROP ... CASCADE, TRUNCATE, DELETE FROM), face backup schemei `mark_ai`, apoi aplica.
Backupurile (`~/.local/state/markai/backups`) se gestioneaza cu `./dbmg.sh backup list|create [eticheta]|show <nr>|delete <nr>...`.

Nu folosi EnsureCreated/EnsureDeleted pe baza existenta. Nu modifica migrari deja aplicate.

## Lectii, teste si fisiere

| Tabel | Rol |
|---|---|
| parcurs | Lectia: materie, clasa, autor, organizatie, vizibilitate |
| curs | Capitolele Lectiei (insulele hartii): notite JSON, video |
| intrebare / intrebare_varianta | Testul Lectiei (si intrebarile Triviador); fara `parcurs_id` = intrebari Triviador ale materiei |
| fisier_lectie | Cel mult un PDF, un DOCX si un PPTX pe Lectie (index unic pe `parcurs_id, tip`) |
| progres_lectie | Ultimul capitol deschis si data la care elevul a trecut testul (singurul lucru care termina Lectia) |
| incercare_quiz | Incercarile testului; raspunsurile corecte se arata doar la final |

Stergerea unei Lectii sterge in cascada capitolele, intrebarile, fisierele, progresul si incercarile; XP-ul deja acordat ramane.
Triviador ruleaza in memoria API-ului (o insula = o Lectie); vechile tabele `trivia_camera/runda/raspuns` au fost scoase.

## Verificare reproductibila

```bash
dotnet run --project tools/MarkAi.Database -- verify
```

Necesita acces PostgreSQL si dreptul CREATEDB. Creeaza o baza izolata cu nume aleator, aplica migrarea si verifica regulile bazei: subiectul capitolelor si al intrebarilor, o singura varianta corecta, un fisier de fiecare tip, XP o singura data la teste trecute concurent, rezervari concurente si stergerea in cascada a unei Lectii. La final elimina baza de test, inclusiv la eroare. Nu introduce date demonstrative in baza aplicatiei.

La terminarea sesiunii:

```bash
unset PGPASSWORD MARKAI_CONNECTION_STRING
```

Pe PowerShell:

```powershell
Remove-Item Env:PGPASSWORD, Env:MARKAI_CONNECTION_STRING
```


