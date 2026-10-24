param([string]$NodeExe, [switch]$Instaleaza, [switch]$Browser)
$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '.node-version')).Trim()
if (-not $NodeExe) {
    $cached = Join-Path $env:USERPROFILE ".cache\markai-node-v$version\node.exe"
    $NodeExe = if (Test-Path -LiteralPath $cached) { $cached } else { (Get-Command node -ErrorAction Stop).Source }
}
$actual = & $NodeExe --version
if ([int]($actual.TrimStart('v').Split('.')[0]) -lt 26) { throw 'Selectează Node 26 cu -NodeExe sau instalează versiunea din .node-version.' }
$npmHome = Split-Path (Get-Command npm.cmd -ErrorAction Stop).Source
$npmCli = Join-Path $npmHome 'node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path -LiteralPath $npmCli)) { throw 'Nu găsesc npm. Instalează distribuția Node.js cu npm.' }
$oldPath = $env:Path
$oldCa = $env:NODE_USE_SYSTEM_CA
Push-Location $PSScriptRoot
try {
    $env:Path = (Split-Path $NodeExe) + ';' + $oldPath
    $env:NODE_USE_SYSTEM_CA = '1'
    if ($Instaleaza) {
        & $NodeExe $npmCli ci
        if ($LASTEXITCODE -ne 0) { throw 'Instalarea dependențelor a eșuat.' }
    }
    & $NodeExe $npmCli run verifica
    if ($LASTEXITCODE -ne 0) { throw 'Verificările au eșuat.' }
    if ($Browser) {
        & $NodeExe 'node_modules\@playwright\test\cli.js' install chromium
        if ($LASTEXITCODE -ne 0) { throw 'Instalarea browserului a eșuat.' }
        & $NodeExe 'node_modules\@playwright\test\cli.js' test --config playwright.upload.config.ts
        if ($LASTEXITCODE -ne 0) { throw 'Testele din browser au eșuat.' }
    }
} finally {
    Pop-Location
    $env:Path = $oldPath
    $env:NODE_USE_SYSTEM_CA = $oldCa
}
