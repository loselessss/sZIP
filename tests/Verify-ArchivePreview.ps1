param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo 'artifacts\preview-ui-checks'
$fixtures = Join-Path $output ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, WindowsFormsIntegration, System.Windows.Forms, System.IO.Compression, System.IO.Compression.FileSystem
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repo "src\sZIP.App\bin\$Configuration\net48\sZIP.App.exe"))
$domain = [Reflection.Assembly]::LoadFrom((Join-Path $repo "src\sZIP.Domain\bin\$Configuration\net48\sZIP.Domain.dll"))
$application = New-Object System.Windows.Application
$application.ShutdownMode = 'OnExplicitShutdown'
[xml]$markup = Get-Content (Join-Path $repo 'src\sZIP.App\App.xaml') -Raw
$application.Resources = [System.Windows.Markup.XamlReader]::Parse(
    '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">' +
    $markup.Application.'Application.Resources'.InnerXml + '</ResourceDictionary>')
$localization = $assembly.GetType('sZIP.App.Localization')
$entryType = $domain.GetType('sZIP.Domain.ArchiveEntryInfo')
$entries = [Array]::CreateInstance($entryType, 7)
$names = @('nested/', 'nested/hello.txt', 'image.png', 'sample.szip-test-binary', 'sample.exe', 'sample.pdf', 'sample.wav')
for ($index = 0; $index -lt $names.Count; $index++) {
    $entries.SetValue([Activator]::CreateInstance($entryType, @(
        $names[$index], [long]50, [long]40, ($index -eq 0), [DateTimeOffset]::Now, $false)), $index)
}
$archivePath = Join-Path $fixtures 'preview.zip'
$archive = [IO.Compression.ZipFile]::Open($archivePath, 'Create')
try {
    $archive.CreateEntry('nested/') | Out-Null
    foreach ($name in @('nested/hello.txt', 'sample.szip-test-binary', 'sample.exe')) {
        $stream = $archive.CreateEntry($name).Open()
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes('Preview <b>plain text</b>')
            $stream.Write($bytes, 0, $bytes.Length)
        } finally { $stream.Dispose() }
    }
    $drawing = New-Object System.Windows.Media.DrawingVisual
    $context = $drawing.RenderOpen()
    $context.DrawRectangle([System.Windows.Media.Brushes]::CornflowerBlue, $null, [System.Windows.Rect]::new(0,0,100,60))
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(100,60,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($drawing)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $imageData = New-Object IO.MemoryStream
    $encoder.Save($imageData)
    $imageData.Position = 0
    $stream = $archive.CreateEntry('image.png').Open()
    try { $imageData.CopyTo($stream) } finally { $stream.Dispose(); $imageData.Dispose() }
    $nl = [Environment]::NewLine
    $pdf = New-Object Text.StringBuilder
    $pdf.Append('%PDF-1.4' + $nl) | Out-Null
    $offsets = New-Object 'Collections.Generic.List[int]'
    $content = 'BT /F1 24 Tf 50 100 Td (sZIP Preview) Tj ET'
    $objects = @(
        '<< /Type /Catalog /Pages 2 0 R >>',
        '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
        '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
        '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
        ("<< /Length $($content.Length) >>" + $nl + 'stream' + $nl + $content + $nl + 'endstream'))
    for ($i=0; $i -lt $objects.Length; $i++) {
        $offsets.Add($pdf.Length)
        $pdf.Append("$($i+1) 0 obj" + $nl + $objects[$i] + $nl + 'endobj' + $nl) | Out-Null
    }
    $xref = $pdf.Length
    $pdf.Append('xref' + $nl + '0 6' + $nl + '0000000000 65535 f ' + $nl) | Out-Null
    foreach ($offset in $offsets) { $pdf.Append($offset.ToString('0000000000') + ' 00000 n ' + $nl) | Out-Null }
    $pdf.Append('trailer' + $nl + '<< /Size 6 /Root 1 0 R >>' + $nl + 'startxref' + $nl + $xref + $nl + '%%EOF') | Out-Null
    $stream = $archive.CreateEntry('sample.pdf').Open()
    try {
        $bytes = [Text.Encoding]::ASCII.GetBytes($pdf.ToString())
        $stream.Write($bytes,0,$bytes.Length)
    } finally { $stream.Dispose() }
    $wave = New-Object IO.MemoryStream
    $writer = [IO.BinaryWriter]::new($wave)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
    $writer.Write([int]8036)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $writer.Write([int]16)
    $writer.Write([int16]1)
    $writer.Write([int16]1)
    $writer.Write([int]8000)
    $writer.Write([int]8000)
    $writer.Write([int16]1)
    $writer.Write([int16]8)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
    $writer.Write([int]8000)
    for ($i=0; $i -lt 8000; $i++) { $writer.Write([byte]128) }
    $writer.Flush()
    $wave.Position = 0
    $stream = $archive.CreateEntry('sample.wav').Open()
    try { $wave.CopyTo($stream) } finally { $stream.Dispose(); $writer.Dispose(); $wave.Dispose() }
} finally { $archive.Dispose() }

