using System.IO;
using System.Windows;
using System.Windows.Interop;
using ClickForge.Core;

namespace ClickForge;
internal static class NativeButtonDiagnostics
{
    internal static void Run(string output)
    {
        var window = new TestWindow(); Application.Current.MainWindow = window;
        var results = new List<string>();
        nint previous = Native.GetForegroundWindow(); bool saved = Native.GetCursorPos(out var original);
        Point lastPosition = default; bool moved = false;
        window.ContentRendered += async (_, _) =>
        {
            int exit = 1;
            try
            {
                nint own = new WindowInteropHelper(window).Handle;
                window.Activate(); Native.SetForegroundWindow(own); await Task.Delay(120);
                if (Native.GetForegroundWindow() != own) { exit = 2; results.Add("UNAVAILABLE Windows did not grant foreground to the isolated safe pad. No input sent."); return; }
                foreach (var button in ClickButtons.MouseChoices)
                {
                    window.ButtonSelector.SelectedIndex = (int)button;
                    window.UpdateLayout(); lastPosition = window.PadCenter;
                    if (!Native.SetCursorPos((int)lastPosition.X, (int)lastPosition.Y)) throw new InvalidOperationException("Cannot position pointer in the safe pad.");
                    moved = true; await Task.Delay(100);
                    window.SendDiagnosticPair(); await Task.Delay(120);
                    if (window.ReceivedDowns != 1 || window.ReceivedUps != 1) throw new InvalidOperationException(ClickButtons.Label(button) + " did not receive exactly one DOWN and UP. No fallback attempted.");
                    results.Add("PASS Native SendInput -> isolated WPF pad: " + ClickButtons.Label(button) + " DOWN=1 UP=1");
                }
                exit = 0;
            }
            catch (Exception e) { results.Add("FAIL " + e.Message); }
            finally
            {
                window.StopTest();
                if (saved && moved && Native.GetCursorPos(out var now) && now.X == (int)lastPosition.X && now.Y == (int)lastPosition.Y) Native.SetCursorPos(original.X, original.Y);
                if (Native.GetForegroundWindow() == new WindowInteropHelper(window).Handle && previous != 0) Native.SetForegroundWindow(previous);
                results.Add("NOTE Any test input is restricted to the isolated safe pad. No production engine or Roblox target was activated.");
                File.WriteAllLines(output, results); Application.Current.Shutdown(exit);
            }
        };
        window.Show();
    }
}
