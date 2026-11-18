using Kyuto.Core;
using Kyuto.Core.Domain;
using Kyuto.Core.Playback;

namespace Kyuto.App.Services;

// What counts as a listen: the history and Last.fm. PlaybackService is partial so this lives next to the feature and
// shares its state.
internal sealed partial class PlaybackService
{
    private void StartListening(Track t)
    {
        var tracker = Listening.start(Clock.UnixNow(), t);
        _tracker = _radioIds.Contains(t.Id) ? Listening.unchosen(tracker) : tracker;
        _account?.ReportPlayback(t.Id);
        ApplyGain();
        Announce(t);
        
        if (_tracker.Listen is { IsSome: true } listen)
            _scrobble?.NowPlaying(listen.Value);
    }

    /// <summary>Feeds the listen tracker; when the track counts it goes to the history and the queue to Last.fm.</summary>
    private void ObserveListening()
    {
        if (_tracker is not { } tracker)
            return;

        var (next, due) = Listening.observe(_clock.Elapsed.TotalSeconds, Position, Paused, Duration, tracker);
        _tracker = next;
        if (!due.IsSome)
            return;

        try
        {
            _db?.AddHistory(due.Value, next.Listened, CurrentId);
        }
        catch (Exception ex)
        {
            Log.Warn("playback", " history: ", ex);
        }
        _scrobble?.Submit(due.Value);
    }
}