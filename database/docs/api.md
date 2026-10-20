# API Mark AI (`src/MarkAi.Api`)

ASP.NET Core (.NET 10) peste `MarkAi.Data`. Frontendul Next.js îl apelează direct (CORS cu cookie).

## Pornire locală

```bash
cd database
export MARKAI_CONNECTION_STRING='Host=...;Port=5432;Database=...;Username=...'   # + PGPASSWORD
dotnet run --project tools/MarkAi.Database -- migrate
MARKAI_ADMIN_EMAIL=admin@exemplu.ro MARKAI_ADMIN_PAROLA='o-parola-lunga' \
  dotnet run --project tools/MarkAi.Database -- seed        # idempotent; citește ../frontend/data
dotnet run --project src/MarkAi.Api --launch-profile http   # http://localhost:5080, Swagger la /swagger
```

`seed` adaugă rolurile, nivelurile XP, tariful fix (80 RON / 60 min), cele 8 teme (anotimp × zi/noapte),
materia „Limba și literatura română” cu lecțiile și întrebările din `frontend/data/*.json` și, dacă sunt setate
`MARKAI_ADMIN_EMAIL`/`MARKAI_ADMIN_PAROLA`, primul cont de administrator.

## Variabile de mediu

Pe calculatorul de demo toate stau în `~/.config/markai/markai.env`, încărcat de scripturile de pornire din rădăcina repo-ului;
lista completă, cu valori de exemplu, e în [markai.env.example](../../markai.env.example). API-ul nu pornește dacă lipsesc cele obligatorii.

| Variabilă | Rol |
|---|---|
| `MARKAI_CONNECTION_STRING` | Conexiunea PostgreSQL (obligatorie) |
| `MARKAI_JWT_KEY` | Cheia de semnare a sesiunilor, minim 32 caractere. Obligatorie în producție; în Development se generează una la pornire (sesiunile nu supraviețuiesc restartului). |
| `ANTHROPIC_API_KEY` | Claude pentru generarea întrebărilor și tutorele de la telefon (furnizorul principal). |
| `GEMINI_API_KEY` | Google Gemini (nivel gratuit), folosit doar dacă `ANTHROPIC_API_KEY` lipsește. Cheie din Google AI Studio. Pe nivelul gratuit Google poate folosi datele trimise pentru a-și îmbunătăți modelele, deci în demo folosiți elevi și apeluri de test, nu date reale. |
| `TWILIO_AUTH_TOKEN` | Validarea semnăturii webhook-ului `/twilio/voce`. Fără ea webhook-ul răspunde 403. |

Setări din `appsettings.json`: `Frontend:Origini` (CORS), `Autentificare:CookieSecure` (`true` când frontendul e pe alt domeniu, cu HTTPS),
`Claude:Model`, `Claude:ModelTelefon`, `Gemini:Model`, `Gemini:ModelTelefon` (implicit `gemini-flash-latest`), `LiniaAi:Numar` (numărul afișat elevilor), `Twilio:UrlPublic` (adresa publică folosită la semnătură), `Stocare:Director` (poze și diplome).

## Securitate (pentru clienți)

- **Antetul `X-MarkAi: 1`** e obligatoriu pe orice cerere care modifică ceva (POST/PUT/PATCH/DELETE), altfel 403.
  `frontend/lib/api.js` îl trimite mereu; un formular de pe alt site nu îl poate adăuga. Excepții: `/twilio/*` (semnat) și `/hub/*`.
- **Sesiunea** e cookie-ul `markai_sesiune` (httpOnly, SameSite=Lax, Secure în modul public). Tokenul conține versiunea sesiunii
  contului; `POST /autentificare/iesire-peste-tot`, schimbarea parolei sau a emailului o cresc și închid sesiunile de pe celelalte dispozitive.
