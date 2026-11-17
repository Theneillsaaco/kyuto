using System.Diagnostics;
using Player.Core.Contracts;
using Player.Core.Domain;
using Player.Core.Playback;
using Player.Mpris;
using Player.Storage.Cache;
using Player.Storage.Database;

namespace Player.App.Services;

/// <summary>
/// The playback engine room: queue, mpv, listen tracking, session save/restore and MPRIS. Plain C#,
/// Qt-free. Everything runs on the Qt thread (the view models call it from QML); the only thing a
/// foreign thread touches is the MPRIS command queue. After any change it raises <see cref="Changed"/>
/// so the view models can copy the new state.
/// While a track plays, the next ones are resolved in the background (and downloaded to the disk cache) and the
/// next one is queued in mpv, so the switch is instant and gapless; <see cref="_queuedNextId"/> is the id of that
/// queued entry.
/// A track that is not cached or resolved yet no longer goes through mpv's yt-dlp hook (seconds of cold
/// start): its stream is asked from the warm sidecar and mpv loads it when it lands (<see cref="_awaitingId"/>).
/// PlaybackService is partial so each feature lives in its own file and shares the state declared here:
/// Polling (what mpv did), Prefetch (streams and the queued entry), Autoplay, Listening (history and scrobbles),
/// Session (save and restore), Mpris and Sound.
/// </summary>
internal sealed partial class PlaybackService
{
    #region What the UI asks for

    /// <summary>A link or absolute path: becomes the whole queue and plays.</summary>
    public void PlayDirect(string source)
    {
        var direct = new Track(source, source, "", [], "", 0.0, "", TrackKind.Song);
        _radioIds.Clear();
        _queue = PlayQueue.replace(_rng.Next(), [direct], 0, _queue);
        RefreshQueue(persist: false);
        PlayCurrent();
        Poll();
    }

    /// <summary>The queue becomes the whole list, starting at <paramref name="index"/>.</summary>
    public void PlayFrom(IReadOnlyList<Track> tracks, int index)
    {
        if (index < 0 || index >= tracks.Count)
            return;

        _radioIds.Clear();
        _queue = PlayQueue.replace(_rng.Next(), tracks, index, _queue);
        RefreshQueue(persist: false);
        PlayCurrent();
        Poll();
    }

    /// <summary>Replaces the queue with a list, in order or shuffled.</summary>
    public void PlayList(IReadOnlyList<Track> tracks, bool shuffle)
    {
        if (tracks.Count == 0)
            return;

        _queue = PlayQueue.setShuffle(_rng.Next(), shuffle, _queue);
        PlayFrom(tracks, shuffle ? _rng.Next(tracks.Count) : 0);
    }

    /// <summary>Adds a track at the end of the queue.</summary>
    public void AddFrom(IReadOnlyList<Track> tracks, int index)
    {
        if (index < 0 || index >= tracks.Count)
            return;

        var wasEmpty = PlayQueue.count(_queue) == 0;
        _queue = PlayQueue.add([tracks[index]], _queue);
        AfterAdd(wasEmpty);
    }

    /// <summary>Adds a track right after the one that plays now.</summary>
    public void PlayNextFrom(IReadOnlyList<Track> tracks, int index)
    {
        if (index < 0 || index >= tracks.Count)
            return;

        var wasEmpty = PlayQueue.count(_queue) == 0;
        _queue = PlayQueue.addNext([tracks[index]], _queue);
        AfterAdd(wasEmpty);
    }

    private void AfterAdd(bool wasEmpty)
    {
        RefreshQueue();
        if (wasEmpty)
        {
            _queue = PlayQueue.jumpTo(0, _queue);
            PlayCurrent();
        }
        else
        {
            AfterQueueEdit(); // the new track may be what plays next
        }
        Poll();
    }

    public void PlayQueueItem(int position)
    {
        _queue = PlayQueue.jumpTo(position, _queue);
        PlayCurrent();
        Poll();
    }

