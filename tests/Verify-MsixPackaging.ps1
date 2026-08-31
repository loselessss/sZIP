param(
    [string]$PublishDirectory = (Join-Path $PSScriptRoot '..\src\sZIP.App\bin\Release\net48'),
    [string]$SdkBinDirectory
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo ('artifacts\msix-schema-tests\' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $root 'fixture'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
foreach ($item in Get-ChildItem -LiteralPath $PublishDirectory) {
    Copy-Item -LiteralPath $item.FullName -Destination $fixture -Recurse
}
# Deliberately non-executable: this test covers package schema/layout, NOT native COM or installation.
[IO.File]::WriteAllText((Join-Path $fixture 'sZIP.ShellExtension.dll'), 'SCHEMA TEST FIXTURE - NOT A NATIVE DLL')
[IO.File]::WriteAllText((Join-Path $fixture 'sZIP.ContextMenu.msix'), 'old sparse package must be excluded')
$classes = @{}
foreach ($channel in @('Store','Direct')) {
    $output = Join-Path $root $channel
    $arguments = @{
        Channel = $channel; IdentityName = "sZIP.SchemaTest.$channel"; Publisher = 'CN=sZIP Schema Test'
        PublisherDisplayName = 'sZIP Schema Test'; PublishDirectory = $fixture; OutputDirectory = $output
        Unsigned = $true
    }
    if ($SdkBinDirectory) { $arguments.SdkBinDirectory = $SdkBinDirectory }
    else { $arguments.PrepareOnly = $true }
    if ($channel -eq 'Direct') {
        $arguments.AppInstallerUri = 'https://example.invalid/sZIP.appinstaller'
        $arguments.PackageUri = 'https://example.invalid/sZIP.msix'
    }
    & (Join-Path $repo 'build_msix.ps1') @arguments
    if ($SdkBinDirectory) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $package = Get-ChildItem -LiteralPath $output -Filter '*.msix' | Select-Object -First 1
        if (-not $package) { throw 'Schema test package missing.' }
        $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            if ($zip.GetEntry('sZIP.ContextMenu.msix')) { throw 'Sparse package leaked into full MSIX.' }
            if ($zip.GetEntry('AppxSignature.p7x')) { throw 'Schema test must not sign packages.' }
            $reader = [IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open())
            try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $classes[$channel] = $manifest.SelectSingleNode("//*[local-name()='Class']").Id
            if ($manifest.Package.Identity.Name -ne "sZIP.SchemaTest.$channel") { throw 'Wrong package identity.' }
            $reader = [IO.StreamReader]::new($zip.GetEntry('MsixDistribution.xml').Open())
            try { [xml]$distribution = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($distribution.Distribution.Channel -ne $channel) { throw 'Wrong update channel.' }
        } finally { $zip.Dispose() }
    }
    if ($channel -eq 'Direct') {
        [xml]$feed = Get-Content -LiteralPath (Join-Path $output 'sZIP.preview.appinstaller.xml')
        if ($feed.AppInstaller.MainPackage.Name -ne 'sZIP.SchemaTest.Direct') { throw 'Wrong feed identity.' }
        if ($feed.AppInstaller.MainPackage.Uri -ne 'https://example.invalid/sZIP.msix') { throw 'Wrong feed URL.' }
        if (Test-Path -LiteralPath (Join-Path $output 'sZIP.appinstaller')) { throw 'Unsigned test must not emit a deployable feed.' }
    }
}
if ($SdkBinDirectory -and $classes.Store -eq $classes.Direct) { throw 'COM IDs must be channel-specific.' }

$blocked = $false
try {
    & (Join-Path $repo 'build_msix.ps1') -Channel Direct -IdentityName sZIP.SchemaTest.Direct -Publisher 'CN=sZIP Schema Test' -PublisherDisplayName Test -PublishDirectory $fixture -AppInstallerUri https://example.invalid/sZIP.appinstaller -PackageUri https://example.invalid/sZIP.msix
} catch {
    if ($_.Exception.Message -notlike '*requires a signing certificate*') { throw }
    $blocked = $true
}
if (-not $blocked) { throw 'Unsigned direct distribution was not blocked.' }
Write-Output 'PASS: Store/Direct layouts, channel separation, package metadata and unsigned-distribution guard. No installation or native COM test was performed.'
