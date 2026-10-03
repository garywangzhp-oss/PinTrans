[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot "..\tests\fixtures\acceptance-template.csv"),
    [double]$MinimumCandidateRate = 0.90,
    [double]$MinimumLatencyRate = 0.90,
    [double]$MinimumAcceptanceRate = 0.85
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) {
    throw "Acceptance CSV not found: $Path"
}

$rows = @(Import-Csv -LiteralPath $Path)
if ($rows.Count -ne 100) {
    throw "Acceptance CSV must contain exactly 100 rows; found $($rows.Count)."
}

$withSource = @($rows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.source) })
if ($withSource.Count -ne 100) {
    throw "Acceptance CSV still has empty source rows: $($rows.Count - $withSource.Count)."
}

$candidateCount = 0
$latencyCount = 0
$acceptedCount = 0
foreach ($row in $rows) {
    $candidateSeen = $false
    if (-not [bool]::TryParse($row.candidate_seen, [ref]$candidateSeen)) {
        throw "Row $($row.id): candidate_seen must be true or false."
    }

    $latency = 0.0
    if (-not [double]::TryParse($row.latency_ms, [ref]$latency)) {
        throw "Row $($row.id): latency_ms must be numeric."
    }

    $humanAccept = $false
    if (-not [bool]::TryParse($row.human_accept, [ref]$humanAccept)) {
        throw "Row $($row.id): human_accept must be true or false."
    }

    if ($candidateSeen) { $candidateCount++ }
    if ($candidateSeen -and $latency -le 1500) { $latencyCount++ }
    if ($humanAccept) { $acceptedCount++ }
}

$candidateRate = $candidateCount / 100.0
$latencyRate = $latencyCount / 100.0
$acceptanceRate = $acceptedCount / 100.0

[pscustomobject]@{
    Rows = 100
    CandidateRate = $candidateRate
    CandidateRateRequired = $MinimumCandidateRate
    Within1500msRate = $latencyRate
    Within1500msRequired = $MinimumLatencyRate
    HumanAcceptanceRate = $acceptanceRate
    HumanAcceptanceRequired = $MinimumAcceptanceRate
    Passed = (
        $candidateRate -ge $MinimumCandidateRate -and
        $latencyRate -ge $MinimumLatencyRate -and
        $acceptanceRate -ge $MinimumAcceptanceRate
    )
} | Format-List

if (
    $candidateRate -lt $MinimumCandidateRate -or
    $latencyRate -lt $MinimumLatencyRate -or
    $acceptanceRate -lt $MinimumAcceptanceRate
) {
    exit 1
}