- **Limite**: intrare în cont 10/min per IP, conturi noi 5/oră per IP, coduri email 6/oră, regenerări AI 10/min, „Sună-mă” 2/min,
  orice cerere 600/min; peste limită → 429 cu `Retry-After`. Valori în `Limite:*` (vezi `markai.env.example`). IP-ul real vine
  din `X-Forwarded-For` (un singur proxy de încredere: Next.js pe localhost).
- **Erori**: 400 (`CerereInvalida`), 404, 409 (`Conflict`) au mesaje pentru utilizator; orice altă eroare devine 500 cu un mesaj
  generic, iar detaliile rămân în jurnal. Cererile au cel mult 2 MB (încărcările de fișiere, 16 MB).
- **Fișiere**: tipul e verificat după primii octeți, nu doar după extensie; documentele se descarcă (`attachment`), nu se afișează.
  Poza unui profesor e publică doar după aprobare (în rest o vede doar el și adminul).
- **Telefonul** se schimbă doar prin `PUT /utilizatori/me/setari/telefon` (verifică să nu fie al altui cont).

## Roluri

- **elev**: învață (progres, XP, quiz), Triviador, rezervări.

Testul unei lecții: `POST /cursuri/{id}/quiz` pornește (sau reia) o încercare cu 5 întrebări alese aleator, fără răspunsuri;
`POST /cursuri/{id}/quiz/{incercareId}/raspuns` spune doar dacă răspunsul e corect. După ultimul răspuns vin corecturile și
explicațiile; lecția e trecută (100 XP, o singură dată) cu 4 din 5 (80%). Între două încercări e o pauză (`Limite:PauzaQuizSecunde`, 60 s).
- **profesor**: se înscrie cu materii, bio, poză și dovada calificării; până la aprobare primește 403 la toate rutele de profesor
  (frontendul îl trimite la `/in-asteptare`). Aprobat: programează ședințe (plătite la tariful fix sau pro bono) și editează lecțiile materiilor predate.
- **admin**: aprobă/respinge profesori, stabilește onorariul negociat (≤ tariful elevului), editează orice conținut.

Plata elevului este simulată (`plata.status = 'simulata'`); profesorul primește onorariul negociat, păstrat în `comision_mentor.suma_comision`.

## Aspect: temă, stil și avatar

`GET/PUT /utilizatori/me/preferinte` ține trei alegeri independente, în `preferinte_utilizator`:

- **tema** (anotimp × zi/noapte): `{ "mod": "auto" }` sau `{ "mod": "static", "tema": "iarna-noapte" }` → `mod_background`, `background_static_id`;
- **stilul desenelor**: `{ "interfata": "insule" | "realist" | "pixel" }` → `interfata_id` (frontendul numește „insule” *Acvatic*);
- **avatarul** din profil: `{ "avatar": "harap-alb" }` → `avatar` (codul unui personaj desenat în frontend).

La `PUT` se trimit doar câmpurile schimbate; cele lipsă rămân cum erau. Răspunsul are mereu toate patru: `mod`, `tema`, `interfata`, `avatar`.
Migrarea `AvatarSiStiluri` adaugă coloana `avatar` și interfețele `realist` și `pixel`.

## Întrebări generate cu AI

Ambele funcții AI (întrebările și tutorele) trec prin `Servicii/ModelAi.cs`: Claude dacă există `ANTHROPIC_API_KEY`,
altfel Gemini dacă există `GEMINI_API_KEY`. Promptul și schema JSON sunt aceleași; se schimbă doar apelul.

`POST /cursuri` și `PUT /cursuri/{id}` (când se schimbă materialul) pun lecția într-o coadă în memorie; un `BackgroundService`
cere lui Claude întrebări grilă + estimări (ieșire JSON structurată, cu fallback pe server la refuz) și le salvează cu `sursa = 'ai'`.
Întrebările AI anterioare sunt dezactivate, nu șterse. O întrebare editată de om devine `manual` și nu mai e înlocuită.
Starea: `GET /cursuri/{id}/generare`. Un profesor poate porni cel mult `Limite:GenerariAiPeZi` (10) generări în 24 de ore
(adminii nu au limită); peste limită, regenerarea cerută primește 409, iar cea automată după o editare nu pornește și starea spune de ce.

