# Local preview only: separate PostgreSQL cluster, real API, demo accounts and fake external providers.
param([ValidateSet('start', 'stop', 'status')][string]$Actiune = 'start')
$ErrorActionPreference = 'Stop'
$previewDir = Join-Path $env:LOCALAPPDATA 'MarkAI\preview-OptimizareV1'
$dataDir = Join-Path $previewDir 'pgdata'
$stateFile = Join-Path $previewDir 'procese.json'
$pgBin = Join-Path $env:ProgramFiles 'PostgreSQL\18\bin'
$nodeVersion = (Get-Content (Join-Path $PSScriptRoot 'frontend\.node-version')).Trim()
$nodeExe = Join-Path $env:USERPROFILE ".cache\markai-node-v$nodeVersion\node.exe"
$dotnetDir = Join-Path $env:USERPROFILE '.cache\markai-dotnet10'
$dotnetExe = Join-Path $dotnetDir 'dotnet.exe'
function Raspunde([string]$url) {
    try { Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 2 | Out-Null; return $true } catch { return $false }
}
function Asteapta([string]$url, [string]$jurnal) {
    for ($i = 0; $i -lt 90; $i++) { if (Raspunde $url) { return }; Start-Sleep -Seconds 1 }
    throw "Serviciul nu răspunde la $url. Vezi $jurnal"
}
function OpresteProces($inregistrare) {
    $p = Get-Process -Id $inregistrare.Id -ErrorAction SilentlyContinue
    if ($p -and $p.StartTime.ToUniversalTime().Ticks.ToString() -eq $inregistrare.StartTicks) {
        & taskkill.exe /PID $p.Id /T /F | Out-Null
    }
}
if ($Actiune -eq 'status') {
    "Site disponibil: $(Raspunde 'http://127.0.0.1:3000')"
    "API disponibil: $(Raspunde 'http://127.0.0.1:5081/materii')"
    "Jurnale: $previewDir"
    exit 0
}
if ($Actiune -eq 'stop') {
    if (Test-Path -LiteralPath $stateFile) {
        $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        foreach ($p in $state.Procese) { OpresteProces $p }
        Remove-Item -LiteralPath $stateFile
    }
    if (Test-Path (Join-Path $dataDir 'PG_VERSION')) {
        & "$pgBin\pg_ctl.exe" -D $dataDir status | Out-Null
        if ($LASTEXITCODE -eq 0) { & "$pgBin\pg_ctl.exe" -D $dataDir -m fast -w stop }
    }
    'Previzualizarea locală este oprită.'
    exit 0
}
if (Test-Path -LiteralPath $stateFile) {
    if ((Raspunde 'http://127.0.0.1:3000') -and (Raspunde 'http://127.0.0.1:5081/materii')) {
        'Previzualizarea rulează deja: http://localhost:3000'
        exit 0
    }
    throw 'Există o pornire anterioară. Rulează mai întâi scriptul cu stop.'
}
foreach ($port in @(3000, 5081)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Portul $port este ocupat de alt serviciu." }
}
foreach ($exe in @($nodeExe, $dotnetExe, "$pgBin\initdb.exe")) {
    if (-not (Test-Path -LiteralPath $exe)) { throw "Lipsește o dependență locală: $exe" }
}
New-Item -ItemType Directory -Force -Path $previewDir | Out-Null
if (-not (Test-Path (Join-Path $dataDir 'PG_VERSION'))) {
    & "$pgBin\initdb.exe" -D $dataDir -U markai_preview -A trust -E UTF8 --locale=C
    if ($LASTEXITCODE -ne 0) { throw 'Inițializarea bazei demonstrative a eșuat.' }
}
& "$pgBin\pg_ctl.exe" -D $dataDir status | Out-Null
if ($LASTEXITCODE -ne 0) {
    & "$pgBin\pg_ctl.exe" -D $dataDir -l (Join-Path $previewDir 'postgres.log') -o '-h 127.0.0.1 -p 55432' -w start
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL de previzualizare nu a pornit.' }
}
$variabile = @('DOTNET_ROOT', 'MARKAI_TEST_CONNECTION', 'NEXT_PUBLIC_API_URL', 'API_INTERN_URL')
$inainte = @{}
foreach ($v in $variabile) { $inainte[$v] = [Environment]::GetEnvironmentVariable($v, 'Process') }
$procese = @()
try {
    $env:DOTNET_ROOT = $dotnetDir
    $env:MARKAI_TEST_CONNECTION = 'Host=127.0.0.1;Port=55432;Database=postgres;Username=markai_preview'
    $env:NEXT_PUBLIC_API_URL = '/api'
    $env:API_INTERN_URL = 'http://127.0.0.1:5081'
    $api = Start-Process -FilePath $dotnetExe -ArgumentList @('run', '--project', 'tools/MarkAi.Previzualizare', '--', '--port', '5081') -WorkingDirectory (Join-Path $PSScriptRoot 'database') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $previewDir 'api.log') -RedirectStandardError (Join-Path $previewDir 'api.err.log') -PassThru
    $procese += @{ Id = $api.Id; StartTicks = $api.StartTime.ToUniversalTime().Ticks.ToString() }
    @{ Procese = $procese } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $stateFile
    Asteapta 'http://127.0.0.1:5081/materii' (Join-Path $previewDir 'api.err.log')
    $site = Start-Process -FilePath $nodeExe -ArgumentList @('node_modules/next/dist/bin/next', 'dev', '-p', '3000', '-H', '127.0.0.1') -WorkingDirectory (Join-Path $PSScriptRoot 'frontend') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $previewDir 'site.log') -RedirectStandardError (Join-Path $previewDir 'site.err.log') -PassThru
    $procese += @{ Id = $site.Id; StartTicks = $site.StartTime.ToUniversalTime().Ticks.ToString() }
    @{ Procese = $procese } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $stateFile
    Asteapta 'http://127.0.0.1:3000/api/materii' (Join-Path $previewDir 'site.err.log')
    'Site: http://localhost:3000'
    'Conturi demo: irina (profesor), admin (administrator), george (elev). Parola: Test-demo-2026!'
    "Jurnale: $previewDir"
    'Oprire: .\porneste-previzualizare.ps1 stop'
} catch {
    foreach ($p in $procese) { OpresteProces $p }
    if (Test-Path -LiteralPath $stateFile) { Remove-Item -LiteralPath $stateFile }
    throw
} finally {
    foreach ($v in $variabile) { [Environment]::SetEnvironmentVariable($v, $inainte[$v], 'Process') }
}
