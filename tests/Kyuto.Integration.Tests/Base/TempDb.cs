using Kyuto.Storage.Database;

namespace Kyuto.Integration.Tests.Base;

/// <summary>A PlayerDatabase in a throw-away folder.</summary>
internal sealed class TempDb : IDisposable
{
    public string Dir { get; } = Directory.CreateTempSubdirectory("player-db-").FullName;
    public string Path => System.IO.Path.Combine(Dir, "kyuto.db");
    public PlayerDatabase Db { get; }
    
    public TempDb() => Db = new PlayerDatabase(Path);

    public void Dispose()
    {
        Db.Dispose();
        
        try { Directory.Delete(Dir, true); }
        catch { /* the OS cleans the temp folder anyway */ }
    }
}