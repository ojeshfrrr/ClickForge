namespace ClickForge.Core;

public static class ShortcutKeys
{
    public static readonly IReadOnlyList<int> Choices = Array.AsReadOnly(Enumerable.Range(0x70, 11).Concat([5, 6]).ToArray());
    public static bool IsMouse(int key) => key is 5 or 6;
    public static bool IsValid(int key) => key is >= 0x70 and <= 0x7A || IsMouse(key);
    public static string Label(int key) => key switch { 5 => "Mouse 4 (Back)", 6 => "Mouse 5 (Forward)", >= 0x70 and <= 0x7A => $"F{key - 0x6F}", _ => "Invalid shortcut" };
    public static bool ConflictsWithHoldOutput(Settings settings) => settings.Mode == ActivationMode.Hold &&
        (settings.ActivateKey == 5 && settings.Button == ClickButton.Mouse4 || settings.ActivateKey == 6 && settings.Button == ClickButton.Mouse5);
}

// No coordinates/content are retained. Injected events cannot trigger shortcuts
// or change physical hold state. Observing a shortcut never consumes the event.
public sealed class MouseShortcutRouter
{
    private readonly object gate = new();
    private readonly Dictionary<int, int> bindings = [];
    private readonly HashSet<int> held = [];
    public int Count { get { lock(gate) return bindings.Count; } }
    public bool Register(int id, int key)
    {
        if (id <= 0 || !ShortcutKeys.IsMouse(key)) return false;
        lock(gate) { if (bindings.ContainsKey(id) || bindings.ContainsValue(key)) return false; bindings.Add(id,key); return true; }
    }
    public bool Unregister(int id) { lock(gate) { if (!bindings.Remove(id,out int key)) return false; held.Remove(key); return true; } }
    public bool IsHeld(int key) { lock(gate) return held.Contains(key); }
    public bool Contains(int id) { lock(gate) return bindings.ContainsKey(id); }
    public int? Process(int message, uint data, uint flags)
    {
        if ((flags & 3) != 0 || message is not (0x020B or 0x020C)) return null;
        int key = (data >> 16) switch { 1 => 5, 2 => 6, _ => 0 };
        lock(gate)
        {
            var entry = bindings.FirstOrDefault(v=>v.Value==key);
            if (entry.Key == 0) return null;
            if (message == 0x020C) { held.Remove(key); return null; }
            return held.Add(key) ? entry.Key : null;
        }
    }
}
