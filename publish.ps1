<#
.SYNOPSIS
    Builds and publishes AetherBar as a self-contained deployment package.
.DESCRIPTION
    Uses dotnet publish to produce a trimmed, self-contained win-x64 deployment,
    then packages it into a .zip archive for distribution.
.PARAMETER Configuration
    Build configuration: Release (default) or Debug.
.PARAMETER OutputDir
    Directory for the published output (default: publish/).
.PARAMETER NoZip
    Skip creating the .zip archive.
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSCommandPath
$PublishDir = Join-Path $RepoRoot $OutputDir
$Rid = "win-x64"

Write-Host "=== AetherBar Publisher ===" -ForegroundColor Cyan
Write-Host ""

# 1. Restore
Write-Host "[1/4] Restoring packages..." -ForegroundColor Yellow
& dotnet restore "$RepoRoot\AetherBar.slnx" --runtime $Rid
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

# 2. Publish self-contained
Write-Host "[2/4] Publishing self-contained deployment..." -ForegroundColor Yellow
$publishArgs = @(
    "publish", "$RepoRoot\AetherBar.UI\AetherBar.UI.csproj"
    "--configuration", $Configuration
    "--runtime", $Rid
    "--self-contained", "true"
    "--output", $PublishDir
    "-p:DebugType=None"
    "-p:DebugSymbols=false"
)
& dotnet $publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# 3. Copy license and readme
Write-Host "[3/4] Copying documentation..." -ForegroundColor Yellow
if (Test-Path "$RepoRoot\LICENSE") {
    Copy-Item "$RepoRoot\LICENSE" "$PublishDir\LICENSE.txt"
}
Copy-Item "$RepoRoot\README.md" "$PublishDir\README.md"

# 4. Create zip
if (-not $NoZip) {
    Write-Host "[4/4] Creating zip archive..." -ForegroundColor Yellow
    $zipFile = Join-Path $RepoRoot "AetherBar-$Configuration-$Rid.zip"
    if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $zipFile, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host "Created: $zipFile" -ForegroundColor Green
} else {
    Write-Host "[4/4] Skipped zip creation (-NoZip)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan
Write-Host "Published to: $PublishDir"
if (-not $NoZip) {
    Write-Host "Package:      $RepoRoot\AetherBar-$Configuration-$Rid.zip"
}
