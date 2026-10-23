#!/usr/bin/env bash
# Deschide API-ul (127.0.0.1:5080) și baza de date (127.0.0.1:5432) pentru colegii din rețeaua Tailscale (tailnet), nu și pentru internet.
# Colegul pornește doar site-ul la el (./client-setup.sh), iar /api ajunge la API-ul de aici, cu datele de aici.
# Nu pornește aplicația: rulează și ./porneste-local.sh (sau ./porneste-public.sh).
# Închidere: ./server-setup.sh close (scoate doar ce a adăugat acest script și oprește Tailscale doar dacă l-a pornit el).
set -euo pipefail
cd "$(dirname "$0")"
STARE="${XDG_STATE_HOME:-$HOME/.local/state}/markai/tailscale-server.stare"; mkdir -p "$(dirname "$STARE")"
PORTURI=(5080 5432)

# Fără `tailscale set --operator=$USER`, regulile serve cer root.
ts() { tailscale "$@" 2>/dev/null || sudo tailscale "$@"; }
are_regula() { tailscale serve status --json 2>/dev/null | grep -q "\"$1\""; }

if [[ "${1:-}" == "close" ]]; then
  [[ -f "$STARE" ]] || { echo "Nimic de închis (nu există $STARE)."; exit 0; }
  . "$STARE"
  for port in ${ADAUGATE:-}; do ts serve --tcp="$port" off >/dev/null && echo "Închis portul $port în tailnet."; done
  if [[ "${PORNIT_TAILSCALED:-0}" == 1 ]]; then
    sudo tailscale down || true
    sudo systemctl stop tailscaled && echo "Tailscale oprit (era oprit înainte)."
  fi
  rm -f "$STARE"
  exit 0
fi

[[ -f "$STARE" ]] && { echo "Deja deschis. Pentru a închide: ./server-setup.sh close"; exit 1; }

PORNIT_TAILSCALED=0
if ! systemctl is-active --quiet tailscaled; then
  sudo systemctl start tailscaled; PORNIT_TAILSCALED=1
  sleep 2
fi
tailscale status >/dev/null 2>&1 || sudo tailscale up   # cere autentificare (link în terminal) dacă nu e conectat

ADAUGATE=""
for port in "${PORTURI[@]}"; do
  if are_regula "$port"; then echo "Portul $port era deja deschis în tailnet (îl las așa și la close)."; continue; fi
  ts serve --bg --tcp="$port" "tcp://127.0.0.1:$port" >/dev/null
  ADAUGATE="$ADAUGATE $port"
done
printf 'PORNIT_TAILSCALED=%s\nADAUGATE="%s"\n' "$PORNIT_TAILSCALED" "$ADAUGATE" > "$STARE"

NUME=$(tailscale status --json | jq -r '.Self.DNSName | rtrimstr(".")')
IP=$(tailscale ip -4)
curl -sf -o /dev/null --max-time 5 http://127.0.0.1:5080/materii \
  || echo "ATENȚIE: API-ul nu răspunde pe 127.0.0.1:5080. Pornește-l cu ./porneste-local.sh (sau ./porneste-public.sh)."
echo
echo "Deschis în tailnet: $NUME ($IP), porturile ${PORTURI[*]}."
echo "Colegul rulează: ./client-setup.sh $NUME"
echo "Bază de date (VS Code): server $NUME, port 5432, baza postgres, SSL dezactivat."
echo "Închidere: ./server-setup.sh close"
