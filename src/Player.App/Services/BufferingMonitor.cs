namespace Player.App.Services;

/// <summary>
/// Tells whether the sound is supposed to be playing and is not (still opening the track, or mpv is paused for the
/// cache), and for how long. <see cref="Waiting"/> is immediate and holds the video still; <see cref="Showing"/> only
/// turns on after <c>showAfterSeconds</c>, so a quick seek does not make the loading bar flash.
/// </summary>
internal sealed class BufferingMonitor(double showAfterSeconds = 0.4)
{
    private double _since = -1;
    
    public bool Waiting { get; private set; }
    
    public bool Showing { get; private set; }

    /// <param name="waiting">The sound should be playing and is not.</param>
    /// <param name="now">Seconds on any steady clock.</param>
    public void Update(bool waiting, double now)
    {
        Waiting = waiting;

        if (!waiting)
        {
            _since = -1;
            Showing = false;
            
            return;
        }

        if (_since < 0)
            _since = now;
        
        Showing = now - _since > showAfterSeconds;
    }
}