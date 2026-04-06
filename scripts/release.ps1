# One-button release: bump version in csproj + repo.json, commit & push release, tag, then set manifest.toml commit and push.
# Run from repo root. Usage: .\scripts\release.ps1 1.0.11

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot ".git"))) {
    Write-Error "Not a git repo root: $repoRoot. Run from repo root."
    exit 1
}

# Validate version (e.g. 1.0.11)
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be X.Y.Z (e.g. 1.0.11). Got: $Version"
    exit 1
}

$csprojPath = Join-Path $repoRoot "SaddlebagExchange\SaddlebagExchange.csproj"
$repoJsonPath = Join-Path $repoRoot "repo.json"
$manifestPath = Join-Path $repoRoot "SaddlebagExchange\manifest.toml"

foreach ($p in $csprojPath, $repoJsonPath, $manifestPath) {
    if (-not (Test-Path $p)) {
        Write-Error "Not found: $p"
        exit 1
    }
}

# 1) Bump version in csproj
$csproj = Get-Content $csprojPath -Raw
$csproj = $csproj -replace '(<AssemblyVersion>)[^<]+(</AssemblyVersion>)', "`${1}$Version`$2"
Set-Content $csprojPath -Value $csproj -NoNewline
Write-Output "Set SaddlebagExchange.csproj AssemblyVersion to $Version"

# 2) Bump version and LastUpdated in repo.json
$repoJson = Get-Content $repoJsonPath -Raw
$repoJson = $repoJson -replace '("AssemblyVersion":\s*")[^"]+(")', "`${1}$Version`$2"
$unixNow = [long]([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
$repoJson = $repoJson -replace '("LastUpdated":\s*)\d+', "`${1}$unixNow"
Set-Content $repoJsonPath -Value $repoJson -NoNewline
Write-Output "Set repo.json AssemblyVersion to $Version and LastUpdated to $unixNow"

# 3) Restore manifest.toml so it cannot be accidentally included
#    in the release commit with a stale commit SHA.
Push-Location $repoRoot
try {
    & git restore "$manifestPath"
    Write-Output "Restored manifest.toml to last committed state (will be updated in a separate commit)"

    # 4) Git: stage only version bump files, commit, push release
    & git add "$csprojPath" "$repoJsonPath"
    & git status
    & git commit -m "Release $Version"
    & git push origin main
} finally {
    Pop-Location
}

# 5) Tag and push tag
Push-Location $repoRoot
try {
    & git tag "v$Version"
    & git push origin "v$Version"
} finally {
    Pop-Location
}

# 6) Capture release commit SHA (this is the code state D17 builds)
$commit = (git -C $repoRoot rev-parse HEAD)
Write-Output "Release commit SHA: $commit"

# 7) Set manifest.toml commit to release commit SHA
$content = Get-Content $manifestPath -Raw
$content = $content -replace '(?m)^commit = .*$', "commit = `"$commit`""
Set-Content $manifestPath -Value $content.TrimEnd() -NoNewline
Write-Output "Set SaddlebagExchange/manifest.toml commit to $commit"

# 8) Commit and push manifest pointer update
Push-Location $repoRoot
try {
    & git add "$manifestPath"
    & git commit -m "Set manifest commit for $Version"
    & git push origin main
} finally {
    Pop-Location
}

Write-Output ""
Write-Output "Release $Version done!"
Write-Output "  Release commit (what D17 builds): $commit"
Write-Output "  Tag: v$Version"
Write-Output "  manifest.toml now points to the release commit."
