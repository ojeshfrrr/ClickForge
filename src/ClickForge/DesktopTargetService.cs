using System.Diagnostics;
using System.Text;
using ClickForge.Core;

namespace ClickForge;

internal sealed record DesktopWindow(nint Handle, int ProcessId, long Started, string Path, string Title, bool VerifiedRoblox = false)
{
    public override string ToString() => $"{Title} · {System.IO.Path.GetFileNameWithoutExtension(Path)}";
}

internal sealed class DesktopTargetService : IVariableTargetGate, IDisposable
{
    internal sealed record Inspection(string Identity, string Title, bool VerifiedRoblox, bool Valid, bool Foreground, bool PointerInside);
    internal Inspection? Snapshot
    {
        get
        {
            lock (gate)
            {
                if (selected is null) return null;
                bool valid = false, foreground = false, pointer = false;
                try
                {
                    valid = SelectionValid(); foreground = valid && Native.GetForegroundWindow() == selected.Handle;
                    pointer = valid && Native.GetCursorPos(out var point) && Native.GetAncestor(Native.WindowFromPoint(point), 2) == selected.Handle;
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                return new(System.IO.Path.GetFileName(selected.Path), valid ? Title(selected.Handle) : selected.Title, selected.VerifiedRoblox, valid, foreground, pointer);
            }
        }
    }
    private readonly object gate = new();
    private DesktopWindow? selected;
    private Process? process;
    public string SelectedTitle { get { lock (gate) return selected?.Title ?? "No desktop window selected"; } }
    internal static string Title(nint window)
    { var text = new StringBuilder(1024); Native.GetWindowText(window, text, text.Capacity); return text.ToString(); }

    private static DesktopWindow? Inspect(nint window, bool excludeSelf)
    {
        try
        {
            if (window == 0 || !Native.IsWindowVisible(window)) return null;
            Native.GetWindowThreadProcessId(window, out uint pid);
            if (pid == 0 || (excludeSelf && pid == Environment.ProcessId)) return null;
            using var candidate = Process.GetProcessById(checked((int)pid));
            string? path = candidate.MainModule?.FileName;
            if (path is null) return null;
            var info = FileVersionInfo.GetVersionInfo(path);
            string title = Title(window);
            bool verifiedRoblox = DesktopTargetPolicy.RequiresRobloxVerification(path, info.CompanyName, info.ProductName, info.OriginalFilename, title) && RobloxIdentity.IsVerified(path, info);
            if (string.IsNullOrWhiteSpace(title) || !DesktopTargetPolicy.IsEligible(path, info.CompanyName, info.ProductName, info.OriginalFilename, title, verifiedRoblox)) return null;
            return new(window, (int)pid, candidate.StartTime.ToUniversalTime().Ticks, path, title, verifiedRoblox);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or System.IO.IOException or UnauthorizedAccessException or NotSupportedException or System.Security.Cryptography.CryptographicException) { return null; }
    }
    public static DesktopWindow[] Enumerate()
    {
        var windows = new List<DesktopWindow>();
        Native.EnumWindows((window, _) => { if (Inspect(window, true) is { } item) windows.Add(item); return true; }, 0);
        return windows.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public void Select(DesktopWindow? item)
    {
        lock (gate)
        {
            process?.Dispose(); process = null; selected = null;
            if (item is null) return;
            var current = Inspect(item.Handle, true);
            if (current is null || current.ProcessId != item.ProcessId || current.Started != item.Started || current.Path != item.Path)
                throw new InvalidOperationException("That window changed or is no longer eligible. Refresh the window list.");
            var retained = Process.GetProcessById(item.ProcessId);
            try
            {
                _ = retained.SafeHandle; // Retain the original process object; PID reuse must not qualify.
                if (retained.HasExited || retained.StartTime.ToUniversalTime().Ticks != item.Started) throw new InvalidOperationException("The selected application exited.");
                selected = current; process = retained;
            }
            catch { retained.Dispose(); throw; }
        }
    }
    private bool SelectionValid()
    {
        if (selected is null || process is null || process.HasExited || !Native.IsWindowVisible(selected.Handle) || Native.IsIconic(selected.Handle)) return false;
        Native.GetWindowThreadProcessId(selected.Handle, out uint pid);
        return pid == selected.ProcessId && (selected.VerifiedRoblox || !Title(selected.Handle).Contains("roblox", StringComparison.OrdinalIgnoreCase));
    }
    public bool CanActivate
    {
        get
        {
            lock (gate)
            {
                // Start from ClickForge or the selected target only. Trust verification
                // happens during selection, never on the click worker or hotkey path.
                try
                {
                    if (!SelectionValid()) return false;
                    nint foreground = Native.GetForegroundWindow();
                    Native.GetWindowThreadProcessId(foreground, out uint pid);
                    return foreground == selected!.Handle || pid == Environment.ProcessId;
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return false; }
            }
        }
    }
    public bool CanClick
    {
        get
        {
            lock (gate)
            {
                try
                {
                    if (!SelectionValid() || Native.GetForegroundWindow() != selected!.Handle || !Native.GetCursorPos(out var point)) return false;
                    // Do not send clicks into a different window under the pointer.
                    return Native.GetAncestor(Native.WindowFromPoint(point), 2) == selected.Handle;
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return false; }
            }
        }
    }
    public void Dispose() { lock (gate) { process?.Dispose(); process = null; selected = null; } }
}
