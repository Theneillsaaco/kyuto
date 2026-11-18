using Kyuto.Core;
using Kyuto.Core.Domain;
using Kyuto.Core.Playback;
using Kyuto.Storage.Database;

namespace Kyuto.App.Services;

/// <summary>
/// The last session as it comes back: the queue (empty when the startup mode says to restore nothing, but with its
/// repeat and shuffle), the volume and where the track resumes. <see cref="HasQueue"/> is false when there is no queue
/// to bring back.
/// </summary>
internal sealed record RestoredSession(QueueState Queue, int Volume, double Seconds, bool HasQueue); 

/// <summary>
/// Saves and brings back the listening session (queue, position, volume, repeat, shuffle) through the database. Knows
/// nothing about mpv or the UI, so it can be tested alone. Every failure is logged and swallowed: losing a session
/// must never stop the player. Without a database it does nothing.
/// </summary>
internal sealed class SessionStore(PlayerDatabase? db)
{
    /// <summary>Writes the whole queue. Called when it changes, not on every tick.</summary>
    public void SaveQueue(QueueState queue, double seconds, int volume)
    {
        if (db is null)
            return;

        try
        {
            db.SaveQueueState(queue.Items, PlayQueue.currentIndex(queue), seconds, volume, PlayQueue.repeatName(queue), queue.Shuffle);
        }
        catch (Exception ex) { Log.Warn("playback", "save queue: ", ex); }
    }
    
    /// <summary>Writes only the position and volume (every few seconds, on pause and at exit).</summary>
    public void SaveProgress(double seconds, int volume)
    {
        try { db?.SaveProgress(seconds, volume); } 
        catch (Exception ex) { Log.Warn("playback", "save progress", ex); }
    }
    
    /// <summary>
    /// What the "Al iniciar" setting says to bring back: queue and position, the queue with the track from 0, or
    /// nothing (volume, repeat and shuffle always come back). Null when there is no database or nothing was saved.
    /// </summary>
    public RestoredSession? Restore(Random rng)
    {
        if (db is null)
            return null;

        SavedQueue? saved;
        StartupMode mode;

        try
        {
            mode = Resume.parse(db.GetSetting(Resume.settingKey));
            saved = db.LoadQueueState();
        }
        catch (Exception ex)
        {
            Log.Warn("playback", "restore", ex);
            return null;
        }

        if (saved is null)
            return null;

        var settings = PlayQueue.setShuffle(rng.Next(), saved.Shuffle, PlayQueue.setRepeat(RepeatOf(saved.Repeat), PlayQueue.empty));
        var plan = Resume.plan(mode, saved.Seconds);

        if (!plan.Queue || saved.Items.Count == 0)
            return new RestoredSession(settings, saved.Volume, 0, HasQueue: false);
        
        var start = Math.Clamp(saved.Index, 0, saved.Items.Count - 1);
        var queue = PlayQueue.replace(rng.Next(), saved.Items, start, settings);
        
        return new RestoredSession(queue, saved.Volume, plan.Seconds, HasQueue: true);
    }
    
    private static RepeatMode RepeatOf(string name) => name switch
    {
        "one" => RepeatMode.One,
        "all" => RepeatMode.All,
        _ => RepeatMode.Off
    };
}