try {
    foreach ($language in @('ko','en')) {
        $localization.GetMethod('Apply').Invoke($null, @($language)) | Out-Null
        for ($index = 0; $index -lt $entries.Length; $index++) {
            $arguments = New-Object object[] 4
            $arguments[0] = $archivePath.PSObject.BaseObject
            $arguments[1] = $entries.GetValue($index).PSObject.BaseObject
            $arguments[3] = $entries.PSObject.BaseObject
            $window = $assembly.GetType('sZIP.App.PreviewWindow').GetConstructors()[0].Invoke($arguments)
            try {
                $window.WindowStartupLocation = 'Manual'
                $window.Left = -10000
                $window.Top = -10000
                $window.ShowActivated = $false
                $window.ShowInTaskbar = $false
                $window.Show()
                $deadline = [DateTime]::UtcNow.AddSeconds(20)
                while ($window.FindName('PreparationProgress').Visibility -ne 'Collapsed') {
                    if ([DateTime]::UtcNow -gt $deadline) { throw "Preview did not finish: $($names[$index])" }
                    $window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ApplicationIdle)
                    Start-Sleep -Milliseconds 20
                }
                $window.UpdateLayout()
                $content = $window.FindName('PreviewContent').Content
                $pdfHandler = [Microsoft.Win32.Registry]::ClassesRoot.OpenSubKey('.pdf\shellex\{8895B1C6-B41F-4C1C-A562-0D564250836F}')
                $pdfExpected = if ($pdfHandler) { 'WindowsPreviewHost' } else { 'TextBox' }
                if ($pdfHandler) { $pdfHandler.Dispose() }
                $expected = @('DataGrid','TextBox','Image','TextBox','TextBox',$pdfExpected,'Grid')[$index]
                if ($index -eq 6 -and $content.GetType().Name -eq 'WindowsPreviewHost') { $expected = 'WindowsPreviewHost' }
                if ($content.GetType().Name -ne $expected) { throw "Unexpected viewer: $($content.GetType().Name)" }
                if ($index -eq 6 -and $expected -eq 'Grid') {
                    $media = $window.GetType().GetField('_media','NonPublic,Instance').GetValue($window)
                    $media.Play()
                    $window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ApplicationIdle)
                    $media.Stop()
                }
                if ($index -eq 1 -and $content.Text -notmatch 'plain text') { throw 'Text preview is missing content.' }
                if ($index -eq 4 -and $window.FindName('OpenFileButton').IsEnabled) { throw 'Executable launch was enabled.' }
                if ($index -eq 0 -and $content.Items.Count -ne 1) { throw 'Folder preview did not filter descendants.' }
                $session = $window.GetType().GetField('_session', 'NonPublic,Instance').GetValue($window)
                $sessionPath = if ($session) { $session.DirectoryPath } else { $null }
                $visual = $window.Content
                $width = [int]($visual.ActualWidth + $visual.Margin.Left + $visual.Margin.Right)
                $height = [int]($visual.ActualHeight + $visual.Margin.Top + $visual.Margin.Bottom)
                $render = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
                    $width, $height, 96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
                $background = New-Object System.Windows.Media.DrawingVisual
                $context = $background.RenderOpen()
                $context.DrawRectangle($window.Background, $null, [System.Windows.Rect]::new(0,0,$width,$height))
                $context.Close()
                $render.Render($background)
                $render.Render($visual)
                $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
                $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($render))
                $stream = [IO.File]::Create((Join-Path $output "preview-$language-$index.png"))
                try { $encoder.Save($stream) } finally { $stream.Dispose() }
                $window.Close()
                if ($sessionPath -and (Test-Path -LiteralPath $sessionPath)) { throw 'Preview directory was not cleaned up.' }
            } finally { $window.Close() }
        }
    }
    Write-Output 'PASS: Korean/English image, text, binary, executable, folder, PDF, media previews and temporary file cleanup.'
} finally {
    # This path is an explicitly created GUID child of the test output directory.
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixtures)) -ne [IO.Path]::GetFullPath($output)) {
        throw 'Unexpected fixture directory.'
    }
    Remove-Item -LiteralPath $fixtures -Recurse -Force
    $application.Shutdown()
}
