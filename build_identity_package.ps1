param(
    [string]$PublishDirectory = (Join-Path $PSScriptRoot 'artifacts\publish')
)

$ErrorActionPreference = 'Stop'
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makeAppx = Get-ChildItem -Path $sdkRoot -Filter MakeAppx.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\MakeAppx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) { throw 'Windows SDK MakeAppx.exe was not found.' }

$layout = Join-Path $PSScriptRoot 'artifacts\identity-layout'
if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\AppxManifest.xml') -Destination $layout
New-Item -ItemType Directory -Path $PublishDirectory -Force | Out-Null
$output = Join-Path $PublishDirectory 'sZIP.ContextMenu.msix'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
& $makeAppx.FullName pack /o /d $layout /nv /p $output
if ($LASTEXITCODE -ne 0) { throw 'Sparse identity package build failed.' }