## Triviador

Jocul rulează pe server (`Servicii/Triviador/MotorTriviador.cs`); clienții trimit doar intenții și primesc starea publică.

- `POST /trivia/camere` `{ materieIds, insule (9–30), maxJucatori (2–4) }` → `{ cod }` (6 caractere). Cine creează camera e gazda.
  Cel mult 3 camere deschise per gazdă și 200 în total; o cameră goală se închide după 10 minute.
- Hub `/hub/joc` (SignalR, doar elevi): `Intra(cod)` întoarce starea; gazda: `AdaugaBot("usor"|"mediu"|"greu")`, `ScoateBot(id)`,
  `Porneste()`, `JocNou()`; jucătorii: `Ataca(insula)`, `Raspunde(index)`, `Estimeaza(numar)`. Serverul trimite `stare` după fiecare schimbare.
- Starea nu conține niciodată răspunsul unei întrebări deschise; `rezultat` (cu răspunsul) apare doar în faza „rezultat”.
  `termenLa` și `acum` sunt timpi ai serverului (ms), pentru cronometru.
- Harta e generată pe server (insule pe o grilă cu poduri care nu se intersectează, mereu conexă; cetățile cât mai departe).
  Insulele poartă numele capitolelor materiilor alese; întrebările vin din toate întrebările active ale acestor materii
  (inclusiv cele ale lecțiilor private: e un joc de exercițiu), cu setul demonstrativ de română dacă materiile nu au întrebări.
- Reguli: 30 s pentru alegerea insulei, 15 s pentru întrebare; duel corect-corect → estimare, egalitatea o câștigă apărătorul.
  Runde = ⌈insule libere / jucători⌉ + 2. Boții răspund corect în 50% / 70% / 90% din cazuri și estimează la ±30% / ±15% / ±5%.
- XP la final, acordat o singură dată de server (`eveniment_recompensa`, tip `triviador`): 10 per insulă + 50 câștigătorului
  (la egalitate, tuturor celor la egalitate); într-un joc cu boți, cu 30% mai puțin; boții nu primesc nimic.
- Camerele sunt în memorie (o singură instanță de API; se pierd la repornire). Tabelele `trivia_*` și `TriviaService` nu sunt folosite
  de joc (au triggere care blochează ștergerea, deci nu se scriu fără să fie schimbate).

## Linia AI (Twilio + Claude)

Elevul sună pe numărul liniei; API-ul îl recunoaște după `utilizator.telefon` (normalizat E.164) și poartă conversația:
`/twilio/voce` (apel nou: salut + ascultare) → `/twilio/voce/raspuns` (textul recunoscut din vorbire → răspunsul tutorelui,
citit cu vocea aleasă de elev) → ... → la „la revedere” tutorele închide apelul. Fiecare replică se salvează ca `mesaj`
și apare pe site în „Apelurile tale”. Tutorele primește la fiecare replică progresul elevului, lista lecțiilor și
materialele lecției la care lucrează sau pe care o pomenește (potrivire fără diacritice, ca la recunoașterea vorbirii).
Răspunsul trebuie să vină în cel mult ~15 s (limita Twilio), de aceea tutorele rulează cu effort scăzut;
dacă un model anume e prea lent, setează `Claude:ModelTelefon`.

### Vocea tutorelui

