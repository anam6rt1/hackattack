# Mark AI — baza de date

Simulările cronometrate și notificările opționale sunt descrise în [B3-B4](docs/B3-B4.md).

Temele pentru organizații și recapitularea greșelilor sunt descrise în [B1-B2](docs/B1-B2.md).

Fotografiile WebP pentru liste/profil și compresia Brotli/gzip sunt descrise în [A3-A4](docs/A3-A4.md).

API-urile locale pentru compresia în browser, exportul și închiderea contului sunt descrise în [A2-A5](docs/A2-A5.md).
Retenția automată rămâne dezactivată. Modelul de acord pentru școli este în [acord-prelucrare-date-model.md](docs/acord-prelucrare-date-model.md) și necesită completare și validare juridică.

## A1 — repornire sigură pentru pilot

Pilotul folosește **o singură instanță API**, cu PostgreSQL persistent. Migrarea `20261025090000_StarePersistentaA1` păstrează coada/starea generării AI și ultima cerere de apel. Aplicați migrările înainte de pornirea versiunii noi.

Generatorul reia cererile `in_asteptare`/`in_lucru` după restart. Întrebările și starea finalizată sunt salvate în aceeași tranzacție, fără taxarea din nou a bugetului intern. Dacă serverul cade în timpul unei cereri către furnizorul AI, acea cerere externă poate fi repetată la reluare. Pauza de 30 secunde dintre apeluri este rezervată atomic în baza de date; un refuz explicit Twilio eliberează rezervarea, iar un rezultat de rețea incert păstrează pauza.

Camerele Triviador rămân temporare. Clientul compară identificatorul procesului la reconectare și anunță că partida s-a încheiat după restart, cu întoarcere la jocuri. Limitele HTTP rămân locale procesului; versiunile sesiunilor sunt în PostgreSQL, cu cache local. Pentru mai multe instanțe sunt necesare revendicarea distribuită a joburilor, limitare distribuită și SignalR backplane; nu porniți mai mulți generatori cu această configurație.

Tot codul bazei de date este in acest subfolder:

- `src/MarkAi.Data`: entitati C#, DbContext, configurari, migrari si regulile trivia.
- `src/MarkAi.Api`: API-ul pentru frontend (autentificare, continut, progres, profesori, Triviador, Linia AI) — vezi [docs/api.md](docs/api.md).
- `tools/MarkAi.Database`: adoptare schema existenta, migrari, date initiale (`seed`) si verificari de integrare.
- `tools/*.sql`: verificari de structura si integritate a datelor.
- `tests/`: testele automate (unit si integrare cu PostgreSQL in Testcontainers) — vezi [docs/teste.md](docs/teste.md).
- `tools/MarkAi.Previzualizare`: un API de test pe 127.0.0.1:5081, pentru incercari manuale si testele din browser.
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

## Previzualizarea Word, Excel și PowerPoint

API-ul folosește LibreOffice headless pentru o copie PDF de previzualizare, afișată în același vizualizator ca PDF-urile. Originalul rămâne neschimbat. Necesită LibreOffice pe server (Linux: `/usr/bin/libreoffice`; Windows: instalarea standard). Pentru altă locație setează `Previzualizare__LibreOffice` la calea absolută a executabilului. Previzualizarea locală recunoaște și `%USERPROFILE%\.cache\markai-libreoffice\program\soffice.exe`.

Conversiile sunt serializate, au maximum 90 de secunde (inclusiv așteptarea) și produc cel mult 20 MB. Închiderea previzualizării anulează cererea și procesul; copiile temporare sunt șterse. Fiecare conversie folosește un profil separat, cu macrocomenzi dezactivate. Pentru documente neîncrezătoare, rulați API-ul/converterul într-un mediu izolat fără acces la rețeaua internă. Instalați aceleași fonturi pe server pentru paginare cât mai apropiată de Office; foile Excel folosesc setările de tipărire ale documentului.

`GET /fisiere/{id}/previzualizare` respectă accesul la lecție; `POST /fisiere/previzualizare` primește un document selectat de editor și aplică aceleași verificări ca încărcarea. Niciuna dintre rute nu trimite documentele către un serviciu extern.

Copiile PDF sunt păstrate în `Stocare:Director/previzualizari-v2`, cu publicare atomică, cheie SHA-256 + format + foaie și buget de 512 MiB (curățat la conversii noi). Cache-ul nu este public; permisiunile se verifică înainte de fiecare servire. Pentru Excel, `GET /fisiere/{id}/foi` întoarce numele foilor, iar `?foaie=0` alege foaia. Pentru selecțiile locale, POST acceptă `?foi=true`, respectiv `?foaie=0`. Copia temporară păstrează toate datele/formulele, ascunde celelalte foi și potrivește tabelul pe lățime.

## Stocare partajată și încărcări reluabile

Aplicați migrarea `20261024130000_StocarePartajata` înainte de pornirea noului API: `dotnet run --project tools/MarkAi.Database -- migrate` (cu conexiunea configurată). Fiecare material păstrează numele, autorul și accesul pe lecție; copiile identice folosesc o cheie de stocare comună. Curățarea periodică consolidează gradual și încărcările vechi. Ștergerea elimină imediat accesul prin rândul lecției; copia fizică se șterge doar când nu mai are referințe și a trecut o oră de grație.

`POST /parcursuri/{id}/incarcari` primește `{ nume, marime, sha256, inlocuieste }` și întoarce `{ token, offset, bucata }`. `PUT /incarcari/{token}?offset=N` primește maximum 1 MiB brut și avansează offsetul numai pentru o porțiune completă. `GET /incarcari/{token}` permite reluarea, `POST /incarcari/{token}/finalizeaza` verifică SHA-256 și folosește validarea normală a fișierelor; reîncercarea finalizării nu creează duplicate. `DELETE /incarcari/{token}` eliberează sesiunea. Sesiunile private, legate de autor și lecție, sunt păstrate 24 de ore pe disc în `Stocare:Director/incarcari`; maximum 10 sesiuni active per autor. Curățarea expiratelor rulează la pornirea unei încărcări noi. După reîncărcarea paginii, utilizatorul reselectează același fișier pentru reluare. Fotografiile/dovezile profesorilor păstrează fluxul multipart existent.
