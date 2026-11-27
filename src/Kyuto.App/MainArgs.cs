using Qt.Quick;

namespace Kyuto.App;

/// <summary>
/// What Program.cs hands to Main.qml when it loads it. Each public property becomes an initial property of the
/// root window (StartMini -> startMini), set before the first frame, with its type kept.
/// </summary>
internal sealed class MainArgs : IQmlLoadArgs
{
    public static string Name => "Main";
    
    /// <summary>Open as the small window (cover and controls only). Set by <c>kyuto --mini</c>.</summary>
    public bool StartMini { get; set; }
}
