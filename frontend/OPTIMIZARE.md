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
Office: recompresie ZIP nivel 9, păstrând fiecare fișier intern identic octet cu
octet după dezarhivare; documentele semnate sunt păstrate originale. Containerul
rezultat diferă de original, deci aceste operații nu promit identitate binară a
întregului fișier. Nu se reduc rezoluția sau calitatea imaginilor din documente.

Documentele sunt procesate secvențial, într-un worker, maximum 20 MB la intrare,
20 secunde per fișier, maximum 500 pagini PDF sau 64 MB/5000 intrări Office la
dezarhivare. Fișierele sub 32 KiB sunt păstrate originale. La erori, depășirea
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
introduse encodere pentru aceste formate și nici conversie pe server.
Gzip nu este aplicat documentelor deoarece API-ul și cititorul de text
așteaptă fișierele originale, nu un înveliș comprimat.

Fotografia se pregătește imediat după selecție, înainte de trimitere: se pot
previzualiza fotografia care va fi încărcată. Schimbarea
selecției sau ieșirea din pagină oprește workerul vechi.

Formularele profesorului și fișierele lecției folosesc transfer multipart cu
progres real, anulare și mesaje pentru timeout/rețea. Anularea nu poate retrage
un fișier deja salvat pe server; lista trebuie verificată înainte de reîncercare.
La lecții, fișierele selectate se trimit numai după apăsarea butonului de upload;
cele nereușite rămân în selecție, iar cele deja salvate sunt eliminate din ea.

Imaginile se deschid prin URL-uri locale revocate la închidere. PDF-urile sunt
redate local cu PDF.js, cu navigare între pagini și text accesibil separat;
workerul este oprit la închidere, iar originalul rămâne disponibil într-o filă nouă.
Office este citit pe dispozitiv, fără servicii externe: text Word, textul
diapozitivelor în ordinea prezentării și celulele Excel, inclusiv valori memorate
ale formulelor. Nu se reproduc aspectul original, imagini sau grafice Office.
Sunt afișate maximum 200 paragrafe, 20 diapozitive sau 5 foi cu 50 rânduri și
20 coloane fiecare. Dezarhivarea rulează într-un worker, cu limite de memorie
și timp; documentele prea complexe primesc un mesaj și rămân descărcabile.
Fișierele salvate se citesc prin API-ul autentificat, cu o limită de 20 MB pentru
previzualizare. Formatele vechi DOC/PPT/XLS necesită salvare în DOCX/PPTX/XLSX.

Verificări independente de Vitest:

```sh
node --test teste/optimizare-fisiere.node.mjs
```
