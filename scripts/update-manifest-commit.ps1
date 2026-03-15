# Updates SaddlebagExchange/manifest.toml with the current git commit hash.
# Run from repo root before opening a D17 PR or when cutting a release.
# Usage: .\scripts\update-manifest-commit.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "SaddlebagExchange\manifest.toml"

if (-not (Test-Path $manifestPath)) {
    Write-Error "Not found: $manifestPath. Run from repo root or ensure SaddlebagExchange/manifest.toml exists."
    exit 1
}

$commit = (git -C $repoRoot rev-parse HEAD)
$content = Get-Content $manifestPath -Raw
$content = $content -replace '(?m)^commit = .*$', "commit = `"$commit`""
Set-Content $manifestPath -Value $content.TrimEnd() -NoNewline

Write-Output "Set SaddlebagExchange/manifest.toml commit to $commit"
