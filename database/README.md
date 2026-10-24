# Mark AI — baza de date

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

API-ul folosește LibreOffice headless pentru o copie PDF temporară, afișată în același vizualizator ca PDF-urile. Originalul rămâne neschimbat. Necesită LibreOffice pe server (Linux: `/usr/bin/libreoffice`; Windows: instalarea standard). Pentru altă locație setează `Previzualizare__LibreOffice` la calea absolută a executabilului. Previzualizarea locală recunoaște și `%USERPROFILE%\.cache\markai-libreoffice\program\soffice.exe`.

Conversiile sunt serializate, au maximum 90 de secunde (inclusiv așteptarea) și produc cel mult 20 MB. Închiderea previzualizării anulează cererea și procesul; copiile temporare sunt șterse. Fiecare conversie folosește un profil separat, cu macrocomenzi dezactivate. Pentru documente neîncrezătoare, rulați API-ul/converterul într-un mediu izolat fără acces la rețeaua internă. Instalați aceleași fonturi pe server pentru paginare cât mai apropiată de Office; foile Excel folosesc setările de tipărire ale documentului.

`GET /fisiere/{id}/previzualizare` respectă accesul la lecție; `POST /fisiere/previzualizare` primește un document selectat de editor și aplică aceleași verificări ca încărcarea. Niciuna dintre rute nu trimite documentele către un serviciu extern.
