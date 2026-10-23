using System.Diagnostics;
using ClickForge.Core;
namespace ClickForge;

public sealed class ForegroundTargetService : ITargetGate, IDisposable
{
    private readonly object gate = new();
    private volatile bool restricted = true;
    private nint cachedWindow;
    private uint cachedPid;
    private Process? verifiedProcess;
    private bool checking, checkedIdentity, disposed;
    public bool Restricted { get => restricted; set => restricted = value; }
    public bool CanClick => !restricted || RobloxForeground;
    public bool IsChecking { get { lock (gate) return checking; } }
    public bool RobloxForeground
    {
        get
        {
            nint window = Native.GetForegroundWindow();
            if (window == 0 || Native.IsIconic(window)) return false;
            Native.GetWindowThreadProcessId(window, out uint pid);
            lock (gate)
            {
                if (disposed) return false;
                if (window != cachedWindow || pid != cachedPid)
                {
                    verifiedProcess?.Dispose(); verifiedProcess = null;
                    cachedWindow = window; cachedPid = pid; checkedIdentity = false;
                }
                if (verifiedProcess is not null)
                {
                    try { return !verifiedProcess.HasExited && Native.GetForegroundWindow() == window; }
                    catch { return false; }
                }
                if (!checking && !checkedIdentity)
                {
                    checking = true;
                    // Signature verification never blocks the click worker or stop.
                    _ = Task.Run(() => VerifyForeground(window, pid));
                }
                return false;
            }
        }
    }
    private void VerifyForeground(nint window, uint pid)
    {
        Process? candidate = null;
        try
        {
            candidate = Process.GetProcessById((int)pid);
            _ = candidate.SafeHandle; // Retain process identity across possible PID reuse.
            string? path = candidate.MainModule?.FileName;
            if (path is null) return;
            if (!RobloxIdentity.IsVerified(path, FileVersionInfo.GetVersionInfo(path))) return;
            lock (gate)
            {
                if (disposed || window != cachedWindow || pid != cachedPid) return;
                verifiedProcess = candidate; candidate = null;
            }
        }
        catch { /* Unverifiable or inaccessible targets always remain blocked. */ }
        finally
        {
            candidate?.Dispose();
            lock (gate)
            {
                if (window == cachedWindow && pid == cachedPid) checkedIdentity = true;
                checking = false;
            }
        }
    }
    public void Dispose() { lock (gate) { disposed = true; verifiedProcess?.Dispose(); verifiedProcess = null; } }
}
