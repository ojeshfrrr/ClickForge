namespace ClickForge.Core;

// IDs are distinct from the existing Activate, Pause and Emergency actions.
// The existing physical mouse router ignores injected events and repeat downs.
public static class SideButtonOutputs
{
    public const int LeftId = 4, RightId = 5;
    public static IReadOnlyList<(int Id, int Key)> Bindings { get; } =
        Array.AsReadOnly(new[] { (LeftId, 5), (RightId, 6) });
    public static ClickButton OutputFor(int id) => id switch
    {
        LeftId => ClickButton.Left,
        RightId => ClickButton.Right,
        _ => throw new ArgumentException("Unknown side-button output toggle.", nameof(id))
    };
}
