#!/usr/bin/env bash
# Administrarea bazei mark_ai: migrările EF (verifică modelul, creează, arată starea, aplică cu backup, anulează) și backupurile.
# `apply` refuză SQL-ul care pierde date (DROP TABLE/COLUMN/SCHEMA/SEQUENCE, DROP ... CASCADE, TRUNCATE, DELETE FROM); ștergerile de indexuri,
# constrângeri, valori implicite etc. doar se afișează. Backupurile și SQL-ul verificat stau în ~/.local/state/markai/backups.
# Comenzile care ating baza folosesc MARKAI_CONNECTION_STRING din ~/.config/markai/markai.env (vezi incarca-setari.sh).
# Utilizare: ./dbmg.sh help
set -euo pipefail
cd "$(dirname "$0")"
RADACINA=$PWD
MIGRARI=database/src/MarkAi.Data/Migrations
BACKUPS="${XDG_STATE_HOME:-$HOME/.local/state}/markai/backups"
# Pentru check/create: fabrica EF cere un șir de conexiune, dar aceste comenzi nu deschid conexiunea.
OFFLINE='Host=127.0.0.1;Port=1;Database=offline;Username=offline'

ajutor() {
  cat <<'EOF'
Utilizare: ./dbmg.sh <comandă> [opțiuni]

Migrări, fără bază de date (nu citesc markai.env):
  check                 Spune dacă entitățile/configurarea s-au schimbat față de ultima migrare (deci dacă e nevoie de create).
  create <Nume> [--gol] Creează migrarea <Nume> (PascalCase) și arată ce ar șterge. Refuză dacă modelul nu s-a schimbat;
                        --gol o creează oricum, goală, pentru SQL scris de mână (migrationBuilder.Sql).

Migrări, pe baza din markai.env:
  status                Lista migrărilor: aplicate și restante.
  apply [--dry-run]     Aplică migrările restante: verifică SQL-ul, face backup schemei mark_ai, apoi rulează migrate.
                        Refuză (cod 2) dacă SQL-ul pierde date. --dry-run: doar verificarea, fără backup și fără modificări.
  remove                Șterge ultima migrare din cod, doar dacă NU e aplicată pe bază (pentru o migrare creată greșit).

Backupuri (schema mark_ai, în ~/.local/state/markai/backups):
  backup list               Backupurile, numerotate de la cel mai nou.
  backup create [eticheta]  Backup acum, din baza din markai.env (eticheta: litere, cifre, -).
  backup show <nr|fișier>   Detaliile unui backup: data, baza, versiunea, tabelele salvate și SQL-ul migrării, dacă există.
  backup delete <nr|fișier>... [--da]
                            Șterge backupuri (și SQL-ul migrării lor), după confirmare; --da sare peste confirmare.

Date de volum (~51 000 de conturi vol-…@volum.hackattack.com, pe baza din markai.env; vezi database/docs/teste.md):
  volum status          Câte conturi vol-… există și câte conturi reale sunt legate de ele.
  volum add             Backup, apoi le generează (câteva minute). Refuză dacă există deja.
  volum remove [--da]   Arată ce se pierde, cere confirmare, face backup, apoi le șterge (doar ce a generat date-volum).

  help                  Acest mesaj.

Flux obișnuit: modifici entitățile → check → create NumeModificare → revizuiești fișierul generat → apply → repornești API-ul.
O ștergere de date intenționată nu trece prin apply: fă backup și aplică de mână (database/docs/database.md).
EOF
}

ef() { (cd "$RADACINA/database" && dotnet ef "$@" --project src/MarkAi.Data --prefix-output --no-color </dev/null); }
# Migrările din cod, în ordine (fără .Designer.cs și snapshot).
migrari_cod() { find "$MIGRARI" -maxdepth 1 -name '[0-9]*_*.cs' ! -name '*.Designer.cs' -printf '%f\n' | sed 's/\.cs$//' | sort; }
porneste_pg() { distrobox enter pgSQL -- true </dev/null >/dev/null 2>&1 || true; }   # pornește containerul cu PostgreSQL, dacă e oprit
# pg_dump/pg_restore/psql vin din containerul pgSQL (pe gazdă nu există).
pg() { distrobox enter pgSQL -- env PGPASSFILE="$LUCRU/pgpass" "$@" </dev/null; }

