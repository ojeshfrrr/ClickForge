namespace ClickForge.Core;

public enum MouseSetup { Standard, MinecraftBedwars }

// IDs are distinct from the existing Activate, Pause and Emergency actions.
// The existing physical mouse router ignores injected events and repeat downs.
public static class SideButtonOutputs
{
    public const int LeftId = 4, RightId = 5;
    public static IReadOnlyList<(int Id, int Key)> Bindings { get; } =
        Array.AsReadOnly(new[] { (LeftId, 5), (RightId, 6) });
    public static ClickButton OutputFor(int id, MouseSetup setup = MouseSetup.Standard)
    {
        if (!Enum.IsDefined(setup)) throw new ArgumentException("Choose a supported mouse setup.");
        var standard = id switch
        {
            LeftId => ClickButton.Left,
            RightId => ClickButton.Right,
            _ => throw new ArgumentException("Unknown side-button output toggle.", nameof(id))
        };
        return setup == MouseSetup.Standard ? standard : standard == ClickButton.Left ? ClickButton.Right : ClickButton.Left;
    }
    public static string Summary(MouseSetup setup) => $"Front (Mouse 5): {ClickButtons.Label(OutputFor(RightId, setup))} · Back (Mouse 4): {ClickButtons.Label(OutputFor(LeftId, setup))}";
}
