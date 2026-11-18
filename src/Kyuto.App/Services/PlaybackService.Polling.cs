using Player.Core;
using Player.Core.Playback;

namespace Player.App.Services;

// What mpv did since the last look: errors, a track that ended, the position, and whatever background work finished.
// PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    public void Poll()
    {
        PollCore();
        UpdateWaiting();
        PublishMpris();
        Changed?.Invoke();
    }

    private void PollCore()
    {
        var error = _engine.DrainEvents();
        var ended = _engine.TakeEnded();

        if (_engine.TakeNextFailed() && _queuedNextId is { } failedNext)
        {
            _prefetch.Forget(failedNext);
            _cache?.Remove(failedNext);
            _queuedNextId = null;
            Log.Warn("playback", "queued stream failed to open: " + failedNext);
        }

        // mpv finished a track and went on into the entry we queued: follow it in our own state.
        if (ended && _queuedNextId is not null)
            OnEngineAdvanced();

        if (error.Length > 0)
        {
            // The track that should be playing failed, and the engine already stopped.
            _queuedNextId = null;

            if (TryFallback())
                return;

            if (PlayQueue.current(_queue) is { } failed)
            {
                _prefetch.Forget(failed.Value.Id); // so pressing play resolves it again instead of reusing the dead URL
                _cache?.Remove(failed.Value.Id);
            }

            IsLoaded = false;
            _sawActive = false;
            Log.Warn("playback", "play failed: " + error);
            _status.Text = ErrorText(error);
            return;
        }

        var awaiting = _awaitingId is not null;
        Position = awaiting ? _pendingSeek : _engine.Position;
        var d = _engine.Duration;
        Duration = d > 0 ? d : _knownDuration;
        Paused = awaiting ? _startPaused : _engine.Paused;

        // idle-active can still read "yes" for a moment right after loadfile.
        if (!_engine.Idle)
        {
            _sawActive = true;
            if (d > 0 && _status.Text == Loading)
                _status.Text = Tr.T("status.playing");
            if (_pendingSeek > 0 && d > 0)
            {
                _engine.Seek(_pendingSeek); // a restored track resumes where it was left
                _pendingSeek = 0;
            }
            ObserveListening();
            if (!Paused && _clock.Elapsed.TotalSeconds - _lastSavedAt > 5)
                SaveProgress();
        }
        else if (_sawActive)
        {
            OnTrackEnded();
        }

        if (_autoplay.Version != _seenAutoplay)
        {
            _seenAutoplay = _autoplay.Version;
            ApplyAutoplay();
        }

        if (_loudness.Version != _seenLoudness)
        {
            _seenLoudness = _loudness.Version;
            ApplyGain();
        }

        var version = _prefetch.Version;
        if (version != _seenPrefetch)
        {
            _seenPrefetch = version;
            ContinueAwaiting();
            FeedCache();
            SyncNext(); // a stream we were waiting for is ready
        }

        if (_awaitingId is not null)
            ContinueAwaiting();

        var cacheVersion = _cache?.Version ?? 0;
        if (cacheVersion != _seenCache)
        {
            _seenCache = cacheVersion;
            SyncNext(); // a song just reached the disk: the next one may now come from there
        }
    }

    /// <summary>
    /// Whether the sound is supposed to be playing and is not (still opening the track, or mpv is paused for the
    /// cache). The video holds still while it lasts; the loading bar only shows after a moment, so a quick seek
    /// does not make it flash.
    /// </summary>
    private void UpdateWaiting()
    {
        _buffering.Update(IsLoaded && !Paused && (!_sawActive || _engine.Buffering), _clock.Elapsed.TotalSeconds);
        AudioWaiting = _buffering.Waiting;
        Buffering = _buffering.Showing;
    }

    private void OnTrackEnded()
    {
        var next = PlayQueue.advance(_queue);
        if (next is not null)
        {
            _queue = next.Value;
            PlayCurrent();
            return;
        }

        _sawActive = false;
        IsLoaded = false;
        Position = 0;

        if (_prefs.AutoPlay && Autoplay.wanted(_queue) && PlayQueue.current(_queue) is { } last)
        {
            _waitingAutoplay = true;
            _status.Text = Tr.T("status.autoplay");
            _autoplay.Request(last.Value.Id);
            ApplyAutoplay(); // already known: goes on at once; otherwise the poll picks the answer up
        }
        else
        {
            _status.Text = Tr.T("status.finished");
        }

        PersistQueue();
    }

    /// <summary>The queued entry started by itself (gapless): the queue moves on, mpv is not touched.</summary>
    private void OnEngineAdvanced()
    {
        _queuedNextId = null;
        _audioOverride = null;

        var next = PlayQueue.advance(_queue);
        if (next is null || PlayQueue.current(next.Value) is not { } current)
            return;

        _queue = next.Value;
        var t = current.Value;
        SetCurrent(t);
        _viaPrefetch = true;
        _pendingSeek = 0;
        Position = 0;
        _status.Text = Tr.T("status.playing");
        StartListening(t);
        RefreshQueue();
        AfterQueueEdit();
    }

    /// <summary>
    /// A direct stream or a cached file failed (expired, 403, a bad file): play the same track once through the
    /// yt-dlp route of mpv, which resolves it again. False when that is not what was playing.
    /// </summary>
    private bool TryFallback()
    {
        if (!_viaPrefetch || PlayQueue.current(_queue) is not { } current || TrackSource.IsDirect(current.Value.Id))
            return false;

        var t = current.Value;
        Log.Warn("playback", "direct stream failed, using the yt-dlp route");
        _cache?.Remove(t.Id);
        _prefetch.Forget(t.Id);
        _viaPrefetch = false;
        _queuedNextId = null;
        _sawActive = false;
        _pendingSeek = Position;
        _status.Text = Loading;

        _engine.Load(SourceFor(t), []);
        _engine.SetVolume(Volume);
        _engine.SetPaused(false);
        SyncNext();
        return true;
    }

    /// <summary>A playback error as the listener should read it: the known reasons in words, the rest as they are.</summary>
    private static string ErrorText(string raw)
    {
        var key = PlayErrors.reason(raw);
        return key.Length > 0 ? Tr.T(key) : Tr.F("status.error", PlayErrors.firstLine(raw));
    }
}
