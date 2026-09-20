[CmdletBinding()]
param(
    [string]$PublishDirectory = (Join-Path $PSScriptRoot 'artifacts\publish'),
    [string]$OutputDirectory,
    [string]$SdkBinDirectory
)
$ErrorActionPreference = 'Stop'
$arguments = @{
    Channel = 'Store'
    IdentityName = '8B91E56A.sZIP'
    Publisher = 'CN=2F094A43-B9EE-4C1D-8DA7-69C507CA03A4'
    PublisherDisplayName = '신상규'
    PublishDirectory = $PublishDirectory
}
if ($OutputDirectory) { $arguments.OutputDirectory = $OutputDirectory }
if ($SdkBinDirectory) { $arguments.SdkBinDirectory = $SdkBinDirectory }
& (Join-Path $PSScriptRoot 'build_msix.ps1') @arguments
