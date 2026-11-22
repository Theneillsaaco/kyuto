using Kyuto.Core.Playback;

namespace Kyuto.App.Services;

// Undo of queue edits and the sleep timer. PlaybackService is partial so this lives next to the feature.
internal sealed partial class PlaybackService
{
    private double _sleepAt = -1; // _clock seconds at which playback pauses, -1 = off

    public QueueState QueueSnapshot => _queue;

    public void RestoreQueue(QueueState snapshot)
    {
        var playing = IsLoaded ? CurrentId : "";
        _queue = snapshot;
        RefreshQueue();

        if (PlayQueue.current(_queue) is { } current)
        {
            if (current.Value.Id != playing)
            {
                StopPlayback();
                SetCurrent(current.Value);
            }
        }
        else
        {
            StopPlayback();
            ClearCurrent();
        }
        
        AfterQueueEdit();
        Poll();
    }
    
    public double SleepRemaining => _sleepAt < 0 ? 0 : Math.Max(0, _sleepAt - _clock.Elapsed.TotalSeconds);
    
    /// <summary>Pauses after this many minutes of the player's clock; 0 turns it off.</summary>
    public void ChooseSleepMinutes(int minutes)
    {
        _sleepAt = minutes > 0 ? _clock.Elapsed.TotalSeconds + minutes * 60 : -1;
        Changed?.Invoke();
    }
    
    private void CheckSleep()
    {
        if (_sleepAt < 0 || _clock.Elapsed.TotalSeconds < _sleepAt)
            return;

        _sleepAt = -1;

        if (_awaitingId is not null)
            _startPaused = true;
        else if (IsLoaded && !Paused)
            _engine.SetPaused(true);
        else
            return;
        
        SaveProgress();
        _status.Text = Tr.T("sleep.paused");
    }
}