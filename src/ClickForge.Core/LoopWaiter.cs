using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace ClickForge.Core;

// Reuse an interruptible high-resolution Windows timer; never spin or change
// the system-wide timer resolution. Other platforms retain ordinary waits.
internal sealed class LoopWaiter : IDisposable
{
    private readonly AutoResetEvent signal = new(false);
    private readonly TimerHandle? timer;
    private readonly WaitHandle[]? handles;
    public LoopWaiter()
    {
        if (!OperatingSystem.IsWindows()) return;
        nint handle = CreateWaitableTimerEx(0, null, 2, 0x1F0003);
        if (handle == 0) return;
        timer = new TimerHandle(handle); handles = [signal, timer];
    }
    public void Set() => signal.Set();
    public void WaitOne(int milliseconds)
    {
        if (milliseconds <= 0 || timer is null) { signal.WaitOne(milliseconds); return; }
        long due = -milliseconds * 10000L;
        if (!SetWaitableTimer(timer.SafeWaitHandle, ref due, 0, 0, 0, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
        WaitHandle.WaitAny(handles!);
    }
    public void Dispose() { signal.Dispose(); timer?.Dispose(); }
    private sealed class TimerHandle : WaitHandle { public TimerHandle(nint handle) => SafeWaitHandle = new SafeWaitHandle(handle, true); }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWaitableTimerEx(nint attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long due, int period, nint callback, nint argument, [MarshalAs(UnmanagedType.Bool)] bool resume);
}
