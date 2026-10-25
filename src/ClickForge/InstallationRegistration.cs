using System.IO;
using Microsoft.Win32;

namespace ClickForge;

internal static class InstallationRegistration
{
    internal const string InstallerId = "ClickForge.Desktop";
    internal const string GuardName = @"Local\ClickForge.SetupGuard";
    internal const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClickForge.Desktop_is1";
    internal static void RefreshVersion(string directory, string version, string keyPath = UninstallKey)
    {
        // Only the registration created by our installer at this exact executable
        // location can be refreshed. Portable instances create no registry entries.
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
        if (key?.GetValue("ClickForgeInstallerId") as string != InstallerId || key.GetValue("InstallLocation") is not string installed) return;
        if (!string.Equals(Path.GetFullPath(installed).TrimEnd('\\'), Path.GetFullPath(directory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
        key.SetValue("DisplayVersion", version, RegistryValueKind.String);
    }
}
