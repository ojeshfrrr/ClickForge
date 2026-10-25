using System.Windows;
using System.IO;
namespace ClickForge;
public partial class App : Application
{
    private SingleInstance? instance;
    private Mutex? installationGuard;
    protected override void OnStartup(StartupEventArgs e)
    {
        // Presence, rather than ownership, is checked by Inno Setup and Uninstall.
        // Every app mode keeps the guard alive through the shutdown cleanup path.
        installationGuard = new(false, InstallationRegistration.GuardName);
        int buttonDiagnostic = Array.IndexOf(e.Args, "--native-button-self-test");
        if (buttonDiagnostic >= 0 && buttonDiagnostic + 1 < e.Args.Length) { base.OnStartup(e); NativeButtonDiagnostics.Run(e.Args[buttonDiagnostic + 1]); return; }
        try { InstallationRegistration.RefreshVersion(AppContext.BaseDirectory, ClickForge.MainWindow.InstalledVersion.Value.Split('+')[0]); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { /* Registry display metadata cannot block startup. */ }
        int diagnosticIndex = Array.IndexOf(e.Args, "--ui-self-test");
        string? diagnostic = diagnosticIndex >= 0 && diagnosticIndex + 1 < e.Args.Length ? e.Args[diagnosticIndex + 1] : null;
        if (diagnostic is not null)
        {
            System.IO.File.WriteAllText(diagnostic, "STARTING WPF diagnostics" + Environment.NewLine);
            DispatcherUnhandledException += (_, failure) => { System.IO.File.AppendAllText(diagnostic, "FAIL unhandled UI error: " + failure.Exception); failure.Handled = true; Shutdown(1); };
        }
        if (!e.Args.Contains("--ui-self-test") && !e.Args.Contains("--ui-interactive-test") && !e.Args.Contains("--render-preview"))
        {
            instance = new("", () => Dispatcher.BeginInvoke(new Action(() => (MainWindow as MainWindow)?.RestoreWindow())));
            if (!instance.IsPrimary) { Shutdown(); return; }
        }
        base.OnStartup(e);
        MainWindow = new MainWindow(); MainWindow.Show();
        if (diagnostic is not null) System.IO.File.AppendAllText(diagnostic, "WPF window shown" + Environment.NewLine);
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { (MainWindow as MainWindow)?.PrepareExit(); base.OnSessionEnding(e); }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); installationGuard?.Dispose(); base.OnExit(e); }
}
