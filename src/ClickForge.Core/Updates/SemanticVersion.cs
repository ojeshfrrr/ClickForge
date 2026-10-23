using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ClickForge.Core.Updates;

public sealed record SemanticVersion : IComparable<SemanticVersion>
{
    private static readonly Regex Pattern = new(@"^(?:v)?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public string Value { get; }
    public string? Prerelease { get; }
    private readonly BigInteger major, minor, patch;
    private SemanticVersion(string value, Match match)
    {
        Value = value.TrimStart('v'); major = BigInteger.Parse(match.Groups[1].Value); minor = BigInteger.Parse(match.Groups[2].Value); patch = BigInteger.Parse(match.Groups[3].Value);
        Prerelease = match.Groups[4].Success ? match.Groups[4].Value : null;
        if (Prerelease?.Split('.').Any(p => p.All(char.IsAsciiDigit) && p.Length > 1 && p[0] == '0') == true) throw new FormatException("Numeric prerelease identifiers cannot have leading zeros.");
    }
    public static SemanticVersion Parse(string? value)
    {
        if (value is null || value.Length > 200) throw new FormatException("Version is missing or too long.");
        var match = Pattern.Match(value); if (!match.Success) throw new FormatException("Expected a semantic version such as 1.2.3.");
        return new(value, match);
    }
    public static SemanticVersion Installed(Assembly assembly) => Parse(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? throw new InvalidOperationException("Installed version metadata is missing."));
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        foreach (int c in new[] { major.CompareTo(other.major), minor.CompareTo(other.minor), patch.CompareTo(other.patch) }) if (c != 0) return c;
        if (Prerelease is null) return other.Prerelease is null ? 0 : 1;
        if (other.Prerelease is null) return -1;
        string[] a = Prerelease.Split('.'), b = other.Prerelease.Split('.');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            bool an = a[i].All(char.IsAsciiDigit), bn = b[i].All(char.IsAsciiDigit);
            int c = an && bn ? BigInteger.Parse(a[i]).CompareTo(BigInteger.Parse(b[i])) : an != bn ? an ? -1 : 1 : string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }
    public override string ToString() => Value;
}
