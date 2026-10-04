[CmdletBinding()]
param(
    [string]$RimeUserDir = (Join-Path $env:APPDATA "Rime"),
    [string]$WeaselDir = "",
    [switch]$RemoveApiKeys,
    [switch]$RemoveCache,
    [switch]$RemoveAllData,
    [switch]$NoDeploy,
    [switch]$SkipAutostart,
    [string]$AppRoot = (Join-Path $env:LOCALAPPDATA "HanBridge")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ManagedStart = "  # >>> HanBridge managed block >>>"
$ManagedEnd = "  # <<< HanBridge managed block <<<"
$RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$Root = $AppRoot
$AppDir = Join-Path $Root "app"

function Stop-HanBridge {
    Get-Process -Name "HanBridge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

function Remove-ManagedBlock {
    param([string]$Path)

    if (-not (Test-Path $Path)) { return }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    Copy-Item -LiteralPath $Path -Destination "$Path.hanbridge-backup-$timestamp" -Force

    $lines = Get-Content -LiteralPath $Path
    $result = [System.Collections.Generic.List[string]]::new()
    $inside = $false
    foreach ($line in $lines) {
        if ($line.Trim() -eq $ManagedStart.Trim()) { $inside = $true; continue }
        if ($line.Trim() -eq $ManagedEnd.Trim()) { $inside = $false; continue }
        if (-not $inside) { $result.Add($line) }
    }

    [IO.File]::WriteAllText($Path, (($result -join [Environment]::NewLine) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
}

function Resolve-WeaselDirectory {
    param([string]$ExplicitPath)
    $candidates = @()
    if ($ExplicitPath) { $candidates += $ExplicitPath }
    $candidates += @(
        (Join-Path $env:ProgramFiles "Rime\weasel-0.17.4"),
        (Join-Path $env:ProgramFiles "Rime\weasel")
    )
    foreach ($candidate in $candidates | Where-Object { $_ } | Select-Object -Unique) {
        if (Test-Path (Join-Path $candidate "WeaselDeployer.exe")) { return (Resolve-Path $candidate).Path }
    }
    return $null
}

Stop-HanBridge
if (-not $SkipAutostart) {
    Remove-ItemProperty -Path $RunKey -Name "HanBridge" -ErrorAction SilentlyContinue
}

Remove-ManagedBlock -Path (Join-Path $RimeUserDir "rime_ice.custom.yaml")
foreach ($name in "hanbridge_filter.lua", "hanbridge_refresh.lua") {
    $path = Join-Path $RimeUserDir "lua\$name"
    if (Test-Path $path) { Remove-Item -LiteralPath $path -Force }
}

if (Test-Path $AppDir) {
    $resolved = (Resolve-Path $AppDir).Path
    if (-not $resolved.StartsWith((Resolve-Path $Root).Path, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

if ($RemoveAllData -or $RemoveApiKeys) {
    $secrets = Join-Path $Root "secrets.dat"
    if (Test-Path $secrets) { Remove-Item -LiteralPath $secrets -Force }
}
if ($RemoveAllData -or $RemoveCache) {
    foreach ($name in "cache.db", "cache.db-shm", "cache.db-wal") {
        $path = Join-Path $Root $name
        if (Test-Path $path) { Remove-Item -LiteralPath $path -Force }
    }
}

if ($RemoveAllData -and (Test-Path $Root)) {
    $resolvedRoot = (Resolve-Path $Root).Path
    $expected = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "HanBridge"))
    if (-not $resolvedRoot.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected root: $resolvedRoot"
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}

if (-not $NoDeploy) {
    $resolvedWeasel = Resolve-WeaselDirectory -ExplicitPath $WeaselDir
    if ($resolvedWeasel) {
        Start-Process -FilePath (Join-Path $resolvedWeasel "WeaselDeployer.exe") -ArgumentList "/deploy" -WindowStyle Hidden | Out-Null
        Start-Sleep -Seconds 2
    }
}

Write-Host "PinTrans uninstalled. User Rime data was preserved unless removal switches were supplied." -ForegroundColor Green