using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace ClickForge;

internal enum TrayCommand { Open, StartStop, PauseResume, EmergencyStop, Profiles, Updates, Settings, Exit }

// Direct Shell notification-area integration; no additional runtime or package.
internal sealed class TrayService : IDisposable
{
    private const uint CallbackMessage = 0x8001;
    private readonly HwndSource source;
    private readonly Action<TrayCommand> execute;
    private readonly ContextMenu menu = new();
    private readonly Dictionary<TrayCommand, MenuItem> items = [];
    private Data data;
    private bool disposed;
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    public bool Available { get; private set; }
    public event Action? Unavailable;
    public string Tooltip => data.Tip;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Data
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Identifier { public uint Size; public nint Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIcon(uint action, ref Data data);
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref Identifier id, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern nint CreateIconFromResourceEx(byte[] bits, uint length, bool icon, uint version, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    public TrayService(HwndSource source, ImageSource image, Action<TrayCommand> execute, bool simulateFailure = false)
    {
        this.source = source; this.execute = execute;
        var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawImage(image, new(0, 0, 32, 32));
        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = new MemoryStream(); encoder.Save(stream);
        byte[] png = stream.ToArray();
        data = new() { Size = (uint)Marshal.SizeOf<Data>(), Window = source.Handle, Id = 1, Flags = 1 | 2 | 4 | 32,
            Callback = CallbackMessage, Icon = CreateIconFromResourceEx(png, (uint)png.Length, true, 0x30000, 32, 32, 0),
            Guid = Guid.NewGuid(), Tip = "ClickForge — Stopped", Info = "", Title = "" };
        try
        {
            if (data.Icon == 0 || simulateFailure || !Shell_NotifyIcon(0, ref data)) throw new InvalidOperationException("Windows could not create the tray icon. Tray hiding is unavailable; use the main window.");
            Available = true;
            foreach (var (command, title) in new[] { (TrayCommand.Open, "Open ClickForge"), (TrayCommand.StartStop, "Start clicking"), (TrayCommand.PauseResume, "Pause"), (TrayCommand.EmergencyStop, "Emergency stop"), (TrayCommand.Profiles, "Profiles…"), (TrayCommand.Updates, "Check for updates…"), (TrayCommand.Settings, "Settings and diagnostics…"), (TrayCommand.Exit, "Exit ClickForge") })
            {
                var item = new MenuItem { Header = title }; item.Click += (_, _) => Route(command); items[command] = item; menu.Items.Add(item);
                if (command is TrayCommand.Open or TrayCommand.EmergencyStop or TrayCommand.Settings) menu.Items.Add(new Separator());
            }
            source.AddHook(Hook);
        }
        catch { Dispose(); throw; }
    }
    public void Route(TrayCommand command) { if (!disposed && (!items.TryGetValue(command, out var item) || item.IsEnabled)) execute(command); }
    public void Update(string state, bool enabled, bool paused, bool startAllowed)
    {
        if (disposed || !Available) return;
        string tip = "ClickForge — " + state;
        items[TrayCommand.StartStop].Header = enabled ? "Stop clicking" : "Start clicking";
        items[TrayCommand.StartStop].IsEnabled = enabled || startAllowed;
        items[TrayCommand.PauseResume].Header = paused ? "Resume" : "Pause"; items[TrayCommand.PauseResume].IsEnabled = enabled;
        if (tip == data.Tip) return;
        data.Tip = tip;
        if (!Shell_NotifyIcon(1, ref data)) { Available = false; Unavailable?.Invoke(); }
    }
    internal bool HasShellRectangle()
    {
        var id = new Identifier { Size = (uint)Marshal.SizeOf<Identifier>(), Window = data.Window, Id = data.Id, Guid = data.Guid };
        return !disposed && Shell_NotifyIconGetRect(ref id, out _) == 0;
    }
    public void Notify(string text)
    {
        if (disposed || !Available) return;
        data.Info = text[..Math.Min(255, text.Length)]; data.Title = "ClickForge"; data.InfoFlags = 1; data.Flags |= 16;
        Shell_NotifyIcon(1, ref data); data.Flags &= ~16u; data.Info = "";
    }
    private nint Hook(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (disposed) return 0;
        if ((uint)message == taskbarCreated)
        { Available = Shell_NotifyIcon(0, ref data); if (!Available) Unavailable?.Invoke(); }
        if ((uint)message != CallbackMessage) return 0;
        int action = (int)lParam;
        if (action is 0x202 or 0x203 or 0x400 or 0x401) Route(TrayCommand.Open);
        if (action is 0x205 or 0x7B)
        {
            SetForegroundWindow(source.Handle);
            menu.Placement = PlacementMode.MousePoint; menu.IsOpen = true;
            // Shell notification menus require a follow-up WM_NULL for dismissal.
            PostMessage(source.Handle, 0, 0, 0);
        }
        handled = true; return 0;
    }
    internal ContextMenu Menu => menu;
    public void Dispose()
    {
        if (disposed) return; disposed = true; menu.IsOpen = false;
        if (data.Icon != 0) Shell_NotifyIcon(2, ref data);
        Available = false; source.RemoveHook(Hook);
        if (data.Icon != 0) { DestroyIcon(data.Icon); data.Icon = 0; }
    }
}