# Citește conexiunea din MARKAI_CONNECTION_STRING în GAZDA/PORT/BAZA/UTILIZATOR. Parola merge într-un fișier pgpass
# temporar (mod 600), nu pe linia de comandă, unde s-ar vedea în `ps`.
conexiune() {
  camp() { tr ';' '\n' <<<"$MARKAI_CONNECTION_STRING" | sed -n "s/^[[:space:]]*\($1\)[[:space:]]*=//Ip" | head -1; }
  local parola
  GAZDA=$(camp 'host\|server'); PORT=$(camp port); BAZA=$(camp 'database'); UTILIZATOR=$(camp 'username\|user id\|user\|userid')
  parola=$(camp 'password\|pwd'); GAZDA=${GAZDA:-127.0.0.1}; PORT=${PORT:-5432}
  [[ -n "$BAZA" && -n "$UTILIZATOR" ]] || { echo "Nu pot citi baza/utilizatorul din MARKAI_CONNECTION_STRING."; exit 1; }
  ( umask 077; printf '%s:%s:%s:%s:%s\n' "$GAZDA" "$PORT" "$BAZA" "$UTILIZATOR" "${parola//:/\\:}" > "$LUCRU/pgpass" )
}

# Backup al schemei mark_ai în fișierul $1, verificat cu pg_restore -l. Întoarce 3 dacă schema nu există încă (bază nouă).
fa_backup() {
  local backup=$1 schema
  mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
  porneste_pg; conexiune
  schema=$(pg psql -h "$GAZDA" -p "$PORT" -U "$UTILIZATOR" -d "$BAZA" -tAc "select count(*) from pg_namespace where nspname = 'mark_ai'") \
    || { echo "Nu m-am putut conecta la $GAZDA:$PORT/$BAZA."; exit 1; }
  [[ "$schema" != 0 ]] || return 3
  echo "Backup schema mark_ai ($GAZDA:$PORT/$BAZA) → $backup"
  pg pg_dump -h "$GAZDA" -p "$PORT" -U "$UTILIZATOR" -d "$BAZA" -n mark_ai -Fc -f "$backup" \
    || { echo "Backupul a eșuat."; rm -f "$backup"; exit 1; }
  pg pg_restore -l "$backup" >/dev/null 2>&1 && [[ -s "$backup" ]] || { echo "Backupul nu poate fi citit de pg_restore."; exit 1; }
  chmod 600 "$backup"
}

