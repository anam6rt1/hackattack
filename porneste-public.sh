#!/usr/bin/env bash
# Pornește site-ul accesibil de pe orice dispozitiv: tunel Cloudflare → site (Next.js, :3000) → /api → API (.NET, 127.0.0.1:5080).
# Secretele NU sunt în repo; se citesc din ~/.config/markai/markai.env (vezi markai.env.example și incarca-setari.sh).
# Adresa tunelului se schimbă la fiecare pornire și e afișată la final.
# Oprire: ./porneste-public.sh stop
set -euo pipefail
cd "$(dirname "$0")"
LOG="${XDG_STATE_HOME:-$HOME/.local/state}/markai"; mkdir -p "$LOG"

opreste() {
  pkill -x cloudflared 2>/dev/null || true
  for port in 3000 5080; do
    for pid in $(ss -ltnp 2>/dev/null | grep ":$port " | grep -oP 'pid=\K[0-9]+'); do kill "$pid" 2>/dev/null || true; done
  done
  distrobox stop pgSQL 2>/dev/null || true
}
if [[ "${1:-}" == "stop" ]]; then opreste; echo "Oprit."; exit 0; fi
opreste; sleep 2

. ./incarca-setari.sh

distrobox enter pgSQL -- true >/dev/null 2>&1 || true   # pornește containerul cu PostgreSQL, dacă e oprit

# Procesele de fundal nu păstrează ieșirea scriptului (altfel `./porneste-public.sh | ...` nu s-ar mai termina).
setsid cloudflared tunnel --no-autoupdate --url http://localhost:3000 < /dev/null > "$LOG/tunel.log" 2>&1 &
for _ in $(seq 1 40); do URL=$(grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' "$LOG/tunel.log" | head -1 || true); [[ -n "$URL" ]] && break; sleep 1; done
[[ -n "${URL:-}" ]] || { echo "Tunelul nu a pornit; vezi $LOG/tunel.log"; exit 1; }

(cd database && Twilio__UrlPublic="$URL/api" ASPNETCORE_URLS=http://127.0.0.1:5080 \
  setsid dotnet run --project src/MarkAi.Api --no-launch-profile < /dev/null > "$LOG/api.log" 2>&1 &) < /dev/null > /dev/null 2>&1
(cd frontend && NEXT_PUBLIC_API_URL=/api npm run build < /dev/null > "$LOG/build.log" 2>&1 \
  && setsid npm run start -- -p 3000 < /dev/null > "$LOG/site.log" 2>&1 &) < /dev/null > /dev/null 2>&1

GATA=0
for _ in $(seq 1 120); do curl -sf -o /dev/null "http://localhost:3000/api/materii" && { GATA=1; break; }; sleep 1; done
if [[ $GATA -eq 0 ]]; then
  echo "ATENȚIE: site-ul nu răspunde corect după 2 minute (bază de date oprită? eroare la build?)."
  echo "Verifică jurnalele din $LOG (api.log, build.log, site.log)."
fi
echo "Site: $URL"
# Generează un cod QR pentru adresa publică a site-ului.
if command -v qrencode >/dev/null 2>&1; then
  qrencode -t ANSIUTF8 "$URL"
  qrencode -o "$LOG/site-qr.png" -s 8 -m 2 "$URL"
  echo "Cod QR salvat în: $LOG/site-qr.png"
else
  echo "Instalează qrencode cu: brew install qrencode"
fi
echo "Webhook Twilio (dacă folosiți număr de intrare): $URL/api/twilio/voce   ·   stare: $URL/api/twilio/voce/stare"
echo "Jurnale: $LOG"
