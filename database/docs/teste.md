# Teste automate (backend)

Două proiecte xUnit (xunit.v3 pe Microsoft.Testing.Platform, aserțiuni Shouldly), rulate cu o singură comandă din `database/`:

```bash
cd database
# pe Bazzite/Fedora cu podman (o singură dată pe sesiune: socket-ul podman pornit)
systemctl --user start podman.socket
export DOCKER_HOST=unix:///run/user/$(id -u)/podman/podman.sock
dotnet test                                                    # tot (unit + integrare)
dotnet test --project tests/MarkAi.Tests.Unit                  # doar unit (câteva secunde, fără bază)
dotnet test --project tests/MarkAi.Tests.Integration           # doar integrare
dotnet test --project tests/MarkAi.Tests.Integration -- --filter-namespace "*Cautare"   # o zonă
dotnet test --project tests/MarkAi.Tests.Integration -- --filter-class "*QuizTests"     # o clasă
```

- `tests/MarkAi.Tests.Unit`: logică pură, fără bază de date (validări, normalizarea căutării, regulile de acces pe date în
  memorie, hărțile Triviador pentru toate mărimile 9–30, tokenuri și coduri, șabloane de email/SMS, verificarea fișierelor,
  extragerea textului, maparea erorilor).
- `tests/MarkAi.Tests.Integration`: API-ul real (`AplicatieMarkAi`, același pipeline ca `Program.cs`) pe Kestrel, pe un port liber,
  cu PostgreSQL 18 într-un container Testcontainers. Fiecare test primește aplicația lui și o copie proaspătă a bazei
  (`CREATE DATABASE … TEMPLATE` dintr-un șablon migrat și populat o singură dată), deci testele nu depind unele de altele.
  Clasele rulează în paralel; testele Twilio rulează singure (colecția „Twilio”: credențialele sunt variabile de mediu ale procesului).
- `tests/MarkAi.Tests.Suport`: `Harness` (aplicația de test), inlocuitorii falși (email, SMS, ceas, AI, API-ul Twilio servit de
  aplicația de test), `BazaTest` (Testcontainers / șablonul), fixturile și documentele de test (PDF, DOCX, PPTX, bombă zip).

Nimic nu ajunge la baza reală, la AI, la Twilio sau la SMTP: setările reale sunt scoase din proces înainte de pornire, deci testele
pot rula și dintr-un terminal care a încărcat `markai.env`. Serviciile de fundal nu pornesc singure; un test le rulează cu
`h.Serviciu<...>().RuleazaOdataAsync(...)`, iar timpul se mută cu `h.Ceas.Advance(...)`.

Un server PostgreSQL existent poate înlocui containerul: `MARKAI_TEST_CONNECTION='Host=…;Username=…;Password=…'` (un utilizator cu
CREATEDB, niciodată baza reală). Un test eșuat afișează ultimele 40 de linii din jurnalul aplicației.

## API de test pentru încercări manuale și Playwright

```bash
dotnet run --project tools/MarkAi.Previzualizare            # 127.0.0.1:5081
dotnet run --project tools/MarkAi.Previzualizare -- --port 5090
```

Aceeași aplicație și aceleași fixturi (george, irina, admin, director; parola `Test-demo-2026!`), plus ședințele programate din
`date-test` (`tools/MarkAi.Database/SedinteTest.cs`: irina are trei, director niciuna, george e înscris la câteva), cu ceasul real, serviciile de fundal
pornite și furnizorul „jurnal” (codurile apar la admin, pagina „Mesaje trimise”). Baza e o copie de unică folosință. Un frontend de test
trebuie construit cu `API_INTERN_URL=http://127.0.0.1:5081`, altfel `/api` ajunge la API-ul real.

## Teste în browser și CI

Testele Playwright (frontend/e2e, `npm run test:e2e`) pornesc singure `tools/MarkAi.Previzualizare --e2e` (fără limite de cereri,
fără pauză între teste) și un build separat al site-ului; vezi frontend/README.md. GitHub Actions (`.github/workflows/teste.yml`)
rulează la fiecare PR și push pe main: `dotnet test`, lint + Vitest + build, apoi Playwright în Chromium, Firefox și WebKit
(motorul din Safari), câte un job pe browser. Eșecurile WebKit apar în raport, dar nu opresc încă rularea. Pe Bazzite WebKit
rulează doar în containerul oficial Playwright: `npm run test:e2e:webkit`.

## Date de volum (pentru a vedea listele pe pagini)

```bash
dotnet run --project tools/MarkAi.Database -- date-volum            # ~51 000 de conturi vol-… (50 000 de elevi, 1 000 de profesori), 180 de organizații, 5–10 lecții de fiecare profesor aprobat (~7 500)
dotnet run --project tools/MarkAi.Database -- date-volum --sterge   # scoate exact ce a generat
```

