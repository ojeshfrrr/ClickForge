namespace ClickForge.Core;

public enum ClickButton { Left, Mouse4, Mouse5, CustomKey }
public readonly record struct MousePacket(uint Flags, uint Data);
public static class ClickButtons
{
    public static IReadOnlyList<ClickButton> MouseChoices { get; } = Array.AsReadOnly(new[] { ClickButton.Left, ClickButton.Mouse4, ClickButton.Mouse5 });
    public static void Validate(ClickButton button) { if (!Enum.IsDefined(button)) throw new ArgumentException("Choose a mouse button or Custom input key."); }
    public static string Label(ClickButton button) => button switch { ClickButton.Left => "Left", ClickButton.Mouse4 => "Mouse 4 (Back)", ClickButton.Mouse5 => "Mouse 5 (Forward)", ClickButton.CustomKey => "Custom input key…", _ => throw new ArgumentException("Invalid click button.") };
    public static string OutputLabel(ClickButton button,int key) => button == ClickButton.CustomKey ? "Key: "+KeyboardOutput.Label(key) : Label(button);
    // Win32 MOUSEINPUT: XDOWN/XUP use mouseData XBUTTON1=1 / XBUTTON2=2.
    public static MousePacket Packet(ClickButton button, bool up) => button switch
    {
        ClickButton.Left => new(up ? 0x0004u : 0x0002u, 0),
        ClickButton.Mouse4 => new(up ? 0x0100u : 0x0080u, 1),
        ClickButton.Mouse5 => new(up ? 0x0100u : 0x0080u, 2),
        _ => throw new ArgumentException("Invalid click button.")
    };
}

public interface IMouseTransport
{
    bool Supports(ClickButton button);
    uint Send(ClickButton button, bool releaseOnly);
    int LastError { get; }
    bool SupportsKey(int key) => false;
    uint SendKey(int key, bool releaseOnly) => throw new InvalidOperationException("Custom keyboard input is not supported. No fallback input was sent.");
}

// Owned by one engine under its existing lock. Pending release retains the original
// button, even if a later configuration selects another button or cleanup fails.
public class ButtonMouseInput(IMouseTransport transport) : IMouseInput
{
    private (ClickButton Button,int Key)? pending;
    public bool Supports(ClickButton button) => button != ClickButton.CustomKey && Enum.IsDefined(button) && transport.Supports(button);
    public bool Supports(ClickButton button,int key) => button == ClickButton.CustomKey ? KeyboardOutput.IsValid(key) && transport.SupportsKey(key) : Supports(button);
    public void Click() => Click(ClickButton.Left);
    public void Click(ClickButton button) => Click(button,0);
    public void Click(ClickButton button,int key)
    {
        KeyboardOutput.ValidateOutput(button,key);
        if (!Supports(button,key)) throw new InvalidOperationException($"{ClickButtons.OutputLabel(button,key)} is not supported by the input service. No fallback input was sent.");
        if (pending.HasValue) Release();
        pending = (button,key);
        uint sent = button == ClickButton.CustomKey ? transport.SendKey(key,false) : transport.Send(button, false);
        if (sent == 2) { pending = null; return; }
        int error = transport.LastError;
        try { Release(); } catch { /* The engine retries, retaining the original button. */ }
        throw new InvalidOperationException($"Windows accepted {sent}/2 {ClickButtons.OutputLabel(button,key)} input events (error {error}). Input may be unsupported or blocked. Clicking stopped; no fallback input was sent.");
    }
    public void Release()
    {
        if (pending is not { } value) return;
        if ((value.Button == ClickButton.CustomKey ? transport.SendKey(value.Key,true) : transport.Send(value.Button, true)) != 1)
            throw new InvalidOperationException($"Windows rejected {ClickButtons.OutputLabel(value.Button,value.Key)} release. Press and release that physical input. Clicking stopped.");
        pending = null;
    }
}
