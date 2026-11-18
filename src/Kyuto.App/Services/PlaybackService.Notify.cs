using System.Security.Cryptography;
using System.Text;
using Kyuto.Core.Domain;
using Kyuto.Storage;

namespace Kyuto.App.Services;

// "Now playing" desktop notification, with the cover. PlaybackService is partial so this lives next to the feature.
internal sealed partial class PlaybackService
{
    private static readonly HttpClient ArtHttp = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const int ArtKept = 20;
    
    private void Announce(Track t)
    {
        if (_notifier is null || !_prefs.Notify || TrackSource.IsDirect(t.Id))
            return;

        var body = t.Album.Length > 0 ? t.Artist + " · " + t.Album : t.Artist;
        var art = t.ArtUrl;
        var notifier = _notifier;
        
        _ = Task.Run(async () => await notifier.NotifyAsync(t.Title, body, await FetchArtAsync(art)));
    }

    /// <summary>The cover as a file in the cache, "" when there is none or it could not be downloaded.</summary>
    private static async Task<string> FetchArtAsync(string url)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return "";

        try
        {
            var dir = AppPaths.CacheDir("art");
            Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            
            var name = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..16] + ".jpg";
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;

            var bytes = await ArtHttp.GetByteArrayAsync(url);
            var temp = path + ".part";
            await File.WriteAllBytesAsync(temp, bytes);
            File.Move(temp, path, overwrite: true);

            foreach (var old in new DirectoryInfo(dir).GetFiles("*.jpg").OrderByDescending(f => f.LastWriteTimeUtc).Skip(ArtKept))
                old.Delete();

            return path;
        }
        catch
        {
            return ""; // no cover: the notification still goes out
        }
    }
}