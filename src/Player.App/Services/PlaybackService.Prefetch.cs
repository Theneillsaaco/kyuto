using Player.Core;
using Player.Core.Domain;
using Player.Core.Playback;

namespace Player.App.Services;

// Streams resolved ahead of time, the disk cache, and the entry queued in mpv so the switch is gapless.
// PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    /// <summary>The queue order or settings changed: resolve what plays now and next and line up mpv's queued entry.</summary>
    private void AfterQueueEdit()
    {
        RequestPrefetch();
        RequestAutoplay();
        SyncNext();
    }

    /// <summary>Resolves what plays now and next (the current track first), and tells the disk cache what not to delete and in which order to download.</summary>
    private void RequestPrefetch()
    {
        var ids = PrefetchPlanner.IdsFor(_queue);
        _planIds = ids;

        _cache?.Protect(ids);
        _prefetch.Want(ids);
        FeedCache();

        if (_prefs.Normalize)
            _loudness.Want(ids);
    }

    /// <summary>Hands every resolved stream of the plan to the disk cache, which downloads what it does not have.</summary>
    private void FeedCache()
    {
        if (_cache is null || _cache.Limit == 0)
            return;

        foreach (var id in _planIds)
            if (_prefetch.TryGet(id) is { } stream)
                _cache.Add(id, stream.Url, stream.Headers);
    }

    /// <summary>
    /// Loads the track into mpv if it is available now: a link or a path, a song on the disk or an already
    /// resolved stream. False when it has to be resolved first.
    /// </summary>
    private bool TryLoadNow(Track t)
    {
        if (TrackSource.IsDirect(t.Id))
        {
            LoadSource(TrackSource.SourceOf(t), [], viaPrefetch: false);
            return true;
        }

        if (_cache?.Get(t.Id) is { } file)
        {
            Log.Info("playback", "from the disk cache: " + t.Id);
            LoadSource(file, [], viaPrefetch: true);
            return true;
        }

        if (_prefetch.TryGet(t.Id) is { } stream)
        {
            LoadSource(stream.Url, stream.Headers, viaPrefetch: true);
            return true;
        }

        return false;
    }

    private void LoadSource(string source, string[] headers, bool viaPrefetch)
    {
        _viaPrefetch = viaPrefetch;
        _engine.Load(source, headers);
        _engine.SetVolume(Volume);
        _engine.SetPaused(_startPaused);
    }

    /// <summary>
    /// The track that was chosen is waiting for its stream. When the sidecar answers (or the file lands on the disk),
    /// mpv loads it. If the resolve failed or takes too long, mpv gets the plain link and uses its own yt-dlp.
    /// </summary>
    private void ContinueAwaiting()
    {
        if (_awaitingId is not { } id)
            return;

        if (PlayQueue.current(_queue) is not { } current || current.Value.Id != id)
        {
            _awaitingId = null; // the user moved on
            return;
        }

        var t = current.Value;

        if (TryLoadNow(t))
        {
            _awaitingId = null;
            SyncNext();
            return;
        }

        var waited = _clock.Elapsed.TotalSeconds - _awaitingSince;

        if (_prefetch.Failed(id) || waited > GiveUpSeconds)
        {
            Log.Warn("playback", $"no stream for {id} after {waited:0.0} s, using the yt-dlp route of mpv");
            _awaitingId = null;
            LoadSource(TrackSource.SourceOf(t), [], viaPrefetch: false);
            SyncNext();
        }
    }

    /// <summary>
    /// Makes mpv's second playlist entry the track that plays after the current one, when its audio is already
    /// at hand (on the disk, or its stream resolved); otherwise leaves it empty (the normal end-of-track path then
    /// takes over).
    /// </summary>
    private void SyncNext()
    {
        if (!IsLoaded || _awaitingId is not null)
            return;

        var following = PlayQueue.advance(_queue);
        var track = following is null ? null : PlayQueue.current(following.Value)?.Value;

        if (track is null || TrackSource.IsDirect(track.Id))
        {
            ClearQueuedNext();
            return;
        }
        if (_queuedNextId == track.Id)
            return;

        if (_cache?.Get(track.Id) is { } file)
        {
            _engine.QueueNext(file, []);
            _queuedNextId = track.Id;
        }
        else if (_prefetch.TryGet(track.Id) is { } stream)
        {
            _engine.QueueNext(stream.Url, stream.Headers);
            _queuedNextId = track.Id;
        }
        else
        {
            ClearQueuedNext();
        }
    }

    private void ClearQueuedNext()
    {
        if (_queuedNextId is null)
            return;

        _engine.ClearNext();
        _queuedNextId = null;
    }
}
