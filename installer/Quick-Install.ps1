[CmdletBinding()]
param(
    [switch]$SkipPrerequisites,
    [switch]$SkipBuild,
    [string]$WeaselDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$rimeUserDir = Join-Path $env:APPDATA "Rime"
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"

function Test-WeaselInstalled {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Rime\weasel-0.17.4"),
        (Join-Path $env:ProgramFiles "Rime\weasel")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "WeaselDeployer.exe")) {
            return (Resolve-Path $candidate).Path
        }
    }
    return $null
}

function Install-Weasel {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget is required to install Weasel automatically. Install Weasel 0.17.x manually."
    }

    Write-Host "Installing Weasel 0.17.4..."
    winget install --id Rime.Weasel --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "Weasel installation failed with exit code $LASTEXITCODE."
    }
}

function Install-RimeIce {
    if (-not (Test-Path $rimeUserDir)) {
        New-Item -ItemType Directory -Force -Path $rimeUserDir | Out-Null
    }

    if ((Get-ChildItem -LiteralPath $rimeUserDir -Force -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
        throw "Rime user directory already exists and is not empty, but rime-ice was not detected. Merge rime-ice manually to avoid overwriting your configuration: $rimeUserDir"
    }

    $download = Join-Path $env:TEMP ("rime-ice-" + [guid]::NewGuid().ToString("N") + ".zip")
    $extract = Join-Path $env:TEMP ("rime-ice-" + [guid]::NewGuid().ToString("N"))
    try {
        Write-Host "Downloading rime-ice..."
        Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/iDvel/rime-ice/archive/refs/heads/main.zip" -OutFile $download
        Expand-Archive -LiteralPath $download -DestinationPath $extract -Force
        $source = Get-ChildItem -LiteralPath $extract -Directory | Select-Object -First 1
        if (-not $source) {
            throw "rime-ice archive did not contain a source directory."
        }

        Copy-Item -Path (Join-Path $source.FullName "*") -Destination $rimeUserDir -Recurse -Force
    }
    finally {
        Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Install-DotNetSdk {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw ".NET 8 SDK is required to build from source. Install it manually or download the PinTrans release ZIP."
    }

    Write-Host "Installing .NET 8 SDK..."
    winget install --id Microsoft.DotNet.SDK.8 --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK installation failed with exit code $LASTEXITCODE."
    }
}

function Resolve-PinTransExecutable {
    $candidates = @(
        (Join-Path $repoRoot "PinTrans.exe"),
        (Join-Path $repoRoot "app\PinTrans.exe"),
        (Join-Path $publishDir "PinTrans.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

if (-not [Environment]::Is64BitOperatingSystem) {
    throw "PinTrans currently supports Windows x64 only."
}

$resolvedWeaselDir = $null
if ($WeaselDir) {
    if (-not (Test-Path (Join-Path $WeaselDir "WeaselDeployer.exe"))) {
        throw "WeaselDeployer.exe was not found under $WeaselDir."
    }
    $resolvedWeaselDir = (Resolve-Path $WeaselDir).Path
}

if (-not $SkipPrerequisites) {
    if (-not $resolvedWeaselDir) {
        $resolvedWeaselDir = Test-WeaselInstalled
    }
    if (-not $resolvedWeaselDir) {
        Install-Weasel
        $resolvedWeaselDir = Test-WeaselInstalled
    }

    $rimeIceFound = @(
        (Join-Path $rimeUserDir "rime_ice.schema.yaml"),
        $(if ($resolvedWeaselDir) { Join-Path $resolvedWeaselDir "data\rime_ice.schema.yaml" })
    ) | Where-Object { Test-Path $_ }

    if (-not $rimeIceFound) {
        Install-RimeIce
    }
}

$pinTransExe = Resolve-PinTransExecutable
if (-not $pinTransExe -and -not $SkipBuild) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Install-DotNetSdk
    }

    & (Join-Path $PSScriptRoot "Build-PinTrans.ps1")
    $pinTransExe = Resolve-PinTransExecutable
}

if (-not $pinTransExe) {
    throw "PinTrans.exe was not found. Download the release ZIP or run Build-PinTrans.ps1 first."
}

$installSource = Split-Path -Parent $pinTransExe
$installArgs = @{
    SourceDir = $installSource
}
if ($resolvedWeaselDir) {
    $installArgs.WeaselDir = $resolvedWeaselDir
}

& (Join-Path $PSScriptRoot "Install-PinTrans.ps1") @installArgs

Write-Host ""
Write-Host "PinTrans is installed." -ForegroundColor Green
Write-Host "Open the PinTrans tray icon, choose DeepSeek or OpenCode Go, enter your API key, test the connection, and save."