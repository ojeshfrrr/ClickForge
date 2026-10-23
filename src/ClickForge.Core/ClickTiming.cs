namespace ClickForge.Core;

public enum ClickTimingMode { Fixed, Variable }

public readonly record struct CpsRange
{
    public int Minimum { get; }
    public int Maximum { get; }
    public CpsRange(int minimum, int maximum)
    {
        Speed.Validate(minimum); Speed.Validate(maximum);
        if (minimum > maximum) throw new ArgumentException("Minimum CPS cannot exceed maximum CPS.");
        Minimum = minimum; Maximum = maximum;
    }
    public void Validate() { _ = new CpsRange(Minimum, Maximum); }
}

// Uniform rates, rather than a human-behavior model. The seed is useful for tests.
public sealed class RateIntervalSequence
{
    private readonly CpsRange range;
    private readonly Random random;
    public RateIntervalSequence(CpsRange range, int? seed = null)
    { range.Validate(); this.range = range; random = seed is { } value ? new Random(value) : new Random(); }
    public double NextMilliseconds() => 1000d / (range.Minimum + random.NextDouble() * (range.Maximum - range.Minimum));
}

public interface IVariableTargetGate : ITargetGate
{
    bool CanActivate { get; }
}

public static class DesktopTargetPolicy
{
    // Missing identity is never eligible. Check metadata as well as the executable name.
    public static bool RequiresRobloxVerification(params string?[] identity) => identity.Any(value => value?.Contains("roblox", StringComparison.OrdinalIgnoreCase) == true);
    public static bool IsEligible(string? path, string? company, string? product, string? originalName, string? title, bool verifiedRoblox = false)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(product)) return false;
        return verifiedRoblox || !RequiresRobloxVerification(path, company, product, originalName, title);
    }
}