# Clasifică ștergerile din fișierul SQL $1. Întoarce 2 dacă vreuna pierde date.
# Un DROP urmat de alt cuvânt decât cele sigure (sau la capăt de rând) e tratat ca pierdere: `ALTER TABLE t DROP c` șterge coloana c.
# Și un DROP sigur devine pierdere cu CASCADE (`DROP TYPE x CASCADE` șterge și coloanele de tipul x); `ON DELETE CASCADE` din FK nu contează.
verifica_sql() {
  local sql=$1 linie potrivire cuvant text sigure=() pierderi=()
  while IFS=: read -r linie potrivire; do
    cuvant=$(awk '{print tolower($2)}' <<<"$potrivire"); text=$(sed -n "${linie}p" "$sql" | sed 's/^[[:space:]]*//')
    case "$cuvant" in
      index|constraint|default|not|view|function|procedure|trigger|extension|identity|expression|policy|rule|type|statistics)
        if grep -qiE '\bdrop\b.*\bcascade\b' <<<"$text"; then pierderi+=("$linie: $text"); else sigure+=("$linie: $text"); fi ;;
      *) pierderi+=("$linie: $text") ;;
    esac
  done < <(grep -noiE '\bdrop([[:space:]]+[a-z_"]+|[[:space:]]*$)' "$sql" || true)
  while IFS=: read -r linie potrivire; do pierderi+=("$linie: $(sed -n "${linie}p" "$sql" | sed 's/^[[:space:]]*//')"); done \
    < <(grep -noiE '\btruncate\b|\bdelete[[:space:]]+from\b' "$sql" || true)
  if (( ${#sigure[@]} )); then
    echo "Ștergeri fără pierdere de date (se refac indexuri/constrângeri/valori implicite):"; printf '  linia %s\n' "${sigure[@]}"
  fi
  if grep -nEi '\balter[[:space:]]+column\b.*\btype\b' "$sql" > "$LUCRU/tipuri.txt"; then
    echo "Atenție: se schimbă tipul unor coloane (conversia poate eșua sau trunchia valori):"; sed -E 's/^([0-9]+):[[:space:]]*/  linia \1: /' "$LUCRU/tipuri.txt"
  fi
  if (( ${#pierderi[@]} )); then
    echo "PIERDERE DE DATE în $sql:"; printf '  linia %s\n' "${pierderi[@]}"; return 2
  fi
  echo "Nicio pierdere de date în SQL ($sql)."
}

# Citește starea migrărilor din bază în TOATE și RESTANTE. Iese cu eroare dacă baza nu răspunde:
# atunci EF iese tot cu 0 și doar omite „(Pending)”, deci totul ar părea aplicat.
stare_baza() {
  porneste_pg
  ef migrations list > "$LUCRU/lista.txt" 2>&1 || { cat "$LUCRU/lista.txt"; echo "Nu am putut citi lista migrărilor."; exit 1; }
  if grep -q 'Pending status not shown' "$LUCRU/lista.txt"; then
    grep -E '^(error|warn):' "$LUCRU/lista.txt" | head -5
    echo "Baza de date nu răspunde, deci nu știu ce migrări sunt aplicate. Nu am modificat nimic."; exit 1
  fi
  mapfile -t TOATE < <(sed -n 's/^data: *\([0-9]\{14\}_[A-Za-z0-9_]*\).*/\1/p' "$LUCRU/lista.txt")
  mapfile -t RESTANTE < <(sed -n 's/^data: *\([0-9]\{14\}_[A-Za-z0-9_]*\) (Pending)$/\1/p' "$LUCRU/lista.txt")
  (( ${#TOATE[@]} )) || { cat "$LUCRU/lista.txt"; echo "Nu am găsit nicio migrare în proiect."; exit 1; }
}

cmd_check() {
  export MARKAI_CONNECTION_STRING=$OFFLINE
  if ef migrations has-pending-model-changes > "$LUCRU/model.txt" 2>&1; then
    echo "Modelul este la zi cu ultima migrare ($(migrari_cod | tail -1)). Nu e nevoie de create."
  elif grep -q 'Changes have been made' "$LUCRU/model.txt"; then
    echo "Entitățile s-au schimbat față de ultima migrare: rulează ./dbmg.sh create NumeModificare"; return 1
  else
    cat "$LUCRU/model.txt"; echo "Verificarea modelului a eșuat (eroare de compilare?)."; return 3
  fi
}

cmd_create() {
  local nume=${1:-} gol=${2:-} anterioara
  [[ "$nume" =~ ^[A-Z][A-Za-z0-9]*$ ]] || { echo "Dă un nume PascalCase, de ex.: ./dbmg.sh create AdaugaTelefonOrganizatie"; exit 1; }
  [[ -z "$gol" || "$gol" == --gol ]] || { echo "Opțiune necunoscută: $gol"; exit 1; }
  migrari_cod | grep -q "^[0-9]*_${nume}$" && { echo "Există deja o migrare $nume."; exit 1; }
  if [[ -z "$gol" ]]; then
    local rc=0; cmd_check >/dev/null || rc=$?
    case $rc in
      1) ;;
      0) echo "Modelul nu s-a schimbat: migrarea ar fi goală. Pentru SQL scris de mână: create $nume --gol."; exit 1 ;;
      *) cat "$LUCRU/model.txt"; echo "Verificarea modelului a eșuat (eroare de compilare?)."; exit 1 ;;
    esac
  fi
  export MARKAI_CONNECTION_STRING=$OFFLINE
  anterioara=$(migrari_cod | tail -1)
  ef migrations add "$nume" > "$LUCRU/add.txt" 2>&1 || { cat "$LUCRU/add.txt"; echo "Crearea migrării a eșuat."; exit 1; }
  local noua; noua=$(migrari_cod | tail -1)
  echo "Creată: $MIGRARI/$noua.cs (plus .Designer.cs și snapshot-ul actualizat)."
  ef migrations script "${anterioara:-0}" "$noua" --output "$LUCRU/noua.sql" > "$LUCRU/script.log" 2>&1 \
    || { cat "$LUCRU/script.log"; echo "Nu am putut genera SQL-ul migrării."; exit 1; }
  echo; verifica_sql "$LUCRU/noua.sql" | sed "s|$LUCRU/noua.sql|migrarea nouă|" || true
  echo; echo "Revizuiește Up() din $noua.cs (o redenumire apare uneori ca DROP + ADD: corecteaz-o cu RenameColumn), apoi: ./dbmg.sh apply"
  echo "Greșită? ./dbmg.sh remove"
}

cmd_status() {
  . ./incarca-setari.sh; stare_baza
  local m
  for m in "${TOATE[@]}"; do
    if printf '%s\n' "${RESTANTE[@]}" | grep -qx "$m"; then echo "  restantă  $m"; else echo "  aplicată  $m"; fi
  done
  (( ${#RESTANTE[@]} )) && echo "${#RESTANTE[@]} restante: ./dbmg.sh apply" || echo "Baza este la zi."
}

cmd_apply() {
  local dry=0
  case "${1:-}" in "") ;; --dry-run) dry=1 ;; *) echo "Opțiune necunoscută: $1 (doar --dry-run)"; exit 1 ;; esac
  . ./incarca-setari.sh
  local moment; moment=$(date +%Y%m%d-%H%M%S)
  mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
  stare_baza
  if (( ${#RESTANTE[@]} == 0 )); then echo "Baza este la zi (ultima migrare: ${TOATE[-1]}). Nimic de aplicat."; return 0; fi

  echo "Migrări de aplicat (${#RESTANTE[@]}):"; printf '  %s\n' "${RESTANTE[@]}"
  # Scriptul pornește de la ultima migrare aplicată („0” = baza nu are încă nicio migrare).
  local de_la=0 m
  for m in "${TOATE[@]}"; do [[ "$m" == "${RESTANTE[0]}" ]] && break; de_la=$m; done
  local sql="$BACKUPS/migrare_${RESTANTE[-1]}_$moment.sql"
  ef migrations script "$de_la" "${RESTANTE[-1]}" --no-build --output "$sql" > "$LUCRU/script.log" 2>&1 \
    || { cat "$LUCRU/script.log"; echo "Nu am putut genera SQL-ul migrărilor."; exit 1; }
  chmod 600 "$sql"
  echo; verifica_sql "$sql" || { echo; echo "OPRIT: nu am făcut nicio modificare."
    echo "Dacă pierderea este intenționată: fă backup, revizuiește scriptul și aplică de mână (database/docs/database.md)."; exit 2; }
  if (( dry )); then echo "--dry-run: nu am făcut backup și nu am aplicat nimic."; return 0; fi

  local backup="$BACKUPS/mark_ai_inainte_${RESTANTE[-1]}_$moment.dump" rc=0
  fa_backup "$backup" || rc=$?
  if (( rc == 3 )); then backup=""; echo "Schema mark_ai nu există încă (bază nouă): nu am ce salva."; fi

  echo "Aplic migrările..."
  (cd database && dotnet run --project tools/MarkAi.Database -- migrate </dev/null) \
    || { echo "Migrarea a eșuat.${backup:+ Restaurare: pg_restore --clean --if-exists -n mark_ai -d <baza> $backup}"; exit 1; }
  echo "Gata: aplicate ${#RESTANTE[@]} migrări. Repornește API-ul ca să folosească noul model:"
  echo "  ./porneste-public.sh stop </dev/null; ./porneste-public.sh </dev/null"
}

cmd_remove() {
  . ./incarca-setari.sh; stare_baza
  local ultima=${TOATE[-1]}
  # Fără --force, EF tot ar încerca să anuleze o migrare aplicată; verificăm noi întâi, pe starea citită din bază.
  printf '%s\n' "${RESTANTE[@]}" | grep -qx "$ultima" \
    || { echo "$ultima este deja aplicată pe bază; nu o șterg. Pentru a o desface, creează o migrare nouă care o inversează."; exit 1; }
  ef migrations remove > "$LUCRU/remove.txt" 2>&1 || { cat "$LUCRU/remove.txt"; echo "Ștergerea a eșuat."; exit 1; }
  echo "Ștearsă: $ultima (fișierele și snapshot-ul au revenit la $(migrari_cod | tail -1))."
}

# Backupurile, de la cel mai nou: *.dump (pg_dump -Fc) și backupurile SQL simple mai vechi; migrare_*.sql e SQL-ul verificat de apply,
# nu un backup. Numărul din `backup list` e poziția în această listă.
e_backup() { [[ "$1" == *.dump || ( "$1" == *.sql && "$1" != migrare_* ) ]]; }
backupuri() {
  [[ -d "$BACKUPS" ]] || return 0
  find "$BACKUPS" -maxdepth 1 \( -name '*.dump' -o \( -name '*.sql' ! -name 'migrare_*' \) \) -printf '%T@ %f\n' | sort -rn | cut -d' ' -f2-
}
# SQL-ul verificat de apply, pereche cu backupul făcut înainte de aceeași migrare.
sql_pereche() { [[ "$1" == mark_ai_inainte_*.dump ]] || return 0; local n=${1%.dump}; echo "$BACKUPS/migrare_${n#mark_ai_inainte_}.sql"; }
# Transformă un număr din `backup list` sau un nume de fișier în numele backupului. Doar fișiere din $BACKUPS.
alege_backup() {
  local x=$1 lista; mapfile -t lista < <(backupuri)
  if [[ "$x" =~ ^[0-9]+$ ]]; then
    (( x >= 1 && x <= ${#lista[@]} )) || { echo "Nu există backupul nr. $x (vezi ./dbmg.sh backup list)." >&2; return 1; }
    echo "${lista[x-1]}"
  else
    [[ "$x" != */* && -f "$BACKUPS/$x" ]] && e_backup "$x" \
      || { echo "Nu există backupul „$x” în $BACKUPS (dă numărul sau numele din backup list)." >&2; return 1; }
    echo "$x"
  fi
}

cmd_backup_list() {
  local lista f i=0; mapfile -t lista < <(backupuri)
  (( ${#lista[@]} )) || { echo "Niciun backup în $BACKUPS."; return 0; }
  echo "Backupuri în $BACKUPS (cel mai nou primul):"
  for f in "${lista[@]}"; do
    i=$((i + 1))
    printf '  %3d  %s  %7s  %s\n' "$i" "$(date -r "$BACKUPS/$f" '+%Y-%m-%d %H:%M')" "$(du -h "$BACKUPS/$f" | cut -f1)" "$f"
  done
  echo "Detalii: ./dbmg.sh backup show <nr>   Ștergere: ./dbmg.sh backup delete <nr>"
}

cmd_backup_create() {
  local eticheta=${1:-manual}
  [[ "$eticheta" =~ ^[A-Za-z0-9-]+$ ]] || { echo "Eticheta poate avea doar litere, cifre și -."; exit 1; }
  . ./incarca-setari.sh
  local backup="$BACKUPS/mark_ai_${eticheta}_$(date +%Y%m%d-%H%M%S).dump" rc=0
  fa_backup "$backup" || rc=$?
  (( rc != 3 )) || { echo "Schema mark_ai nu există pe $GAZDA:$PORT/$BAZA: nu am ce salva."; exit 1; }
  echo "Gata: $(du -h "$backup" | cut -f1)."
}

cmd_backup_show() {
  local f; f=$(alege_backup "$1") || exit 1
  local cale="$BACKUPS/$f" sql tabele; sql=$(sql_pereche "$f")
  echo "Fișier:   $cale ($(du -h "$cale" | cut -f1))"
  if [[ "$f" == *.dump ]]; then
    porneste_pg
    pg pg_restore -l "$cale" > "$LUCRU/cuprins.txt" 2>&1 || { cat "$LUCRU/cuprins.txt"; echo "pg_restore nu poate citi $cale."; exit 1; }
    sed -n 's/^;[[:space:]]*Archive created at /Creat:    /p; s/^;[[:space:]]*dbname: /Baza:     /p; s/^;[[:space:]]*Dumped from database version: /Postgres: /p' "$LUCRU/cuprins.txt"
    mapfile -t tabele < <(sed -n 's/^[0-9]*; [0-9]* [0-9]* TABLE DATA [^ ]* \([^ ]*\) .*/\1/p' "$LUCRU/cuprins.txt" | sort)
  else
    echo "Format:   SQL simplu (backup mai vechi); Creat: $(date -r "$cale" '+%Y-%m-%d %H:%M') (data fișierului)"
    sed -n 's/^-- Dumped from database version /Postgres: /p' "$cale" | head -1
    mapfile -t tabele < <(sed -n 's/^COPY [^.]*\.\("\{0,1\}[^ ("]*"\{0,1\}\) .*/\1/p' "$cale" | tr -d '"' | sort)
  fi
  echo "Tabele cu date (${#tabele[@]}):"
  (( ${#tabele[@]} )) && printf '  %-26s%-26s%-26s%-26s\n' "${tabele[@]}" | sed 's/[[:space:]]*$//'
  if [[ -n "$sql" && -f "$sql" ]]; then echo "SQL-ul migrării aplicate după acest backup: $sql"; fi
  if [[ "$f" == *.dump ]]; then echo "Restaurare (înlocuiește schema mark_ai!): pg_restore --clean --if-exists -n mark_ai -d <baza> $cale"
  else echo "Restaurare (pe o schemă mark_ai goală): psql -d <baza> -f $cale"; fi
}

cmd_backup_delete() {
  local da=0 x f sql alese=()
  for x in "$@"; do [[ "$x" == --da ]] && da=1; done
  for x in "$@"; do [[ "$x" == --da ]] || { f=$(alege_backup "$x") || exit 1; alese+=("$f"); }; done
  (( ${#alese[@]} )) || { echo "Dă numărul sau numele backupului: ./dbmg.sh backup delete <nr|fișier>..."; exit 1; }
  mapfile -t alese < <(printf '%s\n' "${alese[@]}" | sort -u)   # numerele se rezolvă toate înainte de ștergere
  echo "De șters:"
  for f in "${alese[@]}"; do
    echo "  $f ($(du -h "$BACKUPS/$f" | cut -f1))"
    sql=$(sql_pereche "$f"); [[ -n "$sql" && -f "$sql" ]] && echo "    + $(basename "$sql")"
  done
  if (( ! da )); then
    [[ -t 0 ]] || { echo "Fără terminal pentru confirmare: adaugă --da."; exit 1; }
    local r; read -r -p "Ștergi definitiv? [d/N] " r
    [[ "$r" =~ ^[dDyY]$ ]] || { echo "Nu am șters nimic."; exit 0; }
  fi
  for f in "${alese[@]}"; do
    rm -f -- "$BACKUPS/$f"; sql=$(sql_pereche "$f"); [[ -z "$sql" ]] || rm -f -- "$sql"
  done
  echo "Șters: ${#alese[@]} backup(uri)."
}

cmd_backup() {
  local sub=${1:-}; shift || true
  case "$sub" in
    list)   (( $# == 0 )) || { echo "backup list nu primește argumente."; exit 1; }; cmd_backup_list ;;
    create) (( $# <= 1 )) || { echo "Prea multe argumente."; exit 1; }; cmd_backup_create "$@" ;;
    show)   (( $# == 1 )) || { echo "Utilizare: ./dbmg.sh backup show <nr|fișier>"; exit 1; }; cmd_backup_show "$1" ;;
    delete) cmd_backup_delete "$@" ;;
    *) echo "Comenzi backup: list, create [eticheta], show <nr|fișier>, delete <nr|fișier>... [--da] (./dbmg.sh help)"; exit 1 ;;
  esac
}

# Datele de volum: conturile vol-… cu email @volum.hackattack.com (aceleași două mărci pe care le folosește `date-volum --sterge`).
# Citește în VOL numărul lor, în VOL_MEMBRI conturile reale membre în organizațiile lor și în VOL_REZERVARI rezervările conturilor
# reale la profesorii vol-… (pe acestea le șterge și --sterge).
volum_stare() {
  porneste_pg; conexiune
  local r
  r=$(pg psql -h "$GAZDA" -p "$PORT" -U "$UTILIZATOR" -d "$BAZA" -tAqc "
    with v as (select id from mark_ai.utilizator where username like 'vol-%' and email like '%@volum.hackattack.com')
    select (select count(*) from v),
      (select count(distinct m.utilizator_id) from mark_ai.membru_organizatie m join mark_ai.organizatie o on o.id = m.organizatie_id
        where o.admin_id in (select id from v) and m.utilizator_id not in (select id from v)),
      (select count(*) from mark_ai.rezervare r join mark_ai.camera_tutoring c on c.id = r.camera_id
        where c.mentor_id in (select id from v) and r.elev_id not in (select id from v))") \
    || { echo "Nu am putut citi datele de pe $GAZDA:$PORT/$BAZA (baza pornită? schema creată cu ./dbmg.sh apply?)."; exit 1; }
  IFS='|' read -r VOL VOL_MEMBRI VOL_REZERVARI <<<"$r"
}

volum_rezumat() {
  echo "Date de volum pe $GAZDA:$PORT/$BAZA: $VOL conturi vol-…"
  (( VOL_MEMBRI + VOL_REZERVARI == 0 )) && return 0
  echo "Conturi reale legate de ele: $VOL_MEMBRI membre în organizațiile vol-…, $VOL_REZERVARI rezervări la profesorii vol-…"
  echo "  (la remove conturile rămân, dar pierd aceste apartenențe și rezervări)"
}

cmd_volum() {
  local sub=${1:-}; shift || true
  case "$sub" in
    status) (( $# == 0 )) || { echo "volum status nu primește argumente."; exit 1; } ;;
    add)    (( $# == 0 )) || { echo "volum add nu primește argumente."; exit 1; } ;;
    remove) [[ $# -eq 0 || ( $# -eq 1 && "$1" == --da ) ]] || { echo "Utilizare: ./dbmg.sh volum remove [--da]"; exit 1; } ;;
    *) echo "Comenzi volum: status, add, remove [--da] (./dbmg.sh help)"; exit 1 ;;
  esac
  . ./incarca-setari.sh
  volum_stare
  local moment backup rc=0; moment=$(date +%Y%m%d-%H%M%S)
  case "$sub" in
    status)
      volum_rezumat
      (( VOL )) && echo "Ștergere: ./dbmg.sh volum remove" || echo "Adăugare: ./dbmg.sh volum add" ;;
    add)
      (( VOL == 0 )) || { volum_rezumat; echo "Există deja; pentru a le genera din nou: ./dbmg.sh volum remove, apoi volum add."; exit 1; }
      backup="$BACKUPS/mark_ai_inainte_volum_$moment.dump"
      fa_backup "$backup" || rc=$?
      (( rc != 3 )) || { echo "Schema mark_ai nu există: rulează întâi ./dbmg.sh apply, apoi seed-localitati și seed (database/docs/database.md)."; exit 1; }
      echo "Generez datele de volum (câteva minute)..."
      (cd database && dotnet run --project tools/MarkAi.Database -- date-volum </dev/null) \
        || { echo "Generarea a eșuat (tranzacțiile făcute rămân; ./dbmg.sh volum remove le scoate). Backup: $backup"; exit 1; }
      volum_stare; volum_rezumat ;;
    remove)
      (( VOL )) || { volum_rezumat; echo "Nimic de șters."; return 0; }
      volum_rezumat
      if [[ "${1:-}" != --da ]]; then
        [[ -t 0 ]] || { echo "Fără terminal pentru confirmare: adaugă --da."; exit 1; }
        local r; read -r -p "Ștergi datele de volum? (se face backup înainte) [d/N] " r
        [[ "$r" =~ ^[dDyY]$ ]] || { echo "Nu am șters nimic."; exit 0; }
      fi
      backup="$BACKUPS/mark_ai_inainte_sterge_volum_$moment.dump"
      fa_backup "$backup"
      echo "Șterg datele de volum (o singură tranzacție, poate dura câteva minute)..."
      (cd database && dotnet run --project tools/MarkAi.Database -- date-volum --sterge </dev/null) \
        || { echo "Ștergerea a eșuat; tranzacția a fost anulată, baza a rămas cum era. Backup: $backup"; exit 1; }
      volum_stare; volum_rezumat ;;
  esac
}

LUCRU=$(mktemp -d); trap 'rm -rf "$LUCRU"' EXIT
comanda=${1:-help}; shift || true
# dotnet ef nu face restore singur (pe o clonă nouă ar eșua cu NETSDK1004). Backupurile nu au nevoie de dotnet.
[[ "$comanda" =~ ^(help|-h|--help|ajutor|backup)$ ]] || (cd database && dotnet tool restore >/dev/null && dotnet restore MarkAi.slnx -v q >/dev/null) \
  || { echo "dotnet restore a eșuat."; exit 1; }
case "$comanda" in
  help|-h|--help|ajutor) ajutor ;;
  check)  (( $# == 0 )) || { echo "check nu primește argumente."; exit 1; }; cmd_check ;;
  create) (( $# <= 2 )) || { echo "Prea multe argumente."; exit 1; }; cmd_create "$@" ;;
  status) (( $# == 0 )) || { echo "status nu primește argumente."; exit 1; }; cmd_status ;;
  apply)  (( $# <= 1 )) || { echo "Prea multe argumente."; exit 1; }; cmd_apply "$@" ;;
  remove) (( $# == 0 )) || { echo "remove nu primește argumente."; exit 1; }; cmd_remove ;;
  backup) cmd_backup "$@" ;;
  volum)  cmd_volum "$@" ;;
  *) echo "Comandă necunoscută: $comanda"; echo; ajutor; exit 1 ;;
esac
