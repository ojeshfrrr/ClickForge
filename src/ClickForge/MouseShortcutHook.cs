using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClickForge.Core;

namespace ClickForge;
// The hook owns a dedicated message pump: SendInput and UI Stop must never wait
// on each other. No click-engine work is performed in the native callback.
internal sealed class MouseShortcutHook(MouseShortcutRouter router, nint window) : IDisposable
{
    private static long sequence;
    internal nint Token { get; } = (nint)Interlocked.Increment(ref sequence);
    private readonly ManualResetEventSlim ready = new();
    private Thread? thread;
    private Dispatcher? dispatcher;
    private Native.MouseHookCallback? callback;
    private nint hook;
    private volatile bool stopping;
    internal bool Start()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "ClickForge mouse shortcuts" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Wait(3000) && hook != 0;
    }
    private void Run()
    {
        try
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            callback = Observe;
            hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
            ready.Set();
            if (hook != 0 && !stopping) Dispatcher.Run();
        }
        catch { ready.Set(); }
        finally { if (hook != 0) Native.UnhookWindowsHookEx(hook); hook = 0; GC.KeepAlive(callback); }
    }
    private nint Observe(int code, nint message, nint data)
    {
        if (code >= 0 && !stopping && (int)message is 0x020B or 0x020C)
        {
            try
            {
                var packet = Marshal.PtrToStructure<Native.MouseHookData>(data);
                if (router.Process((int)message,packet.MouseData,packet.Flags) is { } id)
                    Native.PostMessage(window,GlobalHotkeyService.MouseMessage,id,Token);
            }
            catch { /* Never allow managed exceptions to cross a native callback. */ }
        }
        return Native.CallNextHookEx(0,code,message,data);
    }
    public void Dispose()
    {
        stopping = true;
        var pump = dispatcher;
        if (pump is not null && !pump.HasShutdownStarted) pump.BeginInvokeShutdown(DispatcherPriority.Send);
        thread?.Join(2000);
        // A rare delayed thread retains its own delegate and signal until its
        // finally block unhooks; neither can be collected while native code uses it.
    }
}
