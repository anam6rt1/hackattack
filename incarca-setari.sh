# Citit cu `source` de porneste-public.sh și porneste-local.sh: încarcă ~/.config/markai/markai.env
# și se oprește dacă lipsesc setările fără de care API-ul nu pornește. Modelul cu toate cheile: markai.env.example.
SETARI="${MARKAI_SETARI:-$HOME/.config/markai/markai.env}"
[[ -f "$SETARI" ]] || { echo "Lipsește $SETARI (copiază markai.env.example și completează-l)."; exit 1; }
set -a; . "$SETARI"; set +a

LIPSA=()
for cheie in MARKAI_CONNECTION_STRING MARKAI_JWT_KEY; do [[ -n "${!cheie:-}" ]] || LIPSA+=("$cheie"); done
if (( ${#LIPSA[@]} )); then echo "Lipsesc din $SETARI: ${LIPSA[*]}"; exit 1; fi
[[ -n "${GEMINI_API_KEY:-}${ANTHROPIC_API_KEY:-}" ]] || echo "Atenție: nicio cheie AI setată; funcțiile AI vor fi oprite."
