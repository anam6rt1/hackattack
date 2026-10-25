# C1–C4, D1–D4 și pregătirea pilotului E

Implementare pe OptimizareV1, 25 octombrie 2026. Starea verificărilor este completată la finalul documentului.

## C: rapoarte, invitații, statistici, părinți

- `/organizatie/raport`: administratorul organizației aprobate vede agregate lunare. Fiecare bloc este agregat în SQL; raportul stă o oră în memoria procesului, cu autorizarea reverificată la fiecare cerere. Tipărirea browserului permite salvarea PDF. Calendar Europe/Bucharest.
- Cohorta este formată din elevii aprobați **în prezent**, inclusiv activitatea lor pe lecții publice. Nu există istoric complet al apartenențelor. Elev activ = ultima accesare a unei lecții în interval, răspuns de quiz în interval sau rezervare la o ședință încheiată în interval. Ultima accesare nu este jurnal de autentificări: revenirea într-o lună ulterioară poate muta indicatorul istoric al elevului.
- Lecții terminate = promovări elev–lecție. Teme = perechi elev–temă cu termen în lună, pentru publicul actual, completate înainte de sfârșitul lunii; include completări anterioare atribuirii, precum B1. Ședințe = camere încheiate, distincte, cu elevi din cohortă; nu se pretinde prezență fizică.
- Greșelile sunt atribuite lecției, nu unui capitol inventat. Corectitudinea este memorată la trimiterea răspunsului; modificarea ulterioară a variantelor nu rescrie statistica. Datele încep la activarea funcției; încercările vechi nu sunt reinterpretate cu răspunsurile curente. Include testele lecțiilor, nu Triviador, recapitularea sau simulările.
- Statisticile întrebărilor sunt paginate și disponibile editorilor lecției; cele cu procent corect mic apar primele. Legătura deschide întrebarea în editor. Numărul reprezintă răspunsuri/încercări, nu elevi unici.
- CSV: UTF-8, antet `nume,email,clasa`, virgulă și ghilimele standard, maximum 100 de elevi și 150 KB. Erori pe rând; trimiterea este blocată dacă există erori. Previzualizarea este păstrată 15 minute în memoria procesului, legată de administrator și organizație; restartul cere o nouă verificare.
- Invitațiile expiră după 7 zile, au token aleator de 256 biți, cu hash în tabel. Linkurile folosesc fragment, iar tokenul este transmis în corp POST. Conturile sunt create **numai la acceptarea explicită**, cu parolă aleasă de elev. Conturile existente trebuie autentificate și confirmate; nu li se schimbă parola. Invitațiile active duplicate sunt omise. Trimiterea și acceptarea sunt tranzacționale.
- Buget `Email:MaxPeZi` (implicit 500) pe ultimele 24 h, comun emailurilor rezervate în coadă. Procesorul verifică și livrările din ultimele 24 h înainte de expedierea cozii. Furnizorul poate avea limite suplimentare; mesajele de securitate expediate imediat își păstrează comportamentul existent.
- Sumarul pentru părinte este oprit implicit. Elevul solicită confirmarea, părintele confirmă explicit prin link valabil 7 zile. Maximum o cerere pe elev în 24 h, inclusiv după ștergerea setării. Luni, după încheierea săptămânii Europe/Bucharest, se pregătește un sumar pentru ultima săptămână completă; nu se recuperează în masă toate săptămânile unei întreruperi.
- Părintele nu primește cont, răspunsuri individuale, conversații sau drept de editare. Elevul poate șterge setarea; fiecare sumar conține oprire fără autentificare. Schimbarea adresei cere o nouă confirmare. Emailurile încă în coadă sunt reverificate înainte de expediere; o livrare deja în curs nu poate fi retrasă.
- Cheia de oprire este protejată cu ASP.NET Data Protection. Păstrați directorul de chei al aplicației între reporniri/deploy-uri. Datele C/E sunt incluse în export/ștergere A5. Retenția generală rămâne dezactivată, conform deciziei existente.

Configurare: `Aplicatie__Url=https://adresa-aplicatiei` și `Email__MaxPeZi=<limita-furnizorului>`. Pentru dezvoltare, URL implicit localhost:3000. În previzualizare emailurile sunt în jurnal, nu sunt expediate către persoane reale. Nu introduceți chei în Git.

## D: costuri și telefoane

