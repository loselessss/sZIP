param(
    [string]$Configuration = 'Release',
    [switch]$UpdateOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo 'artifacts\ui-checks'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Configuration, System.Net.Http
Add-Type -ReferencedAssemblies System.Configuration, System -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
public sealed class SzipMemorySettingsProvider : SettingsProvider {
    readonly Dictionary<string, object> values = new Dictionary<string, object>();
    public int SaveCount;
    public override string ApplicationName { get; set; }
    public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties) {
        var result = new SettingsPropertyValueCollection();
        foreach (SettingsProperty property in properties) {
            object value;
            if (!values.TryGetValue(property.Name, out value))
                value = Convert.ChangeType(property.DefaultValue, property.PropertyType, CultureInfo.InvariantCulture);
            result.Add(new SettingsPropertyValue(property) { PropertyValue = value });
        }
        return result;
    }
    public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection properties) {
        foreach (SettingsPropertyValue value in properties) values[value.Name] = value.PropertyValue;
        SaveCount++;
    }
}
'@
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repo "src\sZIP.App\bin\$Configuration\net48\sZIP.App.exe"))
$applicationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $repo "src\sZIP.Application\bin\$Configuration\net48\sZIP.Application.dll"))
$settings = $assembly.GetType('sZIP.App.UserSettings').GetProperty('Default').GetValue($null, $null)
$provider = New-Object SzipMemorySettingsProvider
$provider.Initialize('UiTests', (New-Object Collections.Specialized.NameValueCollection))
$settings.Providers.Clear()
$settings.Providers.Add($provider)
foreach ($property in $settings.Properties) { $property.Provider = $provider }
$settings.Reload()
$settings.AutomaticArchiveExtractionFolder = 'C:\Downloads'

