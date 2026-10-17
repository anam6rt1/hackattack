# Verificare implementare Code First

Verificat la 17 octombrie 2026 pe PostgreSQL 18.6, prin conexiunea Tailscale existenta.

- Compilare .NET 10: zero erori, zero avertismente.
- Migrarea InitialSchema reproduce semantic cele 28 de tabele originale: coloane, chei, relatii, CHECK-uri si indexuri.
- Migrarile InitialSchema si AddTrivia au fost aplicate pe baza existenta `postgres`, schema `mark_ai`.
- Rezultat: 35 de tabele de aplicatie, plus istoricul EF; 4 trigger-e pentru joc.
- Amprentele continutului celor 28 de tabele originale sunt identice inainte si dupa aplicare.
- Backup custom-format al schemei si datelor salvat local in `%LOCALAPPDATA%/MarkAi/backups`, in afara repository-ului.

Verificarile de integrare din `tools/MarkAi.Database/Verification.cs` ruleaza pe baze temporare reale, create si eliminate automat:

- creare de la zero folosind exclusiv migrarile C#;
- model sincronizat cu snapshotul EF;
- camera cu cod unic, gazda ocupa un loc;
- intrari simultane fara depasirea capacitatii;
- intrare idempotenta, intrare dupa pornire respinsa;
- pornirea rundelor doar de gazda, fara suprapunere;
- raspunsuri simultane duplicate: unul singur acceptat;
- raspunsurile persoanelor din afara camerei respinse;
- punctaj corect/incorect/neparticipare si controlul accesului la clasament;
- snapshoturi pastrate dupa editarea bancii de intrebari;
- scoruri si chei de raspuns ale rundelor nemodificabile;
- variante din alte runde respinse;
- raspuns intarziat respins chiar cu timestamp si punctaj falsificate;
- runda urmatoare dupa expirare, camera finalizata respinge raspunsuri;
- rollback/reaplicare a extensiei de joc fara pierderea entitatilor originale;
- adoptare a unei scheme existente cu date, repetarea adoptarii si normalizarea constrangerilor UNIQUE istorice.

Datele testelor nu au fost introduse in baza comuna. Nu sunt implementate in acest pas interfata, API-ul sau comunicarea live dintre jucatori.
