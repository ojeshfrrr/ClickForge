using System.Runtime.InteropServices;
using ClickForge.Core;
namespace ClickForge;

internal static class Native
{
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] internal struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct Union { [FieldOffset(0)] public Mouse Mouse; [FieldOffset(0)] public Keyboard Keyboard; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public Union Data; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    internal delegate nint MouseHookCallback(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] internal struct MouseHookData { public Point Point; public uint MouseData, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int kind, MouseHookCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetCursorPos(int x, int y);
    internal delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint window, System.Text.StringBuilder text, int maximum);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint window);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public nint File, Subject; }
    [StructLayout(LayoutKind.Sequential)] internal struct TrustData { public uint Size; public nint Policy, Sip; public uint Ui, Revocation, Choice; public nint File; public uint Action; public nint State, Url; public uint Flags, Context; public nint Signature; }
    [DllImport("wintrust.dll", ExactSpelling = true)] internal static extern int WinVerifyTrust(nint window, ref Guid action, ref TrustData data);
    internal static bool HasValidSignature(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        nint pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Choice = 1, File = pointer, Flags = 0x1000 };
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            return WinVerifyTrust(0, ref action, ref data) == 0;
        }
        finally { Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }
}
public sealed class MouseInputService() : ButtonMouseInput(new WindowsMouseTransport());
internal sealed class WindowsMouseTransport : IMouseTransport
{
    private readonly Dictionary<(ClickButton, bool), Native.Input[]> packets = new();
    private readonly Dictionary<(int, bool), Native.Input[]> keys = new();
    public WindowsMouseTransport()
    {
        foreach (var button in ClickButtons.MouseChoices)
        {
            var down = Create(ClickButtons.Packet(button, false)); var up = Create(ClickButtons.Packet(button, true));
            packets[(button, false)] = [down, up]; packets[(button, true)] = [up];
        }
    }
    private static Native.Input Create(MousePacket packet) => new() { Data = new Native.Union { Mouse = new Native.Mouse { Flags = packet.Flags, Data = packet.Data } } };
    public bool Supports(ClickButton button) => OperatingSystem.IsWindows() && ClickButtons.MouseChoices.Contains(button);
    public bool SupportsKey(int key) => OperatingSystem.IsWindows() && KeyboardOutput.IsValid(key);
    public int LastError { get; private set; }
    public uint Send(ClickButton button, bool releaseOnly)
    {
        ClickButtons.Validate(button);
        if (!Supports(button)) throw new InvalidOperationException("This Windows input method is unavailable. No fallback button was sent.");
        var input = packets[(button, releaseOnly)];
        uint sent = Native.SendInput((uint)input.Length, input, Marshal.SizeOf<Native.Input>());
        LastError = Marshal.GetLastWin32Error(); return sent;
    }
    public uint SendKey(int key,bool releaseOnly)
    {
        KeyboardOutput.Validate(key);
        if(!SupportsKey(key))throw new InvalidOperationException("Keyboard input is unavailable. No fallback input was sent.");
        if(!keys.TryGetValue((key,releaseOnly),out var input))
        {
            Native.Input Packet(bool up) => new() { Type=1, Data=new Native.Union { Keyboard=new Native.Keyboard {Key=(ushort)key,Flags=KeyboardOutput.Flags(key,up)} } };
            input=releaseOnly?[Packet(true)]:[Packet(false),Packet(true)];keys[(key,releaseOnly)]=input;
        }
        uint sent=Native.SendInput((uint)input.Length,input,Marshal.SizeOf<Native.Input>());
        LastError=Marshal.GetLastWin32Error();return sent;
    }
}
public sealed class GlobalHotkeyService(nint handle) : IHotkeyRegistrar
{
    internal const int MouseMessage = 0x804D;
    private readonly MouseShortcutRouter mouse = new();
    private MouseShortcutHook? hook;
    public bool Register(int id, int key)
    {
        if (!ShortcutKeys.IsMouse(key)) return Native.RegisterHotKey(handle, id, 0x4000, (uint)key);
        if (!mouse.Register(id,key)) return false;
        if (hook is null)
        {
            hook = new(mouse, handle);
            if (!hook.Start()) { hook.Dispose(); hook = null; mouse.Unregister(id); return false; }
        }
        return true;
    }
    public bool IsHeld(int key) => mouse.IsHeld(key) && (Native.GetAsyncKeyState(key) & 0x8000) != 0;
    internal nint MouseGeneration => hook?.Token ?? 0;
    internal bool AcceptMouseMessage(int id, nint generation) => generation != 0 && generation == MouseGeneration && mouse.Contains(id);
    public void Unregister(int id)
    {
        if (!mouse.Unregister(id)) Native.UnregisterHotKey(handle, id);
        else if (mouse.Count == 0) { hook?.Dispose(); hook = null; }
    }
}