    /// <summary>Drags an entry of the queue (play-order positions). The current track keeps playing.</summary>
    public void MoveQueueItem(int source, int target)
    {
        var moved = PlayQueue.move(source, target, _queue);
        if (ReferenceEquals(moved, _queue) || moved.Equals(_queue))
            return;

        _queue = moved;
        RefreshQueue();
        AfterQueueEdit();
        Poll();
    }

    /// <summary>Takes an entry out of the queue. Removing the playing one goes on with the next.</summary>
    public void RemoveQueueItem(int position)
    {
        if (position < 0 || position >= PlayQueue.count(_queue))
            return;

        var wasCurrent = position == _queue.Position;
        _queue = PlayQueue.remove(position, _queue);
        RefreshQueue();

        if (wasCurrent)
            AfterRemovingCurrent(position);
        else
            AfterQueueEdit();

        Poll();
    }

    /// <summary>Empties the queue and stops.</summary>
    public void ClearQueue()
    {
        if (PlayQueue.count(_queue) == 0)
            return;

        _radioIds.Clear();
        _queue = PlayQueue.clear(_queue);
        StopPlayback();
        ClearCurrent();
        RefreshQueue();
        _status.Text = "";
        Poll();
    }

    private void AfterRemovingCurrent(int position)
    {
        _restoreSeek = 0;
        var current = PlayQueue.current(_queue);
        var nothingFollows = PlayQueue.count(_queue) == 0 || position >= PlayQueue.count(_queue);

        if (current is null)
        {
            StopPlayback();
            ClearCurrent();
        }
        else if (nothingFollows)
        {
            StopPlayback();
            SetCurrent(current.Value);
        }
        else if (IsLoaded)
        {
            PlayCurrent();
        }
        else
        {
            SetCurrent(current.Value); // restored and paused: just show the track that took its place
            Position = 0;
        }
    }

    /// <summary>Unloads mpv. The queue is left as it is.</summary>
    private void StopPlayback()
    {
        if (IsLoaded)
            _engine.Stop();

        IsLoaded = false;
        _sawActive = false;
        _viaPrefetch = false;
        _queuedNextId = null;
        _audioOverride = null;
        _awaitingId = null;
        _pendingSeek = 0;
        _restoreSeek = 0;
        _tracker = null;
        Position = 0;
        Paused = true;
    }

    private void ClearCurrent()
    {
        CurrentTitle = CurrentArtist = CurrentArt = CurrentId = "";
        _knownDuration = 0;
        Duration = 0;
    }

    public void Toggle()
    {
        if (_awaitingId is not null)
        {
            _startPaused = !_startPaused;
            Poll();
            return;
        }

        if (!IsLoaded)
        {
            // Finished or failed: play the current queue entry again.
            if (PlayQueue.currentIndex(_queue) >= 0) { PlayCurrent(); Poll(); }
            return;
        }

        _engine.SetPaused(!_engine.Paused);
        Poll();
        SaveProgress();
    }

    public void Next()
    {
        if (!PlayQueue.hasNext(_queue))
            return;

        _queue = PlayQueue.next(_queue);
        PlayCurrent();
        Poll();
    }

    public void Previous()
    {
        if (IsLoaded && Position > 3)
        {
            _engine.Seek(0);
            _mpris?.Seeked(0);
            Poll();
            return;
        }

        _queue = PlayQueue.previous(_queue);
        PlayCurrent();
        Poll();
    }

    public void CycleRepeat()
    {
        _queue = PlayQueue.cycleRepeat(_queue);
        RefreshQueue();
        AfterQueueEdit();
        Changed?.Invoke();
    }

    public void ToggleShuffle()
    {
        _queue = PlayQueue.setShuffle(_rng.Next(), !_queue.Shuffle, _queue);
        RefreshQueue();
        AfterQueueEdit();
        Changed?.Invoke();
    }

    public void Seek(double seconds)
    {
        if (!IsLoaded || _awaitingId is not null)
            return;

        _engine.Seek(seconds);
        _mpris?.Seeked((long)(seconds * 1e6));
        Poll();
    }

