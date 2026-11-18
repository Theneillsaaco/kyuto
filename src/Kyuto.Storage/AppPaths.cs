using static System.IO.Directory;

namespace Kyuto.Storage;

/// <summary>Where the player keeps its files, following the XDG directories.</summary>
public static class AppPaths
{
    public static string ConfigFile(string name) => Path.Combine(Home("XDG_CONFIG_HOME", ".config"), "kyuto", name);
    
    public static string DataFile(string name) => Path.Combine(Home("XDG_DATA_HOME", ".local/share"), "kyuto", name);

    /// <summary>A folder of throwaway data: $XDG_CACHE_HOME/player/&lt;name&gt;, ~/.cache/kyuto/&lt;name&gt; by default.</summary>
    public static string CacheDir(string name) => Path.Combine(Home("XDG_CACHE_HOME", ".cache"), "kyuto", name);
    
    private static string Home(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
    
    /// <summary>Creates the folder of a file (mode 700 when new) and returns the file path.</summary>
    internal static string Prepare(string file)
    {
        if (Path.GetDirectoryName(file) is { Length: > 0 } dir)
            CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        
        return file;
    }

    /// <summary>Kyuto used to be called "player": moves the old config and data folders over, once.</summary>
    public static void MigrateLegacy()
    {
        foreach (var (variable, fallback) in new[] { ("XDG_CONFIG_HOME", ".config"), ("XDG_DATA_HOME", ".local/share") })
        {
            var root = Home(variable, fallback);
            var old = Path.Combine(root, "player");
            var now = Path.Combine(root, "kyuto");
    
            try
            {
                if (!Directory.Exists(old) || Directory.Exists(now))
                    continue;
    
                Directory.Move(old, now);
    
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    if (File.Exists(Path.Combine(now, "player.db" + suffix)))
                        File.Move(Path.Combine(now, "player.db" + suffix), Path.Combine(now, "kyuto.db" + suffix));
            }
            catch { /* worst case the app starts with fresh folders */ }
        }
    }
}