Pe baza din `~/.config/markai/markai.env`, din radacina repo-ului: `./dbmg.sh volum status|add|remove` (face backup inainte de add/remove,
iar remove arata ce conturi reale sunt legate de datele de volum si cere confirmare).

Doar pe baze de test sau demo. Toate conturile sunt `vol-…@volum.hackattack.com`, cu parola din `date-test`. Mărimile paginilor:
`src/MarkAi.Api/Infrastructura/Pagini.cs` (limitele API-ului) și `frontend/lib/pagini.js` (ce arată site-ul).

## Test de stres (multe apeluri deodată)

`tools/MarkAi.Stres` trimite la un API pornit, pentru fiecare scenariu, câte N apeluri deodată (pornite toate în același moment)
și arată cum a răspuns: coduri, apeluri refuzate de limitator (429), erori și timeout-uri, timpi (p50/p95/p99/max), apeluri pe
secundă. Iese cu 1 dacă o treaptă depășește pragurile (`--prag-erori`, implicit 1 %; `--prag-p95`, implicit 2000 ms).

Niciodată pe API-ul live (5080; scriptul refuză 5080 și 3000): pe o bază de test cu datele de volum și un API separat,
cu limitele ridicate, ca să măsori API-ul și nu limitatorul:

```bash
podman run -d --name markai-stres -e POSTGRES_PASSWORD=stres -p 5433:5432 postgres:18
export MARKAI_CONNECTION_STRING="Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=stres"
for pas in migrate seed-localitati seed date-volum; do dotnet run --project tools/MarkAi.Database -- $pas; done   # date-volum: câteva minute
# jurnal: niciun email sau SMS real, chiar dacă terminalul are deja setările din markai.env
MARKAI_JWT_KEY=$(openssl rand -base64 48) Email__Furnizor=jurnal Sms__Furnizor=jurnal ASPNETCORE_URLS=http://127.0.0.1:5082 \
  Limite__CereriPeMinut=1000000 Limite__AutentificariPeMinut=1000000 dotnet run --project src/MarkAi.Api -c Release --no-launch-profile
# în alt terminal:
dotnet run --project tools/MarkAi.Stres -c Release -- --url http://127.0.0.1:5082 --cereri 100,500,1000,2000 --clienti 200
```

Opțiuni utile: `--scenarii lectii,cautare` (implicit toate: materii, judete, organizatii, profesori, lectii, lectie, cautare,
progres, eu, autentificare, mix), `--rata 200 --durata 30` (încărcare constantă: 200 de apeluri pe secundă timp de 30 s, mai aproape de traficul real decât o
rafală), `--concurenta 100` (la rafale: cel mult atâtea în zbor, în loc de toate deodată), `--ip-uri-diferite`
(fiecare client cu adresa lui, ca limitele pe IP să se vadă ca la utilizatori reali; doar conectat direct la API de pe același
calculator), `--ajutor` pentru restul. Scenariile cu cont intră ca `vol-elev00001` … `vol-elevN` (N = `--clienti`), cu parola din
`date-test`. Fără limite ridicate, apelurile peste `Limite:CereriPeMinut` (600/minut pe utilizator sau IP) și peste 10 autentificări
pe minut pe IP primesc 429; scriptul le numără separat, nu ca erori. `autentificare` verifică parola la fiecare apel, deci arată
cât duce procesorul.

În CI (`.github/workflows/teste.yml`, jobul „Stres (API)”), la fiecare PR și push pe `main`: aceeași pregătire pe un Postgres
de unică folosință, apoi rafalele de 100, 500 și 1000 de apeluri și 15 secunde la 200 de apeluri pe secundă, cu 100 de clienți; autentificarea
doar cu o rafală de 100 (verificarea parolei e lentă intenționat, așa că la 500+ ar pica pe orice mașină, la fiecare rulare). Tabelul cu statistici apare la finalul jurnalului și pe
pagina rulării (Summary). Jobul nu pică build-ul (`continue-on-error`), fiindcă viteza mașinilor GitHub variază de la o rulare la alta.

## Reguli
- Un test care pică uneori se repară (sau se pune deoparte cu o problemă deschisă) înainte de merge; nu se reîncearcă orbește.
  Cursa cunoscută: după `HubConnection.StopAsync`, serverul vede deconectarea puțin mai târziu, deci ceasul fals se mută în buclă
  până la condiție (`PrezentaTriviadorTests.AvanseazaPanaCand`).
- Testele noi stau lângă zona lor (`Conturi/`, `Securitate/`, `Continut/`, `Lectii/`, `Fisiere/`, `Cautare/`, `Organizatii/`,
  `Triviador/`, `Twilio/`) și pornesc de la clasele de bază ale zonei (`TestLectii`, `TestFisiere`, `TestCautare`, …).
