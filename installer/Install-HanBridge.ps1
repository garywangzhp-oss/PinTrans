[CmdletBinding()]
param(
    [string]$SourceDir = (Join-Path $PSScriptRoot "..\artifacts\publish\win-x64"),
    [string]$RimeUserDir = (Join-Path $env:APPDATA "Rime"),
    [string]$WeaselDir = "",
    [string]$AppInstallDir = (Join-Path $env:LOCALAPPDATA "HanBridge\app"),
    [switch]$NoDeploy,
    [switch]$AllowUntestedVersion,
    [switch]$SkipAutostart,
    [switch]$SkipStart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ManagedStart = "  # >>> HanBridge managed block >>>"
$ManagedEnd = "  # <<< HanBridge managed block <<<"
$RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Resolve-WeaselDirectory {
    param([string]$ExplicitPath)

    $candidates = @()
    if ($ExplicitPath) { $candidates += $ExplicitPath }
    $candidates += @(
        (Join-Path $env:ProgramFiles "Rime\weasel-0.17.4"),
        (Join-Path $env:ProgramFiles "Rime\weasel"),
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "Rime\weasel-0.17.4" }),
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "Rime\weasel" })
    )

    foreach ($candidate in $candidates | Where-Object { $_ } | Select-Object -Unique) {
        if (Test-Path (Join-Path $candidate "WeaselDeployer.exe")) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Weasel 0.17.x not found. Install Weasel first or pass -WeaselDir."
}

function Assert-WeaselVersion {
    param([string]$Directory, [switch]$AllowUntested)

    $server = Join-Path $Directory "WeaselServer.exe"
    if (-not (Test-Path $server)) {
        throw "WeaselServer.exe not found under $Directory."
    }

    $version = (Get-Item $server).VersionInfo
    if ($version.FileMajorPart -ne 0 -or $version.FileMinorPart -ne 17) {
        $actual = if ($version.FileVersion) { $version.FileVersion } else { "unknown" }
        if (-not $AllowUntested) {
            throw "Untested Weasel version '$actual'. V1 supports 0.17.x; use -AllowUntestedVersion to continue."
        }
        Write-Warning "Continuing with untested Weasel version $actual."
    }
}

function Assert-RimeIceInstalled {
    param([string]$UserDirectory, [string]$WeaselDirectory)

    $paths = @(
        (Join-Path $UserDirectory "rime_ice.schema.yaml"),
        (Join-Path $WeaselDirectory "data\rime_ice.schema.yaml")
    )

    if (-not ($paths | Where-Object { Test-Path $_ })) {
        throw "rime-ice not found. Install rime-ice into $UserDirectory first."
    }
}

function Get-ManagedBlock {
    @(
        $ManagedStart,
        '  "switches/+":',
        '    - name: hanbridge_translation',
        '      reset: 1',
        '      states: [ 翻译关, 翻译开 ]',
        '  "key_binder/bindings/+":',
        '    - { when: always, accept: Control+Alt+E, toggle: hanbridge_translation }',
        '  "engine/processors/@before 0": lua_processor@*hanbridge_refresh',
        '  "engine/filters/@before last": lua_filter@*hanbridge_filter',
        $ManagedEnd
    )
}

function Remove-ManagedBlock {
    param([string[]]$Lines)

    $result = [System.Collections.Generic.List[string]]::new()
    $inside = $false
    foreach ($line in $Lines) {
        if ($line.Trim() -eq $ManagedStart.Trim()) { $inside = $true; continue }
        if ($line.Trim() -eq $ManagedEnd.Trim()) { $inside = $false; continue }
        if (-not $inside) { $result.Add($line) }
    }
    return $result.ToArray()
}

function Test-ConflictingKeys {
    param([string[]]$Lines)

    $patterns = @(
        '^\s*"switches/\+"\s*:',
        '^\s*"key_binder/bindings/\+"\s*:',
        '^\s*"engine/processors/@before 0"\s*:',
        '^\s*"engine/filters/@before last"\s*:'
    )

    foreach ($line in $Lines) {
        foreach ($pattern in $patterns) {
            if ($line -match $pattern) {
                throw "Existing conflicting patch key found: $($line.Trim()). Refusing to overwrite user configuration."
            }
        }
    }
}