    public void SetVolume(int percent)
    {
        Volume = Math.Clamp(percent, 0, 100);
        _engine.SetVolume(Volume);
        Changed?.Invoke();
    }

    /// <summary>
    /// Plays the current track from another recording of it (the video version), from the same position, or
    /// back from its own with null. Used when the user asks for the video of a studio track: the picture and
    /// the sound must be the same performance. The queue, the track shown and the scrobble do not change.
    /// </summary>
    public void SwapAudio(string? videoId)
    {
        if (!IsLoaded || _awaitingId is not null || videoId == _audioOverride || PlayQueue.current(_queue) is not { } current)
            return;

        var wasPaused = Paused;
        _audioOverride = videoId;
        _pendingSeek = Position;
        _sawActive = false;
        _viaPrefetch = false;
        _queuedNextId = null; // loading replaces mpv's whole playlist
        _status.Text = Loading;

        _engine.Load(SourceFor(current.Value), []);
        _engine.SetVolume(Volume);
        _engine.SetPaused(wasPaused);
        AfterQueueEdit();
        Poll();
    }

    /// <summary>The next tracks in play order (shuffle and repeat respected), at most <paramref name="count"/>.</summary>
    public IReadOnlyList<Track> UpcomingTracks(int count) => [.. Prefetch.upcoming(_queue).Take(count)];

    public void Shutdown()
    {
        SaveProgress();
        _mpris?.Dispose();
    }

    #endregion

    #region The current track

    /// <summary>Loads the queue's current track into mpv. Never calls Poll (it may run inside Poll).</summary>
    private void PlayCurrent()
    {
        var cur = PlayQueue.current(_queue);
        if (cur == null)
        {
            PersistQueue();
            return;
        }

        var t = cur.Value;
        SetCurrent(t);
        _sawActive = false;
        IsLoaded = true;
        _pendingSeek = _restoreSeek;
        _restoreSeek = 0;
        Position = _pendingSeek;
        _status.Text = Loading;
        _queuedNextId = null; // loading replaces mpv's whole playlist
        _audioOverride = null;
        _viaPrefetch = false;
        _awaitingId = null;
        _startPaused = false;

        if (!TryLoadNow(t))
        {
            _engine.Stop(); // the sound of the previous track must not go on while the new one is looked up

            if (_prefetch.Enabled)
            {
                _awaitingId = t.Id;
                _awaitingSince = _clock.Elapsed.TotalSeconds;
            }
            else
            {
                LoadSource(TrackSource.SourceOf(t), [], viaPrefetch: false); // no provider: nothing to wait for
            }
        }

        StartListening(t);
        RefreshQueue();
        AfterQueueEdit();
    }

    /// <summary>What mpv loads for a track: its own source, or the video id whose audio replaces it.</summary>
    private string SourceFor(Track t) =>
        _audioOverride is { } video ? "https://music.youtube.com/watch?v=" + video : TrackSource.SourceOf(t);

    private void SetCurrent(Track t)
    {
        CurrentTitle = t.Title;
        CurrentArtist = t.Artist;
        CurrentArt = t.ArtUrl;
        CurrentId = t.Id;
        _knownDuration = t.DurationSeconds;
        Duration = t.DurationSeconds;
    }

    /// <summary>The queue content or its order changed: bump the revision so the queue panel rebuilds its rows.</summary>
    private void RefreshQueue(bool persist = true)
    {
        QueueRevision++;

        if (persist)
            PersistQueue();
    }

    #endregion

    #region Fields

    private static string Loading => Tr.T("status.loading");

    private const double GiveUpSeconds = 15;

    private readonly IAudioEngine _engine;
    private readonly PlayerDatabase? _db;
    private readonly ScrobbleService? _scrobble;
    private readonly PrefetchService _prefetch;
    private readonly StatusLine _status;
    private readonly Random _rng = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly BufferingMonitor _buffering = new();

