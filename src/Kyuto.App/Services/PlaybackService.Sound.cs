using Player.Core.Playback;

namespace Player.App.Services;

// Speed and volume leveling. PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    /// <summary>Playback speed, 0.5 to 3.</summary>
    public double Speed => _prefs.Speed;

    /// <summary>Goes to the next speed of the list (the pitch stays).</summary>
    public void CycleSpeed() => ChooseSpeed(PlaybackSpeed.next(_prefs.Speed));

    public void ChooseSpeed(double speed)
    {
        var value = PlaybackSpeed.normalize(speed);
        _prefs.SaveSpeed(value);
        _engine.SetSpeed(value);
        Poll();
    }

    /// <summary>Gives mpv the saved speed. Called once at start.</summary>
    public void ApplyStartupSound() => _engine.SetSpeed(_prefs.Speed);

    /// <summary>The levelling setting changed: look the levels up and apply the gain again.</summary>
    public void RefreshSound()
    {
        RequestPrefetch();
        ApplyGain();
    }

    /// <summary>Turns the current track down to the common level, when its loudness is known. Never turns it up.</summary>
    private void ApplyGain()
    {
        var db = 0.0;

        if (_prefs.Normalize && PlayQueue.current(_queue) is { } current && _loudness.TryGet(current.Value.Id) is { } measured)
            db = TrackLoudness.gainDb(measured);

        if (Math.Abs(db - _appliedGain) < 0.05)
            return;

        _appliedGain = db;
        _engine.SetGain(db);
    }
}