function Merge-RimePatch {
    param([string]$RimeDirectory)

    $path = Join-Path $RimeDirectory "rime_ice.custom.yaml"
    if (Test-Path $path) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        Copy-Item -LiteralPath $path -Destination "$path.hanbridge-backup-$timestamp" -Force
        $lines = [System.Collections.Generic.List[string]](Get-Content -LiteralPath $path)
    }
    else {
        $lines = [System.Collections.Generic.List[string]]::new()
        $lines.Add("patch:")
    }

    $withoutManaged = [System.Collections.Generic.List[string]](Remove-ManagedBlock -Lines $lines.ToArray())
    Test-ConflictingKeys -Lines $withoutManaged.ToArray()

    $patchIndex = -1
    for ($index = 0; $index -lt $withoutManaged.Count; $index++) {
        if ($withoutManaged[$index] -match '^patch\s*:\s*$') {
            $patchIndex = $index
            break
        }
    }

    if ($patchIndex -lt 0) {
        throw "Could not find a top-level 'patch:' block in $path. Refusing to guess."
    }

    $block = Get-ManagedBlock
    $insertIndex = $patchIndex + 1
    for ($index = $block.Count - 1; $index -ge 0; $index--) {
        $withoutManaged.Insert($insertIndex, $block[$index])
    }

    Write-Utf8NoBom -Path $path -Content (($withoutManaged -join [Environment]::NewLine) + [Environment]::NewLine)
}

function Stop-HanBridge {
    Get-Process -Name "HanBridge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

function Invoke-WeaselDeploy {
    param([string]$Directory)

    $deployer = Join-Path $Directory "WeaselDeployer.exe"
    Write-Host "Deploying Rime configuration..."
    Start-Process -FilePath $deployer -ArgumentList "/deploy" -WindowStyle Hidden | Out-Null
    Start-Sleep -Seconds 2
}

if (-not (Test-Path (Join-Path $SourceDir "HanBridge.exe"))) {
    throw "HanBridge.exe not found under $SourceDir. Run Build-HanBridge.ps1 first."
}

$ResolvedWeaselDir = Resolve-WeaselDirectory -ExplicitPath $WeaselDir
Assert-WeaselVersion -Directory $ResolvedWeaselDir -AllowUntested:$AllowUntestedVersion
Assert-RimeIceInstalled -UserDirectory $RimeUserDir -WeaselDirectory $ResolvedWeaselDir

New-Item -ItemType Directory -Force -Path $RimeUserDir | Out-Null
$luaDir = Join-Path $RimeUserDir "lua"
New-Item -ItemType Directory -Force -Path $luaDir | Out-Null

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Copy-Item -LiteralPath (Join-Path $repoRoot "rime\lua\hanbridge_filter.lua") -Destination (Join-Path $luaDir "hanbridge_filter.lua") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "rime\lua\hanbridge_refresh.lua") -Destination (Join-Path $luaDir "hanbridge_refresh.lua") -Force

Stop-HanBridge
New-Item -ItemType Directory -Force -Path $AppInstallDir | Out-Null
Copy-Item -Path (Join-Path $SourceDir "*") -Destination $AppInstallDir -Recurse -Force
if (-not $SkipAutostart) {
    Set-ItemProperty -Path $RunKey -Name "HanBridge" -Value "`"$(Join-Path $AppInstallDir 'HanBridge.exe')`""
}

Merge-RimePatch -RimeDirectory $RimeUserDir

if (-not $NoDeploy) {
    Invoke-WeaselDeploy -Directory $ResolvedWeaselDir
}

if (-not $SkipStart) {
    Start-Process -FilePath (Join-Path $AppInstallDir "HanBridge.exe") -WindowStyle Hidden | Out-Null
}
Write-Host ""
Write-Host "HanBridge installed." -ForegroundColor Green
Write-Host "Open the tray icon to configure DeepSeek or OpenCode Go."