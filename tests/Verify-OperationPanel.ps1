param([Parameter(Mandatory = $true)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ExecutablePath).Path)
[System.Windows.Application]::ResourceAssembly = $assembly
$app = New-Object System.Windows.Application
[xml]$markup = Get-Content (Join-Path $PSScriptRoot '..\src\sZIP.App\App.xaml') -Raw
[xml]$dictionary = '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />'
foreach ($entry in $markup.SelectSingleNode("/*/*[local-name()='Application.Resources']").ChildNodes) {
    $dictionary.DocumentElement.AppendChild($dictionary.ImportNode($entry, $true)) | Out-Null
}
$app.Resources = [System.Windows.Markup.XamlReader]::Parse($dictionary.OuterXml)
$window = [Activator]::CreateInstance($assembly.GetType('sZIP.App.MainWindow'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$type = $window.GetType()
$panel = $window.FindName('OperationPanel')
$progress = $window.FindName('OperationProgress')
function Assert-Panel([string]$expected) {
    if ([string]$panel.Visibility -ne $expected) { throw "Expected $expected panel, got $($panel.Visibility)" }
}
try {
    Assert-Panel 'Collapsed'
    $type.GetMethod('SetBusy', $flags).Invoke($window, @($true)) | Out-Null
    Assert-Panel 'Visible'
    $type.GetMethod('SetOperationCompleted', $flags).Invoke($window, @('Done', 'test.zip')) | Out-Null
    $type.GetMethod('SetBusy', $flags).Invoke($window, @($false)) | Out-Null
    Assert-Panel 'Collapsed'
    $type.GetField('_automaticExtractionActive', $flags).SetValue($window, $true)
    $type.GetMethod('UpdateOperationPanelVisibility', $flags).Invoke($window, @()) | Out-Null
    Assert-Panel 'Visible'
    if (-not $progress.IsIndeterminate) { throw 'Automatic progress must be indeterminate.' }
    $type.GetMethod('SetBusy', $flags).Invoke($window, @($true)) | Out-Null
    if ($progress.IsIndeterminate) { throw 'Manual progress must be determinate.' }
    $type.GetField('_automaticExtractionActive', $flags).SetValue($window, $false)
    $type.GetMethod('UpdateOperationPanelVisibility', $flags).Invoke($window, @()) | Out-Null
    Assert-Panel 'Visible'
    $type.GetMethod('SetBusy', $flags).Invoke($window, @($false)) | Out-Null
    Assert-Panel 'Collapsed'
    $window.Measure([System.Windows.Size]::new(1100,720))
    $window.Arrange([System.Windows.Rect]::new(0,0,1100,720))
    $window.UpdateLayout()
    if ($panel.ActualHeight -gt 0) { throw 'Collapsed progress must not reserve layout space.' }
    Write-Output 'PASS: idle/completed hidden, manual/automatic active visible, concurrent work preserved, no reserved space.'
} finally {
    $window.AllowExit()
    $window.Close()
    $app.Shutdown()
}
