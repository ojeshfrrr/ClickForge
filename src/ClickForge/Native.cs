using System.Runtime.InteropServices;
using ClickForge.Core;
namespace ClickForge;

internal static class Native
{
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] internal struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct Union { [FieldOffset(0)] public Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public Union Data; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    internal delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint window);
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
public sealed class MouseInputService : IMouseInput
{
    private readonly Native.Input[] pair = [Create(2), Create(4)];
    private readonly Native.Input[] release = [Create(4)];
    private bool possiblyDown;
    private static Native.Input Create(uint flags) => new() { Data = new Native.Union { Mouse = new Native.Mouse { Flags = flags } } };
    public void Click()
    {
        possiblyDown = true;
        uint sent = Native.SendInput(2, pair, Marshal.SizeOf<Native.Input>());
        if (sent == 2) { possiblyDown = false; return; }
        int code = Marshal.GetLastWin32Error();
        try { Release(); } catch { /* Engine retries cleanup and reports the failure. */ }
        throw new InvalidOperationException($"Windows accepted {sent}/2 input events (error {code}). Input may be blocked by Windows or the target. Clicking stopped.");
    }
    public void Release()
    {
        if (!possiblyDown) return;
        if (Native.SendInput(1, release, Marshal.SizeOf<Native.Input>()) != 1)
            throw new InvalidOperationException("Windows rejected mouse-button release. Press and release the physical left button. Clicking stopped.");
        possiblyDown = false;
    }
}
public sealed class GlobalHotkeyService(nint handle) : IHotkeyRegistrar
{
    public bool Register(int id, int key) => Native.RegisterHotKey(handle, id, 0x4000, (uint)key);
    public void Unregister(int id) => Native.UnregisterHotKey(handle, id);
}
