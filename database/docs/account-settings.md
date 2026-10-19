# Setarile contului — branch feature/account-settings

Pagina noua: `/setari`, accesibila din meniul contului prin **Setarile contului**.
Pentru predarea codului C#, export SQL offline si aplicarea ulterioara de catre administrator, vezi [instructiunile de migrare](account-settings-migrations.md).
Schimbarile nu sunt aplicate pe `main` si nu necesita migrare de schema. Se folosesc utilizatorii, profilurile si stocarea existente.

## Campuri dupa rol

- Elev: nume/prenume, utilizator, telefon optional, email si parola cu confirmare separata.
- Profesor: aceleasi date, telefon obligatoriu, materii predate, descriere publica, fotografie si dovada calificarii.
- Administrator: propriile date de cont si securitate; nu poate schimba rolul folosind formularul.
- Schimbarea materiilor sau a dovezii calificarii readuce profesorul in asteptarea verificarii. Nu se schimba onorariul, rolul sau comisionul din aceste rute.

## Pornire

Porneste backendul existent cu `MARKAI_CONNECTION_STRING` si configurarea JWT uzuala:

```bash
dotnet run --project database/src/MarkAi.Api
```

Din `frontend`, configureaza `NEXT_PUBLIC_API_URL=/api` si `API_INTERN_URL=http://localhost:5080`, apoi:

```bash
npm ci
npm run dev
```

Intra cu un cont existent si deschide `/setari`. Codul nu contine conturi demonstrative in aplicatia reala.

## Trimiterea codurilor prin email

Pe server configureaza variabilele de mediu, fara a comite parole:

```text
SetariEmail__Host=smtp.furnizorul-tau.ro
SetariEmail__Port=587
SetariEmail__Tls=true
SetariEmail__Expeditor=adresa-verificata-la-furnizor
SetariEmail__Utilizator=utilizator-smtp
SetariEmail__Parola=secret-smtp
```

Senderul foloseste SMTP cu STARTTLS, nu portul 465 cu TLS implicit. Foloseste o adresa de expeditor autorizata de furnizor.
Daca trimiterea nu este configurata sau esueaza, API-ul raspunde 503, provocarea este anulata si pagina afiseaza o eroare reala. Nu exista un cod fix, un cod returnat in JSON sau un fallback care simuleaza emailul in productie.

## Confirmarea in doi pasi

1. Utilizatorul introduce parola actuala si parola noua (sau noul email).
2. Serverul verifica parola, creeaza un cod aleator cu 6 cifre si il trimite la **emailul actual**.
3. Codul expira in 5 minute, este legat de utilizator si actiune, poate fi incercat de maximum 5 ori si este consumat atomic o singura data.
4. Serverul pastreaza numai hash-ul codului si hash-ul noii parole. Schimbarea emailului/parolei intre timp invalideaza actualizarea in asteptare.

O cerere noua poate fi initiata cel mult o data pe minut per cont. Codurile sunt in memorie: repornirea serverului le invalideaza; pentru mai multe instante este necesar un depozit distribuit.
Acesta este un pas suplimentar pentru **schimbarea parolei/emailului**, nu o activare globala a MFA la autentificare. Autentificarea existenta nu a fost rescrisa; tokenurile JWT deja emise nu sunt revocate automat la schimbarea parolei. Nu exista inca un flux pentru recuperarea unui email la care utilizatorul nu mai are acces.

## Endpointuri noi

Toate sunt sub `/utilizatori/me/setari` si cer sesiune autentificata:

- `GET /`: datele proprii, inclusiv profilul de profesor unde este cazul.
- `PATCH /`: date personale, descriere si materii dupa rol.
- `POST /documente`: multipart cu poza/dovada, doar profesor.
- `POST /confirmari`: initiere schimbare parola/email cu parola actuala.
- `POST /confirmari/finalizeaza`: confirmare prin ID si cod.

Modificarile cer headerul `X-MarkAi-Settings: 1`, impiedicand formularele cross-site simple sa utilizeze cookie-ul de sesiune. Frontendul foloseste mecanismul CORS si proxy-ul deja existente.
Rolul si utilizatorul sunt determinate de server; nu se accepta un ID de cont sau un rol din formular.
Fisierele noi sunt salvate prin serviciul existent de stocare (poza max. 5 MB, dovada max. 10 MB), fara suprascrierea sau stergerea fisierelor vechi.

## Verificari

```bash
# Server PostgreSQL de unica folosinta (nu baza reala); fiecare grup de verificari isi creeaza si sterge propria baza.
podman run --rm -d --name markai-test -e POSTGRES_PASSWORD=test -p 127.0.0.1:55432:5432 docker.io/library/postgres:18
export MARKAI_TEST_CONNECTION='Host=127.0.0.1;Port=55432;Username=postgres;Password=test'
dotnet run --project database/tests/MarkAi.AccountSettings.Checks            # toate grupurile
dotnet run --project database/tests/MarkAi.AccountSettings.Checks -- setari  # un singur grup
podman stop markai-test
```

Verificarile pornesc aplicatia reala (`Infrastructura/AplicatieMarkAi.cs`, aceeasi ca `Program.cs`) cu inlocuitori falsi:
email (`FakeMail`), ceas (`FakeTimeProvider`, inclusiv temporizatoare), model AI (`FakeAi`) si API-ul REST Twilio (`FakeTwilio`,
servit chiar de aplicatia de test). Inainte de pornire sterg din proces setarile reale (conexiunea, cheile AI, Twilio, SMTP),
deci pot rula si dintr-un terminal care a incarcat `markai.env`. Serviciile de fundal nu pornesc singure: o verificare le
ruleaza cu `h.Serviciu<...>().RuleazaOdataAsync(...)`. Fiecare faza adauga un fisier `Verificari*.cs` si il inregistreaza in `Program.cs`.

Grupuri: `infrastructura` (inlocuitorii sunt cei folositi, generarea de intrebari ruleaza la cerere), `twilio-fals`
(„Suna-ma” ajunge doar la API-ul Twilio fals) si `setari`, cu 23 de verificari pe PostgreSQL real: autentificare, roluri, salvare, telefon, unicitate, protectia modificarilor, OTP legat de cont, cod gresit, expirare, reutilizare, parole vechi/noi, email si erori de livrare. Emailurile sunt interceptate de un sender fals exclusiv in acest executabil de test.

Frontend verificat suplimentar in browser pe desktop si la latime 390px: salvare reala pe baza temporara, notificare de succes, meniu, dialog OTP, campuri de profesor si absenta scrollului orizontal. Buildul Next.js include ruta `/setari`.
