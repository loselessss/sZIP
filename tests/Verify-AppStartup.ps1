param(
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [int]$TimeoutSeconds = 30,
    [switch]$NormalStartup
)

$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $ExecutablePath).Path
$report = Join-Path ([IO.Path]::GetTempPath()) ('szip-startup-' + [Guid]::NewGuid().ToString('N') + '.txt')
$process = $null
try {
    if ($NormalStartup) {
        $process = Start-Process -FilePath $app -WorkingDirectory (Split-Path -Parent $app) `
            -WindowStyle Hidden -PassThru
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            if ($process.HasExited) { throw "Normal startup exited early: $($process.ExitCode)" }
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                Start-Sleep -Milliseconds 1500
                $process.Refresh()
                if ($process.HasExited) { throw 'App exited after opening its main window.' }
                Write-Output "PASS: normal startup opened its main window. Executable: $app"
                return
            }
        } while ([DateTime]::UtcNow -lt $deadline)
        throw 'Normal startup never opened its main window.'
    }
    $process = Start-Process -FilePath $app -WorkingDirectory (Split-Path -Parent $app) `
        -ArgumentList "--startup-smoke-test `"$report`"" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Startup timed out after $TimeoutSeconds seconds: $app"
    }
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $report) { Write-Output (Get-Content -LiteralPath $report -Raw) }
        throw "Startup failed with exit code $($process.ExitCode): $app"
    }
    if (-not (Test-Path -LiteralPath $report)) { throw 'Startup exited without rendering the windows.' }
    $result = Get-Content -LiteralPath $report -Raw
    if ($result.Trim() -ne 'PASS: main window and update window rendered.') { throw $result }
    Write-Output "$result Executable: $app"
}
finally {
    if ($process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
}
