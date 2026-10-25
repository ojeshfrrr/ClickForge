using System.Security.Principal;

namespace ClickForge;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle open;
    private readonly RegisteredWaitHandle? registration;
    private readonly bool owns;
    private bool disposed;
    public bool IsPrimary => owns;
    internal SingleInstance(string suffix, Action restore)
    {
        using var user = WindowsIdentity.GetCurrent();
        string identity = user.User?.Value ?? Environment.UserName;
        string name = @"Local\ClickForge-" + identity + suffix;
        mutex = new(false, name);
        try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
        open = new(false, EventResetMode.AutoReset, name + "-open");
        if (owns) registration = ThreadPool.RegisterWaitForSingleObject(open, (_, _) => restore(), null, Timeout.Infinite, false);
        else open.Set();
    }
    public void Dispose() { if (disposed) return; disposed = true; registration?.Unregister(null); open.Dispose(); if (owns) mutex.ReleaseMutex(); mutex.Dispose(); }
}
