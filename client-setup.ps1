# Varianta pentru Windows a lui client-setup.sh; se pornește prin client-setup.cmd (ocolește blocarea scripturilor PowerShell).
# Pornește doar site-ul (Next.js, http://localhost:3000) pe calculatorul colegului; /api merge la API-ul serverului prin Tailscale.
# Datele sunt cele de pe server: orice modificare de aici se face în baza lui de date.
# Utilizare: .\client-setup.cmd [server]   (implicit bazzite.tail37781b.ts.net; merge și IP-ul Tailscale, ex. 100.80.30.123)
# Închidere: .\client-setup.cmd close (oprește site-ul și pune la loc frontend\.env.local de dinainte).
param([string]$Server = "bazzite.tail37781b.ts.net")
# Fără ErrorActionPreference=Stop: în PowerShell 5.1 ar opri scriptul la orice text pe stderr al lui taskkill/tailscale; pașii importanți au -ErrorAction Stop.
$Stare = Join-Path $PSScriptRoot ".client-setup"
$Frontend = Join-Path $PSScriptRoot "frontend"
$EnvFisier = Join-Path $Frontend ".env.local"
New-Item -ItemType Directory -Force $Stare -ErrorAction Stop | Out-Null

function Raspunde($url) {
  try { Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 $url | Out-Null; return $true } catch { return $false }
}

function Opreste-Site {
  $pidFisier = Join-Path $Stare "site.pid"
  if (Test-Path $pidFisier) { taskkill /PID (Get-Content $pidFisier) /T /F 2>$null | Out-Null; Remove-Item $pidFisier }
  # next dev pornește procese copil; oprește tot ce a rămas pe portul 3000.
  Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}

if ($Server -eq "close") {
  Opreste-Site
  if (Test-Path "$Stare\env.inainte") { Move-Item -Force "$Stare\env.inainte" $EnvFisier; "Pus la loc frontend\.env.local." }
  elseif (Test-Path "$Stare\env.nou") { Remove-Item -Force -ErrorAction SilentlyContinue $EnvFisier; "Șters frontend\.env.local (nu exista înainte)." }
  Remove-Item -Recurse -Force $Stare
  "Închis."
  exit 0
}

$Api = "http://${Server}:5080"
if ((Test-Path "$Stare\env.inainte") -or (Test-Path "$Stare\env.nou")) { "Deja pornit. Pentru a închide: .\client-setup.cmd close"; exit 1 }

$ts = Get-Command tailscale -ErrorAction SilentlyContinue
$tsExe = if ($ts) { $ts.Source } else { Join-Path $env:ProgramFiles "Tailscale\tailscale.exe" }
if (-not (Test-Path $tsExe)) { "Tailscale nu e instalat (https://tailscale.com/download/windows)."; exit 1 }
& $tsExe status *> $null
if ($LASTEXITCODE -ne 0) { "Tailscale nu e conectat. Deschide aplicația Tailscale și autentifică-te, apoi reîncearcă."; exit 1 }
if (-not (Raspunde "$Api/materii")) {
  "API-ul serverului nu răspunde la $Api."
  "Verifică: serverul a rulat ./server-setup.sh și aplicația lui e pornită; încearcă și cu IP-ul Tailscale în loc de nume."
  exit 1
}

# .env.local are prioritate față de .env; îl păstrăm pe al tău și îl punem la loc la close. ASCII, fără BOM (altfel Next nu citește prima variabilă).
if (Test-Path $EnvFisier) { Copy-Item $EnvFisier "$Stare\env.inainte" -ErrorAction Stop } else { New-Item -ItemType File "$Stare\env.nou" | Out-Null }
Set-Content -Encoding ascii -ErrorAction Stop $EnvFisier "NEXT_PUBLIC_API_URL=/api", "API_INTERN_URL=$Api"

if (-not (Test-Path (Join-Path $Frontend "node_modules"))) {
  Push-Location $Frontend; try { npm.cmd install; if ($LASTEXITCODE -ne 0) { throw "npm install a eșuat." } } finally { Pop-Location }
}
Opreste-Site
# Fereastră ascunsă, ieșirea în jurnal; la close, taskkill /T oprește tot arborele de procese pornit de aici.
$jurnal = Join-Path $Stare "site.log"
$proc = Start-Process cmd.exe -ArgumentList "/c npm run dev -- -p 3000 > `"$jurnal`" 2>&1" -WorkingDirectory $Frontend -WindowStyle Hidden -PassThru
Set-Content (Join-Path $Stare "site.pid") $proc.Id

$gata = $false
for ($i = 0; $i -lt 90 -and -not $gata; $i++) { $gata = Raspunde "http://localhost:3000/api/materii"; if (-not $gata) { Start-Sleep 1 } }
if (-not $gata) { "ATENȚIE: site-ul nu răspunde după 90 de secunde. Vezi $jurnal." }
"Site: http://localhost:3000 (API: $Api)"
"Jurnal: $jurnal"
"Închidere: .\client-setup.cmd close"
