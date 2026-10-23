#!/usr/bin/env bash
# Pornește doar site-ul (Next.js, http://localhost:3000) pe calculatorul colegului; /api merge la API-ul serverului prin Tailscale.
# Datele sunt cele de pe server: orice modificare de aici se face în baza lui de date.
# Utilizare: ./client-setup.sh [server]   (implicit bazzite.tail37781b.ts.net; merge și IP-ul Tailscale, ex. 100.80.30.123)
# Închidere: ./client-setup.sh close (oprește site-ul și pune la loc frontend/.env.local de dinainte).
set -euo pipefail
cd "$(dirname "$0")"
STARE="$PWD/.client-setup"; mkdir -p "$STARE"
ENV=frontend/.env.local

opreste_site() {
  [[ -f "$STARE/site.pid" ]] && { kill -- "-$(cat "$STARE/site.pid")" 2>/dev/null || kill "$(cat "$STARE/site.pid")" 2>/dev/null || true; }
  # next dev pornește procese copil; oprește tot ce a rămas pe portul 3000.
  for pid in $( (lsof -ti tcp:3000 2>/dev/null || ss -ltnp 2>/dev/null | grep ':3000 ' | grep -oE 'pid=[0-9]+' | cut -d= -f2) | sort -u); do
    kill "$pid" 2>/dev/null || true
  done
  rm -f "$STARE/site.pid"
}

if [[ "${1:-}" == "close" ]]; then
  opreste_site
  if [[ -f "$STARE/env.inainte" ]]; then mv "$STARE/env.inainte" "$ENV"; echo "Pus la loc $ENV."
  elif [[ -f "$STARE/env.nou" ]]; then rm -f "$ENV"; echo "Șters $ENV (nu exista înainte)."; fi
  rm -rf "$STARE"
  echo "Închis."
  exit 0
fi

SERVER="${1:-bazzite.tail37781b.ts.net}"
API="http://$SERVER:5080"
[[ -f "$STARE/env.inainte" || -f "$STARE/env.nou" ]] && { echo "Deja pornit. Pentru a închide: ./client-setup.sh close"; exit 1; }

tailscale status >/dev/null 2>&1 || { echo "Tailscale nu e conectat. Pornește-l și autentifică-te (tailscale up), apoi reîncearcă."; exit 1; }
curl -sf -o /dev/null --max-time 5 "$API/materii" || {
  echo "API-ul serverului nu răspunde la $API."
  echo "Verifică: serverul a rulat ./server-setup.sh și aplicația lui e pornită; încearcă și cu IP-ul Tailscale în loc de nume."
  exit 1
}

# .env.local are prioritate față de .env; îl păstrăm pe al tău și îl punem la loc la close.
if [[ -f "$ENV" ]]; then cp "$ENV" "$STARE/env.inainte"; else touch "$STARE/env.nou"; fi
printf 'NEXT_PUBLIC_API_URL=/api\nAPI_INTERN_URL=%s\n' "$API" > "$ENV"

[[ -d frontend/node_modules ]] || (cd frontend && npm install)
opreste_site
# Ieșirea merge în jurnal, ca scriptul să se termine; setsid (dacă există) pune site-ul într-un grup propriu, oprit întreg la close.
PORNESTE=(npm run dev -- -p 3000); command -v setsid >/dev/null && PORNESTE=(setsid "${PORNESTE[@]}")
(cd frontend && nohup "${PORNESTE[@]}" < /dev/null > "$STARE/site.log" 2>&1 & echo $! > "$STARE/site.pid")

GATA=0
for _ in $(seq 1 90); do curl -sf -o /dev/null "http://localhost:3000/api/materii" && { GATA=1; break; }; sleep 1; done
[[ $GATA -eq 1 ]] || echo "ATENȚIE: site-ul nu răspunde după 90 de secunde. Vezi $STARE/site.log."
echo "Site: http://localhost:3000 (API: $API)"
echo "Jurnal: $STARE/site.log"
echo "Închidere: ./client-setup.sh close"
