param(
    [Parameter(Mandatory = $true)][string]$ArtifactsDirectory,
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'
$artifacts = (Resolve-Path -LiteralPath $ArtifactsDirectory).Path
$required = @(
    "sZIP_Setup_$Version.exe",
    'sZIP_Setup_latest.exe',
    "sZIP-$Version-source.zip",
    "sZIP-$Version-source.zip.sha256",
    "sZIP-$Version-build-info.md"
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifacts $name) -PathType Leaf)) {
        throw "Missing release asset: $name"
    }
}

$sourceName = "sZIP-$Version-source.zip"
$sourcePath = Join-Path $artifacts $sourceName
$expected = ((Get-Content -LiteralPath "$sourcePath.sha256" -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash.ToLowerInvariant()
if ($expected -ne $actual) {
    throw "Source archive SHA-256 mismatch: expected $expected, got $actual"
}

$installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $artifacts "sZIP_Setup_$Version.exe")).Hash
$latestHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $artifacts 'sZIP_Setup_latest.exe')).Hash
if ($installerHash -ne $latestHash) {
    throw 'The latest installer alias does not match the versioned installer.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($sourcePath)
try {
    $licenseEntry = $archive.Entries | Where-Object FullName -eq "sZIP-$Version-source/THIRD-PARTY-NOTICES.md"
    if ($null -eq $licenseEntry) {
        throw 'The source archive is missing THIRD-PARTY-NOTICES.md.'
    }
} finally {
    $archive.Dispose()
}

$buildInfo = Get-Content -LiteralPath (Join-Path $artifacts "sZIP-$Version-build-info.md") -Raw
foreach ($requiredText in @("sZIP $Version", "sZIP-$Version-source.zip", '## Dependencies', '## Build')) {
    if ($buildInfo.IndexOf($requiredText, [StringComparison]::Ordinal) -lt 0) {
        throw "Build information document is incomplete: $requiredText"
    }
}

Write-Output "Verified release assets for sZIP $Version."