Elevul poate cere în apel „vorbește mai rar”, „ca o bunică”, „mai repede” etc. Fiecare răspuns al lui Claude este JSON
structurat (`raspuns`, `final`, `voce`), iar setările de voce se aplică chiar pe replica ce confirmă schimbarea și se salvează
în `preferinte_utilizator` (`voce_tutor`, `viteza_tutor` 70–130%, `ton_tutor` −20…+20%), deci și apelurile următoare le folosesc.
Pe site, pagina Linia AI are aceleași setări. Twilio are pentru română doar voci feminine (Google Wavenet-B, Google Standard-B,
Polly Carmen; vezi `Servicii/VociTutor.cs`); viteza și tonul se aplică prin SSML `<prosody>`.

### „Sună-mă” și contul Twilio de probă

Pe un cont Twilio trial elevii nu pot suna la un număr; în schimb apasă **Sună-mă** pe pagina Linia AI, iar API-ul
cere Twilio să îi sune (`POST /linia-ai/suna-ma` → Twilio REST `Calls.json`). Când elevul răspunde, Twilio cere
`/twilio/voce` (cu `Direction=outbound-api`, elevul e `To`) și conversația continuă ca la un apel primit.
Pe pagină apare mesajul că apelarea unui număr va fi disponibilă în curând.

Fiecare apel are propria sesiune, creată înainte ca Twilio să sune, cu o cheie aleatorie (în baza de date e doar hash-ul ei).
Toate adresele webhook ale apelului au forma `/twilio/voce...?s={sesiune}&k={cheie}`; o cerere e acceptată doar cu cheia sesiunii
(nexpirată) sau cu semnătura Twilio, și doar pentru `CallSid`-ul acelui apel. Contul trial cere TwiML printr-un proxy a cărui
semnătură nu se potrivește cu adresa noastră, de aceea cheia din adresă e necesară. Cheia nu apare niciodată în jurnale.
Un apel primit (elevul sună el) trebuie să aibă semnătura Twilio. Limite: `Twilio:ApeluriPeZi` (3 per elev în 24 de ore) și
`Twilio:MinutePeZi` (20 de minute în total în 24 de ore, după durata raportată de Twilio la final).

Limitele trial (`Twilio:Trial = true`, implicit): Twilio așteaptă răspunsul serverului doar 5 s (tutorele are 4 s,
altfel elevul aude „poți să repeți?”), un apel poate urma cel mult 10 documente TwiML (`Twilio:LimitaPasi`) și durează
cel mult 10 minute. La ultimul pas (sau după 9 minute) tutorele rezumă și încheie cu „Vom continua data viitoare.”
Cu un cont plătit, setați `Twilio:Trial = false` (fără limită de pași, 12 s pentru răspuns).

Configurare:
1. Cont pe twilio.com (trial). Numărul tău de la înregistrare devine număr verificat; alți elevi trebuie adăugați
   ca „Verified Caller IDs” (maxim 5 pe trial). Apelurile funcționează doar în țara contului.
2. Tunel public spre API: `cloudflared tunnel --url http://localhost:5080` (adresa se schimbă la fiecare pornire).
3. Pornește API-ul cu `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `Twilio__Numar=+...` (numărul Twilio din care se sună),
   `Twilio__UrlPublic=https://adresa-tunelului` și o cheie AI (`ANTHROPIC_API_KEY` sau `GEMINI_API_KEY`).
4. Elevul își salvează numărul pe pagina Linia AI (același număr verificat în Twilio) și apasă **Sună-mă**.

Fără nicio cheie AI (`ANTHROPIC_API_KEY` sau `GEMINI_API_KEY`), tutorele spune că nu e pornit; fără `TWILIO_AUTH_TOKEN`,
webhook-urile răspund 403; fără SID/număr/URL public, butonul Sună-mă e dezactivat.
Rezumatul prin SMS de la finalul apelului nu este încă implementat.

## Contract

Documentul OpenAPI generat din cod este la `/openapi/v1.json` (Development). Rutele urmează convențiile din `api-mock/openapi.yaml`
(nume românești, liste `{ elemente, total }`), dar sursa de adevăr este acum API-ul real.
