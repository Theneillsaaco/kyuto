using System.Diagnostics;
using Kyuto.Core;

namespace Kyuto.App.Services;

/// <summary>
/// Whether a StatusNotifier host (the tray of the shell or the bar) is running. Qt.labs.platform's SystemTrayIcon
/// falls back to a QWidget when there is none, and a Qt Bridge app has no QApplication: the process dies with
/// "QWidget: Cannot create a QWidget without QApplication". Main.qml only creates the tray when this is true.
/// It reads the same property Qt reads (IsStatusNotifierHostRegistered), with busctl or, in a Flatpak, gdbus.
/// </summary>
internal static class TrayProbe
{
    public static bool HostRegistered()
    {
        string[][] attempts =
        [
            ["busctl", "--user", "get-property", "org.kde.StatusNotifierWatcher", "/StatusNotifierWatcher",
                "org.kde.StatusNotifierWatcher", "IsStatusNotifierHostRegistered"],
            ["gdbus", "call", "--session", "--dest", "org.kde.StatusNotifierWatcher",
                "--object-path", "/StatusNotifierWatcher", "--method", "org.freedesktop.DBus.Properties.Get",
                "org.kde.StatusNotifierWatcher", "IsStatusNotifierHostRegistered"]
        ];

        foreach (var command in attempts)
        {
            try
            {
                var info = new ProcessStartInfo(command[0])
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var argument in command[1..])
                    info.ArgumentList.Add(argument);

                using var process = Process.Start(info);
                if (process is null)
                    continue;

                if (!process.WaitForExit(1500))
                {
                    try { process.Kill(true); } catch { /* it ended on its own */ }
                    continue;
                }

                // busctl prints "b true", gdbus "(<true>,)". A failure (no watcher on the bus) means no tray.
                var found = process.ExitCode == 0 && process.StandardOutput.ReadToEnd().Contains("true", StringComparison.OrdinalIgnoreCase);
                if (!found)
                    Log.Warn("tray", "no StatusNotifier host on the session bus: the tray icon stays off");
                return found;
            }
            catch (Exception)
            {
                // The tool is not installed: try the next one.
            }
        }

        Log.Warn("tray", "neither busctl nor gdbus could be run: the tray icon stays off");
        return false;
    }
}
