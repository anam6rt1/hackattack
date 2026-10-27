# Optimizarea încărcărilor

Fotografia profesorului este optimizată înainte de upload, atât la înscriere,
cât și în setările contului. Se afișează doar fotografia pregătită, fără comparații
sau informații despre optimizare.
Conversia locală în WebP folosește un Web Worker și OffscreenCanvas, maximum
2048 px pe latura lungă, calitate 0,88. Imaginea nu este mărită.

Se procesează numai JPEG, PNG și WebP între 128 KiB și 16 MiB. Imaginile
decodate peste 24 megapixeli sunt ignorate; decodarea însă poate necesita memorie
înainte de verificarea dimensiunilor. Workerul este oprit după maximum 12 secunde.
La eroare, browser incompatibil sau economie sub 5%, se încarcă originalul.
Serverul continuă să verifice dimensiunea, extensia și semnătura fișierului.
Optimizarea nu garantează încadrarea în limita de 5 MB a fotografiei.

Dovezile calificării se păstrează originale. Documentele lecției PDF/DOCX/PPTX/XLSX
au optimizare automată înainte de upload. Interfața arată doar fișierul pregătit,
cu numele, dimensiunea și o singură previzualizare, fără comparații sau detalii
despre optimizare. În timpul pregătirii apare doar „Se pregătește fișierul…”.
PDF: rescriere structurală cu obiecte comprimate, fără rasterizare sau re-encodarea
imaginilor; formularele, semnăturile și protecțiile determină păstrarea originalului.
Office (decizia proprietarului, planul 4): textul, XML-ul și relațiile rămân identice
octet cu octet, dar imaginile din folderele media (PNG/JPEG) sunt micșorate mereu,
cu o pierdere mică de calitate:
- latura lungă a imaginii este redusă la cel mult 1920 px;
- fotografiile PNG opace (cel puțin 1024 de culori, fără transparență) devin JPEG
  calitate 0,85; relațiile (.rels) și [Content_Types].xml sunt actualizate;
- diagramele, desenele cu paletă și imaginile transparente rămân PNG;
- toate PNG-urile rămase sunt recomprimate fără pierderi (nivel 9 cât ține bugetul
  de 16 MiB pixeli per document, apoi fluxul nativ al browserului, mai rapid);
- fotografiile JPEG rotite prin EXIF nu sunt atinse, ca să nu apară întoarse;
- o imagine al cărei rezultat ar fi mai mare rămâne cum era.
Documentele semnate sunt păstrate originale. Pe prezentările testate (25 oct. 2026):
21,2 → 7,9 MiB (-63%), 4,2 → 1,8 MiB (-57%), 3,4 → 1,0 MiB (-70%), 0,7 → 0,55 MiB
(-20%), în 1–6 secunde, în Chromium și Firefox. Rezultatele depind de document.

Documentele sunt procesate secvențial, într-un worker, maximum 64 MB la intrare
(un fișier peste limita de upload poate încăpea după micșorarea imaginilor),
20 secunde plus o secundă per MiB (maximum 90), maximum 500 pagini PDF sau
160 MB/5000 intrări Office la dezarhivare. Fișierele sub 32 KiB sunt păstrate originale. La erori, depășirea
limitelor, rezultat mai mare sau economie sub 5%, se trimite originalul și motivul
rămâne intern, fără a încărca interfața cu detalii tehnice. Schimbarea selecției anulează workerul. Nicio reducere nu este garantată.

Datele exacte despre original, rezultat și economii sunt păstrate în procesare și
verificate prin teste; nu sunt afișate utilizatorului în selecția fișierelor. MiB
înseamnă 1024 × 1024 octeți. O schimbare de afișaj de la 1,24 la 1,2 nu înseamnă
compresie. Fișierele deja salvate nu sunt rescrise automat.

Verificarea unui PDF de 70 pagini: 1.301.840 → 878.801 octeți (32,50% economie),
cu toate textele pe pagini egale și toate cele 70 de randări la 72 DPI identice
pixel cu pixel. Acest rezultat descrie documentul testat, nu toate PDF-urile.
Aplicația actuală nu are upload de audio, video, text sau arhive; nu sunt
introduse encodere pentru aceste formate. Conversia Office pe server este folosită doar pentru previzualizare.
Gzip nu este aplicat documentelor deoarece API-ul și cititorul de text
așteaptă fișierele originale, nu un înveliș comprimat.

Fotografia se pregătește imediat după selecție, înainte de trimitere: se pot
previzualiza fotografia care va fi încărcată. Schimbarea
selecției sau ieșirea din pagină oprește workerul vechi.

Formularele profesorului folosesc transfer multipart cu progres real și anulare.
Fișierele lecției se încarcă în porțiuni de 1 MiB, cu sesiuni autentificate păstrate 24 de ore.
Reîncercarea verifică porțiunile deja primite; după reîncărcarea paginii, reselectează același fișier.
Identificatorul sesiunii este păstrat local, iar hash-ul SHA-256 verifică întregul document la final.
Anularea oprește transferul; porțiunile rămân temporar disponibile pentru reluare.
La lecții, fișierele selectate se trimit numai după apăsarea butonului de upload;
cele nereușite rămân în selecție, iar cele deja salvate sunt eliminate din ea.

Imaginile se deschid prin URL-uri locale revocate la închidere. PDF-urile sunt
redate local cu PDF.js, cu navigare între pagini și text accesibil separat;
workerul este oprit la închidere, iar originalul rămâne disponibil într-o filă nouă.
Word, PowerPoint și Excel se convertesc în copii PDF pe serverul aplicației, fără servicii externe.
PDF.js oferă zoom 50–300%, potrivire la lățime și ecran complet (Escape închide).
Pentru Excel alegi foaia; copia de previzualizare ascunde celelalte foi și potrivește tabelul pe lățime,
păstrând datele/formulele și paginarea verticală. Originalul nu este modificat.
Copiile PDF se păstrează pe disc după repornire, separate pe hash și foaie, cu buget de 512 MiB.
Fișierele identice din lecții au aceeași copie fizică, însă numele și accesul rămân pe lecție.
Ștergerea elimină referința; copia fizică dispare prin curățarea orfanelor, după perioada de grație.
Fișierele salvate se citesc prin API-ul autentificat, cu o limită de 20 MB pentru
previzualizare. Formatele vechi DOC/PPT/XLS necesită salvare în DOCX/PPTX/XLSX.

Verificări independente de Vitest:

```sh
node --test teste/optimizare-fisiere.node.mjs
```