# Load only application resources; never start the watcher, tray, or updater.
$application = New-Object System.Windows.Application
$application.ShutdownMode = 'OnExplicitShutdown'
[xml]$appXaml = Get-Content -LiteralPath (Join-Path $repo 'src\sZIP.App\App.xaml') -Raw
$resourceMarkup = '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">' + $appXaml.Application.'Application.Resources'.InnerXml + '</ResourceDictionary>'
$application.Resources = [System.Windows.Markup.XamlReader]::Parse($resourceMarkup)
$localization = $assembly.GetType('sZIP.App.Localization')
function Set-Language([string]$language) { $localization.GetMethod('Apply').Invoke($null, @($language)) | Out-Null }
function New-TestWindow([string]$type) {
    $window = [Activator]::CreateInstance($assembly.GetType('sZIP.App.' + $type))
    $window.WindowStartupLocation = 'Manual'
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    return $window
}
function New-UpdateWindow([string]$language) {
    $versionType = $applicationAssembly.GetType('sZIP.Application.ReleaseVersion')
    $assetType = $applicationAssembly.GetType('sZIP.Application.ReleaseAsset')
    $updateType = $applicationAssembly.GetType('sZIP.Application.AvailableUpdate')
    $serviceType = $applicationAssembly.GetType('sZIP.Application.GitHubUpdateService')
    $currentVersion = [Activator]::CreateInstance($versionType, @([int]1, [int]6, [int]1))
    $newVersion = [Activator]::CreateInstance($versionType, @([int]1, [int]6, [int]2))
    $asset = [Activator]::CreateInstance($assetType, @(
        'sZIP_Setup_1.6.2.exe',
        [Uri]'https://example.invalid/sZIP_Setup_1.6.2.exe',
        [long]16777216,
        ('a' * 64)))
    $notes = "## Highlights`r`n`r`n- Added an application language setting.`r`n- Moved preferences into a dedicated settings window."
    $update = [Activator]::CreateInstance($updateType, @(
        $newVersion,
        'v1.6.2',
        'sZIP 1.6.2',
        $notes,
        [Uri]'https://example.invalid/releases/v1.6.2',
        '2026-08-27T00:00:00Z',
        $asset))
    $service = $serviceType.GetConstructor(@($versionType, [System.Net.Http.HttpClient], [string], [string])).Invoke(
        @($currentVersion, $null, $null, $language))
    $window = $assembly.GetType('sZIP.App.UpdateDialog').GetConstructor(@($serviceType, $updateType)).Invoke(
        @($service, $update))
    $window.WindowStartupLocation = 'Manual'
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    return @{ Window = $window; Service = $service }
}
function Close-TestWindow($window) {
    if ($window.GetType().Name -eq 'MainWindow') { $window.AllowExit() }
    $window.Close()
}
function Assert-TextFits($root, $node) {
    if ($node -is [System.Windows.Controls.TextBlock] -and $node.IsVisible -and $node.Text -and
        $node.TextWrapping -eq 'NoWrap' -and $node.TextTrimming -eq 'None' -and $node.ActualWidth -gt 0) {
        $font = New-Object System.Windows.Media.Typeface($node.FontFamily, $node.FontStyle, $node.FontWeight, $node.FontStretch)
        $formatted = New-Object System.Windows.Media.FormattedText($node.Text, [Globalization.CultureInfo]::CurrentCulture,
            $node.FlowDirection, $font, $node.FontSize, [System.Windows.Media.Brushes]::Black)
        if ($formatted.Width -gt $node.ActualWidth + 2) { throw "Clipped text: $($node.Text)" }
        $position = $node.TransformToAncestor($root).Transform([System.Windows.Point]::new(0, 0))
        if ($position.X + $node.ActualWidth -gt $root.ActualWidth + 2) { throw "Text outside window: $($node.Text)" }
    }
    for ($i = 0; $i -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($node); $i++) {
        Assert-TextFits $root ([System.Windows.Media.VisualTreeHelper]::GetChild($node, $i))
    }
}
function Render-Window($window, [string]$name, [double]$scale) {
    $window.Show()
    $window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($window.GetType().Name -eq 'SettingsWindow') {
        $deadline = [DateTime]::UtcNow.AddSeconds(40)
        while (-not $window.FindName('SaveButton').IsEnabled) {
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Shell status check did not finish.' }
            $window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ApplicationIdle)
            Start-Sleep -Milliseconds 25
        }
    }
    $window.UpdateLayout()
    $content = $window.Content
    $content.Background = $window.Background
    Assert-TextFits $content $content
    $renderWidth = $content.ActualWidth + $content.Margin.Left + $content.Margin.Right
    $renderHeight = $content.ActualHeight + $content.Margin.Top + $content.Margin.Bottom
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        [int][Math]::Ceiling($renderWidth * $scale),
        [int][Math]::Ceiling($renderHeight * $scale),
        (96 * $scale), (96 * $scale), [System.Windows.Media.PixelFormats]::Pbgra32)
    $background = New-Object System.Windows.Media.DrawingVisual
    $drawing = $background.RenderOpen()
    $drawing.DrawRectangle($window.Background, $null,
        [System.Windows.Rect]::new(0, 0, $renderWidth, $renderHeight))
    $brush = New-Object System.Windows.Media.VisualBrush($content)
    $drawing.DrawRectangle($brush, $null,
        [System.Windows.Rect]::new($content.Margin.Left, $content.Margin.Top,
            $content.ActualWidth, $content.ActualHeight))
    $drawing.Close()
    $bitmap.Render($background)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $output "$name.png"))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

foreach ($language in @('en', 'ko')) {
    Set-Language $language
    $windowTypes = if ($UpdateOnly) { @() } else { @('MainWindow', 'SettingsWindow', 'AuditWindow', 'PasswordDialog', 'RenameEntryDialog') }
    foreach ($type in $windowTypes) {
        foreach ($scale in @(1, 2)) {
            $window = New-TestWindow $type
            try {
                if ($type -eq 'MainWindow') { $window.Width = 900 }
                if ($type -eq 'SettingsWindow') { $window.Width = 500; $window.Height = 480 }
                Render-Window $window "$type-$language-$scale" $scale
                if ($type -eq 'SettingsWindow') {
                    foreach ($removedControl in @('ShellStatusText','RefreshShellButton','RepairShellButton','ShellDetailsExpander')) {
                        if ($null -ne $window.FindName($removedControl)) { throw "Removed control still present: $removedControl" }
                    }
                    if ($null -eq $window.FindName('ShellIntegrationCheckBox')) { throw 'Explorer integration toggle missing.' }
                    if (-not $window.FindName('SaveButton').IsEnabled) { throw 'Save unavailable while idle.' }
                    $configure = $window.GetType().GetMethod('ConfigureDeploymentUi', [Reflection.BindingFlags]'Instance,NonPublic')
                    $configure.Invoke($window, @($true)) | Out-Null
                    if ($window.FindName('StartupCheckBox').Visibility -ne 'Collapsed' -or
                        $window.FindName('ShellIntegrationCheckBox').Visibility -ne 'Collapsed' -or
                        $window.FindName('MsixSettingsPanel').Visibility -ne 'Visible') { throw 'Packaged settings UI is incorrect.' }
                    Render-Window $window "$type-msix-$language-$scale" $scale
                }
            } finally { Close-TestWindow $window }
        }
    }
    foreach ($scale in @(1, 2)) {
        $updateWindow = New-UpdateWindow $language
        try {
            $english = @{
                ReleasePageButton = 'Release Page'
                SkipButton = 'Skip This Version'
                LaterButton = 'Later'
                InstallButton = 'Download and Install'
            }
            foreach ($button in $english.Keys) {
                $actual = [string]$updateWindow.Window.FindName($button).Content
                if (($language -eq 'ko' -and $actual -eq $english[$button]) -or
                    ($language -eq 'en' -and $actual -ne $english[$button])) {
                    throw "Update button language mismatch: $button is '$actual' in $language mode."
                }
            }
            Render-Window $updateWindow.Window "UpdateDialog-$language-$scale" $scale
        } finally {
            Close-TestWindow $updateWindow.Window
            $updateWindow.Service.Dispose()
        }
    }
}

