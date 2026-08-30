namespace sZIP.App;

internal sealed class ShellIntegrationResult
{
    public ShellIntegrationResult(string messageKey, bool success, string details = "")
    {
        MessageKey = messageKey;
        Success = success;
        Details = details;
    }

    public string MessageKey { get; }
    public bool Success { get; }
    public string Details { get; }
}

// Pure command construction and status interpretation; no registry or process access.
internal static class ShellMenuRegistration
{
    private const string FindPackage = "$p=@(Get-AppxPackage -Name 'sZIP.ContextMenu'); ";

    public static string ProbeCommand(string version) => FindPackage
        + "if($p.Count -eq 0){'MISSING'} elseif($p.Count -eq 1 -and "
        + $"$p[0].Version.ToString() -eq '{Literal(version)}' -and $p[0].Status.ToString() -eq 'Ok')"
        + "{'READY'} else {'REPAIR'}";

    public static string RegistrationCommand(bool enabled, bool force, string version, string package, string location)
    {
        if (!enabled) return FindPackage + "if($p.Count -gt 0){$p | Remove-AppxPackage}";
        var condition = force ? "$true" : $"$p.Count -ne 1 -or $p[0].Version.ToString() -ne '{Literal(version)}' -or $p[0].Status.ToString() -ne 'Ok'";
        return FindPackage + $"if({condition}){{ "
            + "if($p.Count -gt 0){$p | Remove-AppxPackage}; "
            + $"Add-AppxPackage -Path '{Literal(package)}' -ExternalLocation '{Literal(location)}' -AllowUnsigned }}";
    }

    public static ShellIntegrationResult InterpretStatus(bool classicRegistered, bool payloadAvailable, string output,
        bool partialClassicRegistration = false)
    {
        var marker = output.Trim();
        if (marker != "READY" && marker != "MISSING" && marker != "REPAIR")
            return new ShellIntegrationResult("ShellStatusCheckFailed", false, output);
        if (marker == "MISSING" && !classicRegistered && !partialClassicRegistration)
            return new ShellIntegrationResult("ShellStatusDisabled", true);
        if (!payloadAvailable) return new ShellIntegrationResult("ShellStatusPackageMissing", false);
        if (marker != "READY" || !classicRegistered)
            return new ShellIntegrationResult("ShellStatusRepairNeeded", false);
        return new ShellIntegrationResult("ShellStatusReady", true);
    }

    private static string Literal(string value) => value.Replace("'", "''");
}
