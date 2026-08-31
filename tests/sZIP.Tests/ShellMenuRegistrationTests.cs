using System.Diagnostics;
using System.Text;
using sZIP.App;

namespace sZIP.Tests;

public sealed class ShellMenuRegistrationTests
{
    [Theory]
    [InlineData(true, true, "READY", "ShellStatusReady", true)]
    [InlineData(true, true, "READY\r\n", "ShellStatusReady", true)]
    [InlineData(false, true, "MISSING", "ShellStatusDisabled", true)]
    [InlineData(false, false, "MISSING", "ShellStatusDisabled", true)]
    [InlineData(true, false, "READY", "ShellStatusPackageMissing", false)]
    [InlineData(true, false, "MISSING", "ShellStatusPackageMissing", false)]
    [InlineData(false, true, "READY", "ShellStatusRepairNeeded", false)]
    [InlineData(true, true, "MISSING", "ShellStatusRepairNeeded", false)]
    [InlineData(true, true, "REPAIR", "ShellStatusRepairNeeded", false)]
    [InlineData(true, true, "", "ShellStatusCheckFailed", false)]
    [InlineData(true, true, "unexpected output", "ShellStatusCheckFailed", false)]
    public void StatusDoesNotClaimUnverifiedRegistration(bool classic, bool payload, string output, string key, bool success)
    {
        var result = ShellMenuRegistration.InterpretStatus(classic, payload, output);
        Assert.Equal(key, result.MessageKey);
        Assert.Equal(success, result.Success);
    }

    [Fact]
    public void StaleClassicRegistrationNeedsRepairRatherThanBeingReportedDisabled() =>
        Assert.Equal("ShellStatusRepairNeeded", ShellMenuRegistration.InterpretStatus(false, true, "MISSING", true).MessageKey);

    // Exercise generated commands in real PowerShell with fake cmdlets.
    // These functions shadow Appx commands: tests never register/remove a real package.
    [Theory]
    [InlineData(true, false, "1.8.0.0", "Ok", "")]
    [InlineData(true, true, "1.8.0.0", "Ok", "removed\nadded")]
    [InlineData(true, false, "1.7.0.0", "Ok", "removed\nadded")]
    [InlineData(true, false, "1.8.0.0", "Modified", "removed\nadded")]
    [InlineData(true, false, "", "", "added")]
    [InlineData(false, false, "1.8.0.0", "Ok", "removed")]
    [InlineData(false, false, "", "", "")]
    public void RegistrationCommandsHandleRepairUpgradeAndRemoval(bool enabled, bool force, string installedVersion,
        string status, string expected)
    {
        var command = ShellMenuRegistration.RegistrationCommand(enabled, force, "1.8.0.0",
            @"C:\sZIP's folder\한글\sZIP.ContextMenu.msix", @"C:\sZIP's folder\한글");
        var script = FakePackage(installedVersion, status) + @"
function Remove-AppxPackage {
    param([Parameter(ValueFromPipeline=$true)]$Package)
    process { 'removed' }
}
function Add-AppxPackage {
    param($Path, $ExternalLocation, [switch]$AllowUnsigned)
    if($Path -ne 'C:\sZIP''s folder\한글\sZIP.ContextMenu.msix' -or
       $ExternalLocation -ne 'C:\sZIP''s folder\한글' -or $AllowUnsigned) { throw 'Arguments changed' }
    'added'
}
";
        Assert.Equal(expected, RunFakeScript(script + command));
    }

    [Theory]
    [InlineData("1.8.0.0", "Ok", "READY")]
    [InlineData("1.7.0.0", "Ok", "REPAIR")]
    [InlineData("1.8.0.0", "Modified", "REPAIR")]
    [InlineData("", "", "MISSING")]
    public void StatusProbeIsReadOnlyAndChecksVersionAndHealth(string version, string status, string expected)
    {
        var script = FakePackage(version, status)
            + "function Remove-AppxPackage { throw 'Read-only probe attempted removal' }; "
            + "function Add-AppxPackage { throw 'Read-only probe attempted registration' }; ";
        Assert.Equal(expected, RunFakeScript(script + ShellMenuRegistration.ProbeCommand("1.8.0.0")));
    }

    [Fact]
    public void RegistrationFailureIsNotSilentlySuccessful()
    {
        var script = FakePackage("", "") + "function Add-AppxPackage { Write-Progress -Activity 'Test progress' -Status 'Working'; Write-Error 'Test registration denied' }; "
            + ShellMenuRegistration.RegistrationCommand(true, true, "1.8.0.0", "test.msix", "test");
        RunFakeScript(script, expectFailure: true);
    }

    [Fact]
    public void UnsignedPayloadExplainsTheClassicFallbackWithoutClaimingModernSuccess()
    {
        var result = ShellMenuRegistration.InterpretStatus(true, true, "MISSING", packageSigned: false);
        Assert.Equal("ShellStatusSigningRequired", result.MessageKey);
        Assert.False(result.Success);
        Assert.Equal("ShellStatusReady", ShellMenuRegistration.InterpretStatus(
            true, true, "READY", packageSigned: false).MessageKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignaturePreflightReadsThePackageRatherThanAssumingItIsSigned(bool signed)
    {
        var path = Path.Combine(Path.GetTempPath(), "szip-signature-test-" + Guid.NewGuid().ToString("N") + ".msix");
        try
        {
            using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            {
                zip.CreateEntry("AppxManifest.xml");
                if (signed) zip.CreateEntry("AppxSignature.p7x");
            }
            Assert.Equal(signed, ShellMenuRegistration.HasPackageSignature(path));
        }
        finally { File.Delete(path); }
    }

    private static string FakePackage(string version, string status) => string.IsNullOrEmpty(version)
        ? "function Get-AppxPackage { param($Name) }; "
        : $"function Get-AppxPackage {{ param($Name) [pscustomobject]@{{ Version=[version]'{version}'; Status='{status}' }} }}; ";

    private static string RunFakeScript(string script, bool expectFailure = false)
    {
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(powershell,
            "-NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand "
            + Convert.ToBase64String(Encoding.Unicode.GetBytes(ShellMenuRegistration.WrapCommand(script))))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000)) { process.Kill(); throw new TimeoutException("Fake PowerShell test timed out."); }
        var detail = error.GetAwaiter().GetResult();
        if (expectFailure)
        {
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Test registration denied", detail);
            Assert.DoesNotContain("CLIXML", detail);
            Assert.DoesNotContain("<Objs", detail);
        }
        else Assert.True(process.ExitCode == 0, detail);
        return output.GetAwaiter().GetResult().Replace("\r\n", "\n").Trim();
    }
}
