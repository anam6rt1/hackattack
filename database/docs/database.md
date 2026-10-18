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

Foloseste contul primit de la administrator. Contul de migrari are nevoie de DDL; verificarea/adoptarea necesita si CREATEDB pentru bazele temporare.
Nu folosi contul postgres sau accesul direct la DB in clientul web/joc; backendul trebuie sa autentifice utilizatorii si sa foloseasca un cont cu permisiuni limitate.

## Migrari

- `InitialSchema`: recreeaza cele 28 de tabele initiale din configurarea C#, inclusiv constrangerile si indexurile case-insensitive.
- `AddTrivia`: adauga 7 tabele pentru joc si functii/trigger-e PostgreSQL pentru validarea timpului, scorului si a concurentei.
- `IntegrareFrontend`: parola utilizatorului, continutul lectiilor (capitol, ordine, video, notite JSON), `progres_curs`, cererile profesorilor (`mentor_materie`, poza, dovada, onorariu negociat), sedinte pro bono si intrebari pe lectie (explicatie, tip grila/estimare, sursa ai/manual). Revenirea la migrarea anterioara esueaza intentionat cat timp exista sedinte pro bono.
- Istoricul se afla in `mark_ai.__EFMigrationsHistory`.

Pe o baza noua, goala:

```bash
dotnet run --project tools/MarkAi.Database -- migrate
```

Pe baza originala care contine tabelele SQL, fara istoric EF:

```bash
dotnet run --project tools/MarkAi.Database -- adopt-existing
dotnet run --project tools/MarkAi.Database -- migrate
```

Adoptarea creeaza o baza temporara din prima migrare si compara semantic structura cu schema existenta (coloane, chei, relatii, verificari si indexuri). Daca exista diferente, refuza inregistrarea migrarii. Daca este identica, inregistreaza doar punctul de pornire, fara recrearea tabelelor. Bazele temporare sunt eliminate.
Aceasta comanda este pentru schema initiala definita aici; alte instalari istorice necesita migrare de aliniere.

Pentru modificari viitoare, editeaza entitatile si configurarea, apoi:

```bash
dotnet ef migrations add NumeModificare --project src/MarkAi.Data
dotnet ef migrations script --idempotent --project src/MarkAi.Data --output migration-review.sql
# Revizuieste scriptul si fa backup inainte de aplicare pe serverul comun.
dotnet run --project tools/MarkAi.Database -- migrate
```

Nu folosi EnsureCreated/EnsureDeleted pe baza existenta. Nu modifica migrari deja aplicate.
Indexurile bazate pe expresii (lower(email)/lower(username)) si trigger-ele sunt SQL versionat in migrari, deoarece nu toate aceste functii PostgreSQL sunt reprezentabile direct in EF.
Migrarea AddTrivia aliniaza si unicitatile istorice nereferite de chei externe la indexurile unice reprezentate de EF, in aceeasi tranzactie, pastrand garantiile de unicitate. Cheile alternative referite raman constrangeri UNIQUE.

## Jocul de trivia

Implementarea acopera camere si runde cu intrebari grila, nu mecanica de teritorii din Triviador.

| Tabel | Rol |
|---|---|
| trivia_camera | Cod unic de 6 caractere, gazda, materie optionala, capacitate 2–16, status |
| trivia_participant | Un loc per utilizator/camera, gazda inclusa |
| trivia_intrebare | Banca de intrebari, grupata pe materie |
| trivia_varianta | Variantele din banca; cel mult una corecta per intrebare |
| trivia_runda | Numar in camera, copia intrebarii, inceput si termen UTC, punctaj |
| trivia_runda_varianta | Copii ale variantelor si raspunsului corect pentru runda |
| trivia_raspuns | Un raspuns per utilizator/runda, moment si scor validate pe server |

`TriviaService` expune:

- `CreateRoomAsync`: cod aleator criptografic, gazda adaugata ca participant; retry pentru coliziuni de cod.
- `JoinRoomAsync`: cod case-insensitive, acces doar in lobby, capacitate verificata sub blocare de rand. Reincercarea intrarii este idempotenta.
- `StartRoundAsync`: doar gazda, minimum doi participanti, intrebare activa, minimum doua variante si exact una corecta. Nu se suprapun runde. Termen 1–300 secunde (implicit 30), punctaj 1–10000 (implicit 100).
- `SubmitAnswerAsync`: doar participanti, o singura incercare, varianta din aceeasi runda. Triggerul PostgreSQL verifica `clock_timestamp()` si stabileste punctajul, ignorand timpul/scorul trimis de client.
- `GetLeaderboardAsync`: clasament pentru membrii camerei, include scorul zero; egalitatile sunt ordonate dupa ID pentru afisare.
- `FinishRoomAsync`: doar gazda, inchide camera si respinge raspunsurile ulterioare.

Raspuns corect = punctele rundei, incorect/fara raspuns = 0. Nu exista bonus de viteza, XP automat sau castig financiar. Codurile nu sunt reutilizate.
Durata este masurata la primirea raspunsului de server; un raspuns ajuns exact la termen este respins.
Expirarea nu necesita un proces de fundal: timestampul inchide logic runda; gazda poate porni urmatoarea sau finaliza camera.
Intrebarile/variantele copiate si raspunsurile trimise nu pot fi editate ulterior prin operatii normale.

Serviciul primeste ID-ul utilizatorului autentificat de backend. Nu accepta un ID arbitrar de la client ca dovada de identitate.
`PublicRound` si `PublicOption` nu contin raspunsul corect. Nu serializa entitatile de banca/snapshot direct catre jucatori.
Interfata jocului, API-ul, autentificarea, SignalR si animatiile nu fac parte din aceasta implementare a bazei de date. Trimiterea live a rundelor este urmatorul nivel al aplicatiei.

## Verificare reproductibila

```bash
dotnet run --project tools/MarkAi.Database -- verify
```

Necesita acces PostgreSQL si dreptul CREATEDB. Creeaza o baza izolata cu nume aleator, aplica migrarile, verifica fluxul jocului, concurenta la intrare/raspuns, deadline, punctaj, izolarea rundelor, pastrarea snapshoturilor si adoptarea schemei cu date existente. La final elimina baza de test, inclusiv la eroare. Nu introduce date demonstrative in baza aplicatiei.

La terminarea sesiunii:

```bash
unset PGPASSWORD MARKAI_CONNECTION_STRING
```

Pe PowerShell:

```powershell
Remove-Item Env:PGPASSWORD, Env:MARKAI_CONNECTION_STRING
```


