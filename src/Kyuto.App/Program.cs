using Kyuto.App;
using Kyuto.Core;
using Qt.Quick;

// A crash that comes from managed code leaves its trace in ~/.local/state/kyuto/kyuto.log before the process ends.
AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("crash", "unhandled exception", e.ExceptionObject as Exception);
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.Warn("crash", "unobserved task exception", e.Exception);
    e.SetObserved();
};

// The name of the .desktop file: Wayland uses it as the app_id (window rules) and shells use it to find the icon.
// The Nix package installs kyuto.desktop, the Flatpak one is named after its app id.
global::Qt.Application.DesktopFileName = Environment.GetEnvironmentVariable("FLATPAK_ID") ?? "kyuto";

// Initialize Qt. The start-up options travel as initial properties of Main.qml.
Qml.Load(new MainArgs { StartMini = args.Contains("--mini") });
Qml.WaitForExit();

AppServices.Shutdown();
Environment.Exit(0); // does not wait for foreground threads
