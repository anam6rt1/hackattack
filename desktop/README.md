# TeleBac pentru desktop (Windows, Linux)

Aceeași platformă ca site-ul, ca aplicație: frontendul ca export static (`frontend/out`) într-o fereastră Electron, cu modul
offline (lecții descărcate, teste notate pe loc, rezultate trimise la reconectare). Serverul se alege la prima pornire și se
poate schimba din Setări → Serverul aplicației.

## Cum funcționează
- `app://telebac/...` — fișierele site-ului, de pe disc (merg fără internet).
- `app://telebac/api/...` — trimise serverului de procesul principal (`main.js`), cu sesiunea Electron. Cookie-ul de cont
  rămâne la server și nu ajunge niciodată la pagină, ca pe site.
- Jocul Triviador (WebSocket) merge direct la server, cu un token de două minute valabil doar pentru joc.
- Fără server: pagina primește o eroare de rețea și trece pe lecțiile descărcate (`frontend/lib/offline`).

## Comenzi (din `desktop/`)
```sh
npm install
npm run site          # exportul static al frontendului (frontend/out)
npm start             # pornește aplicația (TELEBAC_SERVER=https://... alege un server implicit)
npm run dist:linux    # AppImage
npm run dist:win      # pe Windows: instalator NSIS + .exe portabil
```
Pe Linux, instalatorul Windows cere Wine; fără să instalezi nimic, folosește imaginea oficială electron-builder:
```sh
podman run --rm --userns=keep-id -e HOME=/tmp/acasa -e ELECTRON_CACHE=/cache/electron -e ELECTRON_BUILDER_CACHE=/cache/electron-builder \
  -v "$PWD/..":/project:Z -v "$HOME/.cache/electron":/cache/electron:Z -v "$HOME/.cache/electron-builder":/cache/electron-builder:Z \
  -w /project/desktop docker.io/electronuserland/builder:wine bash -c 'mkdir -p /tmp/acasa && npx electron-builder --win'
```
Rezultatul: `dist/TeleBac Setup <versiune>.exe` și `dist/TeleBac-<versiune>-portabil.exe`. Nu sunt semnate: Windows SmartScreen
avertizează la prima rulare până la semnarea cu un certificat de cod.

## Test cap-coadă
`npm test` pornește aplicația fără fereastră, pe stiva de test (API pe 5081, site pe 3001 — vezi `test-aplicatie.mjs`):
alegerea serverului, intrarea în cont, descărcarea unei lecții, testul fără server, trimiterea rezultatului.
