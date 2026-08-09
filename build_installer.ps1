$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$publish = Join-Path $root 'artifacts\publish'
& $dotnet publish (Join-Path $root 'src\sZIP.App\sZIP.App.csproj') --configuration Release --output $publish
if ($LASTEXITCODE -ne 0) { throw 'sZIP publish failed.' }

$candidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found.' }
& $iscc (Join-Path $root 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
