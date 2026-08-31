[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Store','Direct')][string]$Channel,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$')][string]$IdentityName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [string]$PublishDirectory = (Join-Path $PSScriptRoot 'artifacts\publish'),
    [string]$OutputDirectory,
    [string]$AppInstallerUri,
    [string]$PackageUri,
    [string]$SdkBinDirectory,
    [ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,
    [uri]$TimestampUri,
    [switch]$Unsigned,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
function Assert-HttpsUri([string]$Value, [string]$Extension) {
    $parsed = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$parsed) -or
        $parsed.Scheme -ne 'https' -or $parsed.UserInfo -or $parsed.Fragment -or $parsed.Query -or
        -not $parsed.AbsolutePath.EndsWith($Extension, [StringComparison]::OrdinalIgnoreCase)) {
        throw "A plain HTTPS $Extension URL without credentials, fragment, or query is required."
    }
}
if ($Publisher -match 'OID\.2\.25\.311729368913984317654407730594956997722') {
    throw 'Full distribution packages require a real publisher, not the unsigned sparse-package marker.'
}
if ($IdentityName -in @('sZIP.ContextMenu','sZIP.Desktop.Template')) {
    throw 'Supply a permanent, channel-specific identity, not the legacy or template identity.'
}
if ($Unsigned -and $CertificateThumbprint) { throw 'Choose unsigned validation or certificate signing, not both.' }
if ($Channel -eq 'Direct') {
    Assert-HttpsUri $AppInstallerUri '.appinstaller'
    Assert-HttpsUri $PackageUri '.msix'
    if (-not $CertificateThumbprint -and -not $Unsigned -and -not $PrepareOnly) {
        throw 'Direct distribution requires a signing certificate. Use -Unsigned only for a validation artifact.'
    }
}
elseif ($AppInstallerUri -or $PackageUri -or $CertificateThumbprint) {
    throw 'Store builds do not use direct-distribution feeds or local signing certificates.'
}

[xml]$props = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
$quadVersion = "$version.0"
$source = (Resolve-Path -LiteralPath $PublishDirectory).Path
$app = Join-Path $source 'sZIP.App.exe'
foreach ($required in @('sZIP.App.exe','sZIP.App.exe.config','sZIP.ShellExtension.dll','sZIP.Archive.dll','sZIP.Application.dll','sZIP.Domain.dll','sZIP.Watcher.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw "Missing build output: $required" }
}
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion -ne $quadVersion) {
    throw 'Published app version does not match Directory.Build.props. Rebuild before packaging.'
}
if (Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object Extension -in @('.pfx','.p12','.key')) {
    throw 'Private signing material must not be stored in the publish directory.'
}
$jobId = [Guid]::NewGuid().ToString('N')
$layout = Join-Path $PSScriptRoot "artifacts\msix-layout\$jobId"
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot "artifacts\msix\$Channel\$jobId" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'MSIX output must not be inside the publish directory.'
}
New-Item -ItemType Directory -Path $layout -Force | Out-Null
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Do not include the old installer/sparse identity or signing material in a full package.
$excluded = @('sZIP.ContextMenu.msix','MsixDistribution.xml','AppxManifest.xml')
foreach ($item in Get-ChildItem -LiteralPath $source) {
    if ($item.Name -in $excluded -or $item.Extension -in @('.pdb','.msix','.appx','.pfx','.p12','.cer')) { continue }
    Copy-Item -LiteralPath $item.FullName -Destination $layout -Recurse
}
if (Get-ChildItem -LiteralPath $layout -Recurse -File | Where-Object Extension -in @('.pfx','.p12','.key')) {
    throw 'Private signing material must never enter the package payload.'
}

[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'packaging\msix\AppxManifest.xml')
$manifest.Package.Identity.Name = $IdentityName
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Identity.Version = $quadVersion
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$menuClsid = if ($Channel -eq 'Store') { '85E1044C-A4EB-4EAC-A3EF-FC7B124D8F01' } else { '85E1044C-A4EB-4EAC-A3EF-FC7B124D8F02' }
foreach ($node in $manifest.SelectNodes("//*[local-name()='Class' or local-name()='Verb'][@Clsid or @Path='sZIP.ShellExtension.dll']")) {
    if ($node.HasAttribute('Clsid')) { $node.SetAttribute('Clsid', $menuClsid) }
    else { $node.SetAttribute('Id', $menuClsid) }
}
$manifest.Save((Join-Path $layout 'AppxManifest.xml'))
$distribution = [xml]'<Distribution />'
$distribution.DocumentElement.SetAttribute('Channel', $Channel)
if ($Channel -eq 'Direct') { $distribution.DocumentElement.SetAttribute('AppInstallerUri', $AppInstallerUri) }
$distribution.Save((Join-Path $layout 'MsixDistribution.xml'))