- Tutor: ultimele cel mult 6 replici, plus un extras de maximum 600 de caractere din replicile mai vechi ale elevului. Extrasul este context citat, nu un rezumat semantic generat de AI; poate pierde detalii. Materialele sunt limitate la 3 × 2.500 caractere în loc de 3 × 8.000; catalogul la 12 titluri, filtrat după clasă și materia discutată, în loc de 80. Lista promovărilor are maximum 12 titluri. Instrucțiunile comune rămân la început pentru prefix stabil.
- Testul cu model fals măsoară caracterele cererii și dimensiunea istoricului, **nu tokeni facturați**. Nu există în această configurație o măsurare reală before/after cu tokenizerul furnizorului. Reducerea contextului poate necesita revenirea elevului la un detaliu din materialul integral.
- Cache explicit Gemini nu este activat: documentația furnizorului îl descrie ca funcție plătită, cu cost de stocare. Cache implicit poate fi folosit automat de modelele compatibile, fără garanția unui hit sau a unei economii. Sursă verificată: [documentația Google](https://ai.google.dev/gemini-api/docs/generate-content/caching?hl=en). Nu se pretinde economie facturată dintr-un test fals.
- Cache public cu maximum 512 intrări și TTL 2 minute: județe, organizații publice, materii pentru utilizatori anonimi. Răspunsurile `/materii` pentru utilizatorii autentificați **nu** sunt partajate: numărătorile depind de acces și progres. Mutările de catalog/conținut invalidează versiunea cache-ului; altfel expiră natural.
- D3: pe o copie restaurată cu date-volum (5.000 elevi, 2.000 lecții, 600 profesori), interogările reprezentative au avut `COUNT` lecții publice 0,400 ms, pagină simplă de 21 lecții 1,030 ms, `COUNT` profesori cu ședințe viitoare 0,417 ms. Sunt măsurători locale SQL încălzite, nu latențe HTTP și nici toate combinațiile de filtre. Nu justifică schimbarea contractului UI acum; totalurile sunt păstrate. Repetați pe infrastructura pilotului dacă latența crește.
- Lora/Pixelify nu mai sunt preîncărcate pe toate paginile; browserul descarcă fontul când stilul îl folosește. Cache Components și partial prefetching sunt dezactivate pentru a evita păstrarea paginilor grele ascunse. Pe ecrane de maximum 768 px/reduced motion se evită filtrul pe întreaga hartă.
- **Rămas pentru verificare:** Lighthouse mobil comparabil înainte/după și probă pe Android real. Nu există un scor inventat sau o promisiune de memorie/FPS măsurată.

## E: funcțiile pentru pilot

- Manifest instalabil și shell offline static. Din lecție, elevul salvează explicit textul capitolelor în IndexedDB; maximum 10 lecții și 2 MB. Nu se salvează automat documente încărcate, videoclipuri, sesiuni autentificate sau răspunsuri API în cache-ul service worker-ului.
- Opțional se pregătește o încercare reală de test online. Offline se aleg variante, păstrate local; la reconectare sunt trimise secvențial. Serverul verifică accesul, proprietarul încercării și răspunsurile și acordă progresul/XP. Un endpoint de confirmare permite reluarea după pierderea răspunsului HTTP fără retrimiterea răspunsurilor deja acceptate. Offline nu se calculează corectitudinea.
- Lecțiile salvate pot deveni vechi sau se poate revoca accesul; copiile locale deja descărcate nu pot fi retrase de la distanță de pe un dispozitiv fără internet. Se recomandă dispozitiv personal; copiile se elimină la ieșirea din cont, iar salvarea pentru alt cont curăță copiile anterioare. Pagina offline permite ștergere manuală. Fișierele locale nu sunt criptate separat de protecția dispozitivului/browserului.
- „Spune-ne ce nu merge”: maximum 5 mesaje/cont/24 h, maximum 2.000 caractere; fără query/fragment în pagina raportată. Administratorul vede lista paginată în `/admin/feedback` și marchează rezolvarea. Nu se trimit mesaje către un serviciu terț.

## Plan operațional pregătit pentru proprietar

1. Alegeți 2–3 școli (urban/rural) și câte un profesor coordonator. Notați datele școlii, județul, persoana de contact, aprobarea de participare și calendarul; nu sunt inventate contacte.
2. Pilot propus: acces gratuit școlilor, fără activarea încasărilor. Tarifele, contractele și fluxul de plăți se decid separat; nu se simulează o plată ca fiind încasată.
3. Înainte de invitațiile elevilor: profesorul verifică fiecare întrebare AI în editor (enunț, un singur răspuns corect, explicație, diacritice, legătura cu programa, drepturi pentru materiale). Începeți cu puține lecții verificate, nu cu întreaga bancă generată.
4. Săptămânal: administratorul citește feedbackul, selectează erorile care blochează învățarea, stabilește responsabil/termen și comunică profesorului coordonator rezolvările. Raportul lunar are definițiile de mai sus.
5. Pentru ISJ: pregătiți o fișă de o pagină cu scop, școli participante, durată, activități, protecția datelor, sprijinul cerut. Trimiterea/contactarea necesită cerere explicită și destinatari reali. Nu s-au făcut contacte.
6. Nu se folosesc în prezentare cifre de piață sau presupuse finanțări PNRR/UE deschise fără verificarea anunțului oficial și a eligibilității concrete. Nu sunt verificate apeluri pentru o școală încă nealeasă.

## Migrare și verificări

Migrațiile `RapoarteInvitatiiParintiC1C4` și `FeedbackPilotE` au fost aplicate pe o copie restaurată din backup; verificările de model, constrângeri, progres/XP și rezervări au trecut. Copia pentru volume este separată de previzualizare.

Verificări automate:
- 354 teste unitare .NET trecute.
- 31 teste de integrare finale trecute (C, buget email, feedback, notificări și date personale). Alte 6 teste de vizibilitate/context tutor au trecut în rularea anterioară, inclusiv limita de context.
- Vitest complet: 499/500 trecute inițial; unica diferență era lista meniului admin, extinsă cu Feedback. După actualizarea așteptării, toate cele 30 de teste din cele 3 fișiere relevante rerulate au trecut. Nu se pretinde că există o rulare unică finală 500/500.
- 3 teste Playwright trecute cu build de producție și bază izolată: teme, simulări/notificări, citire și test offline cu sincronizare la reconectare.
- ESLint pe fișierele modificate și verificarea whitespace au trecut.
- Migrațiile au fost repetate pe copie restaurată; baza locală de previzualizare a fost actualizată după backup, fără ștergerea fișierelor existente.

Rămân probele umane: instalare pe Android real/iOS, comparație Lighthouse mobil înainte/după, lizibilitate PDF tipărit, livrare și oprire prin furnizorul SMTP real, revizia profesorilor și alegerea școlilor pilot. Nu s-au trimis emailuri reale și nu s-au contactat școli/ISJ.
