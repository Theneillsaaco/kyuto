using System.Runtime.InteropServices;
using Kyuto.Core;

namespace Kyuto.App.Services;

/// <summary>
/// Qt Bridge does not expose QGuiApplication::setQuitOnLastWindowClosed, so it is called straight from libQt6Gui
/// (a static member function: no object needed). While the tray keeps the player alive, hiding the window must not
/// end the app, and this replaces the 1x1 helper window that tiling Wayland compositors show as an empty window.
/// If the symbol is not found (another Qt build) it returns false and Main.qml falls back to that helper window.
/// </summary>
internal static class QtApp
{
    /// <summary>True: the app keeps running when its last window is hidden. False: Qt's default (it quits).</summary>
    public static bool HoldWithoutWindows(bool hold)
    {
        try
        {
            SetQuitOnLastWindowClosed((byte)(hold ? 0 : 1));
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Warn("app", "cannot change quitOnLastWindowClosed, the helper window is used instead", ex);
            return false;
        }
    }

    [DllImport("libQt6Gui.so.6", EntryPoint = "_ZN15QGuiApplication25setQuitOnLastWindowClosedEb", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetQuitOnLastWindowClosed(byte quit);
}
