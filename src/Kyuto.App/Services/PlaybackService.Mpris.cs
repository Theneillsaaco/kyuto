using Kyuto.Core;
using Kyuto.Core.Playback;
using Kyuto.Mpris;

namespace Kyuto.App.Services;

// The desktop's side of the player (playerctl, media keys, widgets). What is told to the desktop is built by
// MprisMapper. PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    public void StartMpris()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var service = new MprisService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync();
                _mpris = service;
            }
            catch (Exception ex)
            {
                Log.Warn("playback", "mpris unavailable", ex);
                service.Dispose();
            }
        });
    }

    /// <summary>
    /// Runs what the desktop asked for (playerctl, media keys). QML calls it from an always-on timer,
    /// because a command can arrive while the player is idle and the main poll is not running.
    /// </summary>
    public void Pump()
    {
        PumpWaiting();
        PumpCore();
    }

    private void PumpCore()
    {
        if (_mpris is not { } mpris)
            return;

        var any = false;
        while (mpris.TryDequeue(out var command))
        {
            any = true;
            try
            {
                Handle(command);
            }
            catch (Exception ex)
            {
                Log.Warn("playback", "mpris command", ex);
            }
        }
        if (any)
            Poll();
        else if (!_mprisPublished)
            PublishMpris();
    }

    private void Handle(MprisCommand command)
    {
        switch (command)
        {
            case Cmd.Play: if (!IsLoaded || Paused) Toggle(); break;
            case Cmd.Pause: if (IsLoaded && !Paused) Toggle(); break;
            case Cmd.PlayPause: Toggle(); break;
            case Cmd.Stop:
                if (IsLoaded && _awaitingId is null)
                {
                    _engine.SetPaused(true);
                    _engine.Seek(0);
                    _mpris?.Seeked(0);
                }
                break;
            case Cmd.Next: Next(); break;
            case Cmd.Previous: Previous(); break;
            case Cmd.SeekRelative s: Seek(Math.Clamp(Position + s.Micros / 1e6, 0, Duration)); break;
            case Cmd.SeekTo s: Seek(Math.Clamp(s.Micros / 1e6, 0, Duration)); break;
            case Cmd.SetVolume v: SetVolume((int)Math.Round(Math.Clamp(v.Level, 0, 1) * 100)); break;
            case Cmd.SetRate r: ChooseSpeed(r.Rate); break;
            case Cmd.SetShuffle s: if (_queue.Shuffle != s.On) ToggleShuffle(); break;
            case Cmd.SetLoop l:
                _queue = PlayQueue.setRepeat(MprisMapper.RepeatOf(l.Status), _queue);
                PersistQueue();
                AfterQueueEdit();
                break;
        }
        
        Poll();
    }

    private void PublishMpris()
    {
        if (_mpris is not { } mpris)
            return;

        _mprisPublished = true;

        mpris.Publish(MprisMapper.Build(
            CurrentTrack, IsLoaded, Paused, awaitingStream: _awaitingId is not null, Duration, Position, Volume,
            Speed, RepeatMode, Shuffle, CanNext, CanPrevious, QueueCount));
    }
}
