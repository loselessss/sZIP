$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$publish = Join-Path $root 'artifacts\publish'
& $dotnet publish (Join-Path $root 'src\sZIP.App\sZIP.App.csproj') --configuration Release --output $publish
if ($LASTEXITCODE -ne 0) { throw 'sZIP publish failed.' }

$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio Build Tools were not found.' }
$visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw 'Visual C++ x64 build tools were not found.' }
$msbuild = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $root 'src\sZIP.ShellExtension\sZIP.ShellExtension.vcxproj') /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'Windows 11 shell extension build failed.' }
& (Join-Path $root 'build_identity_package.ps1') $publish
if ($LASTEXITCODE -ne 0) { throw 'Sparse identity package build failed.' }

$candidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found.' }
& $iscc (Join-Path $root 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
