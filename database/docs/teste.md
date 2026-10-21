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

Aceeași aplicație și aceleași fixturi (george, irina, admin, director; parola `Test-demo-2026!`), cu ceasul real, serviciile de fundal
pornite și furnizorul „jurnal” (codurile apar la admin, pagina „Mesaje trimise”). Baza e o copie de unică folosință. Un frontend de test
trebuie construit cu `API_INTERN_URL=http://127.0.0.1:5081`, altfel `/api` ajunge la API-ul real.

## Reguli
- Un test care pică uneori se repară (sau se pune deoparte cu o problemă deschisă) înainte de merge; nu se reîncearcă orbește.
  Cursa cunoscută: după `HubConnection.StopAsync`, serverul vede deconectarea puțin mai târziu, deci ceasul fals se mută în buclă
  până la condiție (`PrezentaTriviadorTests.AvanseazaPanaCand`).
- Testele noi stau lângă zona lor (`Conturi/`, `Securitate/`, `Continut/`, `Lectii/`, `Fisiere/`, `Cautare/`, `Organizatii/`,
  `Triviador/`, `Twilio/`) și pornesc de la clasele de bază ale zonei (`TestLectii`, `TestFisiere`, `TestCautare`, …).
