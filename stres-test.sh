#!/usr/bin/env bash
# Testul de stres al API-ului, pe o bază de test separată (nu pe cea live): containerul Postgres markai-stres (port 5433, cu datele
# de volum, create doar prima dată, ~2 minute), un API de test pe 127.0.0.1:5082 cu limitele ridicate, apoi tools/MarkAi.Stres.
# Tabelul cu statistici apare la final. API-ul de test se oprește la sfârșit; baza rămâne pentru data viitoare.
# Utilizare: ./stres-test.sh [opțiuni MarkAi.Stres]   (implicit --cereri 100,500,1000 --clienti 100; --ajutor le arată pe toate)
# Ștergerea bazei de test: ./stres-test.sh sterge
set -euo pipefail
cd "$(dirname "$0")/database"
LOG="${XDG_STATE_HOME:-$HOME/.local/state}/markai"; mkdir -p "$LOG"
CONTAINER=markai-stres PORT_API=5082

if [[ "${1:-}" == "sterge" ]]; then podman rm -f "$CONTAINER" >/dev/null 2>&1 || true; echo "Baza de test ștearsă."; exit 0; fi
if [[ "${1:-}" =~ ^(--ajutor|--help|-h)$ ]]; then dotnet run --project tools/MarkAi.Stres -c Release -- --ajutor; exit 0; fi
if ss -ltn | grep -q ":$PORT_API "; then echo "Portul $PORT_API e deja folosit (un API de test pornit de mână?). Oprește-l și reîncearcă."; exit 1; fi

# Doar baza de test: nimic din markai.env (cheia AI, SMTP, Twilio), chiar dacă terminalul le are deja.
unset GEMINI_API_KEY ANTHROPIC_API_KEY TWILIO_ACCOUNT_SID TWILIO_AUTH_TOKEN SetariEmail__Host
export MARKAI_CONNECTION_STRING="Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=stres"

if ! podman container exists "$CONTAINER"; then
  podman run -d --name "$CONTAINER" -e POSTGRES_PASSWORD=stres -p 127.0.0.1:5433:5432 docker.io/library/postgres:18 >/dev/null
fi
podman start "$CONTAINER" >/dev/null
for _ in $(seq 1 30); do podman exec "$CONTAINER" pg_isready -U postgres >/dev/null 2>&1 && break; sleep 1; done

ELEVI=$(podman exec "$CONTAINER" psql -U postgres -tAc "select count(*) from mark_ai.utilizator where username like 'vol-elev%'" 2>/dev/null || echo 0)
if [[ "$ELEVI" == 0 ]]; then
  echo "Pregătesc baza de test (doar prima dată, ~2 minute)..."
  for pas in migrate seed-localitati seed date-volum; do dotnet run --project tools/MarkAi.Database -- "$pas" > "$LOG/stres-baza.log" 2>&1 \
    || { echo "Pasul $pas a eșuat; vezi $LOG/stres-baza.log."; exit 1; }; done
fi

echo "Pornesc API-ul de test pe $PORT_API (jurnal: $LOG/stres-api.log)..."
# setsid: dotnet run pornește API-ul ca proces copil; oprim tot grupul la final, oricum s-ar termina scriptul.
MARKAI_JWT_KEY=$(openssl rand -base64 48) Email__Furnizor=jurnal Sms__Furnizor=jurnal ASPNETCORE_URLS="http://127.0.0.1:$PORT_API" \
  Limite__CereriPeMinut=1000000 Limite__AutentificariPeMinut=1000000 \
  setsid dotnet run --project src/MarkAi.Api -c Release --no-launch-profile < /dev/null > "$LOG/stres-api.log" 2>&1 &
API=$!
trap 'kill -- -$API 2>/dev/null || true' EXIT
for _ in $(seq 1 120); do curl -sf -o /dev/null "http://127.0.0.1:$PORT_API/judete" && break; sleep 1; done
curl -sf -o /dev/null "http://127.0.0.1:$PORT_API/judete" || { echo "API-ul de test nu a pornit; vezi $LOG/stres-api.log."; exit 1; }

[[ $# -eq 0 ]] && set -- --cereri 100,500,1000 --clienti 100
dotnet run --project tools/MarkAi.Stres -c Release -- --url "http://127.0.0.1:$PORT_API" "$@"