    private QueueState _queue = PlayQueue.empty;
    private volatile MprisService? _mpris; // set by the task that connects to the session bus
    private bool _mprisPublished;
    private double _restoreSeek;  // where the restored track resumes when it is played
    private double _pendingSeek;  // the same, once the track is loading
    private double _lastSavedAt;
    private ListenTracker? _tracker;
    private double _knownDuration;
    private bool _sawActive;
    private string? _queuedNextId; // the track queued after the current one inside mpv, if any
    private bool _viaPrefetch;     // the current track plays from a resolved URL or a cached file (not the yt-dlp hook)
    private string? _audioOverride; // a video id whose own audio plays instead of the track's (see SwapAudio)
    private int _seenPrefetch;
    private readonly PlaybackPrefs _prefs;
    private readonly AutoplayService _autoplay;
    private readonly LoudnessService _loudness;
    private readonly AccountService? _account;
    private readonly HashSet<string> _radioIds = []; // tracks autoplay added: scrobbled as not chosen by the user
    private int _seenAutoplay;
    private int _seenLoudness;
    private bool _waitingAutoplay; // the queue ended and autoplay is still looking for more
    private double _appliedGain;

    private readonly AudioCache? _cache;
    private string[] _planIds = []; // the ids the prefetch is working on, most urgent first
    private string? _awaitingId;    // the chosen track whose stream is still being resolved; mpv has nothing loaded
    private double _awaitingSince;
    private bool _startPaused;      // the user paused (or resumed) while the stream was awaited
    private int _seenCache;

    private SessionStore? _session;
    private SessionStore Session => _session ??= new SessionStore(_db);

    public PlaybackService(IAudioEngine engine, PlayerDatabase? db, ScrobbleService? scrobble, PrefetchService prefetch, StatusLine status,
        PlaybackPrefs prefs, AutoplayService autoplay, LoudnessService loudness, AccountService? account,
        AudioCache? cache) =>
        (_engine, _db, _scrobble, _prefetch, _status, _prefs, _autoplay, _loudness, _account, _cache) =
            (engine, db, scrobble, prefetch, status, prefs, autoplay, loudness, account, cache);

    public event Action? Changed;

    public double Position { get; private set; }
    public double Duration { get; private set; }
    public bool Paused { get; private set; } = true;
    public bool IsLoaded { get; private set; }
    /// <summary>The sound should be playing but is not yet: the track is loading or the cache is filling. The video waits for it.</summary>
    public bool AudioWaiting { get; private set; }
    /// <summary>The same, once it has lasted long enough for a loading bar to make sense.</summary>
    public bool Buffering { get; private set; }
    public int Volume { get; private set; } = 100;
    public string CurrentId { get; private set; } = "";
    public string CurrentTitle { get; private set; } = "";
    public string CurrentArtist { get; private set; } = "";
    public string CurrentArt { get; private set; } = "";
    /// <summary>The queue's current track (the playing one, or the restored one while paused), null when the queue is empty.</summary>
    public Track? CurrentTrack => PlayQueue.current(_queue)?.Value;
    /// <summary>The video id whose audio plays instead of the current track's own, null when it is the track's own.</summary>
    public string? AudioOverride => _audioOverride;

    /// <summary>Goes up whenever the queue's content or order changes (not when only the position moves).</summary>
    public int QueueRevision { get; private set; }
    public IEnumerable<Track> Ordered => PlayQueue.ordered(_queue);
    public int QueueCount => PlayQueue.count(_queue);
    /// <summary>Position of the current track in play order, -1 if none.</summary>
    public int QueuePosition => PlayQueue.currentIndex(_queue) < 0 ? -1 : _queue.Position;
    public bool CanNext => PlayQueue.hasNext(_queue);
    public bool CanPrevious => QueueCount > 0;
    public string RepeatMode => PlayQueue.repeatName(_queue);
    public bool Shuffle => _queue.Shuffle;

    #endregion
}
