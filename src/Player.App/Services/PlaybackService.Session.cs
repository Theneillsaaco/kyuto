using Player.Core.Playback;

namespace Player.App.Services;

// Saving and bringing back the listening session. The database work is in SessionStore; this part decides when to
// save and applies what comes back. PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    /// <summary>Where listening resumes: the playback position, or the restored one while nothing is loaded.</summary>
    private double ResumeSeconds => IsLoaded ? Position : _restoreSeek;

    /// <summary>Writes the whole queue. Called when it changes, not on every tick.</summary>
    private void PersistQueue() => Session.SaveQueue(_queue, ResumeSeconds, Volume);

    /// <summary>Writes only the position and volume (every few seconds, on pause and at exit).</summary>
    public void SaveProgress()
    {
        _lastSavedAt = _clock.Elapsed.TotalSeconds;
        Session.SaveProgress(ResumeSeconds, Volume);
    }

    /// <summary>
    /// Brings back the last session, paused, as the "Al iniciar" setting says: queue and position, the
    /// queue with the track from 0, or nothing (volume, repeat and shuffle always come back). Nothing is
    /// loaded into mpv until the user presses play. but the stream of the current track is asked for in the
    /// background (and its song saved to the disk), so that pressing play starts at once.
    /// </summary>
    public void Restore()
    {
        if (Session.Restore(_rng) is not { } restored)
            return;

        SetVolume(restored.Volume);
        _queue = restored.Queue;

        if (!restored.HasQueue)
            return;

        RefreshQueue(persist: false);

        if (PlayQueue.current(_queue) is { } current)
            SetCurrent(current.Value);

        _restoreSeek = restored.Seconds;
        Position = _restoreSeek;
        _status.Text = Tr.T("status.restored");
        RequestPrefetch();
    }
}
