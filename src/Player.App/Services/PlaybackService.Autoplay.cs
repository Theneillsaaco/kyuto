using Player.Core.Playback;

namespace Player.App.Services;

// What plays when the queue runs out. PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    /// <summary>The queue is about to run out: ask for what plays after it, so it is there when the track ends.</summary>
    private void RequestAutoplay()
    {
        if (_prefs.AutoPlay && Autoplay.wanted(_queue) && PlayQueue.current(_queue) is { } current)
            _autoplay.Request(current.Value.Id);
    }

    /// <summary>
    /// Something is still being waited for while nothing plays (the stream of the track that was just chosen, or
    /// the autoplay answer): pick it up even though the main poll is not running. QML calls Pump from an always-on timer.
    /// </summary>
    private void PumpWaiting()
    {
        if (_awaitingId is not null || (_waitingAutoplay && _autoplay.Version != _seenAutoplay))
            Poll();
    }
    
    /// <summary>
    /// Adds the radio of the last track to the end of the queue (so the next track is prefetched and the switch stays
    /// gapless). If the queue had already ended, it goes on with the first new song.
    /// </summary>
    private void ApplyAutoplay()
    {
        var waiting = _waitingAutoplay && !IsLoaded;
        _waitingAutoplay = waiting;

        if (_prefs.AutoPlay && Autoplay.wanted(_queue) && PlayQueue.current(_queue) is { } current
            && _autoplay.TryGet(current.Value.Id) is { Length: > 0 } found)
        {
            var fresh = Autoplay.fresh(_queue.Items.Select(item => item.Id), found);

            if (fresh.Length > 0)
            {
                foreach (var added in fresh)
                    _radioIds.Add(added.Id);

                _queue = PlayQueue.add(fresh, _queue);
                RefreshQueue();

                if (waiting)
                {
                    _waitingAutoplay = false;
                    _queue = PlayQueue.next(_queue);
                    PlayCurrent();
                }
                else
                {
                    AfterQueueEdit();
                }

                return;
            }
        }

        if (waiting && !_autoplay.Busy)
        {
            _waitingAutoplay = false;
            _status.Text = Tr.T("status.finished");
        }

    }
}