if ($UpdateOnly) {
    Write-Output 'Verified localized update buttons and layouts.'
    exit 0
}

# Cancel leaves the draft unapplied; saving uses an in-memory provider, never user.config.
Set-Language 'en'
$window = New-TestWindow 'SettingsWindow'
$window.FindName('WatchFolderTextBox').Text = 'Z:\not-saved'
$window.FindName('MaxArchiveMbTextBox').Text = '999'
Close-TestWindow $window
if ($window.HasSavedSettings) { throw 'Cancel reported saved settings.' }
if ($provider.SaveCount -ne 0 -or $settings.AutomaticArchiveExtractionMaxArchiveMb -ne 200) { throw 'Cancel changed settings.' }

$window = New-TestWindow 'SettingsWindow'
$window.FindName('WatchFolderTextBox').Text = $repo
$window.FindName('MaxArchiveMbTextBox').Text = '512'
$window.FindName('LanguageComboBox').SelectedIndex = 1
$window.FindName('DeleteSourceCheckBox').IsChecked = $true
$saveTimer = New-Object System.Windows.Threading.DispatcherTimer
$saveTimer.Interval = [TimeSpan]::FromMilliseconds(50)
$saveDeadline = [DateTime]::UtcNow.AddSeconds(45)
$saveTimer.add_Tick({
    if ([DateTime]::UtcNow -gt $saveDeadline) {
        $saveTimer.Stop()
        $window.Close()
        return
    }
    if ($window.FindName('SaveButton').IsEnabled) {
        $saveTimer.Stop()
        $window.FindName('SaveButton').RaiseEvent([System.Windows.RoutedEventArgs]::new([System.Windows.Controls.Button]::ClickEvent))
    }
})
$saveTimer.Start()
try {
    if ($window.ShowDialog() -ne $true) { throw 'Save did not complete.' }
} finally { $saveTimer.Stop() }
if ($provider.SaveCount -ne 1) { throw 'Settings were not saved once.' }
if (-not $window.HasSavedSettings) { throw 'Saved settings were not reported to the app.' }
$settings.Reload()
if ($settings.Language -ne 'ko' -or $settings.AutomaticArchiveExtractionMaxArchiveMb -ne 512 -or
    -not $settings.AutomaticArchiveExtractionDeleteSourceArchive) { throw 'Saved settings did not persist.' }

$main = New-TestWindow 'MainWindow'
try {
    Set-Language 'ko'
    if ($main.FindName('AutomaticArchiveExtractionCheckBox').Content -ne $application.Resources['Text.AutomaticArchiveExtractionOff']) { throw 'Korean live switch failed.' }
    Set-Language 'en'
    if ($main.FindName('AutomaticArchiveExtractionCheckBox').Content -notlike '*Automatic Archive Extraction*') { throw 'English live switch failed.' }
} finally { Close-TestWindow $main }
$application.Shutdown()
Write-Output 'PASS: language switching, settings save/cancel, persistence, simplified Explorer settings, and 100%/200% layouts including updates.'
