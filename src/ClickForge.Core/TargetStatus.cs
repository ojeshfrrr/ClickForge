namespace ClickForge.Core;

public enum TargetBlockReason
{
    Ready, NoSelection, ProcessClosed, WindowClosed, WindowChanged, WindowHidden,
    WindowMinimized, UnverifiedRoblox, NotForeground, PointerUnavailable, PointerOutside,
    InspectionUnavailable, SelectionRejected, RestrictionOff, NoForeground, CheckingRoblox,
    RobloxVerificationRequired, VerifiedRobloxForeground
}

// Only validation facts; no window titles, paths, identifiers or application content.
public sealed record SelectedTargetFacts
{
    public bool HasSelection { get; init; }
    public bool InspectionSucceeded { get; init; } = true;
    public bool ProcessAlive { get; init; }
    public bool WindowExists { get; init; }
    public bool IdentityMatches { get; init; }
    public bool WindowVisible { get; init; }
    public bool Minimized { get; init; }
    public bool RequiresRobloxVerification { get; init; }
    public bool VerifiedRoblox { get; init; }
    public bool Foreground { get; init; }
    public bool PointerKnown { get; init; }
    public bool PointerInside { get; init; }
    public bool WindowIdentityValid => HasSelection && InspectionSucceeded && ProcessAlive && WindowExists && IdentityMatches;
}

public readonly record struct TargetStatus(TargetBlockReason Reason)
{
    public bool ChecksPassed => Reason is TargetBlockReason.Ready or TargetBlockReason.VerifiedRobloxForeground;
    public string Label => Reason switch
    {
        TargetBlockReason.Ready => "Target ready", TargetBlockReason.NoSelection => "No target selected",
        TargetBlockReason.ProcessClosed => "Target process closed", TargetBlockReason.WindowClosed => "Target window closed",
        TargetBlockReason.WindowChanged => "Target identity changed", TargetBlockReason.WindowHidden => "Target window hidden",
        TargetBlockReason.WindowMinimized => "Target window minimized", TargetBlockReason.UnverifiedRoblox => "Roblox identity unverified",
        TargetBlockReason.NotForeground => "Target not in front", TargetBlockReason.PointerUnavailable => "Pointer status unavailable",
        TargetBlockReason.PointerOutside => "Pointer outside target", TargetBlockReason.InspectionUnavailable => "Target check unavailable",
        TargetBlockReason.SelectionRejected => "Target selection rejected", TargetBlockReason.RestrictionOff => "Foreground restriction off",
        TargetBlockReason.NoForeground => "No foreground window", TargetBlockReason.CheckingRoblox => "Checking Roblox identity",
        TargetBlockReason.VerifiedRobloxForeground => "Roblox foreground verified", _ => "Verified Roblox required"
    };
    public string Explanation => Reason switch
    {
        TargetBlockReason.Ready => "The selected process and window are valid, in front, and under the pointer. Input is eligible while these checks continue to pass.",
        TargetBlockReason.NoSelection => "Select an eligible target window. Refresh windows to find open applications.",
        TargetBlockReason.ProcessClosed => "The selected process has exited. Refresh windows and select a running target.",
        TargetBlockReason.WindowClosed => "The selected window was closed. Refresh windows and select a current window.",
        TargetBlockReason.WindowChanged => "The selected window no longer belongs to the original process. Refresh and select it again.",
        TargetBlockReason.WindowHidden => "The selected window is not visible. Show it or select another target.",
        TargetBlockReason.WindowMinimized => "Restore the target window before sending input.",
        TargetBlockReason.UnverifiedRoblox => "This window identifies as Roblox but has not passed client verification. Refresh and select a verified Roblox client.",
        TargetBlockReason.NotForeground => "Bring the selected window to the front. Input waits while another window has focus.",
        TargetBlockReason.PointerUnavailable => "Windows could not confirm the pointer position. Input remains blocked until the check succeeds.",
        TargetBlockReason.PointerOutside => "Move the pointer over the selected window. Other windows covering it do not qualify.",
        TargetBlockReason.InspectionUnavailable => "The target could not be checked. Refresh the list and select an accessible window.",
        TargetBlockReason.SelectionRejected => "The requested window changed or failed identity validation. Refresh and select an eligible window.",
        TargetBlockReason.RestrictionOff => "Fixed CPS has its foreground restriction off. Input uses the current pointer in any application.",
        TargetBlockReason.NoForeground => "Focus an eligible Roblox client before sending restricted Fixed CPS input.",
        TargetBlockReason.CheckingRoblox => "Windows signature and publisher checks are still in progress. Input waits for verification.",
        TargetBlockReason.VerifiedRobloxForeground => "The current foreground client passed the existing Roblox identity checks. Restricted Fixed CPS input is eligible while it stays in front.",
        _ => "The foreground window has not passed Roblox client verification. Focus a supported verified client."
    };
    public static TargetStatus Selected(SelectedTargetFacts f) => new(
        !f.HasSelection ? TargetBlockReason.NoSelection :
        !f.InspectionSucceeded ? TargetBlockReason.InspectionUnavailable :
        !f.ProcessAlive ? TargetBlockReason.ProcessClosed :
        !f.WindowExists ? TargetBlockReason.WindowClosed :
        !f.IdentityMatches ? TargetBlockReason.WindowChanged :
        f.RequiresRobloxVerification && !f.VerifiedRoblox ? TargetBlockReason.UnverifiedRoblox :
        !f.WindowVisible ? TargetBlockReason.WindowHidden :
        f.Minimized ? TargetBlockReason.WindowMinimized :
        !f.Foreground ? TargetBlockReason.NotForeground :
        !f.PointerKnown ? TargetBlockReason.PointerUnavailable :
        !f.PointerInside ? TargetBlockReason.PointerOutside : TargetBlockReason.Ready);
    public static TargetStatus Fixed(bool restricted, bool hasForeground, bool minimized, bool checking, bool verified) => new(
        !restricted ? TargetBlockReason.RestrictionOff : !hasForeground ? TargetBlockReason.NoForeground :
        minimized ? TargetBlockReason.WindowMinimized : verified ? TargetBlockReason.VerifiedRobloxForeground :
        checking ? TargetBlockReason.CheckingRoblox : TargetBlockReason.RobloxVerificationRequired);
}
