# Optimizarea încărcărilor

Fotografia profesorului este optimizată înainte de upload, atât la înscriere,
cât și în setările contului. Utilizatorul poate bifa „Păstrează fotografia originală”.
Conversia locală în WebP folosește un Web Worker și OffscreenCanvas, maximum
2048 px pe latura lungă, calitate 0,88. Imaginea nu este mărită.

Se procesează numai JPEG, PNG și WebP între 128 KiB și 16 MiB. Imaginile
decodate peste 24 megapixeli sunt ignorate; decodarea însă poate necesita memorie
înainte de verificarea dimensiunilor. Workerul este oprit după maximum 12 secunde.
La eroare, browser incompatibil sau economie sub 5%, se încarcă originalul.
Serverul continuă să verifice dimensiunea, extensia și semnătura fișierului.
Optimizarea nu garantează încadrarea în limita de 5 MB a fotografiei.

Dovezile calificării și documentele PDF/DOCX/PPTX se păstrează integral.
Aplicația actuală nu are upload de audio, video, text sau arhive; nu sunt
introduse encodere pentru aceste formate și nici conversie pe server.
Gzip nu este aplicat documentelor deoarece API-ul și cititorul de text
așteaptă fișierele originale, nu un înveliș comprimat.

Verificări independente de Vitest:

```sh
node --experimental-default-type=module --test teste/optimizare-fisiere.node.mjs
```
