# TeleBac pentru telefon (Android, iOS)

Aceeași platformă ca site-ul, în Capacitor: frontendul ca export static (`frontend/out`) în aplicație, cu modul offline (lecții
descărcate, teste notate pe loc, rezultate trimise la reconectare). Serverul se alege la prima pornire (doar HTTPS în build-ul
de lansare) și se poate schimba din Setări → Serverul aplicației.

- Cererile către server trec prin HTTP-ul nativ (CapacitorHttp): fără CORS, cookie-ul de cont în depozitul nativ.
- Jocul Triviador (WebSocket) primește un token de două minute valabil doar pentru joc.
- Fișierele lecțiilor se deschid cu o aplicație de pe telefon (PDF, Office), prin meniul de partajare.
- Butonul „înapoi” de pe Android merge înapoi în aplicație.

## Android (se construiește și pe Linux)
Cerințe: JDK 21, Android SDK cu platforma 36 și build-tools 36 (`ANDROID_HOME`).
```sh
npm install
npm run android:apk   # export + cap sync + gradlew assembleDebug → android/app/build/outputs/apk/debug/app-debug.apk
```
Pentru Google Play: `cd android && ./gradlew bundleRelease`, semnat cu cheia de lansare a proiectului (de creat și păstrat în
siguranță; nu în repo).

Build-ul debug permite HTTP doar către 127.0.0.1/localhost (`android/app/src/debug`), pentru stiva de test adusă cu
`adb reverse`. Testul cap-coadă: `npm run test:android` (emulator sau telefon în `adb devices`, stiva de test pornită).

## iOS (cere un Mac)
Proiectul e în `ios/` (Swift Package Manager, fără CocoaPods). Pe un Mac cu Xcode:
```sh
npm install
npm run ios:deschide  # export + cap sync + deschide Xcode
```
În Xcode: echipa de dezvoltare (cont Apple Developer, plătit) la Signing & Capabilities, apoi Product → Archive pentru
TestFlight / App Store. Pe Linux proiectul nu se poate compila.
