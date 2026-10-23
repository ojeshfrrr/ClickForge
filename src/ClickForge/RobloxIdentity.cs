using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace ClickForge;

internal static class RobloxIdentity
{
    // Both timing modes use the existing signed-client check. A title alone is insufficient.
    public static bool IsVerified(string path, FileVersionInfo version)
    {
        string name = System.IO.Path.GetFileName(path);
        if (!name.Equals("RobloxPlayerBeta.exe", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("Windows10Universal.exe", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("RobloxPlayer.exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (!(version.CompanyName?.Contains("Roblox", StringComparison.OrdinalIgnoreCase) ?? false) || !Native.HasValidSignature(path)) return false;
#pragma warning disable SYSLIB0057
        using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
        return certificate.Subject.Contains("Roblox", StringComparison.OrdinalIgnoreCase);
    }
}