# Reuse the existing icon artwork at schema-valid scale-100 sizes.
Add-Type -AssemblyName System.Drawing
$original = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'src\sZIP.App\Assets\szip-icon-v2.png'))
try {
    foreach ($asset in @(@('StoreLogo.png',50), @('Square150x150Logo.png',150), @('Square44x44Logo.png',44))) {
        $bitmap = New-Object Drawing.Bitmap([int]$asset[1], [int]$asset[1])
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($original, 0, 0, [int]$asset[1], [int]$asset[1])
            $bitmap.Save((Join-Path $layout ('Assets\' + $asset[0])), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $original.Dispose() }

$feed = $null
if ($Channel -eq 'Direct') {
    $feed = [xml]'<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2017/2"><MainPackage/><UpdateSettings><OnLaunch HoursBetweenUpdateChecks="12"/></UpdateSettings></AppInstaller>'
    $feed.DocumentElement.SetAttribute('Uri', $AppInstallerUri)
    $feed.DocumentElement.SetAttribute('Version', $quadVersion)
    $main = $feed.SelectSingleNode("//*[local-name()='MainPackage']")
    $main.SetAttribute('Name', $IdentityName)
    $main.SetAttribute('Publisher', $Publisher)
    $main.SetAttribute('Version', $quadVersion)
    $main.SetAttribute('ProcessorArchitecture', 'x64')
    $main.SetAttribute('Uri', $PackageUri)
}
if ($PrepareOnly) {
    if ($feed) { $feed.Save((Join-Path $output 'sZIP.preview.appinstaller.xml')) }
    Write-Output "Prepared layout only (not an installable package): $layout"
    return
}

if (-not $SdkBinDirectory) {
    $sdk = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Windows Kits\10\bin'
    $makeAppx = Get-ChildItem -Path $sdk -Filter MakeAppx.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object FullName -match '\\x64\\MakeAppx.exe$' | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $makeAppx) { throw 'Windows SDK MakeAppx was not found. Supply -SdkBinDirectory or install the build tools.' }
    $SdkBinDirectory = $makeAppx.DirectoryName
}
$suffix = if ($Channel -eq 'Direct') { '.unsigned' } else { '' }
$packagePath = Join-Path $output "sZIP-$version-$Channel-x64$suffix.msix"
if (Test-Path -LiteralPath $packagePath) { throw 'Output already exists. Choose a new output directory.' }
& (Join-Path $SdkBinDirectory 'MakeAppx.exe') pack /d $layout /p $packagePath
if ($LASTEXITCODE -ne 0) { throw 'MSIX validation/packing failed.' }

if ($CertificateThumbprint) {
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
    if (-not $certificate.HasPrivateKey -or $certificate.Subject -ne $Publisher -or $certificate.NotAfter -le (Get-Date)) {
        throw 'Certificate must have a private key, be valid, and exactly match the package Publisher.'
    }
    if (-not $TimestampUri -or $TimestampUri.Scheme -notin @('http','https')) { throw 'Supply the signing provider timestamp URI.' }
    $signTool = Join-Path $SdkBinDirectory 'SignTool.exe'
    & $signTool sign /sha1 $CertificateThumbprint /s My /fd SHA256 /tr $TimestampUri.AbsoluteUri /td SHA256 $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'Package signing failed; do not distribute the output.' }
    & $signTool verify /pa $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'Package signature is not trusted; do not distribute the output.' }
    $signedPath = Join-Path $output "sZIP-$version-$Channel-x64.msix"
    if (Test-Path -LiteralPath $signedPath) { throw 'Signed output already exists. Choose a new output directory.' }
    Move-Item -LiteralPath $packagePath -Destination $signedPath
    $packagePath = $signedPath
}
if ($feed) {
    $feedName = if ($Unsigned) { 'sZIP.preview.appinstaller.xml' } else { 'sZIP.appinstaller' }
    $feed.Save((Join-Path $output $feedName))
}
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$packagePath.sha256", "$hash  $([IO.Path]::GetFileName($packagePath))" + [Environment]::NewLine, [Text.Encoding]::ASCII)
Write-Output "Package: $packagePath"
if ($Unsigned) { Write-Warning 'Validation artifact only: unsigned MSIX is not ready for direct installation